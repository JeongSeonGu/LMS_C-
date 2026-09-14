using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>php/features/school.php?action=get 응답의 data 필드. 조회 전용(일반 사용자는 수정 불가).</summary>
public sealed class SchoolInfoResult
{
    [JsonPropertyName("info")] public SchoolInfoDetail Info { get; set; } = new();
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("students")] public List<SchoolStudentRow> Students { get; set; } = new();
    [JsonPropertyName("sum")] public SchoolStudentSum Sum { get; set; } = new();
    [JsonPropertyName("can_edit")] public bool CanEdit { get; set; }
}

public sealed class SchoolInfoDetail
{
    [JsonPropertyName("school_name")] public string? SchoolName { get; set; }
    [JsonPropertyName("school_type")] public string? SchoolType { get; set; }
    [JsonPropertyName("principal")] public string? Principal { get; set; }
    [JsonPropertyName("vice_principal")] public string? VicePrincipal { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("tel")] public string? Tel { get; set; }
    [JsonPropertyName("fax")] public string? Fax { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
    [JsonPropertyName("memo")] public string? Memo { get; set; }
    [JsonPropertyName("has_logo")] public bool HasLogo { get; set; }
    [JsonPropertyName("logo_url")] public string? LogoUrl { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
    [JsonPropertyName("updated_name")] public string? UpdatedName { get; set; }

    /// <summary>학교별 라이센스 인증키. 환경설정 &gt; 라이센스에 입력한 값과 대조합니다.</summary>
    [JsonPropertyName("auth_key")] public string? AuthKey { get; set; }
}

public sealed class SchoolStudentRow
{
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("class_count")] public int ClassCount { get; set; }
    [JsonPropertyName("boys")] public int Boys { get; set; }
    [JsonPropertyName("girls")] public int Girls { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("memo")] public string? Memo { get; set; }
}

public sealed class SchoolStudentSum
{
    [JsonPropertyName("class_count")] public int ClassCount { get; set; }
    [JsonPropertyName("boys")] public int Boys { get; set; }
    [JsonPropertyName("girls")] public int Girls { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}
