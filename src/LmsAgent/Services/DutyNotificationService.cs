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
        if (_settings.DutyNotifyHeadTeacher) watchedPositions.Add("교무부장");
        if (_settings.DutyNotifyAdminChief) watchedPositions.Add("행정실장");

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

            // 오늘 날짜 기록을, 시간이 지정된 경우(하루 종일이 아닌 경우) 그 시간대와
            // 지금 시각의 관계에 따라 두 그룹으로 나눈다 — 예전에는 "지금 활성 시간대가
            // 아니면 무조건 당일에서 빠지고 내일 예정으로 넘어간다"였는데, 이러면 예를
            // 들어 09:00~16:30 출장을 08:50에 확인하면(아직 시작 전) 오늘 기록인데도
            // "내일 부재 예정"으로 잘못 표시됐다(사용자가 실제로 겪은 문제). 지금은:
            //   - 활성 중(시작~종료 사이) → "오늘 부재 안내"(긴급)
            //   - 오늘이지만 아직 시작 전 → "오늘 부재 예정"(안내) — 내일로 넘기지 않는다
            //   - 이미 종료됨 → 어느 쪽에도 포함하지 않는다(요청대로 시간이 지나면 사라짐)
            // 내일 예정 배너는 미리 알려주는 목적이므로 시간 제한 없이 그대로 둔다.
            // 교장/교감이 같은 날 동시에 복무 기록이 있을 수 있으므로, 한 건만 고르지 않고
            // 조건에 맞는 기록을 모두 모아 한 배너에 함께 보여준다(각자 자신의 시간대에서만
            // 보이고 사라지는 것은 그대로 유지).
            var todayRecords = records.Where(r => Matches(r, today)).ToList();
            var activeToday = todayRecords.Where(r => IsWithinActiveWindow(r, DateTime.Now)).ToList();
            var upcomingToday = todayRecords
                .Where(r => !IsWithinActiveWindow(r, DateTime.Now) && !HasEnded(r, DateTime.Now))
                .ToList();
            var tomorrowHits = records.Where(r => Matches(r, tomorrow)).ToList();

            // "오늘인데 내일로 표시된다"는 보고가 있었으나 로직상으로는 재현하지 못해,
            // 다음에 다시 발생했을 때 실제 서버 데이터로 원인을 바로 확인할 수 있도록 매번
            // 조회 결과를 남긴다("실시간 연동 로그 열기"에서 확인). 문제가 더 이상
            // 재현되지 않으면 이 로그는 지워도 된다.
            RealtimeLog.Write(
                $"[복무배너] 오늘={DateOnly.FromDateTime(today)} 내일={DateOnly.FromDateTime(tomorrow)} " +
                $"지금={DateTime.Now:HH:mm:ss} 대상={string.Join(",", watchedPositions)} " +
                $"조회건수={records.Count} 오늘활성={activeToday.Count} 오늘예정={upcomingToday.Count} 내일적중={tomorrowHits.Count}");
            foreach (var r in records)
            {
                RealtimeLog.Write(
                    $"[복무배너]   date={r.Date}(파싱={r.DateValue?.ToString() ?? "실패"}) {r.Position} {r.DutyType} " +
                    $"{r.TimeStart}~{r.TimeEnd} note={r.Note}");
            }

            if (activeToday.Count > 0 || upcomingToday.Count > 0)
            {
                var combined = activeToday.Select(r => (Record: r, Timing: DutyTiming.ActiveToday))
                    .Concat(upcomingToday.Select(r => (Record: r, Timing: DutyTiming.UpcomingToday)))
                    .ToList();
                var urgent = activeToday.Count > 0;
                var headline = urgent ? "오늘 부재 안내" : "오늘 부재 예정";
                ShowBanner(headline, BuildDutyMessage(combined), urgent);
            }
            else if (tomorrowHits.Count > 0)
            {
                ShowBanner("내일 부재 예정", BuildDutyMessage(tomorrowHits.Select(r => (r, DutyTiming.Tomorrow))), urgent: false);
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

    /// <summary>시간이 지정된 기록이면 지금 시각이 그 시작~종료 시간 사이일 때만 true를
    /// 돌려준다(하루 종일 기록이거나 시간 파싱이 실패하면 안전하게 "활성" 취급).</summary>
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

    /// <summary>시간이 지정된 기록의 종료 시각이 이미 지났으면 true(하루 종일 기록은 그
    /// 날 안에서는 끝나지 않는다고 본다). "시간이 종료되면 메시지도 사라진다"는 요구를
    /// 위한 것 — 종료된 기록은 활성(active)에도 예정(upcoming)에도 포함되지 않는다.</summary>
    private static bool HasEnded(DutyRecord r, DateTime now)
    {
        if (r.IsAllDay)
        {
            return false;
        }

        if (!TimeSpan.TryParse(r.TimeEnd, out var end))
        {
            return false;
        }

        return now.TimeOfDay > end;
    }

    private enum DutyTiming { ActiveToday, UpcomingToday, Tomorrow }

    /// <summary>기타 내용(Note)이 있으면 복무 구분 뒤에 괄호로 붙이고, 시간이 지정된
    /// 기록이면 "HH:mm ~ HH:mm" 구간을 함께 보여준다. 예: "교감선생님 연가(지각)
    /// 08:30 ~ 09:00".</summary>
    private static string BuildDutyMessage(DutyRecord r, DutyTiming timing)
    {
        var dutyTypeText = string.IsNullOrWhiteSpace(r.Note) ? r.DutyType : $"{r.DutyType}({r.Note})";

        if (r.IsAllDay)
        {
            return timing == DutyTiming.Tomorrow
                ? $"내일은 {r.Position}선생님이 {dutyTypeText} 예정입니다."
                : $"{r.Position}선생님이 오늘 {dutyTypeText}(으)로 부재중입니다.";
        }

        return timing switch
        {
            DutyTiming.ActiveToday => $"{r.Position}선생님 {dutyTypeText} {r.TimeStart} ~ {r.TimeEnd}",
            DutyTiming.UpcomingToday => $"오늘 {r.Position}선생님 {dutyTypeText} 예정 ({r.TimeStart} ~ {r.TimeEnd})",
            _ => $"내일 {r.Position}선생님 {dutyTypeText} 예정 ({r.TimeStart} ~ {r.TimeEnd})",
        };
    }

    /// <summary>같은 날 조건에 맞는 기록이 여러 건(예: 교장·교감 동시 출장, 또는 한 명은
    /// 지금 활성이고 다른 한 명은 오늘 시작 전)이면 각 기록의 메시지를 줄바꿈으로 이어붙여
    /// 한 배너에 함께 보여준다.</summary>
    private static string BuildDutyMessage(IEnumerable<(DutyRecord Record, DutyTiming Timing)> items) =>
        string.Join("\n", items.Select(i => BuildDutyMessage(i.Record, i.Timing)));

    private void ShowBanner(string headline, string text, bool urgent)
    {
        _banner ??= new DutyBannerForm();
        _banner.UpdateContent(headline, text, urgent);
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
