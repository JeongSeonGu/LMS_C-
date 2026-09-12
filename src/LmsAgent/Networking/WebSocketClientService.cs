using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LmsAgent.Networking;

/// <summary>
/// node2.future-class.kr 웹소켓 서버와의 연결을 관리합니다.
/// 연결이 끊어지면 지수 백오프로 자동 재연결하며, 요청/응답 상관관계 매칭과
/// 서버가 먼저 보내는 작업 요청(push) 수신을 함께 처리합니다.
/// </summary>
public sealed class WebSocketClientService : IAsyncDisposable
{
    private readonly Uri _serverUri;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WsEnvelope>> _pending = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _connectionLoopTask;

    public event Action<ConnectionState>? StateChanged;
    public event Action<WsEnvelope>? TaskRequested;
    public event Action<string>? LogMessage;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public WebSocketClientService(string serverUrl)
    {
        _serverUri = new Uri(serverUrl);
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
        var backoffSeconds = 2.0;
        const double maxBackoffSeconds = 30.0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                SetState(ConnectionState.Connecting);

                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(_serverUri, token).ConfigureAwait(false);

                SetState(ConnectionState.Connected);
                backoffSeconds = 2.0;

                await ReceiveLoopAsync(_socket, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"웹소켓 오류: {ex.Message}");
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

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            backoffSeconds = Math.Min(backoffSeconds * 2, maxBackoffSeconds);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            using var messageStream = new MemoryStream();
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

            WsEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<WsEnvelope>(messageStream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (JsonException)
            {
                LogMessage?.Invoke("잘못된 형식의 메시지를 수신했습니다.");
                continue;
            }

            if (envelope is not null)
            {
                Dispatch(envelope);
            }
        }
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
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("서버에 연결되어 있지 않습니다.");
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(envelope);
        await socket.SendAsync(json, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
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
