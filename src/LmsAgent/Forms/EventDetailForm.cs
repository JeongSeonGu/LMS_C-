using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>달력에서 일정 칩을 클릭했을 때 뜨는 간단한 상세 팝업입니다.</summary>
public sealed class EventDetailForm : Form
{
    public enum DetailAction
    {
        None,
        Edit,
        Delete,
    }

    public DetailAction SelectedAction { get; private set; } = DetailAction.None;

    private readonly Label _titleLabel = new()
    {
        Left = 16, Top = 16, Width = 308, Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
    };

    private readonly TextBox _bodyBox = new()
    {
        Left = 16, Top = 46, Width = 308, Height = 150,
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None, BackColor = UiTheme.Background,
    };

    private readonly Button _editButton = new() { Left = 16, Top = 208, Width = 80, Text = "수정" };
    private readonly Button _deleteButton = new() { Left = 104, Top = 208, Width = 80, Text = "삭제" };
    private readonly Button _closeButton = new() { Left = 244, Top = 208, Width = 80, Text = "닫기" };

    public EventDetailForm(string title, string bodyText, bool canEdit)
    {
        Text = "일정 상세";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 248);

        _titleLabel.Text = title;
        _bodyBox.Text = bodyText;
        _editButton.Enabled = canEdit;
        _deleteButton.Enabled = canEdit;

        Controls.Add(_titleLabel);
        Controls.Add(_bodyBox);
        Controls.Add(_editButton);
        Controls.Add(_deleteButton);
        Controls.Add(_closeButton);

        CancelButton = _closeButton;

        UiTheme.StylePrimaryButton(_editButton);
        UiTheme.StyleDangerButton(_deleteButton);
        UiTheme.StyleSecondaryButton(_closeButton);
        UiTheme.StyleSubHeaderLabel(_titleLabel);

        _editButton.Click += (_, _) => { SelectedAction = DetailAction.Edit; DialogResult = DialogResult.OK; Close(); };
        _deleteButton.Click += (_, _) => { SelectedAction = DetailAction.Delete; DialogResult = DialogResult.OK; Close(); };
        _closeButton.Click += (_, _) => Close();
    }
}
