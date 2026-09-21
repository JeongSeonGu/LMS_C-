using System;
using System.IO;

namespace LmsAgent.Services;

/// <summary>
/// 업무 일지의 "쪽지"(자유 메모) 내용을 로컬 파일로 저장·복원합니다. 서버와 주고받지 않는
/// 개인용 스크래치 메모라, 실제 Windows 스티커 메모처럼 이 PC에만 남습니다.
/// </summary>
public static class WorkNoteStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LmsAgent", "worknote.txt");

    public static string Load()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
        }
        catch
        {
            return "";
        }
    }

    public static void Save(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, text);
        }
        catch
        {
            // 메모 저장 실패는 부가 기능이므로 조용히 무시한다.
        }
    }
}
