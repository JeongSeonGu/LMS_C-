using System;

namespace LmsAgent.Configuration;

public enum ScheduleOutputUnit
{
    Week,
    Month,
}

/// <summary>
/// 사용자 PC마다 로컬로 저장되는 프로그램 환경 설정 값입니다.
/// 환경설정 창(OptionsForm)의 대분류에 맞춰 영역을 나눠 두었습니다.
/// </summary>
public sealed class AppSettings
{
    // ── 네트워크 ──────────────────────────────────────────────
    /// <summary>
    /// 서버가 클라이언트에 작업을 요청/응답하는 실시간 연동용 웹소켓 주소.
    /// 실제 학사 데이터 API(WorkSupport 웹 서비스)는 별도 호스트(<see cref="ApiBaseUrlOverride"/>)를 통합니다.
    /// </summary>
    public string ServerUrl { get; set; } = "wss://node2.future-class.kr/ws";

    /// <summary>업데이트 매니페스트(버전 정보)를 확인할 주소.</summary>
    public string UpdateManifestUrl { get; set; } = "https://node2.future-class.kr/update/manifest.json";

    /// <summary>
    /// WorkSupport(교무업무) 웹 서비스의 실제 접속 주소. 웹소켓 서버는 실시간 연동 전용이고
    /// 학사 데이터 API는 이 주소(웹 서버)를 통해 이루어지므로 반드시 따로 지정합니다.
    /// 비워두면(값이 없으면) 웹소켓 서버 주소에서 스킴만 http(s)로 바꿔 유도하지만,
    /// 두 서버가 서로 다른 호스트이면 이 값을 직접 입력해야 합니다.
    /// </summary>
    public string? ApiBaseUrlOverride { get; set; } = "https://future-class.kr/SchoolWork/WorkSupport";

    /// <summary>트레이 메뉴 "교무업무 페이지"를 클릭했을 때 기본 브라우저로 열 주소.</summary>
    public string WorkSupportPageUrl { get; set; } = "https://future-class.kr/SchoolWork/WorkSupport/index.html";

    /// <summary>쉬는 시간에 자동으로 띄우는 전자칠판용 웹 서비스 주소.</summary>
    public string? SmartBoardPageUrl { get; set; }

    // ── 일반 ──────────────────────────────────────────────────
    /// <summary>화면 상단 표시/인쇄물 등에 사용할 학교명. 로그인 시 서버 값으로 최초 1회 채워집니다.</summary>
    public string SchoolName { get; set; } = "";

    /// <summary>체크 시 로그인 직후 오늘의 일정·요청사항·공지·해야 할 일·법정연수·협의사항을
    /// 모아 보여주는 요약 팝업을 띄웁니다. 기본값은 미체크입니다.</summary>
    public bool ShowStartupNoticeModal { get; set; }

    /// <summary>공지사항 팝업에서 "오늘은 보지 않기"를 체크하면 이 날짜("yyyy-MM-dd")가 기록되고,
    /// 같은 날에는 로그인해도 팝업을 다시 띄우지 않습니다. 사용자가 직접 편집하는 값이 아닙니다.</summary>
    public string? StartupNoticeSuppressedDate { get; set; }

    // ── 프로그램 동작 ─────────────────────────────────────────
    public bool AutoStartWithWindows { get; set; } = true;
    public string? SavedLoginId { get; set; }
    public string? DeviceId { get; set; }

    // ── 학사일정 ──────────────────────────────────────────────
    /// <summary>학사일정을 출력할 모니터의 Screen.AllScreens 인덱스.</summary>
    public int ScheduleMonitorIndex { get; set; }
    public ScheduleOutputUnit ScheduleOutputUnit { get; set; } = ScheduleOutputUnit.Week;

    /// <summary>체크 시 배경화면처럼 학사달력을 상시 표시합니다.</summary>
    public bool ScheduleWallpaperEnabled { get; set; }

    /// <summary>학사달력 배경화면 오버레이의 불투명도(0~100%). 100이면 완전 불투명.</summary>
    public int ScheduleOverlayOpacityPercent { get; set; } = 85;

    // ── 복무 ──────────────────────────────────────────────────
    /// <summary>복무 알림 배너를 띄울 모니터의 Screen.AllScreens 인덱스.</summary>
    public int DutyMonitorIndex { get; set; }
    public bool DutyNotifyVicePrincipal { get; set; } // 교감
    public bool DutyNotifyPrincipal { get; set; }      // 교장

    /// <summary>복무 알림 배너의 불투명도(0~100%). 100이면 완전 불투명.</summary>
    public int DutyBannerOpacityPercent { get; set; } = 100;

    // ── 출력 ──────────────────────────────────────────────────
    public string? PrinterName { get; set; }

    /// <summary>체크 시 평일 08:30~10:00 사이 첫 실행에서 내 학사일정을 자동 인쇄합니다.</summary>
    public bool PrintDailyScheduleEnabled { get; set; }

    /// <summary>자동 인쇄 중복 방지용 마지막 인쇄일("yyyy-MM-dd"). 사용자가 직접 편집하는 값이 아닙니다.</summary>
    public string? LastAutoPrintDate { get; set; }
}
