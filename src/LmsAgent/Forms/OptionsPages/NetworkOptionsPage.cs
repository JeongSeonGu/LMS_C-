using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "네트워크" — 웹소켓 서버, 업데이트 서버 주소를 설정하고 현재 버전을 표시합니다.
/// 버전은 빌드에 고정된 값이라 수정할 수 없습니다.
/// </summary>
public sealed class NetworkOptionsPage : UserControl, IOptionsPage
{
    private readonly TextBox _serverUrlBox = new() { Left = 150, Top = 20, Width = 260 };
    private readonly TextBox _updateUrlBox = new() { Left = 150, Top = 55, Width = 260 };

    private readonly TextBox _versionBox = new()
    {
        Left = 150, Top = 90, Width = 260, ReadOnly = true, BackColor = SystemColors.Control,
    };

    private readonly TextBox _apiBaseUrlBox = new() { Left = 150, Top = 145, Width = 260 };

    public string CategoryName => "네트워크";

    public NetworkOptionsPage()
    {
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Left = 16, Top = 0, Width = 400, Height = 20,
            Text = "네트워크",
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label { Left = 20, Top = 23, Width = 120, Text = "웹소켓 서버" });
        Controls.Add(new Label { Left = 20, Top = 58, Width = 120, Text = "업데이트 서버" });
        Controls.Add(new Label { Left = 20, Top = 93, Width = 120, Text = "프로그램 버전" });
        Controls.Add(_serverUrlBox);
        Controls.Add(_updateUrlBox);
        Controls.Add(_versionBox);

        Controls.Add(new Label
        {
            Left = 20, Top = 128, Width = 300, Text = "WorkSupport 서버 주소 (선택)",
        });
        Controls.Add(_apiBaseUrlBox);
        Controls.Add(new Label
        {
            Left = 20, Top = 172, Width = 380, Height = 55,
            ForeColor = Color.Gray,
            Text = "로그인 시 \"서버 응답 형식이 올바르지 않습니다\" 오류가 나면 학사업무 웹 서비스\n" +
                   "주소가 웹소켓 서버와 다른 것입니다. 비워두면 웹소켓 서버 주소에서 자동으로\n" +
                   "유도하고, 값을 넣으면 그 주소를 그대로 사용합니다. 예) https://school.example.com/SchoolWork/WorkSupport",
        });

        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        _versionBox.Text = version.ToString();
    }

    public void LoadFrom(AppSettings settings)
    {
        _serverUrlBox.Text = settings.ServerUrl;
        _updateUrlBox.Text = settings.UpdateManifestUrl;
        _apiBaseUrlBox.Text = settings.ApiBaseUrlOverride ?? "";
    }

    public void SaveTo(AppSettings settings)
    {
        settings.ServerUrl = _serverUrlBox.Text.Trim();
        settings.UpdateManifestUrl = _updateUrlBox.Text.Trim();
        var apiBaseUrl = _apiBaseUrlBox.Text.Trim();
        settings.ApiBaseUrlOverride = apiBaseUrl.Length > 0 ? apiBaseUrl : null;
    }

    public string? ValidateSettings()
    {
        if (!Uri.TryCreate(_serverUrlBox.Text.Trim(), UriKind.Absolute, out _))
        {
            return "웹소켓 서버 주소가 올바르지 않습니다.";
        }

        if (!Uri.TryCreate(_updateUrlBox.Text.Trim(), UriKind.Absolute, out _))
        {
            return "업데이트 서버 주소가 올바르지 않습니다.";
        }

        var apiBaseUrl = _apiBaseUrlBox.Text.Trim();
        if (apiBaseUrl.Length > 0 && !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out _))
        {
            return "WorkSupport 서버 주소가 올바르지 않습니다.";
        }

        return null;
    }
}
