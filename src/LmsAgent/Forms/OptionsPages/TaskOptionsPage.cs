using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "업무" — 업무 일지(포스트잇 스타일 오버레이)를 상시 표시할지, 어느 모니터에
/// 얼마나 투명하게, 일/주 단위 중 무엇을 보여줄지 설정합니다. 업무 일지에는 내 담당업무
/// 관련 학사일정, 내가 해야 할 할일, 나에게 알림 대상으로 지정된 일정이 날짜별로 표시됩니다.
/// </summary>
public sealed class TaskOptionsPage : UserControl, IOptionsPage
{
    private readonly CheckBox _enabledBox = new()
    {
        Left = 20, Top = 23, Width = 320, Text = "업무 및 할일 모니터 출력",
    };

    private readonly ComboBox _monitorBox = new()
    {
        Left = 130, Top = 58, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly RadioButton _dayRadio = new() { Left = 130, Top = 93, Width = 120, Text = "일 단위" };
    private readonly RadioButton _weekRadio = new() { Left = 250, Top = 93, Width = 120, Text = "주 단위" };

    private readonly TrackBar _opacityTrack = new()
    {
        Left = 130, Top = 123, Width = 200, Minimum = 20, Maximum = 100, TickFrequency = 10,
    };

    private readonly Label _opacityValueLabel = new() { Left = 335, Top = 130, Width = 50 };

    public string CategoryName => "업무";

    public TaskOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "업무" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(_enabledBox);
        Controls.Add(new Label { Left = 20, Top = 61, Width = 100, Text = "출력 모니터" });
        Controls.Add(_monitorBox);
        Controls.Add(new Label { Left = 20, Top = 96, Width = 100, Text = "출력 단위" });
        Controls.Add(_dayRadio);
        Controls.Add(_weekRadio);
        Controls.Add(new Label { Left = 20, Top = 130, Width = 100, Text = "투명도" });
        Controls.Add(_opacityTrack);
        Controls.Add(_opacityValueLabel);

        var hint = new Label
        {
            Left = 20, Top = 160, Width = 400, Height = 80,
            Text = "체크하면 선택한 모니터 왼쪽 위에 포스트잇 형태로 업무 일지를 항상 위에 표시합니다.\n" +
                   "내 담당업무와 관련된 학사일정, 내가 해야 할 할일, 나에게 알림으로 지정된 일정을\n" +
                   "날짜별로 묶어서 보여주고, 설명이 있는 항목은 아이콘을 눌러 내용을 볼 수 있습니다.\n" +
                   "일 단위는 오늘, 주 단위는 이번 주 항목만 보여줍니다.",
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
        _enabledBox.Checked = settings.TaskJournalEnabled;
        ScheduleOptionsPage.SelectMonitor(_monitorBox, settings.TaskJournalMonitorIndex);
        _dayRadio.Checked = settings.TaskJournalOutputUnit == TaskJournalOutputUnit.Day;
        _weekRadio.Checked = settings.TaskJournalOutputUnit == TaskJournalOutputUnit.Week;
        _opacityTrack.Value = Math.Clamp(settings.TaskJournalOpacityPercent, _opacityTrack.Minimum, _opacityTrack.Maximum);
        UpdateOpacityLabel();
    }

    public void SaveTo(AppSettings settings)
    {
        settings.TaskJournalEnabled = _enabledBox.Checked;
        settings.TaskJournalMonitorIndex = (_monitorBox.SelectedItem as MonitorOption)?.Index ?? 0;
        settings.TaskJournalOutputUnit = _weekRadio.Checked ? TaskJournalOutputUnit.Week : TaskJournalOutputUnit.Day;
        settings.TaskJournalOpacityPercent = _opacityTrack.Value;
    }

    public string? ValidateSettings() => null;
}
