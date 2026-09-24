using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 트레이 아이콘을 왼쪽 클릭했을 때 뜨는 창입니다. 오른쪽 클릭 시 나오는 ContextMenuStrip과
/// 완전히 같은 메뉴 구성을, 일반 윈도우 프로그램처럼 화면 가운데에 뜨는 flat 스타일 창으로
/// 다시 보여줍니다 — 왼쪽에 아이콘 사이드바(카테고리), 오른쪽에 그 카테고리의 버튼들을
/// 보여주는 구성입니다. 각 항목의 실제 동작은 여전히 원본 ContextMenuStrip(TrayApplicationContext)이
/// 갖고 있으므로, 이 창은 그 항목의 <see cref="ToolStripItem.PerformClick"/>을 그대로 호출할
/// 뿐 별도의 로직을 갖지 않습니다 — 메뉴 구성이 바뀌어도 이 창을 따로 손볼 필요가 없습니다.
/// </summary>
public sealed class TrayControlPanelForm : Form
{
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 44, BackColor = UiTheme.Sky };
    private readonly Panel _sidebar = new() { Dock = DockStyle.Left, Width = 208, BackColor = UiTheme.SkyDark };
    private readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = 30, BackColor = UiTheme.SkyPale };

    private readonly Label _contentTitle = new()
    {
        Dock = DockStyle.Top, Height = 40, Font = UiTheme.TitleFont, ForeColor = UiTheme.SkyDark,
        Padding = new Padding(0, 4, 0, 8),
    };

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
        BackColor = UiTheme.Surface, Padding = new Padding(24, 16, 24, 16),
    };

    private readonly Panel _contentHost = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

    private Button? _selectedNavButton;
    private bool _dragging;
    private Point _dragStart;

    public TrayControlPanelForm(ContextMenuStrip menu)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Size = new Size(680, 480);
        BackColor = UiTheme.Border; // 바깥쪽 얇은 테두리처럼 보이는 배경색
        Padding = new Padding(1);

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
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📌 LMS 연동 프로그램",
            ForeColor = Color.White,
            Font = UiTheme.BoldFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
        };

        var closeButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 44,
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Font = new Font(UiTheme.BaseFont.FontFamily, 13F, FontStyle.Bold),
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = UiTheme.SkyDark;
        closeButton.Click += (_, _) => Close();

        _header.Controls.Add(titleLabel);
        _header.Controls.Add(closeButton);

        // 테두리 없는 창이라 제목표시줄을 직접 드래그로 옮길 수 있게 한다.
        void StartDrag(object? _, MouseEventArgs e) { _dragging = true; _dragStart = e.Location; }
        void DoDrag(object? _, MouseEventArgs e)
        {
            if (_dragging)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            }
        }
        void EndDrag(object? _, MouseEventArgs e) => _dragging = false;

        _header.MouseDown += StartDrag;
        _header.MouseMove += DoDrag;
        _header.MouseUp += EndDrag;
        titleLabel.MouseDown += StartDrag;
        titleLabel.MouseMove += DoDrag;
        titleLabel.MouseUp += EndDrag;
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
                    ForeColor = UiTheme.TextSecondary,
                    Margin = new Padding(0, 0, 24, 0),
                });
            }
        }

        _statusBar.Controls.Add(flow);
    }

    private void BuildSidebar(ToolStripItemCollection items)
    {
        Button? firstCategoryButton = null;

        foreach (ToolStripItem item in items)
        {
            if (item is not ToolStripMenuItem { Enabled: true } menuItem)
            {
                continue; // 구분선·비활성(상태) 항목은 사이드바에 넣지 않는다.
            }

            var nav = BuildNavButton(menuItem);
            _sidebar.Controls.Add(nav);

            if (menuItem.HasDropDownItems && firstCategoryButton is null)
            {
                firstCategoryButton = nav;
            }
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

    private Button BuildNavButton(ToolStripMenuItem menuItem)
    {
        var text = menuItem.Text?.Replace("...", "").TrimEnd() ?? "";
        var button = new Button
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = $"{IconFor(text)}   {text}",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = UiTheme.SkyDark,
            Font = UiTheme.BaseFont,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.Sky;

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

    private void Select(Button nav)
    {
        if (_selectedNavButton is not null)
        {
            _selectedNavButton.BackColor = UiTheme.SkyDark;
        }

        _selectedNavButton = nav;
        nav.BackColor = UiTheme.OrangeDark;
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
                _content.Controls.Add(new Panel
                {
                    Width = 380, Height = 1, BackColor = UiTheme.Border, Margin = new Padding(0, 8, 0, 8),
                });
            }
            else if (child is ToolStripMenuItem childItem)
            {
                _content.Controls.Add(BuildActionButton(childItem));
            }
        }

        _content.ResumeLayout();
    }

    private Button BuildActionButton(ToolStripMenuItem item)
    {
        var button = new Button
        {
            Width = 380,
            Height = 44,
            Text = item.Text.Replace("...", "").TrimEnd(),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 6),
            FlatStyle = FlatStyle.Flat,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.SkyPale,
            Font = UiTheme.BaseFont,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.SkyLight;

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
