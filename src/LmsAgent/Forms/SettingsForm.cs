using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Forms;

/// <summary>환경 설정 메뉴에서 열리는 창입니다. 서버 주소, 자동 실행 여부를 관리합니다.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly TextBox _serverUrlBox = new() { Left = 160, Top = 20, Width = 250 };
    private readonly TextBox _updateUrlBox = new() { Left = 160, Top = 55, Width = 250 };

    private readonly CheckBox _autoStartBox = new()
    {
        Left = 160,
        Top = 90,
        Width = 250,
        Text = "Windows 시작 시 자동 실행",
    };

    private readonly Button _saveButton = new() { Left = 220, Top = 130, Width = 90, Text = "저장" };
    private readonly Button _cancelButton = new() { Left = 320, Top = 130, Width = 90, Text = "취소" };

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "환경 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(430, 175);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 130, Text = "웹소켓 서버 주소" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 130, Text = "업데이트 서버 주소" });
        Controls.Add(_serverUrlBox);
        Controls.Add(_updateUrlBox);
        Controls.Add(_autoStartBox);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);

        _serverUrlBox.Text = _settings.ServerUrl;
        _updateUrlBox.Text = _settings.UpdateManifestUrl;
        _autoStartBox.Checked = _settings.AutoStartWithWindows;

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        _saveButton.Click += OnSaveClicked;
        _cancelButton.Click += (_, _) => Close();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var serverUrl = _serverUrlBox.Text.Trim();
        var updateUrl = _updateUrlBox.Text.Trim();

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            MessageBox.Show("웹소켓 서버 주소가 올바르지 않습니다.", "환경 설정",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!Uri.TryCreate(updateUrl, UriKind.Absolute, out _))
        {
            MessageBox.Show("업데이트 서버 주소가 올바르지 않습니다.", "환경 설정",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.ServerUrl = serverUrl;
        _settings.UpdateManifestUrl = updateUrl;
        _settings.AutoStartWithWindows = _autoStartBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
