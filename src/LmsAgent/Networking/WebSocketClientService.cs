using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LmsAgent.Configuration;
using SocketIOClient;

namespace LmsAgent.Networking;

/// <summary>
/// node2.future-class.kr 서버와의 실시간 연동을 관리합니다.
///
/// 서버는 Socket.IO로 동작합니다(순수 System.Net.WebSockets 서버가 아닙니다). 다른 서비스들이
/// io.on('connection', socket => { socket.on('ClassVote', ...); socket.on('ClassVote_ms', ...); })
/// 형태로 연결/메세지 이벤트를 나누듯이, 이 프로그램은 "LMS_WindowAgent" 그룹으로 접속하고
/// "LMS_WindowAgent_ms" 이벤트로 메세지를 주고받습니다(nodejs-reference/ 참고).
/// 재연결은 SocketIOClient 라이브러리가 자동으로 처리합니다.
/// </summary>
public sealed class WebSocketClientService : IAsyncDisposable
{
    private const string JoinEvent = "LMS_WindowAgent";
    private const string MessageEvent = "LMS_WindowAgent_ms";

    private readonly Uri _serverUri;
    private readonly AppSettings _settings;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WsEnvelope>> _pending = new();

    private SocketIO? _client;

    public event Action<ConnectionState>? StateChanged;
    public event Action<WsEnvelope>? TaskRequested;
    public event Action<string>? LogMessage;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public WebSocketClientService(AppSettings settings)
    {
        _settings = settings;
        _serverUri = new Uri(settings.ServerUrl);
    }

    public void Start()
    {
        if (_client is not null)
        {
            return;
        }

        var client = new SocketIO(_serverUri, new SocketIOOptions
        {
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay = 2000,
            ReconnectionDelayMax = 30000,
            EIO = EngineIO.V4,
        });

        client.OnConnected += async (_, _) =>
        {
            SetState(ConnectionState.Connected);
            try
            {
                // ClassVote 등 다른 서비스들처럼, 연결 직후 그룹 참가용 이벤트를 한 번 보낸다.
                // Add_UserList(data, io, socket.id, "Connection_LMS_WindowAgent")가 이 payload를
                // userList에 등록하므로, 실제 Add_UserList 구현이 요구하는 필드명에 맞춰 조정하세요.
                await client.EmitAsync(JoinEvent, new
                {
                    schoolName = _settings.SchoolName,
                    licenseKey = _settings.LicenseKey,
                    deviceId = _settings.DeviceId,
                });
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"그룹 참가 메세지 전송 실패: {ex.Message}");
            }
        };

        client.OnDisconnected += (_, reason) =>
        {
            SetState(ConnectionState.Disconnected);
            LogMessage?.Invoke($"웹소켓 연결이 끊어졌습니다: {reason}");
        };

        client.OnReconnectAttempt += (_, _) => SetState(ConnectionState.Connecting);

        client.OnError += (_, error) => LogMessage?.Invoke($"웹소켓 오류: {error}");

        client.On(MessageEvent, response =>
        {
            WsEnvelope? envelope;
            try
            {
                envelope = response.GetValue<WsEnvelope>();
            }
            catch (JsonException)
            {
                LogMessage?.Invoke("잘못된 형식의 메시지를 수신했습니다.");
                return;
            }

            if (envelope is not null)
            {
                Dispatch(envelope);
            }
        });

        _client = client;
        SetState(ConnectionState.Connecting);
        _ = client.ConnectAsync();
    }

    public async Task StopAsync()
    {
        var client = _client;
        _client = null;
        if (client is null)
        {
            return;
        }

        try
        {
            await client.DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // 종료 과정의 예외는 무시합니다.
        }
        finally
        {
            client.Dispose();
        }

        SetState(ConnectionState.Disconnected);
    }

    private void Dispatch(WsEnvelope envelope)
    {
        if (_pending.TryRemove(envelope.Id, out var tcs))
        {
            tcs.TrySetResult(envelope);
            return;
        }

        switch (envelope.Type)
        {
            case MessageTypes.TaskRequest:
                TaskRequested?.Invoke(envelope);
                break;

            case MessageTypes.Ping:
                _ = SendAsync(WsEnvelope.Create(MessageTypes.Pong, envelope.Id));
                break;
        }
    }

    /// <summary>요청을 보내고, 같은 Id를 가진 응답이 올 때까지 기다립니다.</summary>
    public async Task<WsEnvelope> SendRequestAsync(WsEnvelope request, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<WsEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.Id] = tcs;

        try
        {
            await SendAsync(request).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(request.Id, out _);
            throw;
        }

        using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
        using var registration = timeoutCts.Token.Register(() =>
        {
            if (_pending.TryRemove(request.Id, out var pendingTcs))
            {
                pendingTcs.TrySetException(new TimeoutException("서버 응답 시간이 초과되었습니다."));
            }
        });

        return await tcs.Task.ConfigureAwait(false);
    }

    public async Task SendAsync(WsEnvelope envelope)
    {
        var client = _client;
        if (client is null || client.Connected != true)
        {
            throw new InvalidOperationException("서버에 연결되어 있지 않습니다.");
        }

        await client.EmitAsync(MessageEvent, envelope).ConfigureAwait(false);
    }

    private void SetState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
