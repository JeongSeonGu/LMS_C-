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

            // 오늘 기록 중 시간이 지정된 건(하루 종일이 아닌 경우)은, 그 시간대일 때만
            // "지금 부재중" 배너를 띄운다 — 예전에는 날짜만 맞으면 종일 배너가 떠 있어서,
            // 예를 들어 08:30~09:00 지각 기록이 오후까지 계속 "부재중"으로 보였다.
            // 내일 예정 배너는 미리 알려주는 목적이므로 시간 제한 없이 그대로 둔다.
            // 교장/교감이 같은 날 동시에 복무 기록이 있을 수 있으므로, 한 건만 고르지 않고
            // 조건에 맞는 기록을 모두 모아 한 배너에 함께 보여준다(각자 자신의 시간대에서만
            // 보이고 사라지는 것은 그대로 유지).
            var todayHits = records
                .Where(r => Matches(r, today))
                .Where(r => IsWithinActiveWindow(r, DateTime.Now))
                .ToList();
            var tomorrowHits = records.Where(r => Matches(r, tomorrow)).ToList();

            if (todayHits.Count > 0)
            {
                ShowBanner(BuildDutyMessage(todayHits, isToday: true), urgent: true);
            }
            else if (tomorrowHits.Count > 0)
            {
                ShowBanner(BuildDutyMessage(tomorrowHits, isToday: false), urgent: false);
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

    /// <summary>하루 종일 기록이면 항상 true. 시간이 지정된 기록이면 지금 시각이 그
    /// 시작~종료 시간 사이일 때만 true를 돌려준다(시간 파싱이 실패하면 안전하게 종일
    /// 취급). "08:30~09:00 지각"처럼 짧은 시간대 기록이 하루 종일 배너로 남아 있지
    /// 않도록 하기 위한 것이다.</summary>
    private static bool IsWithinActiveWindow(DutyRecord r, DateTime now)
    {
        if (r.IsAllDay)
        {
            return true;
        }

        if (!TimeSpan.TryParse(r.TimeStart, out var start) || !TimeSpan.TryParse(r.TimeEnd, out var end))
        {
            return true;
        }

        var nowTimeOfDay = now.TimeOfDay;
        return nowTimeOfDay >= start && nowTimeOfDay <= end;
    }

    /// <summary>기타 내용(Note)이 있으면 복무 구분 뒤에 괄호로 붙이고, 시간이 지정된
    /// 기록이면 "HH:mm ~ HH:mm" 구간을 함께 보여준다. 예: "교감선생님 연가(지각)
    /// 08:30 ~ 09:00".</summary>
    private static string BuildDutyMessage(DutyRecord r, bool isToday)
    {
        var dutyTypeText = string.IsNullOrWhiteSpace(r.Note) ? r.DutyType : $"{r.DutyType}({r.Note})";

        if (r.IsAllDay)
        {
            return isToday
                ? $"{r.Position}선생님이 오늘 {dutyTypeText}(으)로 부재중입니다."
                : $"내일은 {r.Position}선생님이 {dutyTypeText} 예정입니다.";
        }

        return $"{r.Position}선생님 {dutyTypeText} {r.TimeStart} ~ {r.TimeEnd}";
    }

    /// <summary>같은 날 조건에 맞는 기록이 여러 건(예: 교장·교감 동시 출장)이면 각 기록의
    /// 메시지를 줄바꿈으로 이어붙여 한 배너에 함께 보여준다.</summary>
    private static string BuildDutyMessage(List<DutyRecord> records, bool isToday) =>
        string.Join("\n", records.Select(r => BuildDutyMessage(r, isToday)));

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
