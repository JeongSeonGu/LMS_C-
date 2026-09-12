using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models;
using LmsAgent.Networking;

namespace LmsAgent.Forms;

/// <summary>학사 일정 &gt; 일정 등록 메뉴에서 열리는 창입니다.</summary>
public sealed class ScheduleRegisterForm : Form
{
    private readonly WebSocketClientService _client;

    private readonly TextBox _titleBox = new() { Left = 120, Top = 20, Width = 260 };

    private readonly ComboBox _categoryBox = new()
    {
        Left = 120,
        Top = 55,
        Width = 150,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly DateTimePicker _startDatePicker = new() { Left = 120, Top = 90, Width = 150 };
    private readonly CheckBox _hasEndDateBox = new() { Left = 20, Top = 125, Width = 100, Text = "종료일 지정" };
    private readonly DateTimePicker _endDatePicker = new() { Left = 120, Top = 122, Width = 150, Enabled = false };

    private readonly TextBox _descriptionBox = new()
    {
        Left = 20,
        Top = 155,
        Width = 360,
        Height = 80,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _saveButton = new() { Left = 210, Top = 245, Width = 80, Text = "등록" };
    private readonly Button _cancelButton = new() { Left = 300, Top = 245, Width = 80, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20,
        Top = 280,
        Width = 360,
        Height = 30,
        ForeColor = Color.Firebrick,
    };

    public ScheduleRegisterForm(WebSocketClientService client)
    {
        _client = client;

        Text = "학사 일정 등록";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 320);

        _categoryBox.Items.AddRange(new object[] { "일반", "시험", "행사", "휴일", "공지" });
        _categoryBox.SelectedIndex = 0;

        Controls.Add(new Label { Left = 20, Top = 23, Width = 90, Text = "제목" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 90, Text = "분류" });
        Controls.Add(new Label { Left = 20, Top = 93, Width = 90, Text = "시작일" });
        Controls.Add(_titleBox);
        Controls.Add(_categoryBox);
        Controls.Add(_startDatePicker);
        Controls.Add(_hasEndDateBox);
        Controls.Add(_endDatePicker);
        Controls.Add(_descriptionBox);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
        Controls.Add(_statusLabel);

        _hasEndDateBox.CheckedChanged += (_, _) => _endDatePicker.Enabled = _hasEndDateBox.Checked;
        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
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

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = Color.Firebrick;
        _statusLabel.Text = "등록 중...";

        try
        {
            var item = new ScheduleItem
            {
                Title = title,
                Category = (string)_categoryBox.SelectedItem!,
                StartDate = _startDatePicker.Value.Date,
                EndDate = _hasEndDateBox.Checked ? _endDatePicker.Value.Date : null,
                Description = _descriptionBox.Text.Trim(),
            };

            var request = WsEnvelope.Create(MessageTypes.ScheduleRegister, item);
            var response = await _client.SendRequestAsync(request);
            var result = response.GetPayload<GenericResult>();

            if (result is { Success: true })
            {
                _statusLabel.ForeColor = Color.SeaGreen;
                _statusLabel.Text = "등록되었습니다.";
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.ForeColor = Color.Firebrick;
                _statusLabel.Text = result?.Message ?? "등록에 실패했습니다.";
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
}
