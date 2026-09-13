using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Networking;

namespace LmsAgent.Services;

/// <summary>선택한 프린터로 로그인한 사용자의 오늘자 담당업무 학사일정을 인쇄합니다.</summary>
public static class PrintingService
{
    /// <returns>인쇄를 시도했으면(오늘 일정이 없어 건너뛴 경우 포함) true, 프린터 문제 등으로 인쇄할 수 없었으면 false.</returns>
    public static async Task<bool> PrintTodayScheduleAsync(
        WorkSupportApiClient api, SessionManager session, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PrinterName))
        {
            return false;
        }

        var today = DateTime.Today;
        var result = await api.GetEventsAsync(today.Year, today.Month).ConfigureAwait(true);
        if (!result.Ok || result.Data is null)
        {
            return false;
        }

        // "자신의 업무 일정"만 대상으로 한다 — 담당업무가 본인 소속일 때만 포함.
        var myEvents = result.Data
            .Where(ev => ev.DeptId is int deptId && session.MyDeptIds.Contains(deptId))
            .Where(ev => ev.StartDateTime.Date <= today && ev.EndDateTime.Date >= today)
            .OrderBy(ev => ev.StartDateTime)
            .ToList();

        if (myEvents.Count == 0)
        {
            return true; // 오늘 인쇄할 업무 일정이 없음 — 정상 처리로 간주하고 재시도하지 않는다.
        }

        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = settings.PrinterName;
        if (!doc.PrinterSettings.IsValid)
        {
            return false;
        }

        var title = string.IsNullOrWhiteSpace(settings.SchoolName)
            ? $"{today:yyyy-MM-dd} 나의 학사 업무 일정"
            : $"{settings.SchoolName} {today:yyyy-MM-dd} 나의 학사 업무 일정";

        using var titleFont = new Font("맑은 고딕", 16F, FontStyle.Bold);
        using var bodyFont = new Font("맑은 고딕", 11F);

        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics;
            if (g is null) return;

            var y = 40f;
            g.DrawString(title, titleFont, Brushes.Black, 40, y);
            y += 40;

            foreach (var ev in myEvents)
            {
                var timeText = ev.AllDay ? "종일" : $"{ev.StartDateTime:HH:mm}~{ev.EndDateTime:HH:mm}";
                var location = string.IsNullOrWhiteSpace(ev.Location) ? "" : $" ({ev.Location})";
                g.DrawString($"[{timeText}] {ev.Title}{location}", bodyFont, Brushes.Black, 40, y);
                y += 26;
            }
        };

        doc.Print();
        return true;
    }
}
