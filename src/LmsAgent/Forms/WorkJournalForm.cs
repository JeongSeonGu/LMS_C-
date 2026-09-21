using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Interop;
using LmsAgent.Services;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Forms;

/// <summary>
/// 학사일정(내 담당업무 관련) · 할일(내가 해야 할 일) · 알림(나에게 지정된 일정)을
/// 날짜별로 모아 보여주는 포스트잇 스타일 오버레이입니다. 선택한 모니터 왼쪽 위에 항상
/// 최상단(topmost)으로 표시되며, 설명이 있는 항목은 클릭하면 펼쳐집니다.
/// </summary>
public sealed class WorkJournalItem
{
    /// <param name="time">일정의 시작 시각. 종일 일정·할일처럼 특정 시각이 없으면 null(맨 위에 먼저 표시).</param>
    public WorkJournalItem(DateTime date, string title, string? description, TimeSpan? time = null)
    {
        Date = date;
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Time = time;
    }

    public DateTime Date { get; }
    public string Title { get; }
    public string? Description { get; }
    public TimeSpan? Time { get; }
}

/// <summary>
/// 학사 달력 밖의 요약 항목(요청사항 · 알림 · 법정연수). 날짜별 목록과 달리 날짜가 아니라
/// 분류별로 묶어서 보여주며, 클릭하면 웹 페이지(link)를 엽니다(학사달력외_연동가이드.md).
/// </summary>
public sealed class WorkJournalAlert
{
    /// <param name="category">"요청사항" · "알림" · "법정연수".</param>
    /// <param name="link">서버 기준 상대 경로. 클릭 시 앞에 호스트를 붙여 브라우저로 엽니다.</param>
    /// <param name="badge">"긴급" · "보완요청" · "확인대기" 같은 짧은 상태 배지. 없으면 null.</param>
    public WorkJournalAlert(string category, string title, string? link, string? badge = null)
    {
        Category = category;
        Title = title;
        Link = link;
        Badge = badge;
    }

    public string Category { get; }
    public string Title { get; }
    public string? Link { get; }
    public string? Badge { get; }
}

public sealed class WorkJournalForm : Form
{
    private static readonly Color NoteColor = Color.FromArgb(255, 247, 168);
    private static readonly Color ExpandableColor = Color.FromArgb(196, 92, 16); // 눈에 띄는 주황 — 클릭하면 펼쳐지는 항목 전용
    private static readonly CultureInfo Korean = new("ko-KR");

    // "항상 위(Move To Top)"을 강제로 유지하기 위해 주기적으로 Z-order를 다시 맨 위로 올린다
    // (다른 프로그램의 topmost 창에 가려지는 경우를 방지). 구현 방식은 아래 생성자 참고.
    private readonly Timer _topMostTimer = new() { Interval = 3000 };
    private readonly Timer _memoSaveTimer = new() { Interval = 800 };

    private bool _memoMode;

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

    private readonly Button _memoButton = new()
    {
        Dock = DockStyle.Right,
        Width = 28,
        Text = "🗒",
        FlatStyle = FlatStyle.Flat,
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

    // 쪽지(스티커 메모) 모드 — 헤더의 🗒 버튼을 누르면 목록 대신 이 자유 메모 화면으로 바뀐다.
    // 서버와 주고받지 않는 개인용 메모라 로컬 파일에만 저장한다(WorkNoteStore).
    private readonly Panel _memoPanel = new()
    {
        Dock = DockStyle.Fill,
        BackColor = NoteColor,
        Padding = new Padding(8, 6, 8, 8),
        Visible = false,
    };

    private readonly TextBox _memoBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        BackColor = NoteColor,
        ForeColor = Color.FromArgb(60, 45, 10),
        Font = new Font("맑은 고딕", 10F),
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

        _memoButton.FlatAppearance.BorderSize = 0;
        _memoButton.Click += (_, _) => ToggleMemoMode();

        // 헤더 순서: 닫기(×) 버튼을 먼저 추가해 항상 맨 오른쪽 끝에 고정하고,
        // 쪽지(🗒) 버튼을 그 다음에 추가해 닫기 버튼 바로 왼쪽(타이틀 근처)에 놓는다.
        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_closeButton);
        _header.Controls.Add(_memoButton);

        _memoPanel.Controls.Add(_memoBox);
        _memoBox.Text = WorkNoteStore.Load();
        _memoBox.TextChanged += (_, _) =>
        {
            _memoSaveTimer.Stop();
            _memoSaveTimer.Start();
        };
        _memoSaveTimer.Tick += (_, _) =>
        {
            _memoSaveTimer.Stop();
            WorkNoteStore.Save(_memoBox.Text);
        };

        Controls.Add(_content);
        Controls.Add(_memoPanel);
        Controls.Add(_header);

        // ⚠ Form.TopMost를 껐다 켜는 방식(TopMost = false; TopMost = true;)은
        // 내부적으로 HWND_NOTOPMOST → HWND_TOPMOST 순서로 두 번 Z-order를 바꾸는데,
        // 이 과정에서 현재 활성 창에 WM_NCACTIVATE(비활성) 메시지가 전달되어
        // 다른 프로그램의 모달 창 활성 표시가 사라지거나, 열려 있던 컨텍스트 메뉴/팝업이
        // 저절로 닫히거나, 입력 필드의 커서가 사라지는 등의 부작용을 일으킨다.
        // SWP_NOACTIVATE를 준 SetWindowPos로 HWND_TOPMOST 위치만 다시 확인시켜주면
        // 활성 상태를 건드리지 않고 "맨 위 유지" 효과만 얻을 수 있다.
        _topMostTimer.Tick += (_, _) =>
        {
            if (Visible)
            {
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
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

    /// <summary>🗒 버튼으로 목록/쪽지 화면을 전환합니다. 쪽지 내용은 로컬에만 저장됩니다.</summary>
    private void ToggleMemoMode()
    {
        _memoMode = !_memoMode;
        _content.Visible = !_memoMode;
        _memoPanel.Visible = _memoMode;
        _memoButton.ForeColor = _memoMode ? UiTheme.OrangeDark : Color.FromArgb(90, 60, 10);

        if (_memoMode)
        {
            _memoBox.Focus();
            _memoBox.SelectionStart = _memoBox.Text.Length;
        }
        else if (_memoSaveTimer.Enabled)
        {
            // 화면을 바로 닫아도 마지막 입력이 사라지지 않도록 즉시 저장한다.
            _memoSaveTimer.Stop();
            WorkNoteStore.Save(_memoBox.Text);
        }
    }

    /// <param name="alerts">요청사항·알림·법정연수 등 학사 달력 밖의 "확인 필요" 항목(분류별로 묶어 상단에 표시).</param>
    /// <param name="items">학사일정·할일 등 날짜별 항목(기존 방식대로 날짜별로 묶어 표시).</param>
    public void SetContent(IReadOnlyList<WorkJournalAlert> alerts, IReadOnlyList<WorkJournalItem> items)
    {
        _content.SuspendLayout();
        _content.Controls.Clear();

        if (alerts.Count > 0)
        {
            foreach (var group in alerts.GroupBy(a => a.Category))
            {
                _content.Controls.Add(BuildAlertCategoryHeader(group.Key, group.Count()));
                foreach (var alert in group)
                {
                    _content.Controls.Add(BuildAlertRow(alert));
                }
            }

            _content.Controls.Add(new Panel
            {
                Width = 268, Height = 1, BackColor = Color.FromArgb(224, 196, 120),
                Margin = new Padding(0, 8, 0, 4),
            });
        }

        if (items.Count == 0)
        {
            if (alerts.Count == 0)
            {
                _content.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "표시할 항목이 없습니다.",
                    ForeColor = Color.FromArgb(120, 95, 40),
                    Margin = new Padding(4, 8, 0, 0),
                });
            }
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

                // 시각이 있는 항목은 이른 시간 순으로, 시각이 없는 종일 일정·할일은 맨 위에 모아 보여준다.
                foreach (var item in group.OrderBy(i => i.Time.HasValue).ThenBy(i => i.Time).ThenBy(i => i.Title))
                {
                    _content.Controls.Add(BuildItemRow(item));
                }
            }
        }

        _content.ResumeLayout();
    }

    private static Control BuildAlertCategoryHeader(string category, int count)
    {
        var (icon, color) = category switch
        {
            "요청사항" => ("📮", UiTheme.OrangeDark),
            "알림" => ("🔔", UiTheme.Danger),
            "법정연수" => ("🎓", UiTheme.SkyDark),
            _ => ("•", Color.FromArgb(150, 90, 10)),
        };

        return new Label
        {
            AutoSize = true,
            Text = $"{icon} {category} ({count})",
            Font = UiTheme.BoldFont,
            ForeColor = color,
            Margin = new Padding(0, 6, 0, 2),
        };
    }

    private static Control BuildAlertRow(WorkJournalAlert alert)
    {
        var row = new Panel { Width = 268, AutoSize = true, Margin = new Padding(4, 0, 0, 2) };
        var badge = string.IsNullOrWhiteSpace(alert.Badge) ? "" : $"[{alert.Badge}] ";
        var hasLink = !string.IsNullOrWhiteSpace(alert.Link);

        var label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = (hasLink ? "▶ " : "• ") + badge + alert.Title,
            ForeColor = hasLink ? ExpandableColor : Color.FromArgb(60, 45, 10),
            Cursor = hasLink ? Cursors.Hand : Cursors.Default,
        };

        if (hasLink)
        {
            label.Click += (_, _) => OpenLink(alert.Link!);
        }

        row.Controls.Add(label);
        row.Height = label.PreferredHeight;
        return row;
    }

    private static void OpenLink(string relativeLink)
    {
        try
        {
            var url = relativeLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? relativeLink
                : "https://future-class.kr" + relativeLink;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 브라우저를 열지 못해도 업무 일지 자체는 계속 동작해야 한다.
        }
    }

    private static Control BuildItemRow(WorkJournalItem item)
    {
        var hasDescription = item.Description is not null;

        var row = new Panel { Width = 268, AutoSize = true, Margin = new Padding(4, 0, 0, 2) };

        var timePrefix = item.Time is { } t ? t.ToString(@"hh\:mm") + "  " : "";

        // 클릭하면 설명이 펼쳐지는 항목은 화살표(▶/▼)로 눈에 띄게 표시하고, 색도 다르게 준다.
        // 설명이 없는 항목은 기존처럼 수수한 점(•)만 붙는다.
        var titleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = timePrefix + (hasDescription ? "▶ " : "• ") + item.Title,
            ForeColor = hasDescription ? ExpandableColor : Color.FromArgb(60, 45, 10),
            Font = hasDescription ? UiTheme.BoldFont : UiTheme.BaseFont,
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
                titleLabel.Text = timePrefix + (descLabel.Visible ? "▼ " : "▶ ") + item.Title;
                row.Height = titleLabel.PreferredHeight + (descLabel.Visible ? descLabel.PreferredHeight + 4 : 0);
            };
        }

        return row;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_memoSaveTimer.Enabled)
            {
                WorkNoteStore.Save(_memoBox.Text);
            }

            _topMostTimer.Dispose();
            _memoSaveTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
