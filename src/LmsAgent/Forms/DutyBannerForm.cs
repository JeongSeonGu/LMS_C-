using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Interop;

namespace LmsAgent.Forms;

/// <summary>
/// 복무 변동사항(출장/연가) 알림 배너. 화면 우측 상단에 항상 위로 고정 표시됩니다.
/// 포커스를 가져가지 않도록 WS_EX_NOACTIVATE를 사용합니다.
/// </summary>
public sealed class DutyBannerForm : Form
{
    private readonly Label _label = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.White,
        Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
        Padding = new Padding(12),
    };

    public DutyBannerForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(340, 64);
        BackColor = Color.FromArgb(0, 90, 158);
        Controls.Add(_label);
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

    /// <param name="urgent">true면 "당일 부재중"(강한 경고색), false면 "하루 전 예정"(안내색)으로 표시합니다.</param>
    public void UpdateContent(string text, bool urgent)
    {
        _label.Text = text;
        BackColor = urgent ? Color.FromArgb(198, 40, 40) : Color.FromArgb(0, 90, 158);
    }

    public void PositionTopRight(Screen screen)
    {
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Top + 16);
    }

    /// <summary>환경설정 &gt; 복무의 투명도(0~100%)를 적용합니다.</summary>
    public void SetOpacityPercent(int percent)
    {
        Opacity = Math.Clamp(percent, 5, 100) / 100.0;
    }
}
