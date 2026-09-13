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

    private readonly TextBox _titleBox = new() { Left = 120, Top = 20, Width = 260 };

    private readonly ComboBox _deptBox = new()
    {
        Left = 120,
        Top = 55,
        Width = 260,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly DateTimePicker _startPicker = new()
    {
        Left = 120,
        Top = 90,
        Width = 260,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd (ddd) HH:mm",
        ShowUpDown = false,
    };

    private readonly DateTimePicker _endPicker = new()
    {
        Left = 120,
        Top = 122,
        Width = 260,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd (ddd) HH:mm",
        ShowUpDown = false,
    };

    private readonly CheckBox _allDayBox = new() { Left = 120, Top = 154, Width = 150, Text = "종일 일정" };
    private readonly TextBox _locationBox = new() { Left = 120, Top = 184, Width = 260 };

    private readonly TextBox _noteBox = new()
    {
        Left = 20,
        Top = 218,
        Width = 360,
        Height = 80,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _saveButton = new() { Left = 210, Top = 308, Width = 80 };
    private readonly Button _cancelButton = new() { Left = 300, Top = 308, Width = 80, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20,
        Top = 342,
        Width = 360,
        Height = 30,
        ForeColor = Color.Firebrick,
    };

    /// <param name="editing">null이면 신규 등록, 값이 있으면 해당 일정을 수정합니다.</param>
    public ScheduleRegisterForm(WorkSupportApiClient api, SessionManager session, SchoolEvent? editing = null)
    {
        _api = api;
        _session = session;
        _editing = editing;

        Text = editing is null ? "학사 일정 등록" : "학사 일정 수정";
        Icon = AppIconProvider.Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 380);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 90, Text = "제목" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 90, Text = "담당업무" });
        Controls.Add(new Label { Left = 20, Top = 93, Width = 90, Text = "시작" });
        Controls.Add(new Label { Left = 20, Top = 125, Width = 90, Text = "종료" });
        Controls.Add(new Label { Left = 20, Top = 187, Width = 90, Text = "장소" });
        Controls.Add(_titleBox);
        Controls.Add(_deptBox);
        Controls.Add(_startPicker);
        Controls.Add(_endPicker);
        Controls.Add(_allDayBox);
        Controls.Add(_locationBox);
        Controls.Add(_noteBox);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
        Controls.Add(_statusLabel);

        _saveButton.Text = editing is null ? "등록" : "수정";

        PopulateDeptBox();

        if (editing is not null)
        {
            _titleBox.Text = editing.Title;
            SelectDept(editing.DeptId);
            _allDayBox.Checked = editing.AllDay;
            if (editing.StartDateTime != DateTime.MinValue) _startPicker.Value = editing.StartDateTime;
            if (editing.EndDateTime != DateTime.MinValue) _endPicker.Value = editing.EndDateTime;
            _locationBox.Text = editing.Location ?? "";
            _noteBox.Text = editing.Note ?? "";
        }

        _allDayBox.CheckedChanged += (_, _) =>
        {
            _startPicker.ShowUpDown = !_allDayBox.Checked;
            _endPicker.ShowUpDown = !_allDayBox.Checked;
        };

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
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
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = "제목을 입력하세요.";
            return;
        }

        var selectedDeptId = (_deptBox.SelectedItem as DeptOption)?.DeptId;

        if (!_session.CanUseDept(selectedDeptId))
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = "본인의 담당업무로만 등록/수정할 수 있습니다.";
            return;
        }

        if (_editing is not null && !_session.CanUseDept(_editing.DeptId))
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = "이 일정은 본인의 담당업무와 관련이 없어 수정할 수 없습니다.";
            return;
        }

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = Color.Firebrick;
        _statusLabel.Text = _editing is null ? "등록 중..." : "수정 중...";

        try
        {
            var ev = new SchoolEvent
            {
                Id = _editing?.Id ?? 0,
                Title = title,
                DeptId = selectedDeptId,
                CreatedBy = _editing?.CreatedBy ?? _session.Profile?.UserId,
                AllDay = _allDayBox.Checked,
                Start = _startPicker.Value.ToString("yyyy-MM-ddTHH:mm"),
                End = _endPicker.Value.ToString("yyyy-MM-ddTHH:mm"),
                Location = _locationBox.Text.Trim(),
                Note = _noteBox.Text.Trim(),
            };

            var result = _editing is null
                ? await _api.AddEventAsync(ev)
                : await _api.UpdateEventAsync(ev);

            if (result.Ok)
            {
                _statusLabel.ForeColor = Color.SeaGreen;
                _statusLabel.Text = _editing is null ? "등록되었습니다." : "수정되었습니다.";
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.ForeColor = Color.Firebrick;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "처리에 실패했습니다."
                    : result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Color.Firebrick;
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
