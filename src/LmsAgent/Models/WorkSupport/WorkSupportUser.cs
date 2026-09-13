using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// php/lib/ws_auth.php의 ws_build_profile()이 만들어내는 통합 로그인 프로필.
/// (php/auth/ws_login.php, php/auth/ws_me.php 응답의 "user" 필드)
/// </summary>
public sealed class WorkSupportUser
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("teacher_id")]
    public int? TeacherId { get; set; }

    [JsonPropertyName("user_uuid")]
    public string? UserUuid { get; set; }

    [JsonPropertyName("login_id")]
    public string LoginId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("school_name")]
    public string? SchoolName { get; set; }

    /// <summary>admin | manager | teacher</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "teacher";

    [JsonPropertyName("is_admin")]
    public bool IsAdmin { get; set; }

    [JsonPropertyName("menu_access")]
    public List<string> MenuAccess { get; set; } = new();

    /// <summary>대표(단일) 담당업무 — school_users.dept_id 기준. 여러 업무는 TeacherSummary.DeptIds 참고.</summary>
    [JsonPropertyName("dept_id")]
    public int? DeptId { get; set; }

    [JsonPropertyName("dept_name")]
    public string? DeptName { get; set; }

    [JsonPropertyName("dept_color")]
    public string? DeptColor { get; set; }

    [JsonPropertyName("grade")]
    public int? Grade { get; set; }

    [JsonPropertyName("class_num")]
    public int? ClassNum { get; set; }

    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }
}

/// <summary>php/auth/ws_login.php 성공 응답의 data 필드.</summary>
public sealed class LoginResultData
{
    [JsonPropertyName("user")]
    public WorkSupportUser? User { get; set; }

    [JsonPropertyName("home")]
    public string? Home { get; set; }
}

/// <summary>php/auth/profile.php?action=get 응답의 data 필드 (개인정보 수정 화면용).</summary>
public sealed class ProfileInfo
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("login_id")]
    public string LoginId { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("teacher_id")]
    public int? TeacherId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("dept_name")]
    public string? DeptName { get; set; }

    [JsonPropertyName("has_teacher")]
    public bool HasTeacher { get; set; }
}
