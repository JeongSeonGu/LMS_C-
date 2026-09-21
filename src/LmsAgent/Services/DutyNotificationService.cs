using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 교감/교장의 출장·연가 등 복무 변동사항을 주기적으로 확인해
/// 하루 전에는 "예정" 배너를, 당일에는 "부재중" 배너를 화면 우측 상단에 표시합니다.
/// (환경설정 &gt; 복무에서 대상과 출력 모니터를 지정합니다.)
/// </summary>
public sealed class DutyNotificationService : IDisposable
{
    private static readonly string[] NotifiableDutyTypes = { "출장", "연가" };

    private readonly WorkSupportApiClient _api;
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private DutyBannerForm? _banner;

    public DutyNotificationService(WorkSupportApiClient api, AppSettings settings)
    {
        _api = api;
        _settings = settings;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(15).TotalMilliseconds };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start()
    {
        _timer.Start();
        _ = CheckAsync();
    }

    public void Stop()
    {
        _timer.Stop();
        HideBanner();
    }

    /// <summary>15분 타이머를 기다리지 않고 즉시 다시 확인합니다(실시간 연동 알림 수신 시 호출).</summary>
    public void RefreshNow() => _ = CheckAsync();

    private async Task CheckAsync()
    {
        if (!_settings.DutyBannerEnabled)
        {
            HideBanner();
            return;
        }

        var watchedPositions = new List<string>();
        if (_settings.DutyNotifyVicePrincipal) watchedPositions.Add("교감");
        if (_settings.DutyNotifyPrincipal) watchedPositions.Add("교장");

        if (watchedPositions.Count == 0)
        {
            HideBanner();
            return;
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        try
        {
            var records = new List<DutyRecord>();

            var thisMonth = await _api.GetDutyStatusAsync(today.Year, today.Month).ConfigureAwait(true);
            if (thisMonth.Ok && thisMonth.Data is not null) records.AddRange(thisMonth.Data);

            if (tomorrow.Month != today.Month)
            {
                var nextMonth = await _api.GetDutyStatusAsync(tomorrow.Year, tomorrow.Month).ConfigureAwait(true);
                if (nextMonth.Ok && nextMonth.Data is not null) records.AddRange(nextMonth.Data);
            }

            bool Matches(DutyRecord r, DateTime day) =>
                watchedPositions.Contains(r.Position)
                && NotifiableDutyTypes.Contains(r.DutyType)
                && r.DateValue == DateOnly.FromDateTime(day);

            var todayHit = records.FirstOrDefault(r => Matches(r, today));
            var tomorrowHit = records.FirstOrDefault(r => Matches(r, tomorrow));

            if (todayHit is not null)
            {
                ShowBanner($"{todayHit.Position}선생님이 오늘 {todayHit.DutyType}(으)로 부재중입니다.", urgent: true);
            }
            else if (tomorrowHit is not null)
            {
                ShowBanner($"내일은 {tomorrowHit.Position}선생님이 {tomorrowHit.DutyType} 예정입니다.", urgent: false);
            }
            else
            {
                HideBanner();
            }
        }
        catch
        {
            // 네트워크 오류는 조용히 무시하고 다음 주기에 다시 확인합니다.
        }
    }

    private void ShowBanner(string text, bool urgent)
    {
        _banner ??= new DutyBannerForm();
        _banner.UpdateContent(text, urgent);
        _banner.SetOpacityPercent(_settings.DutyBannerOpacityPercent);
        _banner.PositionTopRight(DisplayHelper.ResolveScreen(_settings.DutyMonitorIndex));
        if (!_banner.Visible)
        {
            _banner.Show();
        }
    }

    private void HideBanner()
    {
        _banner?.Hide();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _banner?.Dispose();
    }
}
