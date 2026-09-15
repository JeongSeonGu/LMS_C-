using System;
using System.Reflection;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "네트워크" — 실시간 연동(Socket.IO) 서버, 업데이트 서버 주소를 설정하고 현재 버전을 표시합니다.
/// 버전은 빌드에 고정된 값이라 수정할 수 없습니다.
/// </summary>
public sealed class NetworkOptionsPage : UserControl, IOptionsPage
{
    private const AnchorStyles StretchAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

    private readonly TextBox _serverUrlBox = new() { Left = 150, Top = 20, Width = 260, Anchor = StretchAnchor };
    private readonly TextBox _updateUrlBox = new() { Left = 150, Top = 55, Width = 260, Anchor = StretchAnchor };

    private readonly TextBox _versionBox = new()
    {
        Left = 150, Top = 90, Width = 260, ReadOnly = true, Anchor = StretchAnchor,
    };

    private readonly TextBox _apiBaseUrlBox = new() { Left = 150, Top = 145, Width = 260, Anchor = StretchAnchor };

    private readonly TextBox _workSupportPageUrlBox = new() { Left = 150, Top = 240, Width = 260, Anchor = StretchAnchor };
    private readonly TextBox _smartBoardPageUrlBox = new() { Left = 150, Top = 275, Width = 260, Anchor = StretchAnchor };

    public string CategoryName => "네트워크";

    public NetworkOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;
        _versionBox.BackColor = UiTheme.Background;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "네트워크" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 23, Width = 120, Text = "실시간 연동 서버" });
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

        var hint = new Label
        {
            Left = 20, Top = 172, Width = 380, Height = 55,
            Text = "로그인 시 \"서버 응답 형식이 올바르지 않습니다\" 오류가 나면 학사업무 웹 서비스\n" +
                   "주소가 실시간 연동 서버와 다른 것입니다. 비워두면 실시간 연동 서버 주소에서 자동으로\n" +
                   "유도하고, 값을 넣으면 그 주소를 그대로 사용합니다. 예) https://school.example.com/SchoolWork/WorkSupport",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);

        Controls.Add(new Label { Left = 20, Top = 243, Width = 300, Text = "교무업무 페이지" });
        Controls.Add(_workSupportPageUrlBox);
        Controls.Add(new Label { Left = 20, Top = 278, Width = 300, Text = "전자칠판 페이지" });
        Controls.Add(_smartBoardPageUrlBox);

        var pageHint = new Label
        {
            Left = 20, Top = 308, Width = 380, Height = 40,
            Text = "교무업무 페이지는 트레이 메뉴에서 바로 열 수 있습니다. 전자칠판 페이지는\n" +
                   "쉬는 시간에 자동으로 띄우는 화면 주소입니다(일반 설정에서 활성화).",
        };
        UiTheme.StyleHintLabel(pageHint);
        Controls.Add(pageHint);

        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        _versionBox.Text = version.ToString();
    }

    public void LoadFrom(AppSettings settings)
    {
        _serverUrlBox.Text = settings.ServerUrl;
        _updateUrlBox.Text = settings.UpdateManifestUrl;
        _apiBaseUrlBox.Text = settings.ApiBaseUrlOverride ?? "";
        _workSupportPageUrlBox.Text = settings.WorkSupportPageUrl;
        _smartBoardPageUrlBox.Text = settings.SmartBoardPageUrl ?? "";
    }

    public void SaveTo(AppSettings settings)
    {
        settings.ServerUrl = _serverUrlBox.Text.Trim();
        settings.UpdateManifestUrl = _updateUrlBox.Text.Trim();
        var apiBaseUrl = _apiBaseUrlBox.Text.Trim();
        settings.ApiBaseUrlOverride = apiBaseUrl.Length > 0 ? apiBaseUrl : null;
        settings.WorkSupportPageUrl = _workSupportPageUrlBox.Text.Trim();
        var smartBoardUrl = _smartBoardPageUrlBox.Text.Trim();
        settings.SmartBoardPageUrl = smartBoardUrl.Length > 0 ? smartBoardUrl : null;
    }

    public string? ValidateSettings()
    {
        if (!Uri.TryCreate(_serverUrlBox.Text.Trim(), UriKind.Absolute, out _))
        {
            return "실시간 연동 서버 주소가 올바르지 않습니다.";
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

        if (!Uri.TryCreate(_workSupportPageUrlBox.Text.Trim(), UriKind.Absolute, out _))
        {
            return "교무업무 페이지 주소가 올바르지 않습니다.";
        }

        var smartBoardUrl = _smartBoardPageUrlBox.Text.Trim();
        if (smartBoardUrl.Length > 0 && !Uri.TryCreate(smartBoardUrl, UriKind.Absolute, out _))
        {
            return "전자칠판 페이지 주소가 올바르지 않습니다.";
        }

        return null;
    }
}
