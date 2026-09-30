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
    /// <summary>manifest 확인 결과 더 높은 버전이 있다 — 사용자에게 물어본 뒤 다운로드를 진행해야 한다.</summary>
    Available,

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
/// <see cref="UpdateService.CheckForUpdateAsync"/>의 반환값. <see cref="Result"/>가
/// <see cref="UpdateCheckResult.Available"/>일 때만 <see cref="Manifest"/>·<see cref="CurrentVersion"/>·
/// <see cref="LatestVersion"/>이 채워진다(확인 화면에 "1.2.0 → 1.3.0" 같은 안내를 보여주기 위함).
/// </summary>
public sealed class UpdateCheckOutcome
{
    public required UpdateCheckResult Result { get; init; }
    public UpdateManifest? Manifest { get; init; }
    public Version? CurrentVersion { get; init; }
    public Version? LatestVersion { get; init; }
}

/// <summary>
/// 업데이트 서버의 버전 매니페스트를 확인하고, 새 버전이 있으면 내려받아
/// 적용용 스크립트를 실행한 뒤 프로그램을 재시작합니다.
/// 프로그램은 실행될 때마다 이 검사를 수행합니다.
/// </summary>
public sealed class UpdateService
{
    // manifest.json은 몇 줄짜리 텍스트라 15초면 충분하지만, 자체 포함(self-contained) 게시라
    // zip 하나가 100MB를 넘길 수 있어 같은 시간 제한을 쓰면 느린 회선에서 다운로드 도중
    // 타임아웃이 나 버린다. 그래서 다운로드 전용으로 훨씬 긴 타임아웃의 별도 클라이언트를 둔다.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly HttpClient DownloadHttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly AppSettings _settings;

    public UpdateService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// manifest.json을 조회해서 더 높은 버전이 있는지만 확인합니다(다운로드는 하지 않습니다).
    /// 새 버전이 있으면 <see cref="UpdateCheckResult.Available"/>과 함께 manifest·버전 정보를
    /// 담아 반환하니, 호출자가 사용자에게 물어본 뒤 <see cref="DownloadAndApplyAsync"/>를
    /// 이어서 호출하세요(업데이트 UX: 확인 → 안내 팝업 → 동의 시 진행률 표시).
    /// </summary>
    public async Task<UpdateCheckOutcome> CheckForUpdateAsync()
    {
        var manifest = await FetchManifestAsync().ConfigureAwait(false);
        if (manifest is null)
        {
            return new UpdateCheckOutcome { Result = UpdateCheckResult.ManifestUnavailable };
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            RealtimeLog.Write($"[업데이트] manifest.json의 version 값을 해석할 수 없습니다: \"{manifest.Version}\"");
            return new UpdateCheckOutcome { Result = UpdateCheckResult.ManifestUnavailable };
        }

        if (latestVersion <= currentVersion)
        {
            RealtimeLog.Write($"[업데이트] 이미 최신 버전입니다 (현재 {currentVersion}, 서버 {latestVersion}).");
            return new UpdateCheckOutcome
            {
                Result = UpdateCheckResult.UpToDate, CurrentVersion = currentVersion, LatestVersion = latestVersion,
            };
        }

        RealtimeLog.Write($"[업데이트] 새 버전 발견: {currentVersion} → {latestVersion}. 사용자 확인을 기다립니다.");
        return new UpdateCheckOutcome
        {
            Result = UpdateCheckResult.Available,
            Manifest = manifest,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
        };
    }

    /// <summary>
    /// 사용자가 업데이트에 동의한 뒤 실제로 다운로드→체크섬 검증→적용을 진행합니다.
    /// <paramref name="onProgress"/>는 0~100 사이의 진행률로 호출되며(전체 크기를 모르면
    /// 호출되지 않습니다 — 호출자는 그 경우 진행률 미상(Marquee) 표시를 유지하면 됩니다),
    /// 백그라운드 스레드에서 호출되므로 UI를 직접 건드리지 말고 Control.BeginInvoke로
    /// 마샬링해야 합니다. 성공(<see cref="UpdateCheckResult.Applied"/>)하면 내부에서 이미
    /// 새 프로세스를 띄우고 현재 프로세스를 종료(Environment.Exit)하므로, 사실상 이 값이
    /// 정상적으로 "반환"되는 경우는 없습니다.
    /// </summary>
    public async Task<UpdateCheckResult> DownloadAndApplyAsync(UpdateManifest manifest, Action<double>? onProgress = null)
    {
        RealtimeLog.Write($"[업데이트] 다운로드를 시작합니다 (주소: {manifest.DownloadUrl}).");

        var (zipPath, downloadResult) = await DownloadAsync(manifest, onProgress).ConfigureAwait(false);
        if (zipPath is null)
        {
            return downloadResult;
        }

        onProgress?.Invoke(100);
        RealtimeLog.Write("[업데이트] 다운로드·체크섬 검증 완료. 적용 스크립트를 실행하고 프로그램을 종료합니다.");

        // 사용자가 "완료"를 실제로 인지할 수 있도록 진행률 100%를 잠깐 보여준 뒤 종료한다.
        await Task.Delay(400).ConfigureAwait(false);

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

    private static async Task<(string? ZipPath, UpdateCheckResult Result)> DownloadAsync(
        UpdateManifest manifest, Action<double>? onProgress)
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
            // ResponseHeadersRead: 응답 헤더만 오면 바로 반환하고, 본문은 아래에서 파일로
            // 직접 스트리밍한다 — 146MB 전체를 메모리에 먼저 버퍼링하지 않는다.
            using var response = await DownloadHttpClient
                .GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                RealtimeLog.Write(
                    $"[업데이트] 새 버전 파일 다운로드 실패: HTTP {(int)response.StatusCode} (주소: {manifest.DownloadUrl})");
                return (null, UpdateCheckResult.DownloadFailed);
            }

            var totalBytes = response.Content.Headers.ContentLength;

            await using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var fileStream = File.Create(zipPath))
            {
                // 서버가 Content-Length를 안 줄 수도 있으므로(그 경우 진행률 없이 미상 표시),
                // 직접 청크 단위로 읽으면서 알 때만 퍼센트를 계산해 콜백한다.
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await responseStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes is > 0)
                    {
                        onProgress?.Invoke(Math.Min(99.0, totalRead * 100.0 / totalBytes.Value));
                    }
                }
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

        // ⚠ 배포용 zip을 "LmsAgent.exe가 든 폴더 자체"로 압축하면(폴더 안의 내용물이 아니라
        // 폴더를 통째로) 압축을 풀었을 때 exe가 extractDir 바로 밑이 아니라
        // extractDir\LmsAgent-x.x.x.x\ 처럼 한 단계 더 들어간 곳에 생긴다. 이걸 그대로
        // xcopy하면 그 하위 폴더째로 설치 폴더 안에 복사되어, 실제 exe는
        // installDir\LmsAgent-x.x.x.x\LmsAgent.exe에 놓이는데 아래 재시작 명령은
        // installDir\LmsAgent.exe를 찾다가 "파일을 찾을 수 없습니다" 오류가 난다(실제로
        // 겪은 문제 — Downloads\LmsAgent-1.4.0.0\LmsAgent.exe를 찾지 못함). extractDir
        // 바로 밑에 exe가 없고, 최상위 항목이 폴더 하나뿐이면 그 폴더 안을 "진짜 복사
        // 원본"으로 대신 쓴다 — 압축을 어느 구조로 하든 다음 업데이트부터는 안전하다.
        var copySource = extractDir;
        if (!File.Exists(Path.Combine(extractDir, exeName)))
        {
            var topEntries = Directory.GetFileSystemEntries(extractDir);
            if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
            {
                copySource = topEntries[0];
            }
        }

        var scriptPath = Path.Combine(updateRoot, "apply_update.bat");
        var targetExePath = Path.Combine(installDir, exeName);
        var errorLogPath = Path.Combine(installDir, "update_error.log");

        // ⚠ 실제로 겪은 또 다른 원인 — zip이 정상적으로(폴더를 한 겹 더 싸지 않고) 압축돼
        // 있었는데도 같은 "파일을 찾을 수 없습니다" 오류가 났다. 고정된 2초만 기다리고
        // xcopy를 실행하는데, 방금 종료 명령을 내린 현재 프로세스(자기 자신의 exe 파일)가
        // 실제로 파일 핸들을 놓기까지 2초보다 더 걸리면(자체 포함 실행 파일이라 크고,
        // 백신 검사 등으로 지연될 수 있다) xcopy가 그 파일 하나만 덮어쓰지 못한 채 지나갈
        // 수 있다 — 그러면 재시작 시점에 exe가 없거나 손상된 상태일 수 있다. 고정 대기
        // 대신 "같은 이름의 프로세스가 작업 목록에서 없어질 때까지" 최대 15초 정도 반복
        // 확인하도록 바꾸고, 복사 뒤에는 실제로 exe가 그 자리에 있는지 확인해서 없으면
        // 조용히 실패하는 대신 오류를 파일로 남긴다.
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        // ⚠ 진짜 원인이었던 버그 — 이 파일을 Encoding.ASCII로 저장했었는데, Windows
        // 사용자 계정 이름이 한글이면(예: "C:\Users\홍길동\...") ASCII로 표현할 수 없는
        // 글자가 전부 '?'로 뭉개져 버린다. 그러면 아래 xcopy/start 명령에 박힌 경로
        // 자체가 실제로 "C:\Users\??\..." 처럼 깨진 채로 이 파일에 저장되고, cmd.exe는
        // 그 깨진 경로를 그대로 찾다가 "파일을 찾을 수 없습니다" 오류를 낸다(사용자가
        // 실제로 겪은 오류 메시지에 그대로 "??"가 찍혀 있었다 — 사용자가 지운 게 아니라
        // 인코딩 손상이었다). UTF-8(BOM 없이)로 저장하고, cmd.exe가 그 UTF-8을 제대로
        // 읽도록 맨 앞에서 코드 페이지를 65001(UTF-8)로 바꾼다.
        script.AppendLine("chcp 65001 > nul");
        script.AppendLine("setlocal enabledelayedexpansion");
        script.AppendLine("set WAITED=0");
        script.AppendLine(":waitloop");
        script.AppendLine($"tasklist /FI \"IMAGENAME eq {exeName}\" 2>NUL | find /I \"{exeName}\" >NUL");
        script.AppendLine("if errorlevel 1 goto copyfiles");
        script.AppendLine("if !WAITED! GEQ 15 goto copyfiles");
        script.AppendLine("timeout /t 1 /nobreak > NUL");
        script.AppendLine("set /a WAITED=WAITED+1");
        script.AppendLine("goto waitloop");
        script.AppendLine(":copyfiles");
        script.AppendLine($"xcopy /E /Y /I \"{copySource}\" \"{installDir}\"");
        script.AppendLine($"if not exist \"{targetExePath}\" (");
        script.AppendLine($"  echo [%date% %time%] 업데이트 파일 복사 후에도 실행 파일을 찾을 수 없습니다: {targetExePath} >> \"{errorLogPath}\"");
        script.AppendLine("  exit /b 1");
        script.AppendLine(")");
        script.AppendLine($"start \"\" \"{targetExePath}\"");
        script.AppendLine("del \"%~f0\"");

        // BOM을 붙이면 첫 줄(@echo off)이 깨져 보이는 cmd.exe 버전이 있어 BOM 없는
        // UTF-8로 저장한다 — 위 "chcp 65001"과 짝을 맞춰야 한글 경로가 안전하다.
        File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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
