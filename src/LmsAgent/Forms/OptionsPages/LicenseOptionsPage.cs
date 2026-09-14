using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "라이센스" — 학교에 발급된 인증키를 입력합니다. 로그인 직후 서버의 학교 정보(auth_key)와
/// 대조해서 값이 다르면 경고와 함께 3분 뒤 프로그램이 자동 종료됩니다(LicenseGuardService 참고).
/// </summary>
public sealed class LicenseOptionsPage : UserControl, IOptionsPage
{
    private readonly TextBox _licenseKeyBox = new() { Left = 130, Top = 20, Width = 260 };

    public string CategoryName => "라이센스";

    public LicenseOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "라이센스" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "인증키" });
        Controls.Add(_licenseKeyBox);

        var hint = new Label
        {
            Left = 20, Top = 55, Width = 380, Height = 60,
            Text = "학교에 발급된 인증키를 입력하세요. 로그인 직후 서버에 등록된 학교 인증키와\n" +
                   "대조하며, 값이 다르면 경고 후 3분 뒤 프로그램이 자동으로 종료됩니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);
    }

    public void LoadFrom(AppSettings settings)
    {
        _licenseKeyBox.Text = settings.LicenseKey ?? "";
    }

    public void SaveTo(AppSettings settings)
    {
        var key = _licenseKeyBox.Text.Trim();
        settings.LicenseKey = key.Length > 0 ? key : null;
    }
}
