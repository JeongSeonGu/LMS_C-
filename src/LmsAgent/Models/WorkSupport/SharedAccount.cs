using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// php/features/accounts.php의 학교 공통 계정 정보. 등록/수정/삭제는 관리자만 가능하며,
/// 일반 사용자는 목록 조회와 비밀번호 열람("보기")만 할 수 있습니다.
/// </summary>
public sealed class SharedAccount
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("site_url")] public string? SiteUrl { get; set; }
    [JsonPropertyName("account_id")] public string? AccountId { get; set; }
    [JsonPropertyName("owner_dept")] public string? OwnerDept { get; set; }
    [JsonPropertyName("owner_name")] public string? OwnerName { get; set; }
    [JsonPropertyName("memo")] public string? Memo { get; set; }
    [JsonPropertyName("has_pw")] public bool HasPassword { get; set; }
    [JsonPropertyName("updated_name")] public string? UpdatedName { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
}

/// <summary>php/features/accounts.php?action=reveal 응답 (비밀번호 평문 포함).</summary>
public sealed class SharedAccountSecret
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("account_id")] public string? AccountId { get; set; }
    [JsonPropertyName("account_pw")] public string? AccountPw { get; set; }
}

public sealed class SharedAccountListResult
{
    [JsonPropertyName("items")] public List<SharedAccount> Items { get; set; } = new();
    [JsonPropertyName("can_edit")] public bool CanEdit { get; set; }
}
