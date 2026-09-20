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

    public DateOnly? DateValue => DateOnly.TryParse(Date, out var d) ? d : null;

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
