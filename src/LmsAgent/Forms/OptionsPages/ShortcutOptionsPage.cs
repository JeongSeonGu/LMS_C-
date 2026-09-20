using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "단축키" — 학사달력보기/관리자 복무상황 보기/업무 일지 보기 창을 전역 단축키로
/// 화면에 나타내거나(안 보이면) 사라지게(보이면) 토글합니다. 단축키는 반드시 Ctrl/Alt/Shift
/// 중 하나 이상 + 다른 키의 조합이어야 하며(<see cref="HotkeyCaptureBox"/>가 강제), 실제
/// 등록/해제는 <see cref="GlobalHotkeyService"/>가 담당합니다.
/// </summary>
public sealed class ShortcutOptionsPage : UserControl, IOptionsPage
{
    private readonly CheckBox _scheduleEnabledBox = new() { Left = 20, Top = 26, Width = 20, Text = "" };
    private readonly Label _scheduleLabel = new() { Left = 44, Top = 24, Width = 150, Text = "학사달력보기" };
    private readonly HotkeyCaptureBox _scheduleHotkeyBox = new() { Left = 200, Top = 21, Width = 200, ReadOnly = true };

    private readonly CheckBox _dutyEnabledBox = new() { Left = 20, Top = 61, Width = 20, Text = "" };
    private readonly Label _dutyLabel = new() { Left = 44, Top = 59, Width = 150, Text = "관리자 복무상황 보기" };
    private readonly HotkeyCaptureBox _dutyHotkeyBox = new() { Left = 200, Top = 56, Width = 200, ReadOnly = true };

    private readonly CheckBox _taskEnabledBox = new() { Left = 20, Top = 96, Width = 20, Text = "" };
    private readonly Label _taskLabel = new() { Left = 44, Top = 94, Width = 150, Text = "업무 일지 보기" };
    private readonly HotkeyCaptureBox _taskHotkeyBox = new() { Left = 200, Top = 91, Width = 200, ReadOnly = true };

    public string CategoryName => "단축키";

    public ShortcutOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "단축키" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(_scheduleEnabledBox);
        Controls.Add(_scheduleLabel);
        Controls.Add(_scheduleHotkeyBox);
        Controls.Add(_dutyEnabledBox);
        Controls.Add(_dutyLabel);
        Controls.Add(_dutyHotkeyBox);
        Controls.Add(_taskEnabledBox);
        Controls.Add(_taskLabel);
        Controls.Add(_taskHotkeyBox);

        var hint = new Label
        {
            Left = 20, Top = 126, Width = 400, Height = 90,
            Text = "체크하면 그 단축키가 활성화됩니다. 입력칸을 클릭한 뒤 원하는 키 조합을 누르면\n" +
                   "그대로 저장되며, 반드시 Ctrl/Alt/Shift 중 하나 이상과 다른 키를 함께 눌러야\n" +
                   "합니다(예: Ctrl+Alt+C). Esc를 누르면 그 칸의 설정을 지웁니다.\n" +
                   "단축키를 누르면 해당 창이 안 보이는 상태면 나타나고, 보이는 상태면 사라집니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);
    }

    public void LoadFrom(AppSettings settings)
    {
        _scheduleEnabledBox.Checked = settings.ScheduleViewHotkey.Enabled;
        _scheduleHotkeyBox.SetBinding(settings.ScheduleViewHotkey);

        _dutyEnabledBox.Checked = settings.DutyStatusViewHotkey.Enabled;
        _dutyHotkeyBox.SetBinding(settings.DutyStatusViewHotkey);

        _taskEnabledBox.Checked = settings.TaskJournalViewHotkey.Enabled;
        _taskHotkeyBox.SetBinding(settings.TaskJournalViewHotkey);
    }

    public void SaveTo(AppSettings settings)
    {
        settings.ScheduleViewHotkey = BuildBinding(_scheduleEnabledBox, _scheduleHotkeyBox);
        settings.DutyStatusViewHotkey = BuildBinding(_dutyEnabledBox, _dutyHotkeyBox);
        settings.TaskJournalViewHotkey = BuildBinding(_taskEnabledBox, _taskHotkeyBox);
    }

    private static HotkeyBinding BuildBinding(CheckBox enabledBox, HotkeyCaptureBox hotkeyBox)
    {
        var binding = hotkeyBox.Binding ?? new HotkeyBinding();
        binding.Enabled = enabledBox.Checked && binding.Key != Keys.None;
        return binding;
    }

    public string? ValidateSettings()
    {
        if (_scheduleEnabledBox.Checked && _scheduleHotkeyBox.Binding is null)
        {
            return "학사달력보기 단축키를 지정하세요.";
        }

        if (_dutyEnabledBox.Checked && _dutyHotkeyBox.Binding is null)
        {
            return "관리자 복무상황 보기 단축키를 지정하세요.";
        }

        if (_taskEnabledBox.Checked && _taskHotkeyBox.Binding is null)
        {
            return "업무 일지 보기 단축키를 지정하세요.";
        }

        return null;
    }
}
