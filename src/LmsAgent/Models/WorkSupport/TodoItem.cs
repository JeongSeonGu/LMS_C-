using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// SchoolCalendar/php/api/todos.php(school_todos 테이블)의 할일 레코드.
/// 서버 DB 컬럼은 status('pending'|'done')이지만, listTodos()가 응답에서 done(bool)으로
/// 변환해 내려준다. 할일은 school_events처럼 담당업무(deptId) 단위로 배정되며, 개인별
/// 담당자 목록 같은 것은 없다 — "내가 해야 할 일"은 deptId가 내 담당업무 중 하나인 것으로 판단한다.
/// </summary>
public sealed class TodoItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>마감/예정일. "yyyy-MM-dd" 형식. 없으면 null.</summary>
    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    /// <summary>담당업무 id. null이면 특정 담당업무 없음.</summary>
    [JsonPropertyName("deptId")]
    public int? DeptId { get; set; }

    /// <summary>"high" / "medium" / "low".</summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("gcalTaskId")]
    public string? GcalTaskId { get; set; }

    [JsonPropertyName("gcalTaskListId")]
    public string? GcalTaskListId { get; set; }

    [JsonPropertyName("createdBy")]
    public int? CreatedBy { get; set; }
}
