using System.Globalization;
using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// SchoolCalendar/php/api/duty_status.php의 복무사항(연가/출장/조퇴) 기록.
/// Position은 "교장" · "교감" · "교무부장" · "행정실장" 중 하나.
/// </summary>
public sealed class DutyRecord
{
    /// <summary>서버(duty_status.php)가 허용하는 기록 대상 직위.</summary>
    public static readonly string[] Positions = { "교장", "교감", "교무부장", "행정실장" };

    /// <summary>서버(duty_status.php)가 허용하는 복무 구분.</summary>
    public static readonly string[] DutyTypes = { "연가", "출장", "조퇴" };

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    /// <summary>"yyyy-MM-dd" 형식.</summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("position")]
    public string Position { get; set; } = "";

    /// <summary>연가 | 출장 | 조퇴</summary>
    [JsonPropertyName("dutyType")]
    public string DutyType { get; set; } = "";

    [JsonPropertyName("timeStart")]
    public string? TimeStart { get; set; }

    [JsonPropertyName("timeEnd")]
    public string? TimeEnd { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>"오늘 대신 내일로 표시된다"는 보고가 있어, 실행 PC의 지역 설정(culture)에
    /// 영향을 받는 <c>DateOnly.TryParse(Date, ...)</c> 대신 "yyyy-MM-dd" 형식을 명시해서
    /// 파싱합니다 — 지역 설정에 따라 "yyyy-MM-dd"가 월·일 순서를 다르게 해석할 가능성을
    /// 원천적으로 없앱니다. 형식이 안 맞으면(서버가 다른 형식을 보냈다면) null이 되어
    /// 당일·내일 어느 쪽에도 매칭되지 않습니다(틀린 날짜로 잘못 매칭되는 것보다 안전).</summary>
    public DateOnly? DateValue =>
        DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>시간이 없으면(TimeStart/TimeEnd 모두 null) 종일 기록입니다.</summary>
    public bool IsAllDay => string.IsNullOrWhiteSpace(TimeStart) && string.IsNullOrWhiteSpace(TimeEnd);
}

/// <summary>현재 로그인 계정이 복무사항을 기록(등록/수정/삭제)할 수 있는지 여부.</summary>
public sealed class DutyManageInfo
{
    [JsonPropertyName("canManage")]
    public bool CanManage { get; set; }

    /// <summary>허용 사유 — "admin" · 직위명("교장" 등) · "개별 허용". 권한이 없으면 null.</summary>
    [JsonPropertyName("grant")]
    public string? Grant { get; set; }
}
