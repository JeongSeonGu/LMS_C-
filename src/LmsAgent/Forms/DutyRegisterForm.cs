using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 복무등록. 교장/교감/교무부장/행정실장의 연가·출장·조퇴 기록을
/// 월 단위로 조회하고, 기록 권한이 있는 계정(관리자 또는 해당 직위 본인)만 등록/수정/삭제할 수 있습니다.
/// 권한 여부는 서버(duty_status.php?action=can_manage)가 최종 판단하며, 이 화면은 그 결과에 맞춰
/// 등록/수정/삭제 버튼을 활성화·비활성화합니다.
/// </summary>
public sealed class DutyRegisterForm : Form
{
    private readonly WorkSupportApiClient _api;
    private bool _canManage;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly Button _prevMonthButton = new() { Left = 20, Top = 13, Width = 30, Text = "<" };
    private readonly Button _todayButton = new() { Left = 55, Top = 13, Width = 55, Text = "오늘" };

    private readonly Label _monthLabel = new()
    {
        Left = 115, Top = 16, Width = 140, TextAlign = ContentAlignment.MiddleCenter,
    };

    private readonly Button _nextMonthButton = new() { Left = 255, Top = 13, Width = 30, Text = ">" };

    private readonly Button _addButton = new()
    {
        Left = 560, Top = 12, Width = 90, Text = "등록...", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
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

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DutyRegisterForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "복무등록";
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
        UiTheme.StyleFlatToolButton(_prevMonthButton);
        UiTheme.StyleFlatToolButton(_todayButton);
        UiTheme.StyleFlatToolButton(_nextMonthButton);
        UiTheme.StylePrimaryButton(_addButton);
        UiTheme.StyleSecondaryButton(_editButton);
        UiTheme.StyleDangerButton(_deleteButton);
        UiTheme.StyleSecondaryButton(_closeButton);
        UiTheme.StyleSubHeaderLabel(_monthLabel);
        _monthLabel.ForeColor = UiTheme.SkyDark;
        UiTheme.StyleHintLabel(_hintLabel);

        _topPanel.Controls.Add(_prevMonthButton);
        _topPanel.Controls.Add(_todayButton);
        _topPanel.Controls.Add(_monthLabel);
        _topPanel.Controls.Add(_nextMonthButton);
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

        _prevMonthButton.Click += (_, _) => ChangeMonth(-1);
        _nextMonthButton.Click += (_, _) => ChangeMonth(1);
        _todayButton.Click += (_, _) => GoToMonth(DateTime.Today);
        _addButton.Click += OnAddClicked;
        _editButton.Click += OnEditClicked;
        _deleteButton.Click += OnDeleteClicked;
        _closeButton.Click += (_, _) => Close();
        _grid.SelectionChanged += (_, _) => UpdateButtonStates();

        Load += async (_, _) => await InitializeAsync();
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "날짜", DataPropertyName = "Date", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "대상", DataPropertyName = "Position", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "구분", DataPropertyName = "DutyType", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시간", DataPropertyName = "TimeRange", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "메모", DataPropertyName = "Note", Width = 200 });
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            var manageResult = await _api.GetDutyCanManageAsync();
            _canManage = manageResult.Ok && (manageResult.Data?.CanManage ?? false);
        }
        catch
        {
            _canManage = false;
        }

        _addButton.Enabled = _canManage;
        _hintLabel.Text = _canManage
            ? ""
            : "조회만 가능합니다. 등록/수정/삭제는 교장·교감·교무부장·행정실장 계정만 할 수 있습니다.";

        await LoadAsync();
    }

    private void ChangeMonth(int delta) => GoToMonth(_month.AddMonths(delta));

    private void GoToMonth(DateTime month)
    {
        _month = new DateTime(month.Year, month.Month, 1);
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        _monthLabel.Text = $"{_month.Year}년 {_month.Month}월";

        try
        {
            var result = await _api.GetDutyStatusAsync(_month.Year, _month.Month);
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "복무사항을 불러오지 못했습니다." : result.ErrorMessage,
                    "복무등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rows = new List<DutyRow>();
            foreach (var record in result.Data)
            {
                rows.Add(new DutyRow(record));
            }
            _grid.DataSource = rows;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"복무사항을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "복무등록", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private DutyRecord? SelectedRecord =>
        _grid.SelectedRows.Count > 0 ? (_grid.SelectedRows[0].DataBoundItem as DutyRow)?.Record : null;

    private void UpdateButtonStates()
    {
        var hasSelection = SelectedRecord is not null;
        _editButton.Enabled = _canManage && hasSelection;
        _deleteButton.Enabled = _canManage && hasSelection;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new DutyEditForm(_api, editing: null, initialDate: _month);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var record = SelectedRecord;
        if (record is null) return;

        using var form = new DutyEditForm(_api, record);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var record = SelectedRecord;
        if (record is null) return;

        var confirm = MessageBox.Show(
            $"{record.Date} {record.Position}의 {record.DutyType} 기록을 삭제하시겠습니까?",
            "복무등록 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _api.DeleteDutyAsync(record.Id);
            if (result.Ok)
            {
                await LoadAsync();
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "삭제에 실패했습니다." : result.ErrorMessage,
                    "복무등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                "복무등록", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class DutyRow
    {
        public DutyRow(DutyRecord record)
        {
            Record = record;
        }

        public DutyRecord Record { get; }
        public string Date => Record.Date;
        public string Position => Record.Position;
        public string DutyType => Record.DutyType;
        public string? Note => Record.Note;

        public string TimeRange => Record.IsAllDay
            ? "종일"
            : $"{ShortTime(Record.TimeStart)} ~ {ShortTime(Record.TimeEnd)}";

        private static string ShortTime(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : (value.Length > 5 ? value[..5] : value);
    }
}
