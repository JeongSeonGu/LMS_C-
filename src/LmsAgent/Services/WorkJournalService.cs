using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 업무의 "업무 및 할일 모니터 출력"이 켜져 있으면 업무 일지(포스트잇 오버레이)를
/// 선택한 모니터에 상시 표시합니다. 내 담당업무 관련 학사일정, 내가 해야 할 할일(미완료),
/// 나에게 알림 대상으로 지정된 일정을 날짜별로 모아 보여주고, 그 위에 학사 달력 밖의
/// 요청사항·알림·법정연수 "진행 중" 항목도 분류별로 함께 정리해 보여줍니다.
/// </summary>
public sealed class WorkJournalService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly AppSettings _settings;
    private readonly PersonalScheduleStore _personalSchedules;
    private readonly Timer _timer;
    private readonly StickyNoteService _stickyNotes = new();
    private WorkJournalForm? _form;

    public WorkJournalService(
        WorkSupportApiClient api, SessionManager session, AppSettings settings, PersonalScheduleStore personalSchedules)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _personalSchedules = personalSchedules;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(15).TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAsync();
        // 로그인/로그아웃(SessionManager.Clear) 시 즉시 로그인 전 안내 ↔ 실제 내용으로 전환한다.
        _session.SessionChanged += RefreshNow;
    }

    /// <summary>환경설정이 바뀔 때마다(또는 로그인 직후) 호출해서 오버레이 상태를 다시 맞춥니다.
    /// 사용자가 만든 스티커 메모(<see cref="StickyNoteService"/>)도 이 기능의 켜짐/꺼짐을
    /// 그대로 따릅니다 — 업무 일지를 끄면 메모 창도 함께 닫히고(내용은 보존), 다시 켜면
    /// 함께 돌아옵니다.</summary>
    public void ApplySettings()
    {
        if (!_settings.TaskJournalEnabled)
        {
            _timer.Stop();
            _form?.Hide();
            _stickyNotes.HideAll();
            return;
        }

        if (_form is null)
        {
            _form = new WorkJournalForm();
            _form.MemoButtonClicked += (_, _) => _stickyNotes.ShowAll();
            _form.LinkClicked += async (_, link) => await OpenLinkWithSsoAsync(link);
        }

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
        if (_form is null)
        {
            return;
        }

        if (_session.Profile is null)
        {
            _form.SetLoggedOut();
            return;
        }

        var (rangeStart, rangeEnd) = ResolveRange();
        _form.SetHeaderRange(rangeStart, rangeEnd);
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
                // ⚠ 반드시 true여야 한다 — 아래 _form.SetContent(...)가 WinForms 컨트롤을 직접
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

        // 개인일정은 서버 조회와 무관한 로컬 파일 읽기이므로 실패해도 나머지 항목에 영향이
        // 없도록 따로 감싼다. 학사 일정과 구분되도록 지정된 아이콘·색으로 표시한다.
        try
        {
            var accentColor = ColorHelper.ParseHexOrDefault(_settings.PersonalScheduleColor, Color.MediumPurple);
            foreach (var personal in _personalSchedules.Load())
            {
                if (personal.Date < rangeStart || personal.Date > rangeEnd)
                {
                    continue;
                }

                items.Add(new WorkJournalItem(
                    personal.Date, personal.Title, personal.Note, personal.Time,
                    _settings.PersonalScheduleIcon, accentColor));
            }
        }
        catch
        {
            // 개인일정 로딩 실패도 조용히 건너뛴다.
        }

        var alerts = await BuildAlertsAsync().ConfigureAwait(true);
        _form.SetContent(alerts, items);
    }

    /// <summary>
    /// C#에서 이미 로그인되어 있으면 SSO 1회용 티켓을 발급받아, 클릭한 상세 주소로 바로
    /// 이동하도록 next 파라미터를 붙여 연다(SSO 자동 로그인 적용 안내.md §5-7).
    /// 로그인 전이거나 티켓 발급이 실패하면 평소처럼 연다.
    /// </summary>
    private async Task OpenLinkWithSsoAsync(string relativeLink)
    {
        var absoluteUrl = relativeLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeLink
            : "https://future-class.kr" + relativeLink;

        if (_session.IsLoggedIn)
        {
            try
            {
                var ticket = await _api.GetSsoTicketAsync().ConfigureAwait(true);
                if (ticket.Ok && !string.IsNullOrWhiteSpace(ticket.Data?.LoginUrl))
                {
                    // ⚠ next는 반드시 이 서비스 안의 "상대경로"(/SchoolWork/WorkSupport/...)여야
                    // 한다 — 서버가 오픈 리다이렉트 방지를 위해 스킴이 있는 절대 URL은 전부
                    // 무시하고 대시보드로 보낸다(SSO 자동 로그인 적용 안내.md §5-7). alert.Link는
                    // 이미 이런 상대경로로 내려오므로(학사달력외_연동가이드.md), 그대로 쓰면 되고
                    // 혹시 절대 URL 형태로 들어오면 경로+쿼리만 추려서 보낸다.
                    var nextPath = relativeLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? new Uri(relativeLink).PathAndQuery
                        : relativeLink;
                    var separator = ticket.Data!.LoginUrl.Contains('?') ? "&" : "?";
                    var loginUrl = $"{ticket.Data.LoginUrl}{separator}next={Uri.EscapeDataString(nextPath)}";
                    Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });
                    return;
                }
            }
            catch
            {
                // 티켓 발급 실패(세션 만료 등)는 치명적이지 않다 — 아래에서 평소처럼 연다.
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo(absoluteUrl) { UseShellExecute = true });
        }
        catch
        {
            // 브라우저를 열지 못해도 업무 일지 자체는 계속 동작해야 한다.
        }
    }

    /// <summary>
    /// 학사 달력 밖의 요약 항목(요청사항 · 알림 · 법정연수)을 모읍니다(학사달력외_연동가이드.md).
    /// 세 소스를 각각 독립적으로 시도하므로, 한 쪽이 실패해도 나머지는 그대로 반영됩니다.
    /// </summary>
    private async Task<List<WorkJournalAlert>> BuildAlertsAsync()
    {
        var alerts = new List<WorkJournalAlert>();

        try
        {
            var result = await _api.GetActiveRequestsAsync().ConfigureAwait(true);
            if (result.Ok && result.Data is not null)
            {
                foreach (var req in result.Data.Items)
                {
                    var badge = req.IsOverdue ? "기한초과" : (req.Priority == "high" ? "긴급" : null);
                    var due = DateTime.TryParse(req.DueDate, out var dueDate) ? $" (~{dueDate:M/d})" : "";
                    alerts.Add(new WorkJournalAlert("요청사항", req.Title + due, req.Link, badge));
                }
            }
        }
        catch
        {
            // 이 소스만 조용히 건너뛴다.
        }

        try
        {
            var result = await _api.GetUnreadNoticesAsync().ConfigureAwait(true);
            if (result.Ok && result.Data is not null)
            {
                foreach (var notice in result.Data.Items)
                {
                    alerts.Add(new WorkJournalAlert("알림", notice.Title, notice.Link));
                }
            }
        }
        catch
        {
            // 이 소스만 조용히 건너뛴다.
        }

        try
        {
            var result = await _api.GetActiveTrainingAsync().ConfigureAwait(true);
            if (result.Ok && result.Data is not null)
            {
                foreach (var todo in result.Data.Todo)
                {
                    var badge = todo.Status == "rejected" ? "보완요청" : (todo.IsOverdue ? "기한초과" : null);
                    alerts.Add(new WorkJournalAlert("법정연수", todo.Title, todo.Link, badge));
                }

                foreach (var review in result.Data.Review)
                {
                    alerts.Add(new WorkJournalAlert(
                        "법정연수", $"{review.Title} (확인대기 {review.PendingCount}건)", review.Link));
                }
            }
        }
        catch
        {
            // 이 소스만 조용히 건너뛴다.
        }

        return alerts;
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
        _session.SessionChanged -= RefreshNow;
        _timer.Dispose();
        _form?.Dispose();
        _stickyNotes.Dispose();
    }
}
