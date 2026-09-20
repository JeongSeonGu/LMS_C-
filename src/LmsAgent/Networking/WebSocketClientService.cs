using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using LmsAgent.Models.Realtime;

namespace LmsAgent.Networking;

/// <summary>
/// future-class.kr(WorkSupport) ↔ node2.future-class.kr(Node.js) 실시간 연동 클라이언트입니다.
///
/// 서버는 raw WebSocket으로 동작하며(Socket.IO 아님), 접속 경로는 반드시 "/ws-lms"입니다
/// (같은 서버의 "/ws"는 이 프로그램과 무관한 다른 프로그램 전용 엔드포인트이므로 붙으면 안 됩니다).
/// 접속에는 60초짜리 1회용 티켓이 필요하고(기기 토큰을 소켓에 직접 보내지 않음), 소켓으로는
/// "무엇이 바뀌었다"는 사실(domain.event)만 오므로 받으면 기존 HTTP API로 재조회해야 합니다.
/// 자세한 규약은 웹소켓_데이터통신규칙.md 참고.
/// </summary>
public sealed class WebSocketClientService : IAsyncDisposable
{
    private static readonly string[] Modules = { "calendar", "notice" };

    private readonly WorkSupportApiClient _api;
    private readonly AppSettings _settings;
    private readonly Dictionary<string, int> _revisions = new();
    private readonly Queue<string> _seenEventIds = new();
    private readonly HashSet<string> _seenEventIdSet = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _connectionLoopTask;
    private string? _myClientId;
    private int _consecutiveAuthFailures;

    public event Action<ConnectionState>? StateChanged;

    /// <summary>서버가 "이 scope의 데이터가 바뀌었다"고 알려올 때 발생합니다. UI 스레드로 마샬링해서 쓰세요.</summary>
    public event Action<string, string>? ScopeChanged; // (scope, type)

    public event Action<string>? LogMessage;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public WebSocketClientService(WorkSupportApiClient api, AppSettings settings)
    {
        _api = api;
        _settings = settings;
    }

    public void Start()
    {
        if (_connectionLoopTask is not null)
        {
            return;
        }

        _lifetimeCts = new CancellationTokenSource();
        _connectionLoopTask = Task.Run(() => ConnectionLoopAsync(_lifetimeCts.Token));
    }

    public async Task StopAsync()
    {
        _lifetimeCts?.Cancel();

        if (_connectionLoopTask is not null)
        {
            try
            {
                await _connectionLoopTask.ConfigureAwait(false);
            }
            catch
            {
                // 종료 과정의 예외는 무시합니다.
            }
        }

        await CloseSocketAsync().ConfigureAwait(false);
    }

    private async Task ConnectionLoopAsync(CancellationToken token)
    {
        var delaySeconds = 1.0;
        const double maxDelaySeconds = 30.0;

        while (!token.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(_settings.DeviceId) || string.IsNullOrWhiteSpace(_settings.DeviceTokenProtected))
            {
                // 기기 등록 전에는 실시간 연동을 시도하지 않는다(환경설정 > 실시간 연동에서 설정).
                SetState(ConnectionState.Disconnected);
                return;
            }

            try
            {
                SetState(ConnectionState.Connecting);

                var ticket = await IssueTicketAsync(token).ConfigureAwait(false);
                MergeRevisions(ticket.Revisions);
                _myClientId = ticket.ClientId;
                // 저장(HTTP POST) 요청에도 같은 clientId를 실어 보내 에코를 억제한다(연동가이드.md §4).
                _api.ClientId = ticket.ClientId;
                _consecutiveAuthFailures = 0;

                _socket = new ClientWebSocket();
                _socket.Options.SetRequestHeader("x-auth-token", ticket.Token);
                await _socket.ConnectAsync(new Uri(ticket.AgentUrl), token).ConfigureAwait(false);

                SetState(ConnectionState.Connected);
                delaySeconds = 1.0;

                await ReceiveLoopAsync(_socket, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _consecutiveAuthFailures++;
                LogMessage?.Invoke($"실시간 연동 접속 오류: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"실시간 연동 오류: {ex.Message}");
            }
            finally
            {
                SetState(ConnectionState.Disconnected);
                await CloseSocketAsync().ConfigureAwait(false);
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            if (_consecutiveAuthFailures >= 5)
            {
                // 인증이 반복 실패하면 설정 문제일 가능성이 크므로, 무한 재시도 대신 멈추고 로그만 남긴다.
                LogMessage?.Invoke("실시간 연동 인증이 반복 실패했습니다. 환경설정 > 실시간 연동의 기기 ID/토큰을 확인하세요.");
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
        }
    }

    private async Task<DeviceTicket> IssueTicketAsync(CancellationToken token)
    {
        var tokenPlain = DeviceTokenProtector.Unprotect(_settings.DeviceTokenProtected);
        if (string.IsNullOrEmpty(tokenPlain))
        {
            throw new InvalidOperationException("기기 토큰을 복호화할 수 없습니다. 환경설정에서 다시 입력하세요.");
        }

        var result = await _api.IssueDeviceTicketAsync(_settings.DeviceId!, tokenPlain).ConfigureAwait(false);
        if (!result.Ok || result.Data is null)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Message) ? "티켓 발급에 실패했습니다." : result.Message);
        }

        return result.Data;
    }

    /// <summary>재접속 시 서버 리비전이 로컬보다 크면(끊긴 동안 놓친 변경이 있으면) 갱신하고
    /// 알려서, 호출부(TrayApplicationContext)가 그 scope를 재조회하도록 한다.</summary>
    private void MergeRevisions(Dictionary<string, int> serverRevisions)
    {
        foreach (var (scope, serverRevision) in serverRevisions)
        {
            var hasLocal = _revisions.TryGetValue(scope, out var localRevision);
            _revisions[scope] = serverRevision;

            if (hasLocal && serverRevision > localRevision)
            {
                ScopeChanged?.Invoke(scope, "resync");
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        await SendAsync(socket, new { type = "subscribe", modules = Modules }, token).ConfigureAwait(false);

        var buffer = new byte[16 * 1024];

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            using var messageStream = new System.IO.MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            messageStream.Position = 0;

            ServerMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ServerMessage>(messageStream, JsonOptions);
            }
            catch (JsonException)
            {
                LogMessage?.Invoke("잘못된 형식의 메시지를 수신했습니다.");
                continue;
            }

            if (message is not null)
            {
                HandleMessage(message);
            }
        }
    }

    private void HandleMessage(ServerMessage message)
    {
        switch (message.Type)
        {
            case "rt.ready":
                LogMessage?.Invoke("실시간 연동 접속 완료");
                break;

            case "subscribed":
                var subscribed = message.Data?.Deserialize<SubscribedData>(JsonOptions);
                if (subscribed?.Denied is { Count: > 0 })
                {
                    LogMessage?.Invoke("실시간 연동 구독 거절: " + string.Join(",", subscribed.Denied));
                }
                break;

            case "domain.event":
                var ev = message.Data?.Deserialize<DomainEventData>(JsonOptions);
                if (ev is not null && ShouldHandle(ev))
                {
                    ScopeChanged?.Invoke(ev.Scope, ev.Type);
                }
                break;

            case "pong":
                break;

            default:
                // 모르는 type은 서버에 기능이 추가된 것일 수 있으므로 조용히 무시한다.
                break;
        }
    }

    /// <summary>웹소켓_데이터통신규칙.md §7의 리비전 처리 알고리즘. 반드시 이 순서(중복 → 에코 →
    /// 오래된 이벤트 → 리비전 먼저 갱신 → 재조회) 그대로 지켜야 데이터가 어긋나지 않는다.</summary>
    private bool ShouldHandle(DomainEventData ev)
    {
        if (!string.IsNullOrEmpty(ev.EventId))
        {
            if (_seenEventIdSet.Contains(ev.EventId))
            {
                return false; // 1. 중복 수신
            }

            _seenEventIds.Enqueue(ev.EventId);
            _seenEventIdSet.Add(ev.EventId);
            while (_seenEventIds.Count > 200)
            {
                _seenEventIdSet.Remove(_seenEventIds.Dequeue());
            }
        }

        if (ev.Origin?.ClientId is { Length: > 0 } originClientId && originClientId == _myClientId)
        {
            _revisions[ev.Scope] = ev.Revision; // 2. 내가 만든 변경의 메아리 — 리비전만 갱신하고 재조회는 생략
            return false;
        }

        if (_revisions.TryGetValue(ev.Scope, out var currentRevision) && ev.Revision <= currentRevision)
        {
            return false; // 3. 오래된(순서가 뒤바뀐) 이벤트
        }

        _revisions[ev.Scope] = ev.Revision; // 4. 재조회보다 먼저 갱신
        return true; // 5. 호출부에서 재조회
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static async Task SendAsync(ClientWebSocket socket, object message, CancellationToken token)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        await socket.SendAsync(json, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
    }

    private void SetState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private async Task CloseSocketAsync()
    {
        var socket = _socket;
        _socket = null;
        if (socket is null)
        {
            return;
        }

        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // 종료 중 발생하는 오류는 무시합니다.
        }
        finally
        {
            socket.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifetimeCts?.Dispose();
    }
}
