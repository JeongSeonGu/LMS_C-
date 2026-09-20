using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Interop;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사일정(내 담당업무 관련) · 할일(내가 해야 할 일) · 알림(나에게 지정된 일정)을
/// 날짜별로 모아 보여주는 포스트잇 스타일 오버레이입니다. 선택한 모니터 왼쪽 위에 항상
/// 최상단(topmost)으로 표시되며, 설명이 있는 항목은 클릭하면 펼쳐집니다.
/// </summary>
public sealed class WorkJournalItem
{
    public WorkJournalItem(DateTime date, string title, string? description)
    {
        Date = date;
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
    }

    public DateTime Date { get; }
    public string Title { get; }
    public string? Description { get; }
}

public sealed class WorkJournalForm : Form
{
    private static readonly Color NoteColor = Color.FromArgb(255, 247, 168);
    private static readonly CultureInfo Korean = new("ko-KR");

    // "항상 위(Move To Top)"을 강제로 유지하기 위해 주기적으로 TopMost를 껐다 켠다.
    // (다른 프로그램의 topmost 창에 가려지는 경우를 방지)
    private readonly Timer _topMostTimer = new() { Interval = 3000 };

    private readonly Panel _header = new()
    {
        Dock = DockStyle.Top,
        Height = 28,
        BackColor = Color.FromArgb(255, 200, 90),
    };

    private readonly Label _titleLabel = new()
    {
        Dock = DockStyle.Fill,
        Text = "📌 업무 일지",
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0),
        Font = UiTheme.BoldFont,
        ForeColor = Color.FromArgb(90, 60, 10),
    };

    private readonly Button _closeButton = new()
    {
        Dock = DockStyle.Right,
        Width = 28,
        Text = "×",
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.FromArgb(90, 60, 10),
    };

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = NoteColor,
        Padding = new Padding(8, 6, 8, 8),
    };

    public WorkJournalForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(300, 420);
        BackColor = Color.FromArgb(232, 178, 60); // 바깥쪽 얇은 테두리처럼 보이는 배경색
        Padding = new Padding(2);

        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Click += (_, _) => Hide();

        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_closeButton);

        Controls.Add(_content);
        Controls.Add(_header);

        _topMostTimer.Tick += (_, _) =>
        {
            if (Visible)
            {
                TopMost = false;
                TopMost = true;
            }
        };
        _topMostTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void PositionTopLeft(Screen screen)
    {
        var area = screen.WorkingArea;
        Location = new Point(area.Left + 16, area.Top + 16);
    }

    /// <summary>환경설정 &gt; 업무의 투명도(0~100%)를 적용합니다.</summary>
    public void SetOpacityPercent(int percent)
    {
        Opacity = Math.Clamp(percent, 20, 100) / 100.0;
    }

    public void SetItems(IReadOnlyList<WorkJournalItem> items)
    {
        _content.SuspendLayout();
        _content.Controls.Clear();

        if (items.Count == 0)
        {
            _content.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "표시할 항목이 없습니다.",
                ForeColor = Color.FromArgb(120, 95, 40),
                Margin = new Padding(4, 8, 0, 0),
            });
        }
        else
        {
            foreach (var group in items.GroupBy(i => i.Date).OrderBy(g => g.Key))
            {
                _content.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = group.Key.ToString("M월 d일 (ddd)", Korean),
                    Font = UiTheme.BoldFont,
                    ForeColor = Color.FromArgb(150, 90, 10),
                    Margin = new Padding(0, 10, 0, 2),
                });

                foreach (var item in group.OrderBy(i => i.Title))
                {
                    _content.Controls.Add(BuildItemRow(item));
                }
            }
        }

        _content.ResumeLayout();
    }

    private static Control BuildItemRow(WorkJournalItem item)
    {
        var hasDescription = item.Description is not null;

        var row = new Panel { Width = 268, AutoSize = true, Margin = new Padding(4, 0, 0, 2) };

        var titleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = (hasDescription ? "📝 " : "• ") + item.Title,
            ForeColor = Color.FromArgb(60, 45, 10),
            Cursor = hasDescription ? Cursors.Hand : Cursors.Default,
        };

        var descLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(244, 0),
            Left = 16,
            Top = titleLabel.PreferredHeight,
            Text = item.Description ?? "",
            ForeColor = Color.FromArgb(110, 90, 40),
            Visible = false,
        };

        row.Controls.Add(titleLabel);
        row.Controls.Add(descLabel);
        row.Height = titleLabel.PreferredHeight;

        if (hasDescription)
        {
            titleLabel.Click += (_, _) =>
            {
                descLabel.Visible = !descLabel.Visible;
                row.Height = titleLabel.PreferredHeight + (descLabel.Visible ? descLabel.PreferredHeight + 4 : 0);
            };
        }

        return row;
    }
}
