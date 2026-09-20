using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// SchoolCalendar/php/api/todos.php의 할일 레코드.
/// ⚠️ 이 저장소에는 todos.php의 정확한 응답 필드 스펙이 없어, events.php/duty_status.php와
/// 같은 규칙(action=list, PropertyNameCaseInsensitive)을 그대로 가정해 작성했습니다.
/// 실제 필드명이 다르면 이 모델만 서버 스펙에 맞춰 조정하면 됩니다.
/// </summary>
public sealed class TodoItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>마감/예정일. "yyyy-MM-dd" 형식. 없으면(날짜 미지정) null.</summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("isDone")]
    public bool IsDone { get; set; }

    /// <summary>이 할일을 해야 하는 교사 id 목록. 비어 있으면 담당업무 전체 등으로 간주하지 않고,
    /// "내가 해야 할 일" 필터에서는 여기 내 teacherId가 포함된 것만 사용합니다.</summary>
    [JsonPropertyName("assigneeTeacherIds")]
    public List<int> AssigneeTeacherIds { get; set; } = new();

    [JsonPropertyName("createdBy")]
    public int? CreatedBy { get; set; }
}
