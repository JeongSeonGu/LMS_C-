using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 일정 등록(및 목록에서의 수정) 창입니다.
/// 구글 캘린더의 일정 입력 방식을 따라, "종일" 체크 시 날짜만 입력하고
/// 체크를 해제하면 날짜와 별도로 시작/종료 시간을 입력할 수 있습니다.
///
/// 권한 규칙:
///  - 등록은 누구나 가능하지만, 담당업무는 자신이 맡은 업무 중에서만 고를 수 있고
///    아무 것도 선택하지 않으면(=관련 업무 없음) 그대로 등록됩니다.
///  - 기존 일정을 수정할 때는 그 일정의 담당업무가 자신의 업무와 일치할 때만 편집할 수 있습니다
///    (관리자 계정은 모든 일정을 편집할 수 있습니다).
/// </summary>
public sealed class ScheduleRegisterForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly SchoolEvent? _editing;

    private bool _suppressAutoAdjust;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _titleBox = new() { Left = 100, Top = 8, Width = 260 };

    private readonly CheckBox _allDayBox = new() { Left = 100, Top = 43, Width = 200, Text = "종일" };

    private readonly DateTimePicker _startDatePicker = new()
    {
        Left = 100, Top = 76, Width = 130, Format = DateTimePickerFormat.Short,
    };

    private readonly DateTimePicker _startTimePicker = new()
    {
        Left = 240, Top = 76, Width = 110, Format = DateTimePickerFormat.Time, ShowUpDown = true,
    };

    private readonly DateTimePicker _endDatePicker = new()
    {
        Left = 100, Top = 108, Width = 130, Format = DateTimePickerFormat.Short,
    };

    private readonly DateTimePicker _endTimePicker = new()
    {
        Left = 240, Top = 108, Width = 110, Format = DateTimePickerFormat.Time, ShowUpDown = true,
    };

    private readonly ComboBox _deptBox = new()
    {
        Left = 100,
        Top = 141,
        Width = 260,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly TextBox _locationBox = new() { Left = 100, Top = 176, Width = 260 };

    private readonly TextBox _noteBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _saveButton = new() { Left = 210, Top = 8, Width = 80 };
    private readonly Button _cancelButton = new() { Left = 300, Top = 8, Width = 80, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20,
        Top = 42,
        Width = 360,
        Height = 30,
        ForeColor = UiTheme.Danger,
    };

    /// <param name="editing">null이면 신규 등록, 값이 있으면 해당 일정을 수정합니다.</param>
    /// <param name="initialDate">신규 등록 시(달력에서 날짜를 클릭한 경우) 미리 채워 넣을 날짜.</param>
    public ScheduleRegisterForm(
        WorkSupportApiClient api, SessionManager session, SchoolEvent? editing = null, DateTime? initialDate = null)
    {
        _api = api;
        _session = session;
        _editing = editing;

        Text = editing is null ? "학사 일정 등록" : "학사 일정 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 420);
        MinimumSize = new Size(420, 440);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 11, Width = 90, Text = "제목" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 79, Width = 90, Text = "시작" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 111, Width = 90, Text = "종료" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 144, Width = 90, Text = "담당업무" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 179, Width = 90, Text = "장소" });
        _topPanel.Controls.Add(_titleBox);
        _topPanel.Controls.Add(_allDayBox);
        _topPanel.Controls.Add(_startDatePicker);
        _topPanel.Controls.Add(_startTimePicker);
        _topPanel.Controls.Add(_endDatePicker);
        _topPanel.Controls.Add(_endTimePicker);
        _topPanel.Controls.Add(_deptBox);
        _topPanel.Controls.Add(_locationBox);
        _bodyPanel.Controls.Add(_noteBox);
        _bottomPanel.Controls.Add(_saveButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_statusLabel);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 218f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        _saveButton.Text = editing is null ? "등록" : "수정";

        PopulateDeptBox();

        var now = RoundToNextHalfHour(DateTime.Now);
        var baseDate = initialDate?.Date ?? now.Date;
        _startDatePicker.Value = baseDate;
        _startTimePicker.Value = baseDate + now.TimeOfDay;
        _endDatePicker.Value = baseDate;
        _endTimePicker.Value = baseDate + now.TimeOfDay.Add(TimeSpan.FromHours(1));

        if (editing is not null)
        {
            _titleBox.Text = editing.Title;
            SelectDept(editing.DeptId);
            _allDayBox.Checked = editing.AllDay;

            if (editing.StartDateTime != DateTime.MinValue)
            {
                _startDatePicker.Value = editing.StartDateTime.Date;
                _startTimePicker.Value = editing.StartDateTime;
            }

            if (editing.EndDateTime != DateTime.MinValue)
            {
                _endDatePicker.Value = editing.EndDateTime.Date;
                _endTimePicker.Value = editing.EndDateTime;
            }

            _locationBox.Text = editing.Location ?? "";
            _noteBox.Text = editing.Note ?? "";
        }

        ApplyAllDayVisibility();

        _allDayBox.CheckedChanged += (_, _) => ApplyAllDayVisibility();
        _startDatePicker.ValueChanged += (_, _) => OnStartChanged();
        _startTimePicker.ValueChanged += (_, _) => OnStartChanged();

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    /// <summary>구글 캘린더처럼 "종일"이면 시간 입력을 감추고 날짜만 받습니다.</summary>
    private void ApplyAllDayVisibility()
    {
        var allDay = _allDayBox.Checked;
        _startTimePicker.Visible = !allDay;
        _endTimePicker.Visible = !allDay;
    }

    /// <summary>시작 일시가 종료보다 늦어지면, 기존 소요 시간만큼 종료도 함께 밀어줍니다(구글 캘린더 방식).</summary>
    private void OnStartChanged()
    {
        if (_suppressAutoAdjust)
        {
            return;
        }

        var start = CombineDateTime(_startDatePicker, _startTimePicker);
        var end = CombineDateTime(_endDatePicker, _endTimePicker);
        if (start < end)
        {
            return;
        }

        // 시작이 종료보다 늦어지면 1시간짜리 일정으로 종료를 다시 맞춘다.
        var newEnd = start.AddHours(1);

        _suppressAutoAdjust = true;
        _endDatePicker.Value = newEnd.Date;
        _endTimePicker.Value = newEnd;
        _suppressAutoAdjust = false;
    }

    private static DateTime CombineDateTime(DateTimePicker datePart, DateTimePicker timePart)
    {
        return datePart.Value.Date + timePart.Value.TimeOfDay;
    }

    private static DateTime RoundToNextHalfHour(DateTime value)
    {
        var minutes = value.Minute < 30 ? 30 - value.Minute : 60 - value.Minute;
        return value.AddMinutes(minutes).AddSeconds(-value.Second);
    }

    private void PopulateDeptBox()
    {
        _deptBox.Items.Add(new DeptOption(null, "관련 업무 없음"));

        var selectable = _session.GetSelectableDepartmentsForNewEvent();
        foreach (var dept in selectable)
        {
            _deptBox.Items.Add(new DeptOption(dept.Id, dept.Name));
        }

        // 수정 대상 일정의 담당업무가 (관리자 편집 등으로) 목록에 없다면 표시용으로 추가한다.
        if (_editing?.DeptId is int existingDeptId && selectable.All(d => d.Id != existingDeptId))
        {
            var name = _session.Departments.FirstOrDefault(d => d.Id == existingDeptId)?.Name ?? $"업무 #{existingDeptId}";
            _deptBox.Items.Add(new DeptOption(existingDeptId, name));
        }

        _deptBox.SelectedIndex = 0;
    }

    private void SelectDept(int? deptId)
    {
        for (var i = 0; i < _deptBox.Items.Count; i++)
        {
            if (_deptBox.Items[i] is DeptOption option && option.DeptId == deptId)
            {
                _deptBox.SelectedIndex = i;
                return;
            }
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = _titleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "제목을 입력하세요.";
            return;
        }

        var selectedDeptId = (_deptBox.SelectedItem as DeptOption)?.DeptId;

        if (!_session.CanUseDept(selectedDeptId))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "본인의 담당업무로만 등록/수정할 수 있습니다.";
            return;
        }

        if (_editing is not null && !_session.CanUseDept(_editing.DeptId))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "이 일정은 본인의 담당업무와 관련이 없어 수정할 수 없습니다.";
            return;
        }

        var allDay = _allDayBox.Checked;
        var start = allDay ? _startDatePicker.Value.Date : CombineDateTime(_startDatePicker, _startTimePicker);
        var end = allDay ? _endDatePicker.Value.Date : CombineDateTime(_endDatePicker, _endTimePicker);

        if (end < start)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "종료 일시가 시작 일시보다 빠를 수 없습니다.";
            return;
        }

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = _editing is null ? "등록 중..." : "수정 중...";

        try
        {
            var ev = new SchoolEvent
            {
                Id = _editing?.Id ?? 0,
                Title = title,
                DeptId = selectedDeptId,
                CreatedBy = _editing?.CreatedBy ?? _session.Profile?.UserId,
                AllDay = allDay,
                Start = start.ToString("yyyy-MM-ddTHH:mm"),
                End = end.ToString("yyyy-MM-ddTHH:mm"),
                Location = _locationBox.Text.Trim(),
                Note = _noteBox.Text.Trim(),
            };

            var result = _editing is null
                ? await _api.AddEventAsync(ev)
                : await _api.UpdateEventAsync(ev);

            if (result.Ok)
            {
                _statusLabel.ForeColor = UiTheme.Success;
                _statusLabel.Text = _editing is null ? "등록되었습니다." : "수정되었습니다.";
                _session.NotifyScheduleChanged();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "처리에 실패했습니다."
                    : result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"오류: {ex.Message}";
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }

    private sealed record DeptOption(int? DeptId, string Name)
    {
        public override string ToString() => Name;
    }
}
