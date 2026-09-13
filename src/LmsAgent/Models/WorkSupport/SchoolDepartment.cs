using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>SchoolCalendar/php/api/departments.php의 담당업무(부서) 항목.</summary>
public sealed class SchoolDepartment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("is_active")]
    public int IsActive { get; set; } = 1;
}
