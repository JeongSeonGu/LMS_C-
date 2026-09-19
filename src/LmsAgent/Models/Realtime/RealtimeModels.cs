using System.Collections.Generic;
using System.Text.Json;

namespace LmsAgent.Models.Realtime;

/// <summary>
/// rt_ticket.php?mode=device 응답. 소켓 접속에 쓸 1회용(60초) 티켓입니다.
/// url/path/namespace 필드는 웹 브라우저(Socket.IO) 전용이라 여기서는 담지 않습니다.
/// </summary>
public sealed class DeviceTicket
{
    public string Token { get; set; } = "";
    public int ExpiresIn { get; set; }
    public string AgentUrl { get; set; } = "";
    public string? AgentPath { get; set; }
    public string ClientId { get; set; } = "";
    public int UserId { get; set; }
    public Dictionary<string, int> Revisions { get; set; } = new();
}

/// <summary>서버 → 클라이언트 메시지의 최상위 봉투. data는 type에 따라 다른 모양이라 지연 파싱한다.</summary>
public sealed class ServerMessage
{
    public string? Type { get; set; }
    public JsonElement? Data { get; set; }
}

/// <summary>domain.event의 data. payload에는 실제 업무 데이터가 들어오지 않으므로, 알림을 받으면
/// scope/type만 보고 기존 HTTP API를 다시 호출해 화면을 갱신해야 한다.</summary>
public sealed class DomainEventData
{
    public int V { get; set; }
    public string? EventId { get; set; }
    public string Type { get; set; } = "";
    public string Scope { get; set; } = "";
    public int Revision { get; set; }
    public string? Op { get; set; }
    public DomainEventOrigin? Origin { get; set; }
}

public sealed class DomainEventOrigin
{
    public string? ClientType { get; set; }
    public string? ClientId { get; set; }
}

public sealed class SubscribedData
{
    public List<string>? Modules { get; set; }
    public List<string>? Denied { get; set; }
}
