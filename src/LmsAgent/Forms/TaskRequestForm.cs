using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>서버가 보낸 작업 요청을 사용자에게 보여주고 수락/거절을 입력받는 창입니다.</summary>
public sealed class TaskRequestForm : Form
{
    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _titlePanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 20, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 8) };
    private readonly Panel _buttonPanel = new() { Dock = DockStyle.Fill };

    private readonly Label _titleLabel = new()
    {
        Dock = DockStyle.Fill,
        Font = UiTheme.SubTitleFont,
        ForeColor = UiTheme.SkyDark,
    };

    private readonly TextBox _descriptionBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _acceptButton = new() { Left = 150, Top = 12, Width = 90, Text = "수락" };
    private readonly Button _rejectButton = new() { Left = 250, Top = 12, Width = 90, Text = "거절" };

    public TaskRequestForm(TaskRequestPayload payload)
    {
        Text = "새로운 작업 요청";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(380, 230);
        MinimumSize = new Size(400, 260);

        _titleLabel.Text = payload.Title ?? payload.TaskType;
        _descriptionBox.Text = payload.Description ?? "";
        _descriptionBox.BackColor = UiTheme.Background;

        UiTheme.StylePrimaryButton(_acceptButton);
        UiTheme.StyleSecondaryButton(_rejectButton);

        _titlePanel.Controls.Add(_titleLabel);
        _bodyPanel.Controls.Add(_descriptionBox);
        _buttonPanel.Controls.Add(_acceptButton);
        _buttonPanel.Controls.Add(_rejectButton);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        _root.Controls.Add(_titlePanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_buttonPanel, 0, 2);

        Controls.Add(_root);

        _acceptButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Yes;
            Close();
        };

        _rejectButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };
    }
}
