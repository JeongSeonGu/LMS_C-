using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 트레이 아이콘을 왼쪽 클릭했을 때 뜨는 flat(WPF 스타일) 창입니다. 오른쪽 클릭 시 나오는
/// ContextMenuStrip과 완전히 같은 메뉴 구성을, 마우스 오른쪽 버튼을 쓰기 번거로운 상황에서도
/// 왼쪽 클릭만으로 접근할 수 있도록 버튼 목록으로 다시 그려서 보여줍니다. 각 항목의 실제
/// 동작은 여전히 원본 ContextMenuStrip(TrayApplicationContext)이 갖고 있으므로, 이 창은
/// 그 항목의 <see cref="ToolStripItem.PerformClick"/>을 그대로 호출할 뿐 별도의 로직을
/// 갖지 않습니다 — 메뉴 구성이 바뀌어도 이 창을 따로 손볼 필요가 없습니다.
/// </summary>
public sealed class TrayControlPanelForm : Form
{
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 36, BackColor = UiTheme.Sky };

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = UiTheme.Surface,
        Padding = new Padding(10, 10, 10, 10),
    };

    private bool _dragging;
    private Point _dragStart;

    public TrayControlPanelForm(ContextMenuStrip menu)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        Width = 300;
        BackColor = UiTheme.Border; // 바깥쪽 얇은 테두리처럼 보이는 배경색
        Padding = new Padding(1);

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📌 LMS 연동 프로그램",
            ForeColor = Color.White,
            Font = UiTheme.BoldFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
        };

        var closeButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 36,
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Font = new Font(UiTheme.BaseFont.FontFamily, 12F, FontStyle.Bold),
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

        Controls.Add(_content);
        Controls.Add(_header);

        BuildMenuItems(menu.Items);

        Height = Math.Clamp(_header.Height + _content.PreferredSize.Height + 4, 160, 560);
        PositionNearCursor();
    }

    private void PositionNearCursor()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        var x = Math.Clamp(Cursor.Position.X - Width / 2, area.Left, area.Right - Width);
        var y = Math.Clamp(Cursor.Position.Y - Height, area.Top, area.Bottom - Height);
        Location = new Point(x, y);
    }

    private void BuildMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            switch (item)
            {
                case ToolStripSeparator:
                    _content.Controls.Add(BuildSeparator(indent: false));
                    break;

                case ToolStripMenuItem { HasDropDownItems: true } group:
                    _content.Controls.Add(BuildSectionHeader(group.Text));
                    foreach (ToolStripItem child in group.DropDownItems)
                    {
                        if (child is ToolStripSeparator)
                        {
                            _content.Controls.Add(BuildSeparator(indent: true));
                        }
                        else if (child is ToolStripMenuItem childItem)
                        {
                            _content.Controls.Add(BuildActionButton(childItem, indent: true));
                        }
                    }
                    break;

                case ToolStripMenuItem { Enabled: false } status:
                    _content.Controls.Add(BuildStatusLabel(status));
                    break;

                case ToolStripMenuItem action:
                    _content.Controls.Add(BuildActionButton(action, indent: false));
                    break;
            }
        }
    }

    private static Panel BuildSeparator(bool indent) => new()
    {
        Width = indent ? 248 : 268,
        Height = 1,
        BackColor = UiTheme.Border,
        Margin = indent ? new Padding(20, 4, 0, 4) : new Padding(0, 6, 0, 6),
    };

    private static Label BuildSectionHeader(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = UiTheme.BoldFont,
        ForeColor = UiTheme.SkyDark,
        Margin = new Padding(0, 8, 0, 4),
    };

    private static Label BuildStatusLabel(ToolStripMenuItem item) => new()
    {
        AutoSize = true,
        Text = item.Text,
        ForeColor = UiTheme.TextSecondary,
        Margin = new Padding(0, 2, 0, 2),
    };

    private Button BuildActionButton(ToolStripMenuItem item, bool indent)
    {
        var button = new Button
        {
            Width = indent ? 248 : 268,
            Height = 30,
            Text = item.Text.Replace("...", "").TrimEnd(),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(indent ? 16 : 6, 0, 0, 0),
            Margin = new Padding(indent ? 20 : 0, 1, 0, 1),
        };
        UiTheme.StyleFlatToolButton(button);
        button.FlatAppearance.BorderSize = 0;

        // 이 창은 항목을 소유하지 않으므로, 실제 처리는 원본 메뉴 항목의 Click 핸들러에
        // 그대로 맡긴다(트레이 컨텍스트 메뉴를 오른쪽 클릭으로 열었을 때와 완전히 동일한 동작).
        button.Click += (_, _) =>
        {
            item.PerformClick();
            Close();
        };

        return button;
    }
}
