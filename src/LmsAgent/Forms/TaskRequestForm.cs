using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models;

namespace LmsAgent.Forms;

/// <summary>서버가 보낸 작업 요청을 사용자에게 보여주고 수락/거절을 입력받는 창입니다.</summary>
public sealed class TaskRequestForm : Form
{
    private readonly Label _titleLabel = new()
    {
        Left = 20,
        Top = 20,
        Width = 340,
        Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
    };

    private readonly TextBox _descriptionBox = new()
    {
        Left = 20,
        Top = 55,
        Width = 340,
        Height = 100,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private readonly Button _acceptButton = new() { Left = 150, Top = 165, Width = 90, Text = "수락" };
    private readonly Button _rejectButton = new() { Left = 250, Top = 165, Width = 90, Text = "거절" };

    public TaskRequestForm(TaskRequestPayload payload)
    {
        Text = "새로운 작업 요청";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(380, 205);

        _titleLabel.Text = payload.Title ?? payload.TaskType;
        _descriptionBox.Text = payload.Description ?? "";

        Controls.Add(_titleLabel);
        Controls.Add(_descriptionBox);
        Controls.Add(_acceptButton);
        Controls.Add(_rejectButton);

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
