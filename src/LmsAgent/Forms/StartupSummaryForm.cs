using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 환경설정 &gt; 일반의 "첫 시작 시 공지사항 알림"이 켜져 있을 때 로그인 직후 띄우는 요약 팝업입니다.
/// 오늘의 일정 / 요청사항 / 안내·공지 / 내가 해야 할 일 / 법정연수 이수 등록 / 협의사항을 모아 보여줍니다.
/// </summary>
public sealed class StartupSummaryForm : Form
{
    private readonly WorkSupportApiClient _api;

    /// <summary>체크된 채로 닫히면 오늘 하루는 로그인해도 이 알림을 다시 띄우지 않습니다.</summary>
    public bool SuppressToday => _dontShowTodayBox.Checked;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
    };

    private readonly Button _closeButton = new()
    {
        Left = 396, Top = 10, Width = 80, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly CheckBox _dontShowTodayBox = new()
    {
        Left = 16, Top = 14, Width = 200, Text = "오늘은 보지 않기", Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    public StartupSummaryForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "오늘의 알림";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(492, 536);
        MinimumSize = new Size(460, 480);

        _content.BackColor = UiTheme.Background;
        UiTheme.StyleSecondaryButton(_closeButton);
        _dontShowTodayBox.ForeColor = UiTheme.TextSecondary;

        _contentPanel.Controls.Add(_content);
        _bottomPanel.Controls.Add(_dontShowTodayBox);
        _bottomPanel.Controls.Add(_closeButton);

        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
        _root.Controls.Add(_contentPanel, 0, 0);
        _root.Controls.Add(_bottomPanel, 0, 1);

        Controls.Add(_root);
        _closeButton.Click += (_, _) => Close();

        Load += async (_, _) => await LoadAsync();
    }

    private void AddSection(string title, List<string> lines)
    {
        var text = lines.Count == 0 ? "해당 사항이 없습니다." : string.Join("\n", lines);

        var group = new GroupBox
        {
            Text = title,
            Width = 436,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = UiTheme.SkyDark,
            Font = UiTheme.BoldFont,
            BackColor = UiTheme.Surface,
        };

        var label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(410, 0),
            Left = 10,
            Top = 20,
            Text = text,
            Font = UiTheme.BaseFont,
            ForeColor = UiTheme.TextPrimary,
        };

        group.Controls.Add(label);
        group.Height = label.Bottom + 12;

        _content.Controls.Add(group);
    }

    private async Task LoadAsync()
    {
        _content.Controls.Clear();
        var loadingLabel = new Label { AutoSize = true, Text = "불러오는 중..." };
        _content.Controls.Add(loadingLabel);

        var today = DateTime.Today;

        var todayLines = new List<string>();
        try
        {
            var events = await _api.GetEventsAsync(today.Year, today.Month);
            if (events.Ok && events.Data is not null)
            {
                foreach (var ev in events.Data)
                {
                    if (ev.StartDateTime.Date <= today && ev.EndDateTime.Date >= today)
                    {
                        todayLines.Add(ev.AllDay ? $"[종일] {ev.Title}" : $"[{ev.StartDateTime:HH:mm}] {ev.Title}");
                    }
                }
            }
        }
        catch
        {
            // 조회 실패 시 해당 섹션만 비워둔다.
        }

        var myOpenLines = await FetchRequestLinesAsync("mine");
        var noticeLines = await FetchRequestLinesAsync("all", "공지");
        var toMeLines = await FetchRequestLinesAsync("tome");

        var trainingLines = new List<string>();
        try
        {
            var training = await _api.GetMyTrainingAsync();
            if (training.Ok && training.Data is not null)
            {
                foreach (var item in training.Data.Items)
                {
                    if (!item.IsDone)
                    {
                        trainingLines.Add(string.IsNullOrWhiteSpace(item.DueDate)
                            ? item.Title
                            : $"{item.Title} (기한 {item.DueDate})");
                    }
                }
            }
        }
        catch
        {
            // 조회 실패 시 해당 섹션만 비워둔다.
        }

        var meetingLines = new List<string>();
        try
        {
            var todo = await _api.GetMeetingsTodoAsync();
            if (todo.Ok && todo.Data is not null)
            {
                foreach (var item in todo.Data.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Agenda))
                    {
                        meetingLines.Add(item.Agenda!);
                    }
                }
            }
        }
        catch
        {
            // 조회 실패 시 해당 섹션만 비워둔다.
        }

        _content.Controls.Clear();
        AddSection("오늘의 일정", todayLines);
        AddSection("요청사항 (내가 등록)", myOpenLines);
        AddSection("안내 및 공지", noticeLines);
        AddSection("내가 해야 할 일", toMeLines);
        AddSection("요청된 법정연수 이수 등록", trainingLines);
        AddSection("처리 중인 협의사항", meetingLines);
    }

    private async Task<List<string>> FetchRequestLinesAsync(string scope, string? category = null)
    {
        var lines = new List<string>();
        try
        {
            var result = await _api.GetRequestsAsync(scope, category);
            if (result.Ok && result.Data is not null)
            {
                foreach (var item in result.Data.Items)
                {
                    if (item.Status is "open" or "doing")
                    {
                        lines.Add($"[{item.Category}] {item.Title}");
                    }
                }
            }
        }
        catch
        {
            // 조회 실패 시 빈 목록을 반환한다.
        }

        return lines;
    }
}
