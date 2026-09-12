using System;

namespace LmsAgent.Configuration;

/// <summary>
/// 사용자 PC마다 로컬로 저장되는 프로그램 환경 설정 값입니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>LMS 연동용 웹소켓 서버 주소.</summary>
    public string ServerUrl { get; set; } = "wss://node2.future-class.kr/ws";

    /// <summary>업데이트 매니페스트(버전 정보)를 확인할 주소.</summary>
    public string UpdateManifestUrl { get; set; } = "https://node2.future-class.kr/update/manifest.json";

    /// <summary>Windows 로그인 시 자동 실행 여부.</summary>
    public bool AutoStartWithWindows { get; set; } = true;

    /// <summary>"아이디 저장"을 선택했을 때 유지되는 마지막 로그인 아이디.</summary>
    public string? SavedLoginId { get; set; }

    /// <summary>서버가 단말을 구분할 수 있도록 최초 실행 시 발급되는 고유 식별자.</summary>
    public string? DeviceId { get; set; }
}
