using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 사용자 정보 &gt; 정보 수정 메뉴에서 열리는 창입니다.
/// php/auth/profile.php 를 사용하며, 이름/직위/담당업무는 서버가 관리하므로 읽기 전용으로 보여주고,
/// 아이디와 연락처만 수정할 수 있습니다. 변경에는 현재 비밀번호 확인이 필요합니다.
/// </summary>
public sealed class UserInfoForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    private readonly Panel _topPanel = new() { Dock = DockStyle.Top, Height = 258, Padding = new Padding(20, 12, 20, 0) };
    private readonly Panel _statusPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 12) };

    private readonly Label _nameValueLabel = new() { Left = 100, Top = 8, Width = 220 };
    private readonly Label _positionValueLabel = new() { Left = 100, Top = 33, Width = 220 };
    private readonly Label _deptValueLabel = new() { Left = 100, Top = 58, Width = 220 };

    private readonly TextBox _loginIdBox = new() { Left = 100, Top = 91, Width = 220 };
    private readonly TextBox _contactBox = new() { Left = 100, Top = 121, Width = 220 };
    private readonly TextBox _currentPwBox = new() { Left = 100, Top = 151, Width = 220, PasswordChar = '*' };
    private readonly TextBox _newPwBox = new() { Left = 100, Top = 181, Width = 220, PasswordChar = '*' };

    private readonly Button _saveButton = new() { Left = 130, Top = 216, Width = 90, Text = "저장" };
    private readonly Button _closeButton = new() { Left = 230, Top = 216, Width = 90, Text = "닫기" };

    private readonly Label _statusLabel = new()
    {
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Danger,
    };

    public UserInfoForm(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;

        Text = "사용자 정보 수정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 340);
        MinimumSize = new Size(400, 380);

        _topPanel.Controls.Add(new Label { Left = 0, Top = 8, Width = 90, Text = "이름" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 33, Width = 90, Text = "직위" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 58, Width = 90, Text = "담당업무" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 94, Width = 90, Text = "아이디" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 124, Width = 90, Text = "연락처" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 154, Width = 90, Text = "현재 비밀번호" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 184, Width = 90, Text = "새 비밀번호" });
        _topPanel.Controls.Add(new Label { Left = 0, Top = 201, Width = 320, Text = "(변경하지 않으려면 비워두세요)", ForeColor = UiTheme.TextSecondary });

        _topPanel.Controls.Add(_nameValueLabel);
        _topPanel.Controls.Add(_positionValueLabel);
        _topPanel.Controls.Add(_deptValueLabel);
        _topPanel.Controls.Add(_loginIdBox);
        _topPanel.Controls.Add(_contactBox);
        _topPanel.Controls.Add(_currentPwBox);
        _topPanel.Controls.Add(_newPwBox);
        _topPanel.Controls.Add(_saveButton);
        _topPanel.Controls.Add(_closeButton);
        _statusPanel.Controls.Add(_statusLabel);

        Controls.Add(_topPanel);
        Controls.Add(_statusPanel);

        UiTheme.StylePrimaryButton(_saveButton);
        UiTheme.StyleSecondaryButton(_closeButton);

        _saveButton.Click += OnSaveClicked;
        _closeButton.Click += (_, _) => Close();

        Load += OnLoadAsync;
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        _statusLabel.ForeColor = UiTheme.TextSecondary;
        _statusLabel.Text = "정보를 불러오는 중...";

        try
        {
            var result = await _api.GetProfileAsync();
            if (!result.Ok || result.Data is null)
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = result.ErrorMessage is { Length: > 0 } msg ? msg : "정보를 불러오지 못했습니다.";
                return;
            }

            var profile = result.Data;
            _nameValueLabel.Text = profile.Name;
            _positionValueLabel.Text = profile.Position ?? "-";
            _deptValueLabel.Text = profile.DeptName ?? "관련 업무 없음";
            _loginIdBox.Text = profile.LoginId;
            _contactBox.Text = profile.Contact ?? "";
            _statusLabel.Text = "";
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"오류: {ex.Message}";
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var loginId = _loginIdBox.Text.Trim();
        var contact = _contactBox.Text.Trim();
        var currentPw = _currentPwBox.Text;
        var newPw = _newPwBox.Text;

        if (string.IsNullOrEmpty(loginId))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "아이디를 입력하세요.";
            return;
        }

        if (string.IsNullOrEmpty(currentPw))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "현재 비밀번호를 입력하세요.";
            return;
        }

        _saveButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = "저장 중...";

        try
        {
            var result = await _api.UpdateProfileAsync(contact, loginId, currentPw, newPw);

            if (result.Ok)
            {
                MessageBox.Show(
                    "저장되었습니다. 변경 사항 적용을 위해 다시 로그인해 주세요.",
                    "사용자 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _session.Clear();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "저장에 실패했습니다."
                    : result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"오류: {ex.Message}";
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }
}
