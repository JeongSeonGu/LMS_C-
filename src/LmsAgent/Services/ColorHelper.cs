using System;
using System.Drawing;

namespace LmsAgent.Services;

/// <summary>담당업무 색상 등 서버가 내려주는 "#rrggbb" 문자열을 안전하게 Color로 바꿉니다.</summary>
public static class ColorHelper
{
    public static Color ParseHexOrDefault(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
