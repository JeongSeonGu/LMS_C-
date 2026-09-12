using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>사용자 정보 &gt; 정보 수정 메뉴에서 열리는 창입니다.</summary>
public sealed class UserInfoForm : Form
{
    private readonly WebSocketClientService _client;
    private readonly SessionManager _session;

    private readonly TextBox _nameBox = new() { Left = 120, Top = 20, Width = 220 };
    private readonly TextBox _emailBox = new() { Left = 120, Top = 55, Width = 220 };
    private readonly TextBox _phoneBox = new() { Left = 120, Top = 90, Width = 220 };
    private readonly Button _saveButton = new() { Left = 150, Top = 130, Width = 90, Text = "저장" };
    private readonly Button _closeButton = new() { Left = 250, Top = 130, Width = 90, Text = "닫기" };

    private readonly Label _statusLabel = new()
    {
        Left = 20,
        Top = 165,
        Width = 320,
        Height = 40,
        ForeColor = Color.Firebrick,
    };

    public UserInfoForm(WebSocketClientService client, SessionManager session)
    {
        _client = client;
        _session = session;

        Text = "사용자 정보 수정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 220);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 90, Text = "이름" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 90, Text = "이메일" });
        Controls.Add(new Label { Left = 20, Top = 93, Width = 90, Text = "전화번호" });
        Controls.Add(_nameBox);
        Controls.Add(_emailBox);
        Controls.Add(_phoneBox);
        Controls.Add(_saveButton);
        Controls.Add(_closeButton);
        Controls.Add(_statusLabel);

        var profile = _session.Profile;
        if (profile is not null)
        {
            _nameBox.Text = profile.Name;
            _emailBox.Text = profile.Email;
            _phoneBox.Text = profile.Phone;
        }

        _saveButton.Click += OnSaveClicked;
        _closeButton.Click += (_, _) => Close();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _saveButton.Enabled = false;
        _statusLabel.ForeColor = Color.Firebrick;
        _statusLabel.Text = "저장 중...";

        try
        {
            var name = _nameBox.Text.Trim();
            var email = _emailBox.Text.Trim();
            var phone = _phoneBox.Text.Trim();

            var request = WsEnvelope.Create(MessageTypes.UserUpdateProfile, new UpdateProfileRequest
            {
                Name = name,
                Email = email,
                Phone = phone,
            });

            var response = await _client.SendRequestAsync(request);
            var result = response.GetPayload<GenericResult>();

            if (result is { Success: true })
            {
                var profile = _session.Profile;
                if (profile is not null)
                {
                    profile.Name = name;
                    profile.Email = email;
                    profile.Phone = phone;
                    _session.UpdateProfile(profile);
                }

                _statusLabel.ForeColor = Color.SeaGreen;
                _statusLabel.Text = "저장되었습니다.";
            }
            else
            {
                _statusLabel.ForeColor = Color.Firebrick;
                _statusLabel.Text = result?.Message ?? "저장에 실패했습니다.";
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
