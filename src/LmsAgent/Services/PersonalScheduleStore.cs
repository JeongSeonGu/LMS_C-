using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LmsAgent.Configuration;
using LmsAgent.Models;

namespace LmsAgent.Services;

/// <summary>
/// 개인일정을 이 PC의 로컬 파일에만 저장합니다. 서버(DB)와는 전혀 통신하지 않으며,
/// 학사 일정과 절대 동기화되지 않습니다(요청사항: "개인일정은 로컬에만 기록될 뿐 학사
/// 일정과 연동이 되지 않습니다"). 저장 폴더는 환경설정 &gt; 개인일정에서 바꿀 수 있고,
/// 비워두면 다른 로컬 전용 기능(쪽지 등)과 같은 %AppData%\LmsAgent\ 를 씁니다.
/// </summary>
public sealed class PersonalScheduleStore
{
    private readonly AppSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public PersonalScheduleStore(AppSettings settings)
    {
        _settings = settings;
    }

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LmsAgent");

    public string CurrentFolder =>
        string.IsNullOrWhiteSpace(_settings.PersonalScheduleStorageFolder)
            ? DefaultFolder
            : _settings.PersonalScheduleStorageFolder!;

    private string FilePath => Path.Combine(CurrentFolder, "personal_schedules.json");

    public List<PersonalScheduleItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new List<PersonalScheduleItem>();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<PersonalScheduleItem>>(json, JsonOptions)
                   ?? new List<PersonalScheduleItem>();
        }
        catch
        {
            // 파일이 손상되었거나 폴더 설정이 잘못되었어도, 개인일정은 부가 기능이므로
            // 프로그램 실행 자체를 막지 않고 빈 목록으로 시작한다.
            return new List<PersonalScheduleItem>();
        }
    }

    public bool Save(List<PersonalScheduleItem> items, out string? error)
    {
        try
        {
            Directory.CreateDirectory(CurrentFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items, JsonOptions));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
