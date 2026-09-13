using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>php/public/catalog.php 응답의 data 필드 — 교무학사 길라잡이 문서함(대분류 &gt; 소분류 &gt; 문서).</summary>
public sealed class GuideCatalogResult
{
    [JsonPropertyName("majors")] public List<GuideMajor> Majors { get; set; } = new();
}

public sealed class GuideMajor
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("subs")] public List<GuideSub> Subs { get; set; } = new();
}

public sealed class GuideSub
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("docs")] public List<GuideDoc> Docs { get; set; } = new();
}

public sealed class GuideDoc
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
    [JsonPropertyName("current_version")] public GuideDocVersion? CurrentVersion { get; set; }
}

public sealed class GuideDocVersion
{
    [JsonPropertyName("version_id")] public int VersionId { get; set; }
    [JsonPropertyName("version_no")] public int VersionNo { get; set; }
    [JsonPropertyName("file")] public GuideDocFile? File { get; set; }
}

public sealed class GuideDocFile
{
    [JsonPropertyName("ext")] public string? Ext { get; set; }
    [JsonPropertyName("mime")] public string? Mime { get; set; }

    /// <summary>서버 호스트 기준 절대 경로(예: "/SchoolWork/WorkSupport/php/public/file.php?..."). 다운로드 시 BaseUri와 결합해서 사용합니다.</summary>
    [JsonPropertyName("download_url")] public string? DownloadUrl { get; set; }
    [JsonPropertyName("inline_url")] public string? InlineUrl { get; set; }
    [JsonPropertyName("preview_url")] public string? PreviewUrl { get; set; }
}
