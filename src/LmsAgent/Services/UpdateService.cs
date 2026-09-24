using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LmsAgent.Configuration;

namespace LmsAgent.Services;

/// <summary>
/// <see cref="UpdateService.CheckForUpdateAsync"/>의 결과. "업데이트 확인..." 메뉴에서
/// "이미 최신 버전"과 "서버 확인 자체에 실패함"을 구분해 보여주기 위한 용도입니다
/// (예전에는 둘 다 같은 메시지로 뭉뚱그려져 있어 원인 파악이 어려웠습니다).
/// </summary>
public enum UpdateCheckResult
{
    /// <summary>새 버전을 내려받아 적용했다. 호출자는 현재 프로세스를 즉시 종료해야 한다.</summary>
    Applied,

    /// <summary>manifest의 버전이 현재 버전보다 높지 않다(정상적으로 최신 상태).</summary>
    UpToDate,

    /// <summary>manifest.json을 가져오지 못했다(주소 오설정, 네트워크 오류, 형식 오류 등).</summary>
    ManifestUnavailable,

    /// <summary>새 버전 파일(zip) 다운로드에 실패했다.</summary>
    DownloadFailed,

    /// <summary>다운로드는 됐지만 sha256 체크섬이 manifest.json과 다르다(파일 손상/오설정).</summary>
    ChecksumMismatch,
}

/// <summary>
/// 업데이트 서버의 버전 매니페스트를 확인하고, 새 버전이 있으면 내려받아
/// 적용용 스크립트를 실행한 뒤 프로그램을 재시작합니다.
/// 프로그램은 실행될 때마다 이 검사를 수행합니다.
/// </summary>
public sealed class UpdateService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly AppSettings _settings;

    public UpdateService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 새 버전이 있으면 내려받아 적용하고 true를 반환합니다.
    /// 이 경우 호출자는 현재 프로세스를 즉시 종료해야 합니다.
    /// 업데이트가 없거나 확인에 실패하면 false를 반환하며, 이 실패가 프로그램 실행을 막지는 않습니다.
    /// 실패 원인은 %AppData%\LmsAgent\realtime.log에 남습니다(자세한 원인이 필요하면
    /// <see cref="CheckForUpdateAsync"/>를 직접 호출하세요 — "업데이트 확인..." 메뉴가 그렇게 합니다).
    /// </summary>
    public bool CheckAndLaunchUpdaterIfAvailable()
    {
        try
        {
            return CheckForUpdateAsync().GetAwaiter().GetResult() == UpdateCheckResult.Applied;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"업데이트 확인 실패: {ex.Message}");
            RealtimeLog.Write($"[업데이트] 확인 중 예외 발생: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 새 버전이 있으면 내려받아 적용하고(<see cref="UpdateCheckResult.Applied"/>) 성공 시
    /// 호출자는 현재 프로세스를 즉시 종료해야 합니다. 그 외의 경우 왜 업데이트가 이루어지지
    /// 않았는지를 나타내는 값을 반환하며, 각 단계의 실패 원인은 실시간 연동 로그에 남깁니다.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        var manifest = await FetchManifestAsync().ConfigureAwait(false);
        if (manifest is null)
        {
            return UpdateCheckResult.ManifestUnavailable;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            RealtimeLog.Write($"[업데이트] manifest.json의 version 값을 해석할 수 없습니다: \"{manifest.Version}\"");
            return UpdateCheckResult.ManifestUnavailable;
        }

        if (latestVersion <= currentVersion)
        {
            RealtimeLog.Write($"[업데이트] 이미 최신 버전입니다 (현재 {currentVersion}, 서버 {latestVersion}).");
            return UpdateCheckResult.UpToDate;
        }

        RealtimeLog.Write($"[업데이트] 새 버전 발견: {currentVersion} → {latestVersion}. 다운로드를 시작합니다.");

        var (zipPath, downloadResult) = await DownloadAsync(manifest).ConfigureAwait(false);
        if (zipPath is null)
        {
            return downloadResult;
        }

        RealtimeLog.Write("[업데이트] 다운로드·체크섬 검증 완료. 적용 스크립트를 실행하고 프로그램을 종료합니다.");
        LaunchUpdaterAndExitCurrentProcess(zipPath);
        return UpdateCheckResult.Applied;
    }

    private async Task<UpdateManifest?> FetchManifestAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.UpdateManifestUrl))
        {
            RealtimeLog.Write("[업데이트] 환경설정 > 네트워크의 \"업데이트 서버\" 주소가 비어 있습니다.");
            return null;
        }

        try
        {
            using var response = await HttpClient.GetAsync(_settings.UpdateManifestUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                RealtimeLog.Write(
                    $"[업데이트] manifest.json 조회 실패: HTTP {(int)response.StatusCode} " +
                    $"(주소: {_settings.UpdateManifestUrl})");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                var snippet = json.Length > 200 ? json[..200] + "…" : json;
                RealtimeLog.Write(
                    $"[업데이트] manifest.json 형식이 올바르지 않습니다(주소: {_settings.UpdateManifestUrl}). " +
                    $"응답 내용: {snippet}");
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            RealtimeLog.Write(
                $"[업데이트] manifest.json 조회 중 오류: {ex.Message} (주소: {_settings.UpdateManifestUrl})");
            return null;
        }
    }

    private static async Task<(string? ZipPath, UpdateCheckResult Result)> DownloadAsync(UpdateManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            RealtimeLog.Write("[업데이트] manifest.json에 downloadUrl이 없습니다.");
            return (null, UpdateCheckResult.DownloadFailed);
        }

        var workDir = Path.Combine(Path.GetTempPath(), "LmsAgentUpdate");
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, $"update-{manifest.Version}.zip");

        try
        {
            using var response = await HttpClient.GetAsync(manifest.DownloadUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                RealtimeLog.Write(
                    $"[업데이트] 새 버전 파일 다운로드 실패: HTTP {(int)response.StatusCode} (주소: {manifest.DownloadUrl})");
                return (null, UpdateCheckResult.DownloadFailed);
            }

            await using (var fileStream = File.Create(zipPath))
            {
                await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            RealtimeLog.Write($"[업데이트] 새 버전 파일 다운로드 중 오류: {ex.Message} (주소: {manifest.DownloadUrl})");
            return (null, UpdateCheckResult.DownloadFailed);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actualHash = ComputeSha256(zipPath);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                RealtimeLog.Write(
                    $"[업데이트] 체크섬 불일치 — manifest: {manifest.Sha256}, 실제: {actualHash}. " +
                    "다운로드한 파일을 삭제하고 적용하지 않습니다.");
                File.Delete(zipPath);
                return (null, UpdateCheckResult.ChecksumMismatch);
            }
        }

        return (zipPath, UpdateCheckResult.Applied);
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 실행 중인 exe는 스스로 덮어쓸 수 없으므로, 별도의 배치 스크립트를 띄워
    /// (1) 현재 프로세스가 끝나기를 잠시 기다렸다가 (2) 새 파일로 교체하고 (3) 프로그램을 재시작합니다.
    /// </summary>
    private static void LaunchUpdaterAndExitCurrentProcess(string zipPath)
    {
        var installDir = AppContext.BaseDirectory;
        var updateRoot = Path.Combine(Path.GetTempPath(), "LmsAgentUpdate");
        var extractDir = Path.Combine(updateRoot, "extracted");

        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, recursive: true);
        }

        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var exeName = Path.GetFileName(Environment.ProcessPath ?? "LmsAgent.exe");
        var scriptPath = Path.Combine(updateRoot, "apply_update.bat");

        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("timeout /t 2 /nobreak > NUL");
        script.AppendLine($"xcopy /E /Y /I \"{extractDir}\" \"{installDir}\"");
        script.AppendLine($"start \"\" \"{Path.Combine(installDir, exeName)}\"");
        script.AppendLine("del \"%~f0\"");

        File.WriteAllText(scriptPath, script.ToString(), Encoding.ASCII);

        var startInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            WorkingDirectory = updateRoot,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }
}
