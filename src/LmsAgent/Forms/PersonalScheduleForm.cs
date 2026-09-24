using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 사용자 정보 &gt; 개인일정 등록. 학사 일정과 완전히 분리된, 이 PC에만 저장되는 개인용
/// 일정 목록입니다(<see cref="PersonalScheduleStore"/>). 로그인 여부와 무관하게 항상 쓸 수
/// 있고, 서버(DB)나 웹 화면과는 전혀 연동되지 않습니다.
/// </summary>
public sealed class PersonalScheduleForm : Form
{
    private readonly PersonalScheduleStore _store;
    private List<PersonalScheduleItem> _items = new();

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly Label _titleLabel = new() { Left = 20, Top = 13, Width = 200, Text = "개인일정 목록" };

    private readonly Button _addButton = new()
    {
        Left = 560, Top = 12, Width = 90, Text = "등록...", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, ReadOnly = true,
    };

    private readonly Button _editButton = new()
    {
        Left = 20, Top = 10, Width = 80, Text = "수정...", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Button _deleteButton = new()
    {
        Left = 108, Top = 10, Width = 80, Text = "삭제", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Label _disclaimerLabel = new()
    {
        Left = 200, Top = 15, Width = 350, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Left,
        Text = "개인일정은 로컬에만 기록될 뿐 학사 일정과 연동이 되지 않습니다.",
    };

    private readonly Button _closeButton = new()
    {
        Left = 560, Top = 10, Width = 80, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    public PersonalScheduleForm(PersonalScheduleStore store)
    {
        _store = store;

        Text = "개인일정 등록";
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
        UiTheme.StyleHintLabel(_disclaimerLabel);

        _topPanel.Controls.Add(_titleLabel);
        _topPanel.Controls.Add(_addButton);
        _contentPanel.Controls.Add(_grid);
        _bottomPanel.Controls.Add(_editButton);
        _bottomPanel.Controls.Add(_deleteButton);
        _bottomPanel.Controls.Add(_disclaimerLabel);
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
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEditClicked(null, EventArgs.Empty); };

        LoadFromStore();
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "날짜", DataPropertyName = "DateText", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시각", DataPropertyName = "TimeText", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "제목", DataPropertyName = "Title", Width = 220 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "메모", DataPropertyName = "Note", Width = 220 });
    }

    private void LoadFromStore()
    {
        _items = _store.Load();
        Refresh();
    }

    private void Refresh()
    {
        var rows = _items
            .OrderBy(i => i.Date)
            .ThenBy(i => i.Time ?? TimeSpan.Zero)
            .Select(i => new Row(i))
            .ToList();
        _grid.DataSource = rows;
        UpdateButtonStates();
    }

    private PersonalScheduleItem? SelectedItem =>
        _grid.SelectedRows.Count > 0 ? (_grid.SelectedRows[0].DataBoundItem as Row)?.Item : null;

    private void UpdateButtonStates()
    {
        var hasSelection = SelectedItem is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void SaveAndRefresh()
    {
        if (!_store.Save(_items, out var error))
        {
            MessageBox.Show($"개인일정을 저장하지 못했습니다: {error}",
                "개인일정 등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        Refresh();
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new PersonalScheduleEditForm();
        if (form.ShowDialog(this) == DialogResult.OK && form.Result is not null)
        {
            _items.Add(form.Result);
            SaveAndRefresh();
        }
    }

    private void OnEditClicked(object? sender, EventArgs e)
    {
        var item = SelectedItem;
        if (item is null) return;

        using var form = new PersonalScheduleEditForm(item);
        if (form.ShowDialog(this) == DialogResult.OK && form.Result is not null)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = form.Result;
                SaveAndRefresh();
            }
        }
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        var item = SelectedItem;
        if (item is null) return;

        var confirm = MessageBox.Show(
            $"'{item.Title}' 개인일정을 삭제하시겠습니까?",
            "개인일정 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _items.RemoveAll(i => i.Id == item.Id);
        SaveAndRefresh();
    }

    private sealed class Row
    {
        public Row(PersonalScheduleItem item)
        {
            Item = item;
        }

        public PersonalScheduleItem Item { get; }
        public string DateText => Item.Date.ToString("yyyy-MM-dd");
        public string TimeText => Item.Time is { } t ? t.ToString(@"hh\:mm") : "종일";
        public string Title => Item.Title;
        public string? Note => Item.Note;
    }
}
