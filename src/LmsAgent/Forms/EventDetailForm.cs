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

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly Panel _titlePanel = new() { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 0) };
    private readonly Panel _bodyPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 8) };
    private readonly Panel _buttonPanel = new() { Dock = DockStyle.Fill };

    private readonly Label _titleLabel = new()
    {
        Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
    };

    private readonly TextBox _bodyBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None, BackColor = UiTheme.Background,
    };

    private readonly Button _editButton = new() { Left = 16, Top = 8, Width = 80, Text = "수정" };
    private readonly Button _deleteButton = new() { Left = 104, Top = 8, Width = 80, Text = "삭제" };

    private readonly Button _closeButton = new()
    {
        Left = 244, Top = 8, Width = 80, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    public EventDetailForm(string title, string bodyText, bool canEdit)
    {
        Text = "일정 상세";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 280);
        MinimumSize = new Size(360, 300);

        _titleLabel.Text = title;
        _bodyBox.Text = bodyText;
        _editButton.Enabled = canEdit;
        _deleteButton.Enabled = canEdit;

        _titlePanel.Controls.Add(_titleLabel);
        _bodyPanel.Controls.Add(_bodyBox);
        _buttonPanel.Controls.Add(_editButton);
        _buttonPanel.Controls.Add(_deleteButton);
        _buttonPanel.Controls.Add(_closeButton);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        _root.Controls.Add(_titlePanel, 0, 0);
        _root.Controls.Add(_bodyPanel, 0, 1);
        _root.Controls.Add(_buttonPanel, 0, 2);

        Controls.Add(_root);

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
