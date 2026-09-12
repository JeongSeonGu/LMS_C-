using System;
using System.IO;
using Microsoft.Win32;

namespace LmsAgent.Services;

/// <summary>
/// HKCU\...\Run 레지스트리 키를 이용해 Windows 로그인 시 자동 실행 여부를 설정합니다.
/// 관리자 권한이 필요 없는 사용자 단위 설정입니다.
/// </summary>
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LmsAgent";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "LmsAgent.exe");
            key.SetValue(ValueName, $"\"{exePath}\" --minimized");
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
