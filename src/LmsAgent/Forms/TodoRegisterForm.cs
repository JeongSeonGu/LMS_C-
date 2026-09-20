using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 할일등록. 학교 전체가 함께 보는 할일 목록을 조회하고, 기록 권한이 있는
/// 계정(관리자 또는 해당 직위 본인, 또는 개별 허용된 계정)만 등록/수정/삭제 및 완료 체크를 할 수 있습니다.
/// 권한 여부는 서버(todos.php?action=can_manage)가 최종 판단하며, 이 화면은 그 결과에 맞춰
/// 등록/수정/삭제 버튼과 완료 체크박스를 활성화·비활성화합니다(연동가이드.md §2-4, §5).
/// </summary>
public sealed class TodoRegisterForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private bool _canManage;
    private int _doneColumnIndex;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly Label _titleLabel = new()
    {
        Left = 20, Top = 13, Width = 200, Text = "할일 목록",
    };

    private readonly Button _addButton = new()
    {
        Left = 560, Top = 12, Width = 90, Text = "등록...", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
    };

    private readonly Button _editButton = new()
    {
        Left = 20, Top = 10, Width = 80, Text = "수정...", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Button _deleteButton = new()
    {
        Left = 108, Top = 10, Width = 80, Text = "삭제", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Label _hintLabel = new()
    {
        Left = 200, Top = 15, Width = 350, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Button _closeButton = new()
    {
        Left = 560, Top = 10, Width = 80, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    public TodoRegisterForm(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;

        Text = "할일등록";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(660, 490);
        MinimumSize = new Size(640, 480);

        BuildColumns();
        UiTheme.StyleGrid(_grid);
        UiTheme.StylePrimaryButton(_addButton);
        UiTheme.StyleSecondaryButton(_editButton);
        UiTheme.StyleDangerButton(_deleteButton);
        UiTheme.StyleSecondaryButton(_closeButton);
        UiTheme.StyleSubHeaderLabel(_titleLabel);
        _titleLabel.ForeColor = UiTheme.SkyDark;
        UiTheme.StyleHintLabel(_hintLabel);

        _topPanel.Controls.Add(_titleLabel);
        _topPanel.Controls.Add(_addButton);
        _contentPanel.Controls.Add(_grid);
        _bottomPanel.Controls.Add(_editButton);
        _bottomPanel.Controls.Add(_deleteButton);
        _bottomPanel.Controls.Add(_hintLabel);
        _bottomPanel.Controls.Add(_closeButton);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_contentPanel, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        _addButton.Click += OnAddClicked;
        _editButton.Click += OnEditClicked;
        _deleteButton.Click += OnDeleteClicked;
        _closeButton.Click += (_, _) => Close();
        _grid.SelectionChanged += (_, _) => UpdateButtonStates();
        _grid.CurrentCellDirtyStateChanged += OnCurrentCellDirtyStateChanged;
        _grid.CellValueChanged += OnCellValueChanged;

        Load += async (_, _) => await InitializeAsync();
    }

    private void BuildColumns()
    {
        var doneColumn = new DataGridViewCheckBoxColumn
        {
            HeaderText = "완료", DataPropertyName = "Done", Width = 50,
        };
        _grid.Columns.Add(doneColumn);
        _doneColumnIndex = doneColumn.Index;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "제목", DataPropertyName = "Title", Width = 220, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "마감일", DataPropertyName = "DueDate", Width = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "담당업무", DataPropertyName = "Dept", Width = 100, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "우선순위", DataPropertyName = "Priority", Width = 70, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "메모", DataPropertyName = "Note", Width = 160, ReadOnly = true });
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            var manageResult = await _api.GetTodoCanManageAsync();
            _canManage = manageResult.Ok && (manageResult.Data?.CanManage ?? false);
        }
        catch
        {
            _canManage = false;
        }

        _addButton.Enabled = _canManage;
        _grid.Columns[_doneColumnIndex].ReadOnly = !_canManage;
        _hintLabel.Text = _canManage
            ? ""
            : "조회만 가능합니다. 등록/수정/삭제 및 완료 체크는 기록 권한이 있는 계정만 할 수 있습니다.";

        await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var result = await _api.GetTodosAsync();
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "할일 목록을 불러오지 못했습니다." : result.ErrorMessage,
                    "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rows = new List<TodoRow>();
            foreach (var item in result.Data)
            {
                var deptName = item.DeptId is int deptId
                    ? _session.Departments.Find(d => d.Id == deptId)?.Name ?? $"업무 #{deptId}"
                    : "";
                rows.Add(new TodoRow(item, deptName));
            }
            _grid.DataSource = rows;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"할일 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private TodoRow? SelectedRow =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as TodoRow : null;

    private void UpdateButtonStates()
    {
        var hasSelection = SelectedRow is not null;
        _editButton.Enabled = _canManage && hasSelection;
        _deleteButton.Enabled = _canManage && hasSelection;
    }

    private void OnCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    /// <summary>완료 체크박스를 직접 눌렀을 때 — id/done만 즉시 저장합니다(연동가이드.md §5-4).</summary>
    private async void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _doneColumnIndex) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not TodoRow row) return;

        try
        {
            var result = await _api.ToggleTodoAsync(row.Item.Id, row.Done);
            if (!result.Ok)
            {
                row.Done = !row.Done;
                _grid.Refresh();
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "완료 처리에 실패했습니다." : result.ErrorMessage,
                    "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            row.Done = !row.Done;
            _grid.Refresh();
            MessageBox.Show($"완료 처리 중 오류가 발생했습니다: {ex.Message}",
                "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new TodoEditForm(_api, _session, editing: null);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        using var form = new TodoEditForm(_api, _session, row.Item);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        var confirm = MessageBox.Show(
            $"'{row.Title}' 할일을 삭제하시겠습니까?",
            "할일등록 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _api.DeleteTodoAsync(row.Item.Id);
            if (result.Ok)
            {
                await LoadAsync();
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "삭제에 실패했습니다." : result.ErrorMessage,
                    "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class TodoRow
    {
        public TodoRow(TodoItem item, string dept)
        {
            Item = item;
            Dept = dept;
        }

        public TodoItem Item { get; }

        public bool Done
        {
            get => Item.Done;
            set => Item.Done = value;
        }

        public string Title => Item.Title;
        public string DueDate => Item.DueDate ?? "";
        public string Dept { get; }
        public string Priority => TodoItem.PriorityLabel(Item.Priority);
        public string? Note => Item.Note;
    }
}
