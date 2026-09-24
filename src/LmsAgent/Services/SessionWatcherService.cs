using System;
using System.Threading.Tasks;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// C#에서 로그인해 있는 동안, 그 서버 세션이 여전히 유효한지 주기적으로(3분마다)
/// ws_me.php로 확인합니다. 세션이 만료됐거나 더 이상 유효하지 않으면(ok:false/401)
/// SessionManager.Clear()를 호출해서, 로그인 화면부터 업무 일지("로그인 전" 안내)까지
/// 모든 화면이 자동으로 로그아웃 상태로 돌아가게 합니다.
///
/// ⚠ 한계 — "웹페이지에서 로그아웃하면 C#도 자동으로 로그아웃"은, 서버의 ws_logout.php가
/// 그 계정의 세션을 전부 무효화하는 경우에만 이 방식으로 감지됩니다. C#은 로그인할 때
/// 자기 자신의 WSSESSID 쿠키를 새로 발급받아 들고 있고, 교무업무 페이지를 SSO로 열 때도
/// 브라우저는 또 다른(별도의) WSSESSID를 발급받으므로, 두 세션은 원래 서로 독립적입니다.
/// 서버가 "계정 단위 전체 로그아웃"을 하지 않는다면, 브라우저 쪽 로그아웃은 C#의 세션에
/// 영향을 주지 않으며 이 감시로도 잡을 수 없습니다 — 이 경우 실시간으로 감지하려면
/// 서버가 로그아웃 시 도메인 이벤트(예: work.session.revoked)를 그 계정의 웹소켓 Room에
/// 발행해 주는 서버 쪽 변경이 필요합니다.
/// </summary>
public sealed class SessionWatcherService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;
    private readonly Timer _timer;

    public SessionWatcherService(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;
        _timer = new Timer { Interval = (int)TimeSpan.FromMinutes(3).TotalMilliseconds };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start() => _timer.Start();

    private async Task CheckAsync()
    {
        if (!_session.IsLoggedIn)
        {
            return;
        }

        try
        {
            var result = await _api.GetMeAsync().ConfigureAwait(true);
            if (!result.Ok)
            {
                RealtimeLog.Write("[세션] 서버 세션이 더 이상 유효하지 않아 로그인 전 상태로 되돌립니다.");
                _session.Clear();
            }
        }
        catch
        {
            // 네트워크 일시 장애와 "정말로 세션이 끊긴 것"을 구분할 수 없으므로,
            // 조회 자체가 실패했을 때는 강제로 로그아웃 처리하지 않는다.
        }
    }

    public void Dispose() => _timer.Dispose();
}
