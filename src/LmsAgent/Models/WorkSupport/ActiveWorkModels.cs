using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// php/features/requests.php?action=active, notifications.php?action=list&amp;only_unread=1,
/// training.php?action=active 응답 모델. 학사달력외_연동가이드.md에 따라 서버가 이미
/// "진행 중"인 것만 걸러 주므로(완료·종료·삭제·읽음 등은 빠짐), 받은 목록을 그대로 쓰면 된다.
/// </summary>
public sealed class ActiveRequestsResult
{
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("me")] public int Me { get; set; }
    [JsonPropertyName("items")] public List<ActiveRequestItem> Items { get; set; } = new();
}

public sealed class ActiveRequestItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("priority_label")] public string? PriorityLabel { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "open";
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("is_overdue")] public bool IsOverdue { get; set; }
    [JsonPropertyName("created_by")] public int? CreatedBy { get; set; }
    [JsonPropertyName("created_name")] public string? CreatedName { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("target_type")] public string? TargetType { get; set; }

    /// <summary>"to_me" 나에게 온 글 · "mine" 내가 올린 글.</summary>
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("my_read")] public bool MyRead { get; set; }
    [JsonPropertyName("my_done")] public bool MyDone { get; set; }
    [JsonPropertyName("comment_count")] public int CommentCount { get; set; }
    [JsonPropertyName("target_count")] public int TargetCount { get; set; }
    [JsonPropertyName("done_count")] public int DoneCount { get; set; }

    /// <summary>서버 기준 상대 경로. https://future-class.kr 를 앞에 붙여서 열면 된다.</summary>
    [JsonPropertyName("link")] public string? Link { get; set; }
}

public sealed class UnreadNoticesResult
{
    [JsonPropertyName("unread")] public int Unread { get; set; }
    [JsonPropertyName("items")] public List<NoticeItem> Items { get; set; } = new();
}

public sealed class NoticeItem
{
    [JsonPropertyName("id")] public int Id { get; set; }

    /// <summary>"request" · "comment" · "training" · "meeting" · "system".</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("link")] public string? Link { get; set; }
    [JsonPropertyName("ref_type")] public string? RefType { get; set; }
    [JsonPropertyName("ref_id")] public int? RefId { get; set; }
    [JsonPropertyName("is_read")] public int IsRead { get; set; }
    [JsonPropertyName("created_name")] public string? CreatedName { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
}

public sealed class ActiveTrainingResult
{
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("me")] public int Me { get; set; }

    /// <summary>내가 이수증을 내야 하는 과정(미제출 또는 보완요청).</summary>
    [JsonPropertyName("todo")] public List<TrainingTodoItem> Todo { get; set; } = new();

    /// <summary>내가 등록한 과정 중 확인을 기다리는 제출이 있는 것.</summary>
    [JsonPropertyName("review")] public List<TrainingReviewItem> Review { get; set; } = new();
}

public sealed class TrainingTodoItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("required_hours")] public int RequiredHours { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("is_required")] public bool IsRequired { get; set; }
    [JsonPropertyName("is_overdue")] public bool IsOverdue { get; set; }

    /// <summary>null(미제출) · "rejected"(보완요청).</summary>
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("review_note")] public string? ReviewNote { get; set; }
    [JsonPropertyName("created_name")] public string? CreatedName { get; set; }
    [JsonPropertyName("link")] public string? Link { get; set; }
}

public sealed class TrainingReviewItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("pending_count")] public int PendingCount { get; set; }
    [JsonPropertyName("last_submitted_at")] public string? LastSubmittedAt { get; set; }
    [JsonPropertyName("link")] public string? Link { get; set; }
}
