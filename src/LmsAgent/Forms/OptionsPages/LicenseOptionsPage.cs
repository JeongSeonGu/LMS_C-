using System;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "라이센스" — 학교에 발급된 인증키를 입력합니다. 로그인 직후(및 이 창을 열 때마다)
/// 서버의 학교 정보(auth_key)와 대조해서 값이 다르면 경고와 함께 3분 뒤 프로그램이 자동
/// 종료됩니다(백그라운드 감시 자체는 LicenseGuardService가 담당). 이 페이지는 그 결과를
/// 트레이 메뉴까지 가지 않아도 바로 확인할 수 있도록 자체적으로도 한 번 더 검사해 보여줍니다.
/// </summary>
public sealed class LicenseOptionsPage : UserControl, IOptionsPage
{
    private readonly WorkSupportApiClient _api;

    private readonly TextBox _licenseKeyBox = new() { Left = 130, Top = 20, Width = 220 };

    private readonly Button _checkButton = new() { Left = 358, Top = 18, Width = 90, Text = "지금 확인" };

    private readonly Label _statusLabel = new()
    {
        Left = 20, Top = 50, Width = 420, Height = 20,
    };

    public string CategoryName => "라이센스";

    public LicenseOptionsPage(WorkSupportApiClient api)
    {
        _api = api;

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "라이센스" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "인증키" });
        Controls.Add(_licenseKeyBox);
        Controls.Add(_checkButton);
        Controls.Add(_statusLabel);
        UiTheme.StyleHintLabel(_statusLabel);
        UiTheme.StyleSecondaryButton(_checkButton);

        var hint = new Label
        {
            Left = 20, Top = 78, Width = 420, Height = 60,
            Text = "학교에 발급된 인증키를 입력하세요. 로그인 직후와 환경설정 저장 시 서버에 등록된\n" +
                   "학교 인증키와 자동으로 대조하며, 값이 다르면 경고 후 3분 뒤 프로그램이 자동으로\n" +
                   "종료됩니다. \"지금 확인\"으로 저장하지 않고도 바로 검증해 볼 수 있습니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);

        _checkButton.Click += async (_, _) => await RunCheckAsync(_licenseKeyBox.Text);
    }

    public void LoadFrom(AppSettings settings)
    {
        _licenseKeyBox.Text = settings.LicenseKey ?? "";
        _ = RunCheckAsync(_licenseKeyBox.Text);
    }

    public void SaveTo(AppSettings settings)
    {
        var key = _licenseKeyBox.Text.Trim();
        settings.LicenseKey = key.Length > 0 ? key : null;
    }

    public string? ValidateSettings() => null;

    /// <summary>
    /// LicenseGuardService.CheckAsync()와 같은 비교 로직을 이 화면에서 바로 보여주기 위한
    /// 것으로, 자동 종료 타이머는 여기서 다루지 않습니다(그건 백그라운드 서비스의 역할).
    /// </summary>
    private async System.Threading.Tasks.Task RunCheckAsync(string keyToCheck)
    {
        _statusLabel.ForeColor = UiTheme.TextSecondary;
        _statusLabel.Text = "확인 중...";

        try
        {
            var result = await _api.GetSchoolInfoAsync();
            var serverKey = result.Ok ? result.Data?.Info.AuthKey : null;

            if (string.IsNullOrWhiteSpace(serverKey))
            {
                _statusLabel.ForeColor = UiTheme.TextSecondary;
                _statusLabel.Text = "학교에 인증키가 설정되어 있지 않습니다(검사하지 않음).";
                return;
            }

            if (string.IsNullOrWhiteSpace(keyToCheck))
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = "인증키를 입력하세요.";
                return;
            }

            if (string.Equals(serverKey.Trim(), keyToCheck.Trim(), StringComparison.Ordinal))
            {
                _statusLabel.ForeColor = UiTheme.Success;
                _statusLabel.Text = "✔ 인증되었습니다.";
            }
            else
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = "✘ 인증키가 일치하지 않습니다.";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"확인 실패: {ex.Message}";
        }
    }
}
