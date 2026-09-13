using System;
using System.Drawing;
using System.Reflection;

namespace LmsAgent.Services;

/// <summary>
/// 프로그램 기본 아이콘 / 트레이 아이콘으로 쓰이는 임베디드 리소스(Resources/AppIcon.ico)를 로드합니다.
/// (WorkSupport 서비스 코드의 viewer/externalLectureViewer.ico를 그대로 사용합니다.)
/// </summary>
public static class AppIconProvider
{
    private static readonly Lazy<Icon> LazyIcon = new(Load);

    public static Icon Icon => LazyIcon.Value;

    private static Icon Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("AppIcon.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
