using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 학사일정의 "배경화면 출력"이 켜져 있으면 학사달력을
/// 선택한 모니터에 상시 표시합니다. 주 단위는 하단 띠, 월 단위는 전체 화면입니다.
/// </summary>
public sealed class ScheduleOverlayService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private ScheduleOverlayForm? _overlay;

    public ScheduleOverlayService(WorkSupportApiClient api, AppSettings settings)
    {
        _api = api;
        _settings = settings;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(30).TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    /// <summary>환경설정이 바뀔 때마다(또는 로그인 직후) 호출해서 오버레이 상태를 다시 맞춥니다.</summary>
    public void ApplySettings()
    {
        if (!_settings.ScheduleWallpaperEnabled)
        {
            _timer.Stop();
            _overlay?.Hide();
            return;
        }

        _overlay ??= new ScheduleOverlayForm();
        _overlay.Configure(
            DisplayHelper.ResolveScreen(_settings.ScheduleMonitorIndex),
            _settings.ScheduleOutputUnit,
            _settings.SchoolName);

        if (!_overlay.Visible)
        {
            _overlay.Show();
        }

        _overlay.SendToBottom();
        _timer.Start();
        _ = RefreshAsync();
    }

    public void Stop()
    {
        _timer.Stop();
        _overlay?.Hide();
    }

    private async Task RefreshAsync()
    {
        if (_overlay is null || !_settings.ScheduleWallpaperEnabled)
        {
            return;
        }

        var today = DateTime.Today;

        try
        {
            List<SchoolEvent> events;
            DateTime periodStart;

            if (_settings.ScheduleOutputUnit == ScheduleOutputUnit.Week)
            {
                var mondayOffset = ((int)today.DayOfWeek + 6) % 7; // 월요일 시작 기준
                periodStart = today.AddDays(-mondayOffset);
                var periodEnd = periodStart.AddDays(6);

                var result = await _api.GetEventsAsync(periodStart.Year, periodStart.Month).ConfigureAwait(true);
                events = result.Ok && result.Data is not null ? result.Data : new List<SchoolEvent>();

                if (periodEnd.Month != periodStart.Month)
                {
                    var extra = await _api.GetEventsAsync(periodEnd.Year, periodEnd.Month).ConfigureAwait(true);
                    if (extra.Ok && extra.Data is not null) events.AddRange(extra.Data);
                }
            }
            else
            {
                periodStart = new DateTime(today.Year, today.Month, 1);
                var result = await _api.GetEventsAsync(today.Year, today.Month).ConfigureAwait(true);
                events = result.Ok && result.Data is not null ? result.Data : new List<SchoolEvent>();
            }

            _overlay.UpdateEvents(periodStart, events);
        }
        catch
        {
            // 네트워크 오류 시 마지막으로 표시된 내용을 그대로 유지합니다.
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _overlay?.Dispose();
    }
}
