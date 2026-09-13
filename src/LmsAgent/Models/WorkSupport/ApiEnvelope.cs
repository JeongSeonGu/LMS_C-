using System.Text.Json.Serialization;

namespace LmsAgent.Models.WorkSupport;

/// <summary>
/// WorkSupport PHP API의 공통 응답 봉투.
/// 엔드포인트에 따라 오류 메시지 키가 "message"(ws_json_out) 또는 "msg"(jsonOut)로
/// 다르게 내려오므로 둘 다 받아서 <see cref="ErrorMessage"/>로 통일한다.
/// </summary>
public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonIgnore]
    public string ErrorMessage => !string.IsNullOrWhiteSpace(Message) ? Message! : (Msg ?? "");
}
