using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 일정 목록 창입니다. 월 단위로 조회하며,
/// 자신의 담당업무와 관련된 일정만 수정/삭제 버튼이 활성화됩니다(관리자는 전체 가능).
/// </summary>
public sealed class ScheduleListForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    private readonly Button _prevMonthButton = new() { Left = 20, Top = 15, Width = 30, Text = "<" };
    private readonly Label _monthLabel = new() { Left = 55, Top = 18, Width = 140, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Button _nextMonthButton = new() { Left = 195, Top = 15, Width = 30, Text = ">" };

    private readonly Button _addButton = new() { Left = 320, Top = 15, Width = 90, Text = "새 일정..." };

    private readonly DataGridView _grid = new()
    {
        Left = 20,
        Top = 50,
        Width = 640,
        Height = 380,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };

    private readonly Button _editButton = new() { Left = 20, Top = 440, Width = 90, Text = "수정..." };
    private readonly Button _deleteButton = new() { Left = 115, Top = 440, Width = 90, Text = "삭제" };
    private readonly Button _refreshButton = new() { Left = 480, Top = 440, Width = 80, Text = "새로고침" };
    private readonly Button _closeButton = new() { Left = 570, Top = 440, Width = 90, Text = "닫기" };

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public ScheduleListForm(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;

        Text = "학사 일정 목록";
        Icon = AppIconProvider.Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 480);

        BuildGridColumns();

        Controls.Add(_prevMonthButton);
        Controls.Add(_monthLabel);
        Controls.Add(_nextMonthButton);
        Controls.Add(_addButton);
        Controls.Add(_grid);
        Controls.Add(_editButton);
        Controls.Add(_deleteButton);
        Controls.Add(_refreshButton);
        Controls.Add(_closeButton);

        _prevMonthButton.Click += (_, _) => ChangeMonth(-1);
        _nextMonthButton.Click += (_, _) => ChangeMonth(1);
        _addButton.Click += OnAddClicked;
        _editButton.Click += OnEditClicked;
        _deleteButton.Click += OnDeleteClicked;
        _refreshButton.Click += async (_, _) => await LoadAsync();
        _closeButton.Click += (_, _) => Close();
        _grid.SelectionChanged += (_, _) => UpdateButtonStates();

        Load += async (_, _) => await LoadAsync();
    }

    private void BuildGridColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Date", HeaderText = "날짜", DataPropertyName = "DisplayDate", Width = 140,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Title", HeaderText = "제목", DataPropertyName = "Title", Width = 220,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Dept", HeaderText = "담당업무", DataPropertyName = "DisplayDept", Width = 120,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Location", HeaderText = "장소", DataPropertyName = "Location", Width = 120,
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "CanEdit", HeaderText = "편집 가능", DataPropertyName = "CanEdit", Width = 70,
        });
    }

    private void ChangeMonth(int delta)
    {
        _month = _month.AddMonths(delta);
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        _monthLabel.Text = $"{_month.Year}년 {_month.Month}월";
        _grid.DataSource = null;

        try
        {
            var result = await _api.GetEventsAsync(_month.Year, _month.Month);
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "일정을 불러오지 못했습니다." : result.ErrorMessage,
                    "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rows = result.Data
                .OrderBy(ev => ev.StartDateTime)
                .Select(ev => new ScheduleRow(ev, _session, DeptName(ev.DeptId)))
                .ToList();

            _grid.DataSource = rows;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"일정을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private string DeptName(int? deptId)
    {
        if (deptId is null) return "관련 업무 없음";
        return _session.Departments.FirstOrDefault(d => d.Id == deptId)?.Name ?? $"업무 #{deptId}";
    }

    private ScheduleRow? SelectedRow =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as ScheduleRow : null;

    private void UpdateButtonStates()
    {
        var selected = SelectedRow;
        _editButton.Enabled = selected is not null && selected.CanEdit;
        _deleteButton.Enabled = selected is not null && selected.CanEdit;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new ScheduleRegisterForm(_api, _session);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        if (!row.CanEdit)
        {
            MessageBox.Show("자신의 담당업무와 관련된 일정만 수정할 수 있습니다.",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new ScheduleRegisterForm(_api, _session, row.Event);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        if (!row.CanEdit)
        {
            MessageBox.Show("자신의 담당업무와 관련된 일정만 삭제할 수 있습니다.",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show($"'{row.Event.Title}' 일정을 삭제하시겠습니까?",
            "학사 일정 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _api.DeleteEventAsync(row.Event.Id);
            if (result.Ok)
            {
                await LoadAsync();
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "삭제에 실패했습니다." : result.ErrorMessage,
                    "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class ScheduleRow
    {
        public ScheduleRow(SchoolEvent ev, SessionManager session, string deptName)
        {
            Event = ev;
            DisplayDept = deptName;
            CanEdit = session.CanUseDept(ev.DeptId);
        }

        public SchoolEvent Event { get; }
        public string Title => Event.Title;
        public string Location => Event.Location ?? "";
        public string DisplayDept { get; }
        public bool CanEdit { get; }

        public string DisplayDate => Event.AllDay
            ? $"{Event.StartDateTime:yyyy-MM-dd} (종일)"
            : $"{Event.StartDateTime:yyyy-MM-dd HH:mm} ~ {Event.EndDateTime:HH:mm}";
    }
}
