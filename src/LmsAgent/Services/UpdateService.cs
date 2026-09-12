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
    /// </summary>
    public bool CheckAndLaunchUpdaterIfAvailable()
    {
        try
        {
            return CheckAndLaunchUpdaterIfAvailableAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"업데이트 확인 실패: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckAndLaunchUpdaterIfAvailableAsync()
    {
        var manifest = await FetchManifestAsync().ConfigureAwait(false);
        if (manifest is null)
        {
            return false;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            return false;
        }

        if (latestVersion <= currentVersion)
        {
            return false;
        }

        var zipPath = await DownloadAsync(manifest).ConfigureAwait(false);
        if (zipPath is null)
        {
            return false;
        }

        LaunchUpdaterAndExitCurrentProcess(zipPath);
        return true;
    }

    private async Task<UpdateManifest?> FetchManifestAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.UpdateManifestUrl))
        {
            return null;
        }

        using var response = await HttpClient.GetAsync(_settings.UpdateManifestUrl).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }).ConfigureAwait(false);
    }

    private static async Task<string?> DownloadAsync(UpdateManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            return null;
        }

        var workDir = Path.Combine(Path.GetTempPath(), "LmsAgentUpdate");
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, $"update-{manifest.Version}.zip");

        using var response = await HttpClient.GetAsync(manifest.DownloadUrl).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using (var fileStream = File.Create(zipPath))
        {
            await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actualHash = ComputeSha256(zipPath);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(zipPath);
                return null;
            }
        }

        return zipPath;
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
