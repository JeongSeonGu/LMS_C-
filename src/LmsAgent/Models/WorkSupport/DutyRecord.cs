using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// SchoolCalendar/php/api/duty_status.php의 복무사항(연가/출장/조퇴) 기록.
/// Position은 "교장" · "교감" · "교무부장" · "행정실장" 중 하나.
/// </summary>
public sealed class DutyRecord
{
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
}
