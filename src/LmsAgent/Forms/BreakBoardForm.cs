using System.Windows.Forms;
using LmsAgent.Interop;

namespace LmsAgent.Forms;

/// <summary>
/// 쉬는 시간에 전자칠판 페이지를 선택한 모니터 전체화면으로 띄우는 창입니다.
/// 포커스를 가져가지 않도록 WS_EX_NOACTIVATE를 사용합니다.
/// </summary>
public sealed class BreakBoardForm : Form
{
    private readonly WebBrowser _browser = new() { Dock = DockStyle.Fill, ScrollBarsEnabled = false };
    private string? _currentUrl;

    public BreakBoardForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Controls.Add(_browser);
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

    public void ShowOn(Screen screen, string url)
    {
        Bounds = screen.Bounds;

        if (_currentUrl != url)
        {
            _currentUrl = url;
            _browser.Navigate(url);
        }

        if (!Visible)
        {
            Show();
        }
    }
}
