using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "학사일정" — 출력 모니터, 출력 단위(주/월), 배경화면 출력 여부를 설정합니다.
/// 배경화면 출력을 체크하면 주 단위일 때는 선택한 모니터 하단에 그 주의 학사달력을,
/// 월 단위일 때는 화면 전체에 그 달의 학사달력을 배경처럼 상시 표시합니다.
/// </summary>
public sealed class ScheduleOptionsPage : UserControl, IOptionsPage
{
    private readonly ComboBox _monitorBox = new()
    {
        Left = 130, Top = 20, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly RadioButton _weekRadio = new() { Left = 130, Top = 55, Width = 120, Text = "주 단위" };
    private readonly RadioButton _monthRadio = new() { Left = 250, Top = 55, Width = 120, Text = "월 단위" };

    private readonly CheckBox _wallpaperBox = new()
    {
        Left = 130, Top = 85, Width = 300, Text = "배경화면처럼 항상 출력",
    };

    private readonly TrackBar _opacityTrack = new()
    {
        Left = 130, Top = 115, Width = 200, Minimum = 10, Maximum = 100, TickFrequency = 10,
    };

    private readonly Label _opacityValueLabel = new() { Left = 335, Top = 122, Width = 50 };

    public string CategoryName => "학사일정";

    public ScheduleOptionsPage()
    {
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Left = 16, Top = 0, Width = 400, Height = 20,
            Text = "학사일정",
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "출력 모니터" });
        Controls.Add(_monitorBox);
        Controls.Add(new Label { Left = 20, Top = 58, Width = 100, Text = "출력 단위" });
        Controls.Add(_weekRadio);
        Controls.Add(_monthRadio);
        Controls.Add(_wallpaperBox);
        Controls.Add(new Label { Left = 20, Top = 122, Width = 100, Text = "투명도" });
        Controls.Add(_opacityTrack);
        Controls.Add(_opacityValueLabel);
        Controls.Add(new Label
        {
            Left = 20, Top = 152, Width = 380, Height = 55,
            ForeColor = Color.Gray,
            Text = "주 단위: 선택한 모니터 하단에 이번 주 학사달력을 표시합니다.\n" +
                   "월 단위: 선택한 모니터 화면 전체를 이번 달 학사달력으로 채웁니다.",
        });

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
        SelectMonitor(_monitorBox, settings.ScheduleMonitorIndex);
        _weekRadio.Checked = settings.ScheduleOutputUnit == ScheduleOutputUnit.Week;
        _monthRadio.Checked = settings.ScheduleOutputUnit == ScheduleOutputUnit.Month;
        _wallpaperBox.Checked = settings.ScheduleWallpaperEnabled;
        _opacityTrack.Value = Math.Clamp(settings.ScheduleOverlayOpacityPercent, _opacityTrack.Minimum, _opacityTrack.Maximum);
        UpdateOpacityLabel();
    }

    public void SaveTo(AppSettings settings)
    {
        settings.ScheduleMonitorIndex = (_monitorBox.SelectedItem as MonitorOption)?.Index ?? 0;
        settings.ScheduleOutputUnit = _monthRadio.Checked ? ScheduleOutputUnit.Month : ScheduleOutputUnit.Week;
        settings.ScheduleWallpaperEnabled = _wallpaperBox.Checked;
        settings.ScheduleOverlayOpacityPercent = _opacityTrack.Value;
    }

    internal static void SelectMonitor(ComboBox box, int index)
    {
        var match = box.Items.OfType<MonitorOption>().FirstOrDefault(o => o.Index == index);
        box.SelectedItem = match ?? box.Items.OfType<MonitorOption>().FirstOrDefault();
    }
}
