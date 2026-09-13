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

    private readonly TextBox _idBox = new() { Left = 110, Top = 20, Width = 200 };
    private readonly TextBox _passwordBox = new() { Left = 110, Top = 55, Width = 200, PasswordChar = '*' };
    private readonly CheckBox _saveIdBox = new() { Left = 110, Top = 85, Width = 200, Text = "아이디 저장" };
    private readonly Button _loginButton = new() { Left = 110, Top = 118, Width = 90, Text = "로그인" };
    private readonly Button _cancelButton = new() { Left = 220, Top = 118, Width = 90, Text = "취소" };

    private readonly Label _statusLabel = new()
    {
        Left = 20,
        Top = 152,
        Width = 290,
        Height = 40,
        ForeColor = Color.Firebrick,
    };

    public bool SaveLoginId => _saveIdBox.Checked;

    public string LoginId => _idBox.Text.Trim();

    public LoginForm(WorkSupportApiClient api, SessionManager session, string? savedLoginId)
    {
        _api = api;
        _session = session;

        Text = "사용자 로그인";
        Icon = AppIconProvider.Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(330, 200);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 80, Text = "아이디" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 80, Text = "비밀번호" });
        Controls.Add(_idBox);
        Controls.Add(_passwordBox);
        Controls.Add(_saveIdBox);
        Controls.Add(_loginButton);
        Controls.Add(_cancelButton);
        Controls.Add(_statusLabel);

        if (!string.IsNullOrWhiteSpace(savedLoginId))
        {
            _idBox.Text = savedLoginId;
            _saveIdBox.Checked = true;
        }

        AcceptButton = _loginButton;
        CancelButton = _cancelButton;

        _loginButton.Click += OnLoginClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var loginId = _idBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrEmpty(loginId) || string.IsNullOrEmpty(password))
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = "아이디와 비밀번호를 입력하세요.";
            return;
        }

        _loginButton.Enabled = false;
        _statusLabel.ForeColor = Color.Firebrick;
        _statusLabel.Text = "로그인 중...";

        try
        {
            var result = await _api.LoginAsync(loginId, password);

            if (!result.Ok || result.Data?.User is null)
            {
                _statusLabel.ForeColor = Color.Firebrick;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "로그인에 실패했습니다."
                    : result.ErrorMessage;
                return;
            }

            _session.SetSession(result.Data.User);

            _statusLabel.ForeColor = Color.SeaGreen;
            _statusLabel.Text = "담당업무 정보를 불러오는 중...";

            await _session.RefreshDepartmentContextAsync(_api);

            _statusLabel.Text = "로그인 성공";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = $"로그인 오류: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }
    }
}
