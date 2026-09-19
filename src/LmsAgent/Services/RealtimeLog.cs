using System;
using System.IO;

namespace LmsAgent.Services;

/// <summary>
/// 실시간 연동(웹소켓) 연결/오류 메시지를 파일로 남깁니다. UI에 노출되지 않는 진단 정보라,
/// 연결이 안 될 때 %AppData%\LmsAgent\realtime.log를 열어 원인을 바로 확인할 수 있게 합니다.
/// </summary>
public static class RealtimeLog
{
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LmsAgent", "realtime.log");

    private static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 로그 기록 실패는 무시합니다(진단용 부가 기능이 본 기능에 영향을 주면 안 됩니다).
        }
    }
}
