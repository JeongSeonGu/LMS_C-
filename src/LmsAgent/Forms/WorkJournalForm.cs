using System;
using System.Collections.Generic;
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
    /// <param name="icon">기본 "•"/"▶" 대신 쓸 아이콘(이모지). 개인일정처럼 학사 일정과 구분해야
    /// 하는 항목에 사용합니다(null이면 기존처럼 설명 유무에 따라 자동으로 정해집니다).</param>
    /// <param name="accentColor">제목 글자색을 이 색으로 덮어씁니다(null이면 기존 기본색을 씁니다).</param>
    public WorkJournalItem(
        DateTime date, string title, string? description, TimeSpan? time = null,
        string? icon = null, Color? accentColor = null)
    {
        Date = date;
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Time = time;
        Icon = icon;
        AccentColor = accentColor;
    }

    public DateTime Date { get; }
    public string Title { get; }
    public string? Description { get; }
    public TimeSpan? Time { get; }
    public string? Icon { get; }
    public Color? AccentColor { get; }
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

    /// <summary>업무 일지 위에 겹쳐 그려지는 것이 아니라, 클릭하면 완전히 별도의 스티커 메모
    /// 창(들)이 뜬다(<see cref="StickyNoteForm"/>). 이 폼은 그 창을 직접 만들지 않고, 이
    /// 이벤트를 구독한 쪽(WorkJournalService)이 StickyNoteService로 열어 준다.</summary>
    public event EventHandler? MemoButtonClicked;

    /// <summary>알림/요청사항/법정연수 항목의 링크를 클릭했을 때 발생합니다(상대 경로 그대로).
    /// 이 폼은 브라우저를 직접 열지 않는다 — C#이 로그인되어 있으면 SSO 티켓으로 열어야 하는데,
    /// 그 로직(및 API 접근)은 WorkJournalService가 갖고 있으므로 여기서는 요청만 알린다.</summary>
    public event EventHandler<string>? LinkClicked;

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

    // 실시간 이벤트를 받을 때마다(요청사항/알림/법정연수/학사일정 갱신 등) SetContent가
    // Controls.Clear() 후 여러 줄을 한꺼번에 다시 추가하는데, 일반 FlowLayoutPanel은
    // 더블 버퍼링이 꺼져 있어 이 과정이 화면에 그대로 비쳐(이전 내용과 새 내용이 한
    // 프레임 안에서 겹쳐 그려지는 "찢어짐") 항목들이 뒤엉켜 보이는 경우가 있었다.
    private readonly DoubleBufferedFlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = NoteColor,
        Padding = new Padding(8, 6, 8, 8),
    };

    /// <summary>더블 버퍼링만 켠 FlowLayoutPanel. <see cref="Control.DoubleBuffered"/>가
    /// protected라 직접 켤 수 없어 이렇게 한 줄짜리 서브클래스로 감쌌다.</summary>
    private sealed class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public DoubleBufferedFlowLayoutPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

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
        _memoButton.Click += (_, _) => MemoButtonClicked?.Invoke(this, EventArgs.Empty);

        // 헤더 순서: 닫기(×) 버튼을 먼저 추가해 항상 맨 오른쪽 끝에 고정하고,
        // 쪽지(🗒) 버튼을 그 다음에 추가해 닫기 버튼 바로 왼쪽(타이틀 근처)에 놓는다.
        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_closeButton);
        _header.Controls.Add(_memoButton);

        Controls.Add(_content);
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

    /// <summary>환경설정 &gt; 업무의 출력 단위(일/주)에 맞춰 헤더에 날짜를 보여줍니다.
    /// 일 단위: "업무 일지(2026.09.25)", 주 단위: "업무 일지(09.21 ~ 09.27)". 주 단위는
    /// 헤더 폭이 좁아 연도까지 넣으면 전체 문구가 잘려 보이므로 연도를 뺀다.</summary>
    public void SetHeaderRange(DateTime rangeStart, DateTime rangeEnd)
    {
        var range = rangeStart == rangeEnd
            ? rangeStart.ToString("yyyy.MM.dd")
            : $"{rangeStart:MM.dd} ~ {rangeEnd:MM.dd}";
        _titleLabel.Text = $"📌 업무 일지({range})";
    }

    /// <summary>아직 로그인하지 않은 상태임을 사용자가 직관적으로 알 수 있도록 보여줍니다.
    /// 담당업무 필터링이 로그인 계정 기준이라 로그인 전에는 조회 자체를 하지 않으므로,
    /// 아무 안내 없이 빈 채로 두면 "고장났다"고 오해할 수 있다.</summary>
    public void SetLoggedOut()
    {
        _titleLabel.Text = "📌 업무 일지";
        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = "🔒 아직 로그인 전입니다.\n트레이 메뉴 > 사용자 정보 > 로그인 후 이용하세요.",
            ForeColor = Color.FromArgb(120, 95, 40),
            Margin = new Padding(4, 8, 0, 0),
        });
        _content.ResumeLayout();
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

    /// <summary>제목과 상태 배지를 같은 줄에 문자열로 이어 붙이면("[기한초과] 제목") 둘이
    /// 하나로 뭉쳐 보여서 무엇이 분류·상태이고 무엇이 실제 내용인지 구분하기 어려웠다.
    /// 지금은 제목을 한 줄로, 배지가 있으면 그 아래 줄에 색이 있는 배지로 따로 보여준다.
    /// 중첩 FlowLayoutPanel(TopDown)에 맡기면 각 줄의 높이를 직접 계산할 필요가 없어
    /// AutoSize Panel에 수동으로 Height를 다시 지정하다 생기던 행간 오정렬도 원천적으로
    /// 피할 수 있다.
    ///
    /// 제목을 클릭해도 바로 웹페이지로 이동하지 않는다 — ▶/▼로 내용(제목+배지)을 먼저
    /// 펼쳐 보여주고, 펼친 내용 맨 아래의 "[세부 페이지 이동]" 링크를 따로 클릭해야
    /// 실제로 브라우저가 열린다(클릭 한 번에 바로 페이지가 튀어나가 당황스럽다는 피드백 반영).</summary>
    private Control BuildAlertRow(WorkJournalAlert alert)
    {
        var hasLink = !string.IsNullOrWhiteSpace(alert.Link);

        var card = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(4, 0, 0, 6),
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(252, 0),
            Text = (hasLink ? "▶ " : "• ") + alert.Title,
            ForeColor = hasLink ? ExpandableColor : Color.FromArgb(60, 45, 10),
            Cursor = hasLink ? Cursors.Hand : Cursors.Default,
            Margin = new Padding(0),
        };

        card.Controls.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(alert.Badge))
        {
            card.Controls.Add(BuildBadge(alert.Badge));
        }

        if (hasLink)
        {
            var goToPageLabel = new Label
            {
                AutoSize = true,
                Text = "[세부 페이지 이동]",
                ForeColor = UiTheme.SkyDark,
                Font = new Font(UiTheme.BaseFont, FontStyle.Underline),
                Cursor = Cursors.Hand,
                Visible = false,
                Margin = new Padding(16, 4, 0, 0),
            };
            goToPageLabel.Click += (_, _) => LinkClicked?.Invoke(this, alert.Link!);
            card.Controls.Add(goToPageLabel);

            titleLabel.Click += (_, _) =>
            {
                goToPageLabel.Visible = !goToPageLabel.Visible;
                titleLabel.Text = (goToPageLabel.Visible ? "▼ " : "▶ ") + alert.Title;
            };
        }

        return card;
    }

    /// <summary>"긴급"·"기한초과"·"보완요청"·"확인대기" 같은 상태를 웹 화면처럼 색이 있는
    /// 작은 배지로 보여준다(둥근 모서리는 아니지만, 텍스트에 섞이지 않고 한눈에 띄는 것이
    /// 목적이라 이 정도로 충분하다).</summary>
    private static Label BuildBadge(string text)
    {
        var color = text switch
        {
            "긴급" or "기한초과" or "보완요청" => UiTheme.Danger,
            _ => UiTheme.TextSecondary, // "확인대기" 등
        };

        return new Label
        {
            AutoSize = true,
            Text = text,
            ForeColor = Color.White,
            BackColor = color,
            Font = new Font(UiTheme.BaseFont.FontFamily, 7.5F, FontStyle.Bold),
            Padding = new Padding(6, 1, 6, 1),
            Margin = new Padding(16, 2, 0, 0),
        };
    }

    private Control BuildItemRow(WorkJournalItem item)
    {
        var hasDescription = item.Description is not null;

        // AutoSize=true인 Panel에 Height까지 수동으로 다시 지정하면(예전 코드) 두 크기
        // 산정 방식이 충돌해 행간이 어긋나 보였다 — 지금은 AutoSize를 끄고 Height만 직접
        // 관리한다(펼침/접힘 토글도 그대로 이 값만 갱신하면 된다).
        var row = new Panel { Width = 268, AutoSize = false, Margin = new Padding(4, 0, 0, 4) };

        var timePrefix = item.Time is { } t ? t.ToString(@"hh\:mm") + "  " : "";

        // 클릭하면 설명이 펼쳐지는 항목은 화살표(▶/▼)로 눈에 띄게 표시하고, 색도 다르게 준다.
        // 설명이 없는 항목은 기존처럼 수수한 점(•)만 붙는다. icon이 지정된 항목(개인일정 등)은
        // 그 아이콘과 지정된 색을 그대로 쓴다.
        var prefix = item.Icon is { } icon ? icon + " " : (hasDescription ? "▶ " : "• ");
        var titleColor = item.AccentColor ?? (hasDescription ? ExpandableColor : Color.FromArgb(60, 45, 10));

        var titleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = timePrefix + prefix + item.Title,
            ForeColor = titleColor,
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
            _topMostTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
