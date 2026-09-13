using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>SchoolCalendar/php/api/teachers.php의 담당업무 항목 (요약).</summary>
public sealed class TeacherDeptRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

/// <summary>
/// SchoolCalendar/php/api/teachers.php의 교사 항목.
/// 담당업무는 N:M 관계라 <see cref="DeptIds"/>에 여러 개가 담길 수 있다.
/// </summary>
public sealed class TeacherSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("roleLevel")]
    public string? RoleLevel { get; set; }

    [JsonPropertyName("notifyAllSchedule")]
    public int NotifyAllSchedule { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("is_active")]
    public int IsActive { get; set; } = 1;

    [JsonPropertyName("deptIds")]
    public List<int> DeptIds { get; set; } = new();

    [JsonPropertyName("depts")]
    public List<TeacherDeptRef> Depts { get; set; } = new();
}
