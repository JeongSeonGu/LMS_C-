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
    private readonly ComboBox _monitorBox = new()
    {
        Left = 130, Top = 20, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _vicePrincipalBox = new() { Left = 130, Top = 55, Width = 200, Text = "교감" };
    private readonly CheckBox _principalBox = new() { Left = 130, Top = 82, Width = 200, Text = "교장" };

    public string CategoryName => "복무";

    public DutyOptionsPage()
    {
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Left = 16, Top = 0, Width = 400, Height = 20,
            Text = "복무",
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "출력 모니터" });
        Controls.Add(_monitorBox);
        Controls.Add(new Label { Left = 20, Top = 58, Width = 100, Text = "알림 대상" });
        Controls.Add(_vicePrincipalBox);
        Controls.Add(_principalBox);
        Controls.Add(new Label
        {
            Left = 20, Top = 115, Width = 380, Height = 55,
            ForeColor = Color.Gray,
            Text = "체크한 대상에게 출장·연가가 있으면 하루 전엔 우측 상단에 예정 안내를,\n" +
                   "당일에는 부재중 안내를 화면 최상단에 고정 표시합니다.",
        });

        foreach (var option in DisplayHelper.GetMonitorOptions())
        {
            _monitorBox.Items.Add(option);
        }
    }

    public void LoadFrom(AppSettings settings)
    {
        ScheduleOptionsPage.SelectMonitor(_monitorBox, settings.DutyMonitorIndex);
        _vicePrincipalBox.Checked = settings.DutyNotifyVicePrincipal;
        _principalBox.Checked = settings.DutyNotifyPrincipal;
    }

    public void SaveTo(AppSettings settings)
    {
        settings.DutyMonitorIndex = (_monitorBox.SelectedItem as MonitorOption)?.Index ?? 0;
        settings.DutyNotifyVicePrincipal = _vicePrincipalBox.Checked;
        settings.DutyNotifyPrincipal = _principalBox.Checked;
    }
}
