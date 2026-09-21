using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 업무의 "업무 및 할일 모니터 출력"이 켜져 있으면 업무 일지(포스트잇 오버레이)를
/// 선택한 모니터에 상시 표시합니다. 내 담당업무 관련 학사일정, 내가 해야 할 할일(미완료),
/// 나에게 알림 대상으로 지정된 일정을 날짜별로 모아 보여줍니다.
/// </summary>
public sealed class WorkJournalService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private WorkJournalForm? _form;

    public WorkJournalService(WorkSupportApiClient api, SessionManager session, AppSettings settings)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(15).TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    /// <summary>환경설정이 바뀔 때마다(또는 로그인 직후) 호출해서 오버레이 상태를 다시 맞춥니다.</summary>
    public void ApplySettings()
    {
        if (!_settings.TaskJournalEnabled)
        {
            _timer.Stop();
            _form?.Hide();
            return;
        }

        _form ??= new WorkJournalForm();
        _form.PositionTopLeft(DisplayHelper.ResolveScreen(_settings.TaskJournalMonitorIndex));
        _form.SetOpacityPercent(_settings.TaskJournalOpacityPercent);

        if (!_form.Visible)
        {
            _form.Show();
        }

        _timer.Start();
        _ = RefreshAsync();
    }

    /// <summary>15분 타이머를 기다리지 않고 즉시 다시 조회합니다(실시간 연동 알림 수신 시 호출).</summary>
    public void RefreshNow() => _ = RefreshAsync();

    /// <summary>단축키로 토글: 보이면 숨기고, 숨겨져 있으면(활성화된 경우) 다시 보여줍니다.</summary>
    public void ToggleVisible()
    {
        if (!_settings.TaskJournalEnabled || _form is null)
        {
            return;
        }

        if (_form.Visible)
        {
            _form.Hide();
        }
        else
        {
            _form.PositionTopLeft(DisplayHelper.ResolveScreen(_settings.TaskJournalMonitorIndex));
            _form.Show();
            _ = RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        if (_form is null || _session.Profile is null)
        {
            return;
        }

        var (rangeStart, rangeEnd) = ResolveRange();
        var myTeacherId = _session.Profile.TeacherId;
        var items = new List<WorkJournalItem>();

        try
        {
            var months = new HashSet<(int Year, int Month)>
            {
                (rangeStart.Year, rangeStart.Month),
                (rangeEnd.Year, rangeEnd.Month),
            };

            foreach (var (year, month) in months)
            {
                // ⚠ 반드시 true여야 한다 — 아래 _form.SetItems(items)가 WinForms 컨트롤을 직접
                // 조작하므로, await 이후 UI 스레드로 반드시 되돌아와야 한다(ConfigureAwait(false)로
                // 두면 스레드풀 스레드에서 컨트롤을 건드리게 되어 예외가 조용히 삼켜지고 화면이
                // 갱신되지 않는다 — 데이터는 정상인데 업무 일지에 아무것도 안 나타나던 원인).
                var result = await _api.GetEventsAsync(year, month).ConfigureAwait(true);
                if (!result.Ok || result.Data is null)
                {
                    continue;
                }

                foreach (var ev in result.Data)
                {
                    var date = ev.StartDateTime.Date;
                    if (date < rangeStart || date > rangeEnd)
                    {
                        continue;
                    }

                    // 종일 일정은 특정 시각이 없으므로 null(업무 일지에서 맨 위에 모아 표시).
                    var time = ev.AllDay ? (TimeSpan?)null : ev.StartDateTime.TimeOfDay;

                    if (ev.DeptId is int deptId && _session.MyDeptIds.Contains(deptId))
                    {
                        items.Add(new WorkJournalItem(date, $"[일정] {ev.Title}", ev.Note, time));
                    }

                    if (myTeacherId is int tid && ev.NotifyTargets.TeacherIds.Contains(tid))
                    {
                        items.Add(new WorkJournalItem(date, $"[알림] {ev.Title}", ev.Note, time));
                    }
                }
            }

            var todoResult = await _api.GetTodosAsync().ConfigureAwait(true);
            if (todoResult.Ok && todoResult.Data is not null)
            {
                foreach (var todo in todoResult.Data)
                {
                    // 할일은 school_events처럼 담당업무(deptId) 단위로 배정된다(개인별 담당자 목록 없음).
                    if (todo.Done || todo.DeptId is not int deptId || !_session.MyDeptIds.Contains(deptId))
                    {
                        continue;
                    }

                    var date = DateTime.TryParse(todo.DueDate, out var parsed) ? parsed.Date : DateTime.Today;
                    if (date < rangeStart || date > rangeEnd)
                    {
                        continue;
                    }

                    items.Add(new WorkJournalItem(date, $"[할일] {todo.Title}", todo.Note));
                }
            }
        }
        catch
        {
            // 업무 일지는 부가 기능이므로, 조회 실패는 조용히 무시하고 이전 내용을 그대로 둔다.
            return;
        }

        _form.SetItems(items);
    }

    private (DateTime Start, DateTime End) ResolveRange()
    {
        var today = DateTime.Today;
        if (_settings.TaskJournalOutputUnit == TaskJournalOutputUnit.Day)
        {
            return (today, today);
        }

        // 월요일 시작 주간.
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-offset);
        return (monday, monday.AddDays(6));
    }

    public void Dispose()
    {
        _timer.Dispose();
        _form?.Dispose();
    }
}
