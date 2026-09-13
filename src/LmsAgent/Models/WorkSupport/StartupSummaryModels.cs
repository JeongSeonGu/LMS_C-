using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>php/features/training.php?action=my 응답의 요약용 항목.</summary>
public sealed class TrainingMyItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("is_done")] public bool IsDone { get; set; }
    [JsonPropertyName("is_overdue")] public bool IsOverdue { get; set; }
}

public sealed class TrainingMyResult
{
    [JsonPropertyName("items")] public List<TrainingMyItem> Items { get; set; } = new();
    [JsonPropertyName("pending")] public int Pending { get; set; }
}

/// <summary>php/features/meetings.php?action=todo 응답의 요약용 항목(진행 중인 협의 안건).</summary>
public sealed class MeetingsTodoItem
{
    [JsonPropertyName("agenda")] public string? Agenda { get; set; }
    [JsonPropertyName("decision")] public string? Decision { get; set; }
    [JsonPropertyName("owner")] public string? Owner { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}

public sealed class MeetingsTodoResult
{
    [JsonPropertyName("items")] public List<MeetingsTodoItem> Items { get; set; } = new();
}
