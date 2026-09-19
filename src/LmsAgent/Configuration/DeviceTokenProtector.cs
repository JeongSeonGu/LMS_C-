using System;
using System.Security.Cryptography;
using System.Text;

namespace LmsAgent.Configuration;

/// <summary>
/// 실시간 연동용 기기 토큰을 DPAPI(현재 Windows 사용자 범위)로 암호화해서 설정 파일에 저장하기
/// 위한 헬퍼입니다. 평문은 절대 디스크에 남기지 않습니다.
/// </summary>
public static class DeviceTokenProtector
{
    public static string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// 복호화에 실패하면(다른 PC/계정으로 settings.json이 복사된 경우 등) null을 반환합니다 —
    /// 이 경우 사용자가 환경설정에서 토큰을 다시 입력해야 합니다.
    /// </summary>
    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
