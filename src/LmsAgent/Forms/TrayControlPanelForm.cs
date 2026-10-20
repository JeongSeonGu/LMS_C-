using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 트레이 아이콘을 왼쪽 클릭했을 때 뜨는 창입니다. 오른쪽 클릭 시 나오는 ContextMenuStrip과
/// 완전히 같은 메뉴 구성을, 네이비 사이드바 + 파란색 강조색의 모던 "메인 창" 디자인으로
/// 다시 보여줍니다 — 왼쪽에 네이비 사이드바(카테고리), 오른쪽에 그 카테고리의 타일들을
/// 보여주는 구성입니다. 각 항목의 실제 동작은 여전히 원본 ContextMenuStrip
/// (TrayApplicationContext)이 갖고 있으므로, 이 창은 그 항목의
/// <see cref="ToolStripItem.PerformClick"/>을 그대로 호출할 뿐 별도의 로직을 갖지 않습니다
/// — 메뉴 구성이 바뀌어도 이 창을 따로 손볼 필요가 없습니다. 이미 열려 있으면 새로 만들지
/// 않고 기존 창을 앞으로 가져오는 것은 <see cref="LmsAgent.App.TrayApplicationContext"/>의
/// 몫입니다(이 클래스는 자기 자신이 중복 실행되었는지는 알 수 없습니다).
///
/// 사이드바의 "오늘 한눈에" 같은 새 대시보드 페이지는 이 창에 없습니다 — 아직 그 데이터를
/// 모아 보여줄 기능 자체가 없어서, 실제 메뉴 항목으로 존재하는 카테고리만 그대로
/// 보여줍니다(목업과 달리 사이드바 항목 구성은 메뉴 그대로입니다).
/// </summary>
public sealed class TrayControlPanelForm : Form
{
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 40, BackColor = Color.White };
    private readonly FlowLayoutPanel _sidebar = new()
    {
        Dock = DockStyle.Left, Width = 232, BackColor = UiTheme.ModernNavy,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
        Padding = new Padding(12, 16, 12, 16),
    };
    private readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.White };

    private readonly Label _contentTitle = new()
    {
        Dock = DockStyle.Top, Height = 40, Font = UiTheme.ModernHeadlineFont, ForeColor = UiTheme.ModernText,
        Padding = new Padding(0, 4, 0, 8),
    };

    // 세로로 긴 목록 대신, 참고 이미지처럼 정사각형/직사각형 타일 버튼이 가로로 나열되다가
    // 폭이 차면 다음 줄로 넘어가는 "카드 그리드" 배치로 바꿨다(WrapContents=true).
    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
        BackColor = UiTheme.Surface, Padding = new Padding(24, 16, 24, 16),
    };

    private const int TileWidth = 200;
    private const int TileHeight = 130;

    // 사이드바(232px) 안쪽에서 버튼/구분선이 실제로 차지할 수 있는 폭. 사이드바 자체의
    // Padding(좌우 12px씩)과 각 항목의 Margin(좌우 4px씩)을 미리 빼둔 값이다 — 이 둘을
    // 감안하지 않고 "_sidebar.Width - 24"만 쓰면 항목이 Margin만큼 오른쪽으로 넘쳐서
    // 가로 스크롤이 생긴다.
    private const int SidebarItemWidth = 232 - 24 - 8;

    private readonly Panel _contentHost = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

    private Button? _selectedNavButton;
    private int _actionWidth = 380;

    private readonly SessionManager _session;
    private readonly Action _onLogout;

    /// <param name="session">사이드바 위쪽 아바타·이름과 로그아웃 버튼 표시 여부를 위한
    /// 읽기 전용 참조입니다(이 창을 여는 시점의 스냅샷 — 다른 패널들과 같은 규칙).</param>
    /// <param name="onLogout">사이드바의 "로그아웃" 버튼을 눌렀을 때 실행할 동작. 실제 로그아웃
    /// 처리(서버 통보 + 로컬 세션 정리)는 <see cref="LmsAgent.App.TrayApplicationContext"/>가
    /// 갖고 있으므로, 이 창은 다른 메뉴 항목들과 마찬가지로 호출만 위임합니다.</param>
    public TrayControlPanelForm(ContextMenuStrip menu, SessionManager session, Action onLogout)
    {
        _session = session;
        _onLogout = onLogout;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = UiTheme.ModernFieldBorder; // 바깥쪽 얇은 테두리처럼 보이는 배경색
        Padding = new Padding(1);
        Font = UiTheme.ModernBodyFont;

        // 학사달력 배경화면(월 단위)과 같은 규칙 — 최대 1024×768, 화면보다 작은 모니터에서는
        // 그 작업 영역에 맞춘다. 화면 가운데가 아니라 "위쪽"에 띄워서, 트레이 아이콘을 눌렀을
        // 때 새 창이 나타났다는 것을 한눈에 알아볼 수 있게 한다.
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var width = Math.Min(1024, area.Width);
        var height = Math.Min(768, area.Height);
        Size = new Size(width, height);
        Location = new Point(area.Left + (area.Width - width) / 2, area.Top + 24);

        // 창이 커진 만큼(1024×768) 오른쪽 버튼 목록도 380px 고정폭 대신 사이드바 폭·여백을
        // 뺀 만큼 넓게 채운다(스크롤바 여유분 포함).
        _actionWidth = Math.Max(380, width - _sidebar.Width - 96);

        BuildHeader();
        BuildStatusBar(menu.Items);

        _contentHost.Controls.Add(_content);
        _contentHost.Controls.Add(_contentTitle);

        // Fill부터 먼저 추가해야 이후 추가하는 Bottom/Left/Top 도킹 컨트롤들이 각자의
        // 가장자리를 정확히 차지하고, Fill은 남은 영역을 채운다(이 저장소의 다른 커스텀
        // 창들과 동일한 컨트롤 추가 순서 규칙).
        Controls.Add(_contentHost);
        Controls.Add(_statusBar);
        Controls.Add(_sidebar);
        Controls.Add(_header);

        BuildSidebar(menu.Items);
    }

    private void BuildHeader()
    {
        var logoHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(14, 10, 0, 0),
        };

        var logo = new Panel { Width = 20, Height = 20, BackColor = UiTheme.ModernAccent, Margin = new Padding(0, 0, 10, 0) };
        UiTheme.ApplyRoundedRegion(logo, 6);
        logoHost.Controls.Add(logo);
        logoHost.Controls.Add(new Label
        {
            AutoSize = true, Text = "LMS 연동 프로그램", Font = UiTheme.ModernLabelBoldFont, ForeColor = UiTheme.ModernText, Margin = new Padding(0, 2, 0, 0),
        });

        var minimizeButton = new Button { Width = 44, Height = 40, Text = "─", Margin = new Padding(0) };
        var closeButton = new Button { Width = 44, Height = 40, Text = "×", Font = new Font(UiTheme.BaseFont.FontFamily, 13F, FontStyle.Bold), Margin = new Padding(0) };
        UiTheme.StyleModernWindowButton(minimizeButton);
        UiTheme.StyleModernWindowButton(closeButton, isClose: true);
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();

        var rightCluster = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White,
        };
        rightCluster.Controls.Add(closeButton);
        rightCluster.Controls.Add(minimizeButton);

        _header.Controls.Add(logoHost);
        _header.Controls.Add(rightCluster);
        _header.Paint += (_, e) => e.Graphics.DrawLine(new Pen(UiTheme.ModernDivider), 0, _header.Height - 1, _header.Width, _header.Height - 1);

        // 테두리 없는 창이라 제목표시줄을 직접 드래그로 옮길 수 있게 한다.
        UiTheme.EnableDragMove(_header, this);
        UiTheme.EnableDragMove(logoHost, this);
    }

    /// <summary>연결 상태·라이센스 상태(Enabled=false인 항목)는 사이드바가 아니라 하단
    /// 상태 표시줄에 보여준다. 창을 여는 시점의 스냅샷이며(우클릭 메뉴도 열 때마다
    /// 다시 그려지는 것은 같으므로), 실시간으로 갱신되진 않는다.</summary>
    private void BuildStatusBar(ToolStripItemCollection items)
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(16, 6, 0, 0),
        };

        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripMenuItem { Enabled: false } status)
            {
                flow.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = status.Text,
                    Font = UiTheme.ModernSmallFont,
                    ForeColor = UiTheme.ModernTextMuted,
                    Margin = new Padding(0, 0, 24, 0),
                });
            }
        }

        _statusBar.Controls.Add(flow);
        _statusBar.Paint += (_, e) => e.Graphics.DrawLine(new Pen(UiTheme.ModernDivider), 0, 0, _statusBar.Width, 0);
    }

    private void BuildSidebar(ToolStripItemCollection items)
    {
        BuildSidebarAvatar();

        Button? firstCategoryButton = null;

        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripSeparator)
            {
                _sidebar.Controls.Add(new Panel
                {
                    Width = SidebarItemWidth, Height = 1, BackColor = UiTheme.ModernNavyBorder, Margin = new Padding(4, 8, 4, 8),
                });
                continue;
            }

            if (item is not ToolStripMenuItem { Enabled: true } menuItem)
            {
                continue; // 비활성(상태) 항목은 사이드바에 넣지 않는다.
            }

            var nav = BuildNavButton(menuItem);
            _sidebar.Controls.Add(nav);

            if (menuItem.HasDropDownItems && firstCategoryButton is null)
            {
                firstCategoryButton = nav;
            }
        }

        // 로그인 중일 때만, 목업처럼 사이드바 맨 아래에 로그아웃 버튼을 보여준다(실제 처리는
        // TrayApplicationContext가 전달한 onLogout 콜백에 위임 — 다른 항목들과 같은 규칙).
        if (_session.Profile is not null)
        {
            _sidebar.Controls.Add(new Panel
            {
                Width = SidebarItemWidth, Height = 1, BackColor = UiTheme.ModernNavyBorder, Margin = new Padding(4, 8, 4, 8),
            });

            var logoutButton = BuildPlainNavButton("로그아웃");
            logoutButton.Click += (_, _) =>
            {
                _onLogout();
                Close();
            };
            _sidebar.Controls.Add(logoutButton);
        }

        // 처음 열었을 때 빈 화면이 아니라, 첫 번째 카테고리를 기본으로 보여준다.
        if (firstCategoryButton is not null)
        {
            firstCategoryButton.PerformClick();
        }
        else
        {
            _contentTitle.Text = "LMS 연동 프로그램";
        }
    }

    private void BuildSidebarAvatar()
    {
        var host = new FlowLayoutPanel
        {
            Width = SidebarItemWidth, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(4, 0, 4, 16),
        };

        var profile = _session.Profile;
        var initial = string.IsNullOrWhiteSpace(profile?.Name) ? "?" : profile.Name[..1];

        var avatar = new Panel { Width = 40, Height = 40, BackColor = UiTheme.ModernNavyActive, Margin = new Padding(0, 0, 12, 0) };
        UiTheme.ApplyRoundedRegion(avatar, 20);
        avatar.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Text = initial, Font = UiTheme.ModernLabelBoldFont, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter,
        });
        host.Controls.Add(avatar);

        var textStack = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Margin = new Padding(0, 2, 0, 0) };
        textStack.Controls.Add(new Label
        {
            AutoSize = true, Text = profile is null ? "로그인 전" : $"{profile.Name} 선생님", Font = UiTheme.ModernLabelBoldFont, ForeColor = Color.White,
        });
        textStack.Controls.Add(new Label
        {
            AutoSize = true, Text = profile is null ? "" : $"{profile.LoginId} · {profile.SchoolName}", Font = UiTheme.ModernTinyFont, ForeColor = UiTheme.ModernNavyMuted,
        });
        host.Controls.Add(textStack);

        _sidebar.Controls.Add(host);
        _sidebar.Controls.Add(new Panel { Width = SidebarItemWidth, Height = 1, BackColor = UiTheme.ModernNavyBorder, Margin = new Padding(4, 0, 4, 12) });
    }

    private Button BuildNavButton(ToolStripMenuItem menuItem)
    {
        var text = menuItem.Text?.Replace("...", "").TrimEnd() ?? "";
        var button = BuildPlainNavButton($"{IconFor(text)}   {text}");

        if (menuItem.HasDropDownItems)
        {
            button.Click += (_, _) =>
            {
                Select(button);
                ShowCategory(text, menuItem.DropDownItems);
            };
        }
        else
        {
            // 하위 메뉴가 없는 단일 동작(예: 환경설정..., 종료)은 눌렀을 때 바로 실행하고 닫는다.
            button.Click += (_, _) =>
            {
                menuItem.PerformClick();
                Close();
            };
        }

        return button;
    }

    /// <summary>사이드바 메뉴 버튼 하나의 모던 스타일(둥근 모서리·네이비 배경·흰 글자)을
    /// 만든다. 실제 동작은 호출자가 Click에 직접 붙인다.</summary>
    private Button BuildPlainNavButton(string text)
    {
        var button = new Button
        {
            Width = SidebarItemWidth,
            Height = 40,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(4, 0, 4, 2),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = UiTheme.ModernNavy,
            Font = UiTheme.ModernBodyFont,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.ModernNavyActive;
        UiTheme.ApplyRoundedRegion(button, 8);
        return button;
    }

    private void Select(Button nav)
    {
        if (_selectedNavButton is not null)
        {
            _selectedNavButton.BackColor = UiTheme.ModernNavy;
        }

        _selectedNavButton = nav;
        nav.BackColor = UiTheme.ModernNavyActive;
    }

    private void ShowCategory(string title, ToolStripItemCollection children)
    {
        _contentTitle.Text = title;
        _content.SuspendLayout();
        _content.Controls.Clear();

        foreach (ToolStripItem child in children)
        {
            if (child is ToolStripSeparator)
            {
                // 타일이 가로로 흐르다가(WrapContents) 이 구분선의 너비가 남은 공간보다
                // 넓어서 자기 자신이 새 줄로 밀려나고, 그 다음 타일도 자연히 그 아래 줄부터
                // 다시 시작한다 — 세로 목록이었을 때와 같은 그룹 구분 효과를 그대로 낸다.
                _content.Controls.Add(new Panel
                {
                    Width = _actionWidth, Height = 1, BackColor = UiTheme.Border, Margin = new Padding(0, 4, 0, 16),
                });
            }
            else if (child is ToolStripMenuItem childItem)
            {
                _content.Controls.Add(BuildActionButton(childItem));
            }
        }

        _content.ResumeLayout();
    }

    /// <summary>세로로 긴 목록 항목 대신, 모서리가 둥근 정사각형/직사각형 타일 버튼으로
    /// 보여준다(참고 이미지의 카드 그리드 스타일).</summary>
    private Button BuildActionButton(ToolStripMenuItem item)
    {
        var button = new Button
        {
            Width = TileWidth,
            Height = TileHeight,
            Text = item.Text.Replace("...", "").TrimEnd(),
            Margin = new Padding(0, 0, 16, 16),
        };
        UiTheme.StyleLiquidTileButton(button);

        // 이 창은 항목을 소유하지 않으므로, 실제 처리는 원본 메뉴 항목의 Click 핸들러에
        // 그대로 맡긴다(트레이 컨텍스트 메뉴를 오른쪽 클릭으로 열었을 때와 완전히 동일한 동작).
        button.Click += (_, _) =>
        {
            item.PerformClick();
            Close();
        };

        return button;
    }

    private static string IconFor(string text) => text switch
    {
        _ when text.Contains("교무업무") => "🌐",
        _ when text.Contains("학사 일정") => "📅",
        _ when text.Contains("사용자 정보") => "👤",
        _ when text.Contains("기본정보") => "📋",
        _ when text.Contains("환경설정") => "⚙",
        _ when text.Contains("업데이트") => "🔄",
        _ when text.Contains("로그") => "📄",
        _ when text.Contains("종료") => "⏻",
        _ => "▪",
    };
}
