using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

public sealed class EventNotifyTargets
{
    [JsonPropertyName("groupIds")]
    public List<int> GroupIds { get; set; } = new();

    [JsonPropertyName("teacherIds")]
    public List<int> TeacherIds { get; set; } = new();
}

/// <summary>
/// SchoolCalendar/php/api/events.php의 school_events 레코드 (학사 일정).
/// <see cref="DeptId"/>가 곧 "담당업무"이며, 이 프로그램에서는
/// 등록 시 본인 담당업무로만, 수정/삭제는 DeptId가 본인 담당업무와 일치할 때만 허용한다.
/// </summary>
public sealed class SchoolEvent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>"yyyy-MM-ddTHH:mm" 형식.</summary>
    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    [JsonPropertyName("end")]
    public string End { get; set; } = "";

    [JsonPropertyName("allDay")]
    public bool AllDay { get; set; }

    /// <summary>담당업무 id. null이면 "관련 업무 없음".</summary>
    [JsonPropertyName("deptId")]
    public int? DeptId { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("notifyBefore")]
    public int NotifyBefore { get; set; }

    [JsonPropertyName("createdBy")]
    public int? CreatedBy { get; set; }

    [JsonPropertyName("gcalEventId")]
    public string? GcalEventId { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("notifyTargets")]
    public EventNotifyTargets NotifyTargets { get; set; } = new();

    public DateTime StartDateTime => ParseServerDateTime(Start);

    public DateTime EndDateTime => ParseServerDateTime(End);

    private static DateTime ParseServerDateTime(string value)
    {
        return DateTime.TryParse(value, out var dt) ? dt : DateTime.MinValue;
    }
}
