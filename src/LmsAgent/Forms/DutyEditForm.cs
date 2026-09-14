using System;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 복무등록의 등록/수정 창입니다. 대상(교장/교감/교무부장/행정실장)과
/// 구분(연가/출장/조퇴), 날짜, 필요 시 시간, 메모(선택)를 입력합니다.
/// "종일" 체크를 해제하면 시작/종료 시간을 입력할 수 있고, 체크된 상태(기본값)이거나
/// 시간을 입력하지 않으면 서버에 종일 기록으로 저장됩니다.
/// </summary>
public sealed class DutyEditForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly DutyRecord? _editing;

    private readonly Panel _topPanel = new() { Dock = DockStyle.Top, Height = 194, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Bottom, Height = 78 };

    private readonly ComboBox _positionBox = new()
    {
        Left = 90, Top = 8, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly ComboBox _dutyTypeBox = new()
    {
        Left = 90, Top = 43, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly DateTimePicker _datePicker = new()
    {
        Left = 90, Top = 78, Width = 260, Format = DateTimePickerFormat.Short,
    };

    private readonly CheckBox _allDayBox = new() { Left = 90, Top = 112, Width = 200, Text = "종일", Checked = true };

    private readonly DateTimePicker _startTimePicker = new()
    {
        Left = 90, Top = 146, Width = 110, Format = DateTimePickerFormat.Time, ShowUpDown = true,
    };

    private readonly Label _timeRangeSeparator = new()
    {
        Left = 206, Top = 150, Width = 16, Text = "~",
    };

    private readonly DateTimePicker _endTimePicker = new()
    {
        Left = 226, Top = 146, Width = 110, Format = DateTimePickerFormat.Time, ShowUpDown = true,
    };

    private readonly Label _timeLabel = new() { Left = 0, Top = 149, Width = 90, Text = "시간" };

    private readonly TextBox _noteBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true, ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _saveButton = new() { Left = 190, Top = 8, Width = 80 };
    private readonly Button _cancelButton = new() { Left = 280, Top = 8, Width = 90, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20, Top = 42, Width = 350, Height = 30, ForeColor = UiTheme.Danger,
    };

    public DutyEditForm(WorkSupportApiClient api, DutyRecord? editing = null, DateTime? initialDate = null)
    {
        _api = api;
        _editing = editing;

        Text = editing is null ? "복무사항 등록" : "복무사항 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(390, 420);
        MinimumSize = new Size(420, 440);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 11, Width = 90, Text = "대상" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 46, Width = 90, Text = "구분" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 81, Width = 90, Text = "날짜" });
        _topPanel.Controls.Add(_timeLabel);
        _topPanel.Controls.Add(_positionBox);
        _topPanel.Controls.Add(_dutyTypeBox);
        _topPanel.Controls.Add(_datePicker);
        _topPanel.Controls.Add(_allDayBox);
        _topPanel.Controls.Add(_startTimePicker);
        _topPanel.Controls.Add(_timeRangeSeparator);
        _topPanel.Controls.Add(_endTimePicker);
        _bodyPanel.Controls.Add(_noteBox);
        _bottomPanel.Controls.Add(_saveButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_statusLabel);

        Controls.Add(_bodyPanel);
        Controls.Add(_topPanel);
        Controls.Add(_bottomPanel);

        _saveButton.Text = editing is null ? "등록" : "수정";

        foreach (var position in DutyRecord.Positions)
        {
            _positionBox.Items.Add(position);
        }
        foreach (var dutyType in DutyRecord.DutyTypes)
        {
            _dutyTypeBox.Items.Add(dutyType);
        }

        _datePicker.Value = initialDate?.Date ?? DateTime.Today;

        if (editing is not null)
        {
            _positionBox.SelectedItem = editing.Position;
            _dutyTypeBox.SelectedItem = editing.DutyType;
            if (editing.DateValue is { } date)
            {
                _datePicker.Value = date.ToDateTime(TimeOnly.MinValue);
            }

            _allDayBox.Checked = editing.IsAllDay;
            if (!editing.IsAllDay)
            {
                if (TryParseTime(editing.TimeStart, out var start)) _startTimePicker.Value = start;
                if (TryParseTime(editing.TimeEnd, out var end)) _endTimePicker.Value = end;
            }
        }

        if (_positionBox.SelectedIndex < 0 && _positionBox.Items.Count > 0) _positionBox.SelectedIndex = 0;
        if (_dutyTypeBox.SelectedIndex < 0 && _dutyTypeBox.Items.Count > 0) _dutyTypeBox.SelectedIndex = 0;

        ApplyAllDayVisibility();
        _allDayBox.CheckedChanged += (_, _) => ApplyAllDayVisibility();

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private void ApplyAllDayVisibility()
    {
        var allDay = _allDayBox.Checked;
        _timeLabel.Visible = !allDay;
        _startTimePicker.Visible = !allDay;
        _endTimePicker.Visible = !allDay;
        _timeRangeSeparator.Visible = !allDay;
    }

    private static bool TryParseTime(string? value, out DateTime result)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateTime.TryParse(value, out var parsed))
        {
            result = DateTime.Today + parsed.TimeOfDay;
            return true;
        }

        result = DateTime.Today;
        return false;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var position = _positionBox.SelectedItem as string;
        var dutyType = _dutyTypeBox.SelectedItem as string;

        if (string.IsNullOrEmpty(position))
        {
            _statusLabel.Text = "대상을 선택하세요.";
            return;
        }

        if (string.IsNullOrEmpty(dutyType))
        {
            _statusLabel.Text = "구분을 선택하세요.";
            return;
        }

        var allDay = _allDayBox.Checked;
        string? timeStart = null;
        string? timeEnd = null;

        if (!allDay)
        {
            if (_endTimePicker.Value.TimeOfDay <= _startTimePicker.Value.TimeOfDay)
            {
                _statusLabel.Text = "종료 시간이 시작 시간보다 늦어야 합니다.";
                return;
            }

            timeStart = _startTimePicker.Value.ToString("HH:mm");
            timeEnd = _endTimePicker.Value.ToString("HH:mm");
        }

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = _editing is null ? "등록 중..." : "수정 중...";

        try
        {
            var record = new DutyRecord
            {
                Id = _editing?.Id ?? 0,
                Date = _datePicker.Value.ToString("yyyy-MM-dd"),
                Position = position,
                DutyType = dutyType,
                TimeStart = timeStart,
                TimeEnd = timeEnd,
                Note = string.IsNullOrWhiteSpace(_noteBox.Text) ? null : _noteBox.Text.Trim(),
            };

            var result = _editing is null
                ? await _api.AddDutyAsync(record)
                : await _api.UpdateDutyAsync(record);

            if (result.Ok)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "처리에 실패했습니다." : result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"오류: {ex.Message}";
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }
}
