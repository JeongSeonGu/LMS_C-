using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LmsAgent.Networking;

namespace LmsAgent.Services;

/// <summary>
/// 이 서비스 안의 상대경로(예: <c>/SchoolWork/WorkSupport/features/requests.html?id=31</c>)를,
/// C#이 로그인되어 있으면 SSO 1회용 티켓으로 자동 로그인하며 연다(SSO 자동 로그인 적용
/// 안내.md §5-7). 업무 일지 항목 클릭(<see cref="WorkJournalService"/>)과 알림 토스트 클릭
/// (<see cref="LmsAgent.App.TrayApplicationContext"/>) 양쪽에서 똑같은 로직이 필요해서
/// 공통으로 뺐다 — 로그인 전이거나 티켓 발급이 실패하면 평소처럼(SSO 없이) 그대로 연다.
/// </summary>
public static class SsoLinkOpener
{
    public static async Task OpenAsync(WorkSupportApiClient api, SessionManager session, string relativeLink)
    {
        var absoluteUrl = relativeLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeLink
            : "https://future-class.kr" + relativeLink;

        if (session.IsLoggedIn)
        {
            try
            {
                var ticket = await api.GetSsoTicketAsync().ConfigureAwait(true);
                if (ticket.Ok && !string.IsNullOrWhiteSpace(ticket.Data?.LoginUrl))
                {
                    // ⚠ next는 반드시 이 서비스 안의 "상대경로"여야 한다 — 서버가 오픈
                    // 리다이렉트 방지를 위해 스킴이 있는 절대 URL은 전부 무시하고 대시보드로
                    // 보낸다. 서버가 이미 상대경로로 내려준 링크는 그대로 쓰고, 혹시 절대
                    // URL로 들어오면 경로+쿼리만 추려서 보낸다.
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
            // 브라우저를 열지 못해도 호출한 쪽의 나머지 기능은 계속 동작해야 한다.
        }
    }
}
