using System;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 할일등록의 등록/수정 창입니다. 제목(필수)·마감일·담당업무·우선순위·메모를 입력합니다.
/// 수정 시에는 연동가이드.md §5-5에 따라 서버가 전체 교체를 하므로, 편집 대상 객체(<see cref="_editing"/>)를
/// 그대로 들고 있다가 사용자가 바꾼 항목만 반영한 전체 객체를 저장합니다(gcalTaskId 등도 그대로 되돌려 보냄).
/// </summary>
public sealed class TodoEditForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly TodoItem? _editing;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _titleBox = new() { Left = 90, Top = 8, Width = 260 };

    private readonly CheckBox _noDueDateBox = new() { Left = 90, Top = 43, Width = 60, Text = "없음" };

    private readonly DateTimePicker _dueDatePicker = new()
    {
        Left = 160, Top = 40, Width = 190, Format = DateTimePickerFormat.Short,
    };

    private readonly ComboBox _deptBox = new()
    {
        Left = 90, Top = 78, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly ComboBox _priorityBox = new()
    {
        Left = 90, Top = 113, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _doneBox = new() { Left = 90, Top = 148, Width = 200, Text = "완료됨" };

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

    public TodoEditForm(WorkSupportApiClient api, SessionManager session, TodoItem? editing = null)
    {
        _api = api;
        _session = session;
        _editing = editing;

        Text = editing is null ? "할일 등록" : "할일 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(390, 440);
        MinimumSize = new Size(420, 460);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 11, Width = 90, Text = "제목" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 46, Width = 90, Text = "마감일" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 81, Width = 90, Text = "담당업무" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 116, Width = 90, Text = "우선순위" });
        _topPanel.Controls.Add(_titleBox);
        _topPanel.Controls.Add(_noDueDateBox);
        _topPanel.Controls.Add(_dueDatePicker);
        _topPanel.Controls.Add(_deptBox);
        _topPanel.Controls.Add(_priorityBox);
        _topPanel.Controls.Add(_doneBox);
        _bodyPanel.Controls.Add(_noteBox);
        _bottomPanel.Controls.Add(_saveButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_statusLabel);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        _saveButton.Text = editing is null ? "등록" : "수정";
        _doneBox.Visible = editing is not null;

        PopulateDeptBox();
        foreach (var priority in TodoItem.Priorities)
        {
            _priorityBox.Items.Add(new PriorityOption(priority, TodoItem.PriorityLabel(priority)));
        }
        _priorityBox.SelectedIndex = 1; // medium

        if (editing is not null)
        {
            _titleBox.Text = editing.Title;
            _noteBox.Text = editing.Note ?? "";
            _doneBox.Checked = editing.Done;

            if (DateTime.TryParse(editing.DueDate, out var due))
            {
                _dueDatePicker.Value = due;
                _noDueDateBox.Checked = false;
            }
            else
            {
                _noDueDateBox.Checked = true;
            }

            SelectDept(editing.DeptId);
            SelectPriority(editing.Priority);
        }
        else
        {
            _noDueDateBox.Checked = true;
        }

        ApplyDueDateVisibility();
        _noDueDateBox.CheckedChanged += (_, _) => ApplyDueDateVisibility();

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private void ApplyDueDateVisibility()
    {
        _dueDatePicker.Enabled = !_noDueDateBox.Checked;
    }

    private void PopulateDeptBox()
    {
        _deptBox.Items.Add(new DeptOption(null, "관련 업무 없음"));
        foreach (var dept in _session.Departments)
        {
            _deptBox.Items.Add(new DeptOption(dept.Id, dept.Name));
        }

        if (_editing?.DeptId is int existingDeptId && _session.Departments.All(d => d.Id != existingDeptId))
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

    private void SelectPriority(string? priority)
    {
        for (var i = 0; i < _priorityBox.Items.Count; i++)
        {
            if (_priorityBox.Items[i] is PriorityOption option && option.Value == priority)
            {
                _priorityBox.SelectedIndex = i;
                return;
            }
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = _titleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _statusLabel.Text = "제목을 입력하세요.";
            return;
        }

        var deptId = (_deptBox.SelectedItem as DeptOption)?.DeptId;
        var priority = (_priorityBox.SelectedItem as PriorityOption)?.Value;
        var dueDate = _noDueDateBox.Checked ? null : _dueDatePicker.Value.ToString("yyyy-MM-dd");

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = _editing is null ? "등록 중..." : "수정 중...";

        try
        {
            var todo = new TodoItem
            {
                Id = _editing?.Id ?? 0,
                Title = title,
                DueDate = dueDate,
                DeptId = deptId,
                Priority = priority,
                Note = string.IsNullOrWhiteSpace(_noteBox.Text) ? null : _noteBox.Text.Trim(),
                Done = _doneBox.Checked,
                GcalTaskId = _editing?.GcalTaskId,
                GcalTaskListId = _editing?.GcalTaskListId,
            };

            var result = _editing is null
                ? await _api.AddTodoAsync(todo)
                : await _api.UpdateTodoAsync(todo);

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

    private sealed record DeptOption(int? DeptId, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record PriorityOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
