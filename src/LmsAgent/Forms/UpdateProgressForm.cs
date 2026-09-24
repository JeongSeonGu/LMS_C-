using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 업데이트 다운로드가 진행되는 동안 보여주는 진행률 창입니다. 사용자가 임의로 닫을 수 없게
/// 만들어(ControlBox=false), 다운로드가 끝나면(적용 성공 시 프로그램이 재시작되며 이 창도
/// 함께 사라지고, 실패 시 호출자가 직접 Close())만 사라지도록 합니다. 전체 크기를 알기 전
/// (Content-Length 없음)에는 진행률 미상(Marquee)으로 보여주다가, 첫 SetProgress 호출부터
/// 실제 퍼센트 막대로 전환합니다.
/// </summary>
public sealed class UpdateProgressForm : Form
{
    private readonly Label _messageLabel = new()
    {
        Left = 24, Top = 24, Width = 332, Height = 40,
        Text = "새 버전을 내려받는 중입니다. 잠시만 기다려주세요...",
    };

    private readonly ProgressBar _progressBar = new()
    {
        Left = 24, Top = 72, Width = 332, Height = 22, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30,
    };

    private readonly Label _percentLabel = new()
    {
        Left = 24, Top = 100, Width = 332, Height = 20, TextAlign = ContentAlignment.MiddleRight,
        ForeColor = UiTheme.TextSecondary,
    };

    public UpdateProgressForm()
    {
        Text = "업데이트 중";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 140);

        Controls.Add(_messageLabel);
        Controls.Add(_progressBar);
        Controls.Add(_percentLabel);
    }

    /// <summary>0~100 사이의 진행률을 표시합니다. 반드시 UI 스레드에서 호출해야 합니다
    /// (백그라운드에서 부르는 쪽은 Control.BeginInvoke로 마샬링해서 불러야 합니다).</summary>
    public void SetProgress(double percent)
    {
        if (_progressBar.Style != ProgressBarStyle.Continuous)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
        }

        var clamped = (int)Math.Clamp(percent, 0, 100);
        _progressBar.Value = clamped;
        _percentLabel.Text = $"{clamped}%";
    }
}
