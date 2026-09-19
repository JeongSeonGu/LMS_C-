using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "실시간 연동" — 학사일정/복무 등이 웹 브라우저와 실시간으로 반영되도록 하는
/// 웹소켓 연결에 쓸 기기 자격증명을 입력합니다. 이 기기 ID/토큰은 서버 관리자가
/// schoolwork_realtime_devices 테이블에 미리 등록해 둔 값과 정확히 같아야 하며,
/// 토큰은 저장 시 이 PC의 Windows 계정으로 DPAPI 암호화되어 저장됩니다(평문 저장 안 함).
/// </summary>
public sealed class RealtimeOptionsPage : UserControl, IOptionsPage
{
    private readonly TextBox _deviceIdBox = new() { Left = 140, Top = 20, Width = 260 };

    private readonly TextBox _deviceTokenBox = new()
    {
        Left = 140, Top = 55, Width = 260, PasswordChar = '*',
    };

    private readonly Label _tokenStatusLabel = new()
    {
        Left = 140, Top = 82, Width = 300, Height = 20,
    };

    public string CategoryName => "실시간 연동";

    private bool _hasStoredToken;

    public RealtimeOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "실시간 연동" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 110, Text = "기기 ID" });
        Controls.Add(_deviceIdBox);
        Controls.Add(new Label { Left = 20, Top = 58, Width = 110, Text = "기기 토큰" });
        Controls.Add(_deviceTokenBox);
        Controls.Add(_tokenStatusLabel);
        UiTheme.StyleHintLabel(_tokenStatusLabel);

        var hint = new Label
        {
            Left = 20, Top = 112, Width = 400, Height = 90,
            Text = "학사일정·복무 등이 웹 브라우저와 실시간으로 서로 반영되게 하려면, 서버 관리자가\n" +
                   "미리 등록해 둔 기기 ID와 토큰을 입력하세요. 기기 ID/토큰이 비어 있으면 이 기능은\n" +
                   "비활성화되고(기존처럼 수동 새로고침으로 동작), 나머지 기능에는 영향이 없습니다.\n" +
                   "토큰은 저장하면 이 PC 계정으로 암호화되어, 이후에는 다시 표시되지 않습니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);
    }

    public void LoadFrom(AppSettings settings)
    {
        _deviceIdBox.Text = settings.DeviceId ?? "";
        _deviceTokenBox.Text = "";
        _hasStoredToken = !string.IsNullOrWhiteSpace(settings.DeviceTokenProtected);
        _tokenStatusLabel.Text = _hasStoredToken
            ? "저장된 토큰이 있습니다 (바꾸려면 새 값을 입력하세요)"
            : "저장된 토큰이 없습니다";
    }

    public void SaveTo(AppSettings settings)
    {
        var deviceId = _deviceIdBox.Text.Trim();
        settings.DeviceId = deviceId.Length > 0 ? deviceId : null;

        var newToken = _deviceTokenBox.Text;
        if (newToken.Length > 0)
        {
            settings.DeviceTokenProtected = DeviceTokenProtector.Protect(newToken);
        }
        else if (!_hasStoredToken)
        {
            settings.DeviceTokenProtected = null;
        }
        // 입력칸이 비어 있고 기존 저장된 토큰이 있으면 그대로 유지한다(재입력을 강제하지 않음).
    }

    public string? ValidateSettings() => null;
}
