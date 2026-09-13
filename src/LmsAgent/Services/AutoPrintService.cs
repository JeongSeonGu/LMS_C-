using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Networking;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 출력의 "나의 일간 일정" 자동 출력을 담당합니다.
/// 평일 08:30~10:00 사이, 그날 아직 인쇄하지 않았다면 로그인 여부를 확인해 1회 인쇄합니다.
/// </summary>
public sealed class AutoPrintService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly AppSettings _settings;
    private readonly Timer _timer;

    public AutoPrintService(WorkSupportApiClient api, SessionManager session, AppSettings settings)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(5).TotalMilliseconds };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start()
    {
        _timer.Start();
        _ = CheckAsync();
    }

    public void Stop() => _timer.Stop();

    private async Task CheckAsync()
    {
        if (!_settings.PrintDailyScheduleEnabled) return;
        if (!_session.IsLoggedIn) return;

        var now = DateTime.Now;
        var todayKey = now.ToString("yyyy-MM-dd");
        if (_settings.LastAutoPrintDate == todayKey) return;

        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday) return;

        var withinWindow = now.TimeOfDay >= new TimeSpan(8, 30, 0) && now.TimeOfDay <= new TimeSpan(10, 0, 0);
        if (!withinWindow) return;

        try
        {
            var printed = await PrintingService.PrintTodayScheduleAsync(_api, _session, _settings);
            if (printed)
            {
                _settings.LastAutoPrintDate = todayKey;
                SettingsStore.Save(_settings);
            }
        }
        catch
        {
            // 인쇄 실패 시 LastAutoPrintDate를 갱신하지 않아 다음 주기에 다시 시도합니다.
        }
    }

    public void Dispose() => _timer.Dispose();
}
