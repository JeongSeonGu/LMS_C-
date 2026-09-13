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
    /// <summary>LMS 연동용 웹소켓 서버 주소. WorkSupport HTTP API도 같은 호스트를 사용합니다.</summary>
    public string ServerUrl { get; set; } = "wss://node2.future-class.kr/ws";

    /// <summary>업데이트 매니페스트(버전 정보)를 확인할 주소.</summary>
    public string UpdateManifestUrl { get; set; } = "https://node2.future-class.kr/update/manifest.json";

    /// <summary>
    /// WorkSupport(교무업무) 웹 서비스의 실제 접속 주소(예: "https://school.example.com/SchoolWork/WorkSupport").
    /// 비워두면 위 웹소켓 서버 주소에서 스킴만 http(s)로 바꿔 자동으로 유도합니다.
    /// 로그인 시 "서버 응답 형식이 올바르지 않습니다" 오류가 나면 웹소켓 서버와 호스트/경로가
    /// 다르다는 뜻이므로 여기에 실제 주소를 직접 입력하세요.
    /// </summary>
    public string? ApiBaseUrlOverride { get; set; }

    // ── 일반 ──────────────────────────────────────────────────
    /// <summary>화면 상단 표시/인쇄물 등에 사용할 학교명. 로그인 시 서버 값으로 최초 1회 채워집니다.</summary>
    public string SchoolName { get; set; } = "";

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

    // ── 복무 ──────────────────────────────────────────────────
    /// <summary>복무 알림 배너를 띄울 모니터의 Screen.AllScreens 인덱스.</summary>
    public int DutyMonitorIndex { get; set; }
    public bool DutyNotifyVicePrincipal { get; set; } // 교감
    public bool DutyNotifyPrincipal { get; set; }      // 교장

    // ── 출력 ──────────────────────────────────────────────────
    public string? PrinterName { get; set; }

    /// <summary>체크 시 평일 08:30~10:00 사이 첫 실행에서 내 학사일정을 자동 인쇄합니다.</summary>
    public bool PrintDailyScheduleEnabled { get; set; }

    /// <summary>자동 인쇄 중복 방지용 마지막 인쇄일("yyyy-MM-dd"). 사용자가 직접 편집하는 값이 아닙니다.</summary>
    public string? LastAutoPrintDate { get; set; }
}
