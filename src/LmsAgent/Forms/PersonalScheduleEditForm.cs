using System;
using System.Windows.Forms;
using LmsAgent.Models;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 사용자 정보 &gt; 개인일정 등록의 등록/수정 창입니다. 제목(필수)·날짜·시각(선택)·메모를
/// 입력합니다. 이 값들은 서버로 전송되지 않고 로컬 파일에만 저장됩니다
/// (<see cref="LmsAgent.Services.PersonalScheduleStore"/>).
/// </summary>
public sealed class PersonalScheduleEditForm : Form
{
    private readonly PersonalScheduleItem? _editing;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _titleBox = new() { Left = 90, Top = 8, Width = 260 };

    private readonly DateTimePicker _datePicker = new()
    {
        Left = 90, Top = 43, Width = 190, Format = DateTimePickerFormat.Short,
    };

    private readonly CheckBox _allDayBox = new() { Left = 90, Top = 78, Width = 100, Text = "종일(시간 없음)" };

    private readonly DateTimePicker _timePicker = new()
    {
        Left = 200, Top = 78, Width = 150, Format = DateTimePickerFormat.Time, ShowUpDown = true,
    };

    private readonly TextBox _noteBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true, ScrollBars = ScrollBars.Vertical,
    };

    private readonly Label _disclaimerLabel = new()
    {
        Left = 20, Top = 42, Width = 350, Height = 30,
        Text = "개인일정은 로컬에만 기록될 뿐 학사 일정과 연동이 되지 않습니다.",
    };

    private readonly Button _saveButton = new() { Left = 190, Top = 8, Width = 80 };
    private readonly Button _cancelButton = new() { Left = 280, Top = 8, Width = 90, Text = "취소" };

    private readonly Label _statusLabel = new() { Left = 20, Top = 8, Width = 160, ForeColor = UiTheme.Danger };

    public PersonalScheduleEditForm(PersonalScheduleItem? editing = null)
    {
        _editing = editing;

        Text = editing is null ? "개인일정 등록" : "개인일정 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(390, 400);
        MinimumSize = new Size(420, 420);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 11, Width = 90, Text = "제목" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 46, Width = 90, Text = "날짜" });
        _topPanel.Controls.Add(_titleBox);
        _topPanel.Controls.Add(_datePicker);
        _topPanel.Controls.Add(_allDayBox);
        _topPanel.Controls.Add(_timePicker);
        _bodyPanel.Controls.Add(_noteBox);
        _bottomPanel.Controls.Add(_saveButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_statusLabel);
        _bottomPanel.Controls.Add(_disclaimerLabel);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        UiTheme.StyleHintLabel(_disclaimerLabel);
        _saveButton.Text = editing is null ? "등록" : "수정";

        if (editing is not null)
        {
            _titleBox.Text = editing.Title;
            _noteBox.Text = editing.Note ?? "";
            _datePicker.Value = editing.Date == default ? DateTime.Today : editing.Date;

            if (editing.Time is { } time)
            {
                _allDayBox.Checked = false;
                _timePicker.Value = DateTime.Today.Add(time);
            }
            else
            {
                _allDayBox.Checked = true;
            }
        }
        else
        {
            _datePicker.Value = DateTime.Today;
            _allDayBox.Checked = true;
        }

        ApplyTimeVisibility();
        _allDayBox.CheckedChanged += (_, _) => ApplyTimeVisibility();

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    /// <summary>저장할 값. 사용자가 "등록/수정"을 눌러 <see cref="DialogResult.OK"/>로 닫혔을 때만 유효합니다.</summary>
    public PersonalScheduleItem? Result { get; private set; }

    private void ApplyTimeVisibility() => _timePicker.Enabled = !_allDayBox.Checked;

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = _titleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _statusLabel.Text = "제목을 입력하세요.";
            return;
        }

        Result = new PersonalScheduleItem
        {
            Id = _editing?.Id ?? Guid.NewGuid(),
            Title = title,
            Date = _datePicker.Value.Date,
            Time = _allDayBox.Checked ? null : _timePicker.Value.TimeOfDay,
            Note = string.IsNullOrWhiteSpace(_noteBox.Text) ? null : _noteBox.Text.Trim(),
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
