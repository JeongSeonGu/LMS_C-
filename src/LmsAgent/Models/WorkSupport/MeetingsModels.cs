using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// php/features/meetings.php?action=list 응답의 data 필드.
/// 협의사항은 Google 시트를 그대로 읽어오는 조회 전용 화면이라 열 구성이 학교마다 다를 수 있어,
/// 각 행을 (열 이름 → 값) 사전으로 그대로 받습니다.
/// </summary>
public sealed class MeetingsListResult
{
    [JsonPropertyName("ready")] public bool Ready { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "recent";
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("headers")] public List<string> Headers { get; set; } = new();
    [JsonPropertyName("rows")] public List<Dictionary<string, string>> Rows { get; set; } = new();
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("can_manage")] public bool CanManage { get; set; }
    [JsonPropertyName("config")] public MeetingsConfig Config { get; set; } = new();
}

public sealed class MeetingsConfig
{
    [JsonPropertyName("appscript_url")] public string? AppscriptUrl { get; set; }
    [JsonPropertyName("sheet_view_url")] public string? SheetViewUrl { get; set; }
    [JsonPropertyName("recent_label")] public string? RecentLabel { get; set; }
    [JsonPropertyName("archive_label")] public string? ArchiveLabel { get; set; }
    [JsonPropertyName("guide")] public string? Guide { get; set; }
}
