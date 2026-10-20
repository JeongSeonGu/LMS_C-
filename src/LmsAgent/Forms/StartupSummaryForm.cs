using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 환경설정 &gt; 일반의 "첫 시작 시 공지사항 알림"이 켜져 있을 때 로그인 직후 띄우는 요약 팝업입니다.
/// 오늘의 일정 / 내가 해야 할 일 / 요청사항 / 안내·공지 / 법정연수 이수 / 협의사항을 모아 보여줍니다.
/// 네이비·블루 모던 디자인 목업("오늘의 알림")을 그대로 옮긴 테두리 없는 커스텀 창입니다.
/// </summary>
public sealed class StartupSummaryForm : Form
{
    private const int FormWidth = 440;
    private const int FormHeight = 660;

    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly Action _openMainWindow;

    /// <summary>체크된 채로 닫히면 오늘 하루는 로그인해도 이 알림을 다시 띄우지 않습니다.</summary>
    public bool SuppressToday => _dontShowTodayBox.Checked;

    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 36, BackColor = Color.White };
    private readonly Panel _hero = new() { Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(24, 12, 24, 20) };
    private readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.White, Padding = new Padding(20, 0, 20, 0) };

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
        BackColor = UiTheme.ModernBackground, Padding = new Padding(20, 16, 20, 16),
    };

    private readonly Label _dateLabel = new() { AutoSize = true, Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernTextMuted };
    private readonly Label _greetingLabel = new() { AutoSize = true, MaximumSize = new Size(392, 0), Font = UiTheme.ModernHeadlineFont, ForeColor = UiTheme.ModernText };
    private readonly FlowLayoutPanel _badgeRow = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Top = 0 };

    private readonly CheckBox _dontShowTodayBox = new() { AutoSize = true, Text = "오늘 하루 보지 않기", Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernText };
    private readonly Button _openMainButton = new() { Width = 110, Height = 40, Text = "메인 창 열기" };
    private readonly Button _closeButton = new() { Width = 70, Height = 40, Text = "확인" };

    public StartupSummaryForm(WorkSupportApiClient api, SessionManager session, Action openMainWindow)
    {
        _api = api;
        _session = session;
        _openMainWindow = openMainWindow;

        Text = "오늘의 알림";
        Icon = AppIconProvider.Icon;
        BackColor = UiTheme.ModernFieldBorder;
        Padding = new Padding(1);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(FormWidth, FormHeight);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        Font = UiTheme.ModernBodyFont;

        BuildHeader();
        BuildHero();
        BuildFooter();

        Controls.Add(_content);
        Controls.Add(_footer);
        Controls.Add(_hero);
        Controls.Add(_header);

        UiTheme.EnableDragMove(_header, this);

        Load += async (_, _) => await LoadAsync();
    }

    private void BuildHeader()
    {
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill, Text = "🔔  오늘의 알림", ForeColor = UiTheme.ModernTextMuted, Font = UiTheme.ModernSmallFont,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0),
        };

        var closeIconButton = new Button { Width = 44, Height = 36, Text = "×", Font = new Font(UiTheme.BaseFont.FontFamily, 12F, FontStyle.Bold), Margin = new Padding(0) };
        UiTheme.StyleModernWindowButton(closeIconButton, isClose: true);
        closeIconButton.Click += (_, _) => Close();

        _header.Controls.Add(titleLabel);
        _header.Controls.Add(closeIconButton);
        UiTheme.EnableDragMove(titleLabel, this);
    }

    private void BuildHero()
    {
        var textStack = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        textStack.Controls.Add(_dateLabel);
        textStack.Controls.Add(_greetingLabel);
        textStack.Controls.Add(_badgeRow);

        _hero.Controls.Add(textStack);

        _dateLabel.Text = DateTime.Today.ToString("M월 d일 dddd", new System.Globalization.CultureInfo("ko-KR"));
    }

    private void BuildFooter()
    {
        _dontShowTodayBox.Anchor = AnchorStyles.Left;
        _dontShowTodayBox.Location = new Point(0, 14);

        UiTheme.StyleModernOutlineButton(_openMainButton);
        UiTheme.StyleModernPrimaryButton(_closeButton);
        _openMainButton.Anchor = AnchorStyles.Right;
        _closeButton.Anchor = AnchorStyles.Right;
        _openMainButton.Top = 2;
        _closeButton.Top = 2;
        _closeButton.Left = FormWidth - 40 - _closeButton.Width;
        _openMainButton.Left = _closeButton.Left - 8 - _openMainButton.Width;

        _footer.Controls.Add(_dontShowTodayBox);
        _footer.Controls.Add(_openMainButton);
        _footer.Controls.Add(_closeButton);

        _openMainButton.Click += (_, _) =>
        {
            _openMainWindow();
            Close();
        };
        _closeButton.Click += (_, _) => Close();
    }

    /// <summary>카드 한 장을 FlowLayoutPanel(TopDown, AutoSize)로 만든다 — 이 저장소에서
    /// 여러 폼이 이미 쓰고 있는, 내용물 개수에 맞춰 세로로 자동으로 늘어나는 검증된
    /// 패턴이다(일반 Panel의 AutoSize는 도킹된 자식 기준으로 신뢰하기 어렵다).</summary>
    private FlowLayoutPanel BuildCardShell(string title, string iconGlyph, Color iconColor)
    {
        var card = new FlowLayoutPanel
        {
            Width = 392, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 12),
        };
        card.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(UiTheme.ModernCardBorder), 0, 0, card.Width - 1, card.Height - 1);

        card.Controls.Add(new Label
        {
            AutoSize = true, Text = $"{iconGlyph}  {title}", Font = UiTheme.ModernBodyBoldFont, ForeColor = iconColor, Margin = new Padding(0, 0, 0, 6),
        });
        return card;
    }

    private static void AddDivider(FlowLayoutPanel card)
    {
        if (card.Controls.Count > 1)
        {
            card.Controls.Add(new Panel { Width = 360, Height = 1, BackColor = UiTheme.ModernDividerLight, Margin = new Padding(0, 4, 0, 4) });
        }
    }

    private void AddScheduleRow(FlowLayoutPanel card, string timeText, bool bold, string? tag, string title)
    {
        AddDivider(card);
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.Add(new Label
        {
            AutoSize = false, Width = 44, Height = 20, Text = timeText, TextAlign = ContentAlignment.MiddleLeft,
            Font = bold ? UiTheme.ModernLabelBoldFont : UiTheme.ModernSmallFont, ForeColor = bold ? UiTheme.ModernAccent : UiTheme.ModernTextMuted,
        });

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var badge = new Label { AutoSize = true, Height = 20, Text = tag, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 0, 8, 0) };
            UiTheme.StyleModernPill(badge, UiTheme.ModernWarnBg, UiTheme.ModernWarnText, 6);
            row.Controls.Add(badge);
        }

        row.Controls.Add(new Label { AutoSize = true, Text = title, Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText, Margin = new Padding(0, 2, 0, 0) });
        card.Controls.Add(row);
    }

    private void AddTodoRow(FlowLayoutPanel card, string category, string title)
    {
        AddDivider(card);
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.Add(new CheckBox { AutoSize = true, Margin = new Padding(0, 2, 8, 0) });

        if (!string.IsNullOrWhiteSpace(category))
        {
            var badge = new Label { AutoSize = true, Height = 20, Text = category, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 0, 8, 0) };
            UiTheme.StyleModernPill(badge, UiTheme.ModernAccentSoft, UiTheme.ModernAccent, 6);
            row.Controls.Add(badge);
        }

        row.Controls.Add(new Label { AutoSize = true, Text = title, Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText, Margin = new Padding(0, 2, 0, 0) });
        card.Controls.Add(row);
    }

    private static void AddPlainRow(FlowLayoutPanel card, string text)
    {
        AddDivider(card);
        card.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(360, 0), Text = text, Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText, Margin = new Padding(0, 4, 0, 4),
        });
    }

    private void AddEmptyState(string message)
    {
        var card = new Panel
        {
            Width = 392, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 0, 0, 12),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.ModernFieldBorder) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        card.Controls.Add(new Label { AutoSize = true, Text = message, Font = UiTheme.ModernLabelBoldFont, ForeColor = UiTheme.ModernTextMuted });
        _content.Controls.Add(card);
    }

    private async Task LoadAsync()
    {
        _content.Controls.Clear();
        _content.Controls.Add(new Label { AutoSize = true, Text = "불러오는 중...", Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernTextMuted });

        var today = DateTime.Today;

        var scheduleRows = new List<(string Time, bool Bold, string? Tag, string Title)>();
        try
        {
            var events = await _api.GetEventsAsync(today.Year, today.Month);
            if (events.Ok && events.Data is not null)
            {
                foreach (var ev in events.Data)
                {
                    if (ev.StartDateTime.Date > today || ev.EndDateTime.Date < today)
                    {
                        continue;
                    }

                    var tag = ev.Tags.FirstOrDefault(t => DutyRecord.DutyTypes.Contains(t));
                    scheduleRows.Add(ev.AllDay
                        ? ("종일", false, tag, ev.Title)
                        : ($"{ev.StartDateTime:HH:mm}", true, tag, ev.Title));
                }
            }
        }
        catch
        {
            // 조회 실패 시 해당 섹션만 비워둔다.
        }

        var todoRows = new List<(string Category, string Title)>();
        try
        {
            var result = await _api.GetRequestsAsync("tome");
            if (result.Ok && result.Data is not null)
            {
                foreach (var item in result.Data.Items)
                {
                    if (item.Status is "open" or "doing")
                    {
                        todoRows.Add((item.Category ?? "", item.Title));
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
                        trainingLines.Add(string.IsNullOrWhiteSpace(item.DueDate) ? item.Title : $"{item.Title} (기한 {item.DueDate})");
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

        var otherCount = myOpenLines.Count + noticeLines.Count + trainingLines.Count + meetingLines.Count;
        var totalCount = scheduleRows.Count + todoRows.Count + otherCount;

        var name = _session.Profile?.Name;
        _greetingLabel.Text = string.IsNullOrWhiteSpace(name)
            ? $"오늘 확인할 항목이 {totalCount}건 있어요"
            : $"{name} 선생님, 오늘 확인할 항목이 {totalCount}건 있어요";

        _badgeRow.Controls.Clear();
        AddBadge($"일정 {scheduleRows.Count}", UiTheme.ModernAccentSoft, UiTheme.ModernAccent);
        AddBadge($"할 일 {todoRows.Count}", UiTheme.ModernSuccessBg, UiTheme.ModernSuccessText);
        AddBadge($"요청·공지·연수·협의 {otherCount}", UiTheme.ModernDividerLight, UiTheme.ModernTextMuted);

        _content.Controls.Clear();

        var scheduleCard = BuildCardShell("오늘의 일정", "📅", UiTheme.ModernText);
        if (scheduleRows.Count == 0)
        {
            AddPlainRow(scheduleCard, "오늘 등록된 일정이 없습니다.");
        }
        else
        {
            foreach (var row in scheduleRows)
            {
                AddScheduleRow(scheduleCard, row.Time, row.Bold, row.Tag, row.Title);
            }
        }
        _content.Controls.Add(scheduleCard);

        var todoCard = BuildCardShell("내가 해야 할 일", "✅", UiTheme.ModernSuccessText);
        if (todoRows.Count == 0)
        {
            AddPlainRow(todoCard, "오늘 해야 할 일이 없습니다.");
        }
        else
        {
            foreach (var row in todoRows)
            {
                AddTodoRow(todoCard, row.Category, row.Title);
            }
        }
        _content.Controls.Add(todoCard);

        if (otherCount == 0)
        {
            AddEmptyState("새 소식 없음");
        }
        else
        {
            var otherCard = BuildCardShell("요청·공지·연수·협의", "📨", UiTheme.ModernTextMuted);
            AddLinesIfAny(otherCard, "요청사항 (내가 등록)", myOpenLines);
            AddLinesIfAny(otherCard, "안내 및 공지", noticeLines);
            AddLinesIfAny(otherCard, "요청된 법정연수 이수", trainingLines);
            AddLinesIfAny(otherCard, "처리 중인 협의사항", meetingLines);
            _content.Controls.Add(otherCard);
        }
    }

    private void AddLinesIfAny(FlowLayoutPanel card, string subTitle, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        AddDivider(card);
        card.Controls.Add(new Label { AutoSize = true, Text = subTitle, Font = UiTheme.ModernLabelBoldFont, ForeColor = UiTheme.ModernTextMuted, Margin = new Padding(0, 4, 0, 2) });
        foreach (var line in lines)
        {
            card.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(360, 0), Text = $"· {line}", Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText, Margin = new Padding(0, 0, 0, 2) });
        }
    }

    private void AddBadge(string text, Color bg, Color fg)
    {
        var badge = new Label { AutoSize = true, Height = 22, Text = text, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 4, 8, 0) };
        UiTheme.StyleModernPill(badge, bg, fg, 10);
        _badgeRow.Controls.Add(badge);
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
