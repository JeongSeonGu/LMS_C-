using System;
using System.IO;
using System.Text.Json;

namespace LmsAgent.Configuration;

/// <summary>
/// %AppData%\LmsAgent\settings.json 에 환경 설정을 저장하고 불러옵니다.
/// </summary>
public static class SettingsStore
{
    private static readonly string DirPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LmsAgent");

    private static readonly string FilePath = Path.Combine(DirPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.DeviceId))
                    {
                        loaded.DeviceId = Guid.NewGuid().ToString("N");
                        Save(loaded);
                    }

                    return loaded;
                }
            }
        }
        catch
        {
            // 설정 파일이 손상된 경우 기본값으로 새로 생성합니다.
        }

        var settings = new AppSettings { DeviceId = Guid.NewGuid().ToString("N") };
        Save(settings);
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirPath);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
