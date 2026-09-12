using System;
using System.Text.Json;

namespace LmsAgent.Networking;

/// <summary>
/// 서버와 주고받는 모든 웹소켓 메시지의 공통 봉투(envelope) 형식입니다.
/// { "type": "auth.login", "id": "...", "payload": { ... } } 형태의 JSON으로 직렬화됩니다.
/// </summary>
public sealed class WsEnvelope
{
    public string Type { get; set; } = "";

    /// <summary>요청/응답을 짝지어 주는 상관관계 ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public JsonElement? Payload { get; set; }

    public T? GetPayload<T>()
    {
        if (Payload is null)
        {
            return default;
        }

        return Payload.Value.Deserialize<T>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
    }

    public static WsEnvelope Create<T>(string type, T payload, string? id = null)
    {
        return new WsEnvelope
        {
            Type = type,
            Id = id ?? Guid.NewGuid().ToString("N"),
            Payload = JsonSerializer.SerializeToElement(payload),
        };
    }

    public static WsEnvelope Create(string type, string? id = null)
    {
        return new WsEnvelope
        {
            Type = type,
            Id = id ?? Guid.NewGuid().ToString("N"),
        };
    }
}
