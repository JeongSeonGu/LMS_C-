namespace LmsAgent.Services;

/// <summary>업데이트 서버가 내려주는 최신 버전 정보 매니페스트.</summary>
public sealed class UpdateManifest
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string? Sha256 { get; set; }
    public string? Notes { get; set; }
    public bool Mandatory { get; set; }
}
