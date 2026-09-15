using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>기본정보 &gt; 요청사항의 등록/수정 창입니다. 대상은 항상 "전체 공개"로 등록됩니다.</summary>
public sealed class RequestEditForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly RequestDetail? _editing;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _titleBox = new() { Left = 90, Top = 8, Width = 280 };

    private readonly ComboBox _categoryBox = new()
    {
        Left = 90, Top = 43, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly ComboBox _priorityBox = new()
    {
        Left = 90, Top = 78, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly ComboBox _statusBox = new()
    {
        Left = 90, Top = 113, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _hasDueBox = new() { Left = 90, Top = 148, Width = 100, Text = "기한 지정" };
    private readonly DateTimePicker _dueDatePicker = new() { Left = 190, Top = 146, Width = 150, Enabled = false };

    private readonly TextBox _bodyBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true, ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _saveButton = new() { Left = 210, Top = 8, Width = 80 };
    private readonly Button _cancelButton = new() { Left = 300, Top = 8, Width = 90, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20, Top = 42, Width = 370, Height = 30, ForeColor = UiTheme.Danger,
    };

    public RequestEditForm(WorkSupportApiClient api, RequestMeta meta, RequestDetail? editing = null)
    {
        _api = api;
        _editing = editing;

        Text = editing is null ? "요청사항 등록" : "요청사항 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(410, 420);
        MinimumSize = new Size(420, 440);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 11, Width = 90, Text = "제목" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 46, Width = 90, Text = "분류" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 81, Width = 90, Text = "우선순위" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 116, Width = 90, Text = "상태" });
        _topPanel.Controls.Add(_titleBox);
        _topPanel.Controls.Add(_categoryBox);
        _topPanel.Controls.Add(_priorityBox);
        _topPanel.Controls.Add(_statusBox);
        _topPanel.Controls.Add(_hasDueBox);
        _topPanel.Controls.Add(_dueDatePicker);
        _bodyPanel.Controls.Add(_bodyBox);
        _bottomPanel.Controls.Add(_saveButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_statusLabel);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        _saveButton.Text = editing is null ? "등록" : "수정";

        foreach (var category in meta.Categories)
        {
            _categoryBox.Items.Add(category);
        }
        foreach (var kv in meta.Priority)
        {
            _priorityBox.Items.Add(new KeyValueOption(kv.Key, kv.Value));
        }
        foreach (var kv in meta.Status)
        {
            _statusBox.Items.Add(new KeyValueOption(kv.Key, kv.Value));
        }

        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
        SelectByKey(_priorityBox, "normal");
        SelectByKey(_statusBox, "open");

        if (editing is not null)
        {
            _titleBox.Text = editing.Title;
            _bodyBox.Text = editing.Body ?? "";
            if (!string.IsNullOrWhiteSpace(editing.Category)) _categoryBox.SelectedItem = editing.Category;
            SelectByKey(_priorityBox, editing.Priority ?? "normal");
            SelectByKey(_statusBox, editing.Status);

            if (!string.IsNullOrWhiteSpace(editing.DueDate) && DateTime.TryParse(editing.DueDate, out var due))
            {
                _hasDueBox.Checked = true;
                _dueDatePicker.Value = due;
            }
        }

        _hasDueBox.CheckedChanged += (_, _) => _dueDatePicker.Enabled = _hasDueBox.Checked;

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private static void SelectByKey(ComboBox box, string key)
    {
        foreach (var item in box.Items)
        {
            if (item is KeyValueOption option && option.Key == key)
            {
                box.SelectedItem = option;
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

        var category = _categoryBox.SelectedItem as string ?? "기타";
        var priority = (_priorityBox.SelectedItem as KeyValueOption)?.Key ?? "normal";
        var status = (_statusBox.SelectedItem as KeyValueOption)?.Key ?? "open";
        var dueDate = _hasDueBox.Checked ? _dueDatePicker.Value.ToString("yyyy-MM-dd") : null;

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = _editing is null ? "등록 중..." : "수정 중...";

        try
        {
            var result = await _api.SaveRequestAsync(
                _editing?.Id ?? 0, title, _bodyBox.Text.Trim(), category, priority, status, dueDate);

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

    private sealed record KeyValueOption(string Key, string Label)
    {
        public override string ToString() => Label;
    }
}
