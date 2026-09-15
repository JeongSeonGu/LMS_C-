using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>사용자 정보 &gt; 사용자 로그인 메뉴에서 열리는 로그인 창입니다.</summary>
public sealed class LoginForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    private readonly Panel _topPanel = new() { Dock = DockStyle.Top, Height = 152, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _statusPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 12) };

    private readonly TextBox _idBox = new() { Left = 90, Top = 11, Width = 200 };
    private readonly TextBox _passwordBox = new() { Left = 90, Top = 46, Width = 200, PasswordChar = '*' };
    private readonly CheckBox _saveIdBox = new() { Left = 90, Top = 76, Width = 200, Text = "아이디 저장" };

    private readonly Button _loginButton = new() { Left = 90, Top = 109, Width = 90, Text = "로그인" };
    private readonly Button _cancelButton = new() { Left = 190, Top = 109, Width = 90, Text = "취소" };

    // 진단 메시지(응답 스니펫 포함)가 길어질 수 있어 스크롤/복사가 가능한 읽기 전용 텍스트박스로 표시한다.
    private readonly TextBox _statusLabel = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = UiTheme.Background,
        ScrollBars = ScrollBars.Vertical,
        ForeColor = UiTheme.Danger,
        TabStop = false,
    };

    public bool SaveLoginId => _saveIdBox.Checked;

    public string LoginId => _idBox.Text.Trim();

    public LoginForm(WorkSupportApiClient api, SessionManager session, string? savedLoginId)
    {
        _api = api;
        _session = session;

        Text = "사용자 로그인";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(330, 280);
        MinimumSize = new Size(360, 340);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 14, Width = 80, Text = "아이디" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 49, Width = 80, Text = "비밀번호" });
        _topPanel.Controls.Add(_idBox);
        _topPanel.Controls.Add(_passwordBox);
        _topPanel.Controls.Add(_saveIdBox);
        _topPanel.Controls.Add(_loginButton);
        _topPanel.Controls.Add(_cancelButton);
        _statusPanel.Controls.Add(_statusLabel);

        Controls.Add(_topPanel);
        Controls.Add(_statusPanel);

        if (!string.IsNullOrWhiteSpace(savedLoginId))
        {
            _idBox.Text = savedLoginId;
            _saveIdBox.Checked = true;
        }

        AcceptButton = _loginButton;
        CancelButton = _cancelButton;

        UiTheme.StylePrimaryButton(_loginButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _loginButton.Click += OnLoginClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var loginId = _idBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrEmpty(loginId) || string.IsNullOrEmpty(password))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "아이디와 비밀번호를 입력하세요.";
            return;
        }

        _loginButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = "로그인 중...";

        try
        {
            var result = await _api.LoginAsync(loginId, password);

            if (!result.Ok || result.Data?.User is null)
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "로그인에 실패했습니다."
                    : result.ErrorMessage;
                return;
            }

            _session.SetSession(result.Data.User);

            _statusLabel.ForeColor = UiTheme.Success;
            _statusLabel.Text = "담당업무 정보를 불러오는 중...";

            await _session.RefreshDepartmentContextAsync(_api);

            _statusLabel.Text = "로그인 성공";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"로그인 오류: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }
    }
}
