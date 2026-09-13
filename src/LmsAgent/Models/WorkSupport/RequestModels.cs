using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>php/features/requests.php의 요청사항 1건(목록 행). 본문(body)은 목록에는 없고 상세에만 있습니다.</summary>
public sealed class RequestItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body_excerpt")] public string? BodyExcerpt { get; set; }
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("priority_label")] public string? PriorityLabel { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "open";
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("created_by")] public int? CreatedBy { get; set; }
    [JsonPropertyName("created_name")] public string? CreatedName { get; set; }
    [JsonPropertyName("created_dept")] public string? CreatedDept { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("is_mine")] public bool IsMine { get; set; }
    [JsonPropertyName("is_overdue")] public bool IsOverdue { get; set; }
    [JsonPropertyName("comment_count")] public int CommentCount { get; set; }
}

/// <summary>php/features/requests.php?action=detail 응답의 data 필드.</summary>
public sealed class RequestDetailResult
{
    [JsonPropertyName("request")] public RequestDetail Request { get; set; } = new();
    [JsonPropertyName("can_edit")] public bool CanEdit { get; set; }
    [JsonPropertyName("is_mine")] public bool IsMine { get; set; }
}

public sealed class RequestDetail
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "open";
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("created_by")] public int? CreatedBy { get; set; }
    [JsonPropertyName("created_name")] public string? CreatedName { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
}

/// <summary>php/features/requests.php?action=meta 응답의 data 필드 — 분류/상태/우선순위 정의.</summary>
public sealed class RequestMeta
{
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
    [JsonPropertyName("status")] public Dictionary<string, string> Status { get; set; } = new();
    [JsonPropertyName("priority")] public Dictionary<string, string> Priority { get; set; } = new();
}

public sealed class RequestListResult
{
    [JsonPropertyName("items")] public List<RequestItem> Items { get; set; } = new();
}
