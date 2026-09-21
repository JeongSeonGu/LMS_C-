using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LmsAgent.Models.Realtime;
using LmsAgent.Networking;

namespace LmsAgent.Services;

/// <summary>
/// 학사달력 웹서비스에서 일정의 담당업무(deptId)가 바뀌었을 때 "내 업무가 되었는가/빠졌는가"를
/// 판단해 알려주는 서비스입니다(웹소켓_데이터통신규칙.md §7-A). 판정에는 domain.event의
/// hint(deptFrom/deptTo)와 로그인 계정의 담당업무 목록(SessionManager.MyDeptIds)을 씁니다.
///
/// 화면 갱신 자체(배경화면 오버레이·업무 일지 재조회)는 이미 TrayApplicationContext의
/// 실시간 이벤트 처리에서 모든 학사 일정 변경에 대해 이루어지고 있으므로, 이 서비스는
/// "무엇이 왜 바뀌었는지" 사용자에게 알려주는 트레이 알림(토스트)만 담당합니다.
/// </summary>
public sealed class MyDutyChangeService
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    /// <summary>삭제되었거나 내 업무에서 빠진 일정은 재조회가 불가능하므로(§7-A), 제목을
    /// 미리 캐시해 둔다. 프로그램을 새로 시작하면 비어 있으며, 그 경우 일반 문구로 대체한다.</summary>
    private readonly Dictionary<int, string> _myEventTitleCache = new();

    /// <summary>알림 문구가 준비되면 발생합니다. UI 스레드로 마샬링해서 트레이 풍선 등으로 표시하세요.</summary>
    public event Action<string>? Notify;

    public MyDutyChangeService(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;
    }

    /// <summary>WebSocketClientService.DomainEventReceived에 그대로 연결하면 됩니다.
    /// work.calendar.event.* 가 아니면 즉시 무시합니다.</summary>
    public void HandleDomainEvent(DomainEventData ev)
    {
        if (_session.Profile is null) return; // 로그인 전에는 "내 업무" 개념이 없다.
        if (!ev.Type.StartsWith("work.calendar.event.", StringComparison.Ordinal)) return;
        if (!int.TryParse(ev.Resource?.Id, out var id)) return;

        var from = ev.Hint?.DeptFrom;
        var to = ev.Hint?.DeptTo;
        bool Mine(int? deptId) => deptId.HasValue && _session.MyDeptIds.Contains(deptId.Value);
        var byMe = ev.Actor?.UserId is int actorId && actorId == _session.Profile.UserId;

        // 웹소켓_데이터통신규칙.md §7-A 판정표.
        string? kind = ev.Op switch
        {
            "created" when Mine(to) => "created",
            "updated" when !Mine(from) && Mine(to) => "assigned",
            "updated" when Mine(from) && !Mine(to) => "unassigned",
            "updated" when Mine(from) && Mine(to) => "changed",
            "deleted" when Mine(from) => "deleted",
            _ => null,
        };

        if (kind is null) return;

        // 내가 웹에서 직접 바꾼 경우와, 단순 수정(changed)은 알리지 않는다(§7-A 1번 규칙).
        _ = HandleMyDutyChangeAsync(kind, id, notify: !byMe && kind != "changed");
    }

    private async Task HandleMyDutyChangeAsync(string kind, int id, bool notify)
    {
        string title;

        if (kind is "unassigned" or "deleted")
        {
            // 이미 내 업무에서 빠졌거나 삭제된 일정은 다시 조회해도 "예전에 내 업무였다"는
            // 사실을 알 수 없으므로(조회 API는 현재 상태만 준다), 캐시된 제목만 쓴다.
            title = _myEventTitleCache.TryGetValue(id, out var cached) ? cached : "(제목 확인 불가)";
            _myEventTitleCache.Remove(id);
        }
        else
        {
            try
            {
                var result = await _api.GetEventAsync(id).ConfigureAwait(false);
                if (!result.Ok || result.Data is null)
                {
                    return; // 조회하는 사이에 다시 삭제되었을 수 있다.
                }

                title = result.Data.Title;
                _myEventTitleCache[id] = title;
            }
            catch
            {
                return;
            }
        }

        if (!notify)
        {
            return;
        }

        var message = kind switch
        {
            "created" => $"내 업무에 새 일정이 등록되었습니다: {title}",
            "assigned" => $"내 업무로 배정되었습니다: {title}",
            "unassigned" => $"내 업무에서 해제되었습니다: {title}",
            "deleted" => $"내 업무 일정이 삭제되었습니다: {title}",
            _ => $"내 업무 일정이 수정되었습니다: {title}",
        };

        Notify?.Invoke(message);
    }
}
