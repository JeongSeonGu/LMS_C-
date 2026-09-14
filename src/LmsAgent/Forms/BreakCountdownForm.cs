using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Interop;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>쉬는 시간 종료 2분 전부터 화면 가운데에 남은 시간을 카운트다운으로 보여주는 안내 창입니다.</summary>
public sealed class BreakCountdownForm : Form
{
    private readonly Label _label = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.White,
        Font = new Font("맑은 고딕", 16F, FontStyle.Bold),
    };

    private readonly Timer _timer = new() { Interval = 1000 };
    private DateTime _breakEndAt;

    public BreakCountdownForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(360, 130);
        BackColor = UiTheme.OrangeDark;
        Controls.Add(_label);

        _timer.Tick += (_, _) => UpdateLabel();
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

    public void ShowCountdown(Screen screen, DateTime breakEndAt)
    {
        _breakEndAt = breakEndAt;

        var area = screen.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);

        UpdateLabel();

        if (!Visible)
        {
            Show();
        }

        _timer.Start();
    }

    public void HideCountdown()
    {
        _timer.Stop();
        Hide();
    }

    private void UpdateLabel()
    {
        var remaining = _breakEndAt - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            HideCountdown();
            return;
        }

        _label.Text = $"쉬는 시간이 곧 끝납니다\n{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }
}
