using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "복무" — 출력 모니터와, 알림 대상(교감/교장)을 설정합니다.
/// 체크된 대상에게 출장·연가 등 복무 변동사항이 있으면 하루 전엔 "내일 예정" 배너를,
/// 당일에는 "부재중" 배너를 화면 우측 상단에 항상 위에 표시합니다.
/// </summary>
public sealed class DutyOptionsPage : UserControl, IOptionsPage
{
    private readonly CheckBox _enabledBox = new()
    {
        Left = 20, Top = 23, Width = 320, Text = "복무 알림 배너 표시",
    };

    private readonly ComboBox _monitorBox = new()
    {
        Left = 130, Top = 58, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _vicePrincipalBox = new() { Left = 130, Top = 93, Width = 200, Text = "교감" };
    private readonly CheckBox _principalBox = new() { Left = 130, Top = 120, Width = 200, Text = "교장" };

    private readonly TrackBar _opacityTrack = new()
    {
        Left = 130, Top = 150, Width = 200, Minimum = 30, Maximum = 100, TickFrequency = 10,
    };

    private readonly Label _opacityValueLabel = new() { Left = 335, Top = 157, Width = 50 };

    public string CategoryName => "복무";

    public DutyOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "복무" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(_enabledBox);
        Controls.Add(new Label { Left = 20, Top = 61, Width = 100, Text = "출력 모니터" });
        Controls.Add(_monitorBox);
        Controls.Add(new Label { Left = 20, Top = 96, Width = 100, Text = "알림 대상" });
        Controls.Add(_vicePrincipalBox);
        Controls.Add(_principalBox);
        Controls.Add(new Label { Left = 20, Top = 157, Width = 100, Text = "투명도" });
        Controls.Add(_opacityTrack);
        Controls.Add(_opacityValueLabel);

        var hint = new Label
        {
            Left = 20, Top = 187, Width = 380, Height = 55,
            Text = "체크한 대상에게 출장·연가가 있으면 하루 전엔 우측 상단에 예정 안내를,\n" +
                   "당일에는 부재중 안내를 화면 최상단에 고정 표시합니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);

        foreach (var option in DisplayHelper.GetMonitorOptions())
        {
            _monitorBox.Items.Add(option);
        }

        _opacityTrack.ValueChanged += (_, _) => UpdateOpacityLabel();
        UpdateOpacityLabel();
    }

    private void UpdateOpacityLabel() => _opacityValueLabel.Text = $"{_opacityTrack.Value}%";

    public void LoadFrom(AppSettings settings)
    {
        _enabledBox.Checked = settings.DutyBannerEnabled;
        ScheduleOptionsPage.SelectMonitor(_monitorBox, settings.DutyMonitorIndex);
        _vicePrincipalBox.Checked = settings.DutyNotifyVicePrincipal;
        _principalBox.Checked = settings.DutyNotifyPrincipal;
        _opacityTrack.Value = Math.Clamp(settings.DutyBannerOpacityPercent, _opacityTrack.Minimum, _opacityTrack.Maximum);
        UpdateOpacityLabel();
    }

    public void SaveTo(AppSettings settings)
    {
        settings.DutyBannerEnabled = _enabledBox.Checked;
        settings.DutyMonitorIndex = (_monitorBox.SelectedItem as MonitorOption)?.Index ?? 0;
        settings.DutyNotifyVicePrincipal = _vicePrincipalBox.Checked;
        settings.DutyNotifyPrincipal = _principalBox.Checked;
        settings.DutyBannerOpacityPercent = _opacityTrack.Value;
    }
}
