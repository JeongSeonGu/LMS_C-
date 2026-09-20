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

    /// <summary>서버가 허용하는 우선순위 값(다른 값을 보내면 서버가 medium으로 바꾼다).</summary>
    public static readonly string[] Priorities = { "high", "medium", "low" };

    public static string PriorityLabel(string? priority) => priority switch
    {
        "high" => "높음",
        "low" => "낮음",
        _ => "보통",
    };
}

/// <summary>현재 로그인 계정이 할일을 기록(등록/수정/삭제)할 수 있는지 여부.
/// 복무와 같은 권한(관리자 · 교장/교감/교무부장/행정실장 · 개별 허용)을 공유한다.</summary>
public sealed class TodoManageInfo
{
    [JsonPropertyName("canManage")]
    public bool CanManage { get; set; }

    /// <summary>허용 사유 — "admin" · 직위명 · "개별 허용". 권한이 없으면 null.</summary>
    [JsonPropertyName("grant")]
    public string? Grant { get; set; }
}
