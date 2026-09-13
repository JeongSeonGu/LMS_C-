using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Models;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.App;

/// <summary>
/// 프로그램은 별도의 메인 창 없이 트레이 아이콘으로만 상주합니다(실행 후 최소화 개념).
/// 트레이 아이콘의 컨텍스트 메뉴가 프로그램의 기본 메뉴 구성입니다:
///   학사 일정 &gt; 일정 등록 / 일정 목록
///   사용자 정보 &gt; 로그인 / 정보 수정
///   환경설정 / 업데이트 확인 / 종료
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly SessionManager _session = new();
    private readonly WebSocketClientService _wsClient;
    private readonly WorkSupportApiClient _api;
    private readonly DutyNotificationService _dutyService;
    private readonly ScheduleOverlayService _scheduleOverlayService;
    private readonly AutoPrintService _autoPrintService;
    private readonly NotifyIcon _trayIcon;

    private readonly ToolStripMenuItem _connectionStatusItem;
    private readonly ToolStripMenuItem _loginItem;
    private readonly ToolStripMenuItem _userInfoItem;

    // 백그라운드 스레드에서 발생하는 이벤트를 UI 스레드로 안전하게 전달하기 위한 숨김 핸들 소유자.
    private readonly Form _uiThreadHandle = new();

    public TrayApplicationContext(AppSettings settings)
    {
        _settings = settings;

        // 핸들을 강제로 생성해 Invoke/BeginInvoke가 즉시 동작하도록 합니다. 창은 표시하지 않습니다.
        _ = _uiThreadHandle.Handle;

        _wsClient = new WebSocketClientService(_settings.ServerUrl);
        _wsClient.StateChanged += OnConnectionStateChanged;
        _wsClient.TaskRequested += OnTaskRequested;

        _api = new WorkSupportApiClient(_settings.ServerUrl, _settings.ApiBaseUrlOverride);

        _dutyService = new DutyNotificationService(_api, _settings);
        _scheduleOverlayService = new ScheduleOverlayService(_api, _settings);
        _autoPrintService = new AutoPrintService(_api, _session, _settings);

        _session.SessionChanged += OnSessionChanged;

        var menu = new ContextMenuStrip();

        _connectionStatusItem = new ToolStripMenuItem("연결 상태: 연결 중...") { Enabled = false };
        menu.Items.Add(_connectionStatusItem);
        menu.Items.Add(new ToolStripSeparator());

        var scheduleMenu = new ToolStripMenuItem("학사 일정");
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("일정 등록...", null, OnScheduleRegisterClicked));
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("일정 목록...", null, OnScheduleListClicked));
        menu.Items.Add(scheduleMenu);

        var userMenu = new ToolStripMenuItem("사용자 정보");
        _loginItem = new ToolStripMenuItem("로그인...", null, OnLoginClicked);
        _userInfoItem = new ToolStripMenuItem("정보 수정...", null, OnUserInfoClicked) { Enabled = false };
        userMenu.DropDownItems.Add(_loginItem);
        userMenu.DropDownItems.Add(_userInfoItem);
        menu.Items.Add(userMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("환경설정...", null, OnOptionsClicked));
        menu.Items.Add(new ToolStripMenuItem("업데이트 확인...", null, OnCheckUpdateClicked));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("종료", null, OnExitClicked));

        _trayIcon = new NotifyIcon
        {
            Icon = AppIconProvider.Icon,
            Text = "LMS 연동 프로그램",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => OnUserInfoClicked(null, EventArgs.Empty);

        _wsClient.Start();
        _dutyService.Start();
        _autoPrintService.Start();
        _scheduleOverlayService.ApplySettings();
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiThreadHandle.InvokeRequired)
        {
            _uiThreadHandle.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        RunOnUiThread(() =>
        {
            _connectionStatusItem.Text = state switch
            {
                ConnectionState.Connected => "연결 상태: 연결됨",
                ConnectionState.Connecting => "연결 상태: 연결 중...",
                _ => "연결 상태: 연결 끊김",
            };

            _trayIcon.Text = state switch
            {
                ConnectionState.Connected => "LMS 연동 프로그램 - 연결됨",
                ConnectionState.Connecting => "LMS 연동 프로그램 - 연결 중",
                _ => "LMS 연동 프로그램 - 연결 끊김",
            };
        });
    }

    private void OnSessionChanged()
    {
        RunOnUiThread(() =>
        {
            _userInfoItem.Enabled = _session.IsLoggedIn;
            _loginItem.Text = _session.IsLoggedIn
                ? $"다시 로그인 ({_session.Profile?.Name})"
                : "로그인...";
        });
    }

    private void OnTaskRequested(WsEnvelope envelope)
    {
        var payload = envelope.GetPayload<TaskRequestPayload>();
        if (payload is null)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            using var form = new TaskRequestForm(payload);
            var accepted = form.ShowDialog() == DialogResult.Yes;

            var response = WsEnvelope.Create(MessageTypes.TaskResponse, new TaskResponsePayload
            {
                TaskId = payload.TaskId,
                Accepted = accepted,
                ResultMessage = accepted ? "작업을 수락했습니다." : "작업을 거절했습니다.",
            });

            _ = _wsClient.SendAsync(response);

            _trayIcon.ShowBalloonTip(3000, "작업 요청",
                accepted ? $"'{payload.Title}' 작업을 수락했습니다." : $"'{payload.Title}' 작업을 거절했습니다.",
                ToolTipIcon.Info);
        });
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        using var form = new LoginForm(_api, _session, _settings.SavedLoginId);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings.SavedLoginId = form.SaveLoginId ? form.LoginId : null;

        // 최초 로그인 시 학교명이 비어 있으면 서버에 등록된 학교명으로 채워준다.
        var schoolName = _session.Profile?.SchoolName;
        if (string.IsNullOrWhiteSpace(_settings.SchoolName) && !string.IsNullOrWhiteSpace(schoolName))
        {
            _settings.SchoolName = schoolName!;
        }

        SettingsStore.Save(_settings);
        _scheduleOverlayService.ApplySettings();
    }

    private void OnUserInfoClicked(object? sender, EventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "사용자 정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new UserInfoForm(_api, _session);
        form.ShowDialog();
    }

    private void OnScheduleRegisterClicked(object? sender, EventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new ScheduleRegisterForm(_api, _session);
        form.ShowDialog();
    }

    private void OnScheduleListClicked(object? sender, EventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new ScheduleListForm(_api, _session);
        form.ShowDialog();
    }

    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        using var form = new OptionsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            AutoStartManager.SetEnabled(_settings.AutoStartWithWindows);
            _scheduleOverlayService.ApplySettings();

            MessageBox.Show(
                "설정이 저장되었습니다. 웹소켓 서버/WorkSupport 서버 주소 변경 사항은 " +
                "프로그램을 다시 시작해야 적용됩니다.",
                "환경설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void OnCheckUpdateClicked(object? sender, EventArgs e)
    {
        var updateService = new UpdateService(_settings);
        var updateApplied = await Task.Run(() => updateService.CheckAndLaunchUpdaterIfAvailable());

        if (updateApplied)
        {
            _trayIcon.Visible = false;
            ExitThread();
        }
        else
        {
            MessageBox.Show("현재 최신 버전을 사용 중입니다.", "업데이트 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _dutyService.Dispose();
        _scheduleOverlayService.Dispose();
        _autoPrintService.Dispose();
        _api.Dispose();
        _ = _wsClient.StopAsync();
        ExitThread();
    }
}
