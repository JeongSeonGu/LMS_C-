using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 학사일정의 "일정 시작 알림"(10/20/30/60분 전)이 켜져 있으면
/// 시간이 지정된 학사 일정의 시작 전에 화면 알림을 띄우도록 알려줍니다.
/// 종일 일정은 정확한 시작 시각이 없어 대상에서 제외합니다.
/// </summary>
public sealed class ScheduleReminderService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private readonly HashSet<string> _notified = new();
    private string _notifiedDate = "";

    /// <summary>알림을 띄워야 할 때 발생합니다(제목, 본문).</summary>
    public event Action<string, string>? ReminderRaised;

    public ScheduleReminderService(WorkSupportApiClient api, SessionManager session, AppSettings settings)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _timer = new Timer { Interval = 30_000 };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private IEnumerable<int> EnabledThresholdsMinutes()
    {
        if (_settings.ScheduleReminder10MinEnabled) yield return 10;
        if (_settings.ScheduleReminder20MinEnabled) yield return 20;
        if (_settings.ScheduleReminder30MinEnabled) yield return 30;
        if (_settings.ScheduleReminder60MinEnabled) yield return 60;
    }

    private async Task CheckAsync()
    {
        if (!_session.IsLoggedIn)
        {
            return;
        }

        var thresholds = EnabledThresholdsMinutes().ToList();
        if (thresholds.Count == 0)
        {
            return;
        }

        var today = DateTime.Today;
        var todayKey = today.ToString("yyyy-MM-dd");
        if (_notifiedDate != todayKey)
        {
            // 날짜가 바뀌면 어제 기록해 둔 알림 이력을 지워 메모리가 계속 쌓이지 않도록 합니다.
            _notified.Clear();
            _notifiedDate = todayKey;
        }

        try
        {
            var result = await _api.GetEventsAsync(today.Year, today.Month).ConfigureAwait(true);
            if (!result.Ok || result.Data is null)
            {
                return;
            }

            var now = DateTime.Now;

            foreach (var ev in result.Data)
            {
                if (ev.AllDay || ev.StartDateTime <= now)
                {
                    continue;
                }

                foreach (var minutes in thresholds)
                {
                    var reminderAt = ev.StartDateTime.AddMinutes(-minutes);
                    if (now < reminderAt)
                    {
                        continue;
                    }

                    var key = $"{ev.Id}:{minutes}";
                    if (!_notified.Add(key))
                    {
                        continue;
                    }

                    ReminderRaised?.Invoke(
                        $"{minutes}분 후 일정 시작",
                        $"'{ev.Title}' 일정이 {ev.StartDateTime:HH:mm}에 시작됩니다.");
                }
            }
        }
        catch
        {
            // 조회 실패는 조용히 무시하고 다음 주기에 다시 시도합니다.
        }
    }

    public void Dispose() => _timer.Dispose();
}
