using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using LmsAgent.Models.Realtime;
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
    private readonly LicenseGuardService _licenseGuardService;
    private readonly BreakBoardService _breakBoardService;
    private readonly ScheduleReminderService _scheduleReminderService;
    private readonly WorkJournalService _workJournalService;
    private readonly MyDutyChangeService _myDutyChangeService;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly NotifyIcon _trayIcon;

    // "학사달력보기"/"관리자 복무상황 보기" 단축키 토글용 인스턴스(트레이 메뉴의 모달 흐름과는 별개).
    private ScheduleListForm? _scheduleHotkeyForm;
    private DutyRegisterForm? _dutyHotkeyForm;

    private readonly ToolStripMenuItem _connectionStatusItem;
    private readonly ToolStripMenuItem _licenseStatusItem;

    // 연결 상태/라이센스 상태 항목은 Enabled=false(클릭 방지용)라서 ToolStripProfessionalRenderer가
    // 기본적으로 회색으로만 그린다. RenderItemText에서 이 색으로 직접 덮어써서
    // 연결됨(초록)/연결 끊김(빨강), 인증됨(초록)/인증 실패(빨강)이 눈에 띄도록 한다.
    private Color _connectionStatusColor = UiTheme.TextSecondary;
    private Color _licenseStatusColor = UiTheme.TextSecondary;
    private readonly ToolStripMenuItem _loginItem;
    private readonly ToolStripMenuItem _userInfoItem;

    // 백그라운드 스레드에서 발생하는 이벤트를 UI 스레드로 안전하게 전달하기 위한 숨김 핸들 소유자.
    private readonly Form _uiThreadHandle = new();

    public TrayApplicationContext(AppSettings settings)
    {
        _settings = settings;

        // 핸들을 강제로 생성해 Invoke/BeginInvoke가 즉시 동작하도록 합니다. 창은 표시하지 않습니다.
        _ = _uiThreadHandle.Handle;

        _api = new WorkSupportApiClient(_settings.ServerUrl, _settings.ApiBaseUrlOverride);

        _wsClient = new WebSocketClientService(_api, _settings);
        _wsClient.StateChanged += OnConnectionStateChanged;
        _wsClient.ScopeChanged += OnRealtimeScopeChanged;
        _wsClient.DomainEventReceived += OnDomainEventReceived;
        _wsClient.LogMessage += RealtimeLog.Write;

        _dutyService = new DutyNotificationService(_api, _settings);
        _scheduleOverlayService = new ScheduleOverlayService(_api, _session, _settings);
        _autoPrintService = new AutoPrintService(_api, _session, _settings);
        _licenseGuardService = new LicenseGuardService(_api, _settings);
        _breakBoardService = new BreakBoardService(_settings);
        _scheduleReminderService = new ScheduleReminderService(_api, _session, _settings);
        _workJournalService = new WorkJournalService(_api, _session, _settings);
        _myDutyChangeService = new MyDutyChangeService(_api, _session);

        _session.SessionChanged += OnSessionChanged;
        _session.ScheduleChanged += OnScheduleChanged;
        _licenseGuardService.StatusChanged += OnLicenseStatusChanged;
        _scheduleReminderService.ReminderRaised += OnScheduleReminderRaised;
        _myDutyChangeService.Notify += OnMyDutyChangeNotify;

        var menu = new ContextMenuStrip { Renderer = UiTheme.CreateMenuRenderer(), Font = UiTheme.BaseFont };
        menu.Renderer.RenderItemText += (_, e) =>
        {
            if (ReferenceEquals(e.Item, _connectionStatusItem))
            {
                e.TextColor = _connectionStatusColor;
            }
            else if (ReferenceEquals(e.Item, _licenseStatusItem))
            {
                e.TextColor = _licenseStatusColor;
            }
        };

        _connectionStatusItem = new ToolStripMenuItem("연결 상태: 연결 중...") { Enabled = false };
        menu.Items.Add(_connectionStatusItem);

        _licenseStatusItem = new ToolStripMenuItem("라이센스: 확인 전")
        {
            Enabled = false,
            Image = UiTheme.CreateStatusIcon(UiTheme.TextSecondary, null),
        };
        menu.Items.Add(_licenseStatusItem);

        menu.Items.Add(new ToolStripMenuItem("교무업무 페이지", null, OnWorkSupportPageClicked));
        menu.Items.Add(new ToolStripSeparator());

        var scheduleMenu = new ToolStripMenuItem("학사 일정");
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("일정 등록...", null, OnScheduleRegisterClicked));
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("일정 목록...", null, OnScheduleListClicked));
        scheduleMenu.DropDownItems.Add(new ToolStripSeparator());
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("복무등록...", null, OnDutyRegisterClicked));
        scheduleMenu.DropDownItems.Add(new ToolStripMenuItem("할일등록...", null, OnTodoRegisterClicked));
        menu.Items.Add(scheduleMenu);

        var userMenu = new ToolStripMenuItem("사용자 정보");
        _loginItem = new ToolStripMenuItem("로그인...", null, OnLoginClicked);
        _userInfoItem = new ToolStripMenuItem("정보 수정...", null, OnUserInfoClicked) { Enabled = false };
        userMenu.DropDownItems.Add(_loginItem);
        userMenu.DropDownItems.Add(_userInfoItem);
        menu.Items.Add(userMenu);

        var basicInfoMenu = new ToolStripMenuItem("기본정보");
        basicInfoMenu.DropDownItems.Add(new ToolStripMenuItem("요청사항", null, OnRequestsClicked));
        basicInfoMenu.DropDownItems.Add(new ToolStripMenuItem("학교기본정보", null, OnSchoolInfoClicked));
        basicInfoMenu.DropDownItems.Add(new ToolStripMenuItem("공통계정", null, OnSharedAccountsClicked));
        basicInfoMenu.DropDownItems.Add(new ToolStripMenuItem("협의사항", null, OnMeetingsClicked));
        basicInfoMenu.DropDownItems.Add(new ToolStripMenuItem("길라잡이 조회", null, OnGuideDocsClicked));
        menu.Items.Add(basicInfoMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("환경설정...", null, OnOptionsClicked));
        menu.Items.Add(new ToolStripMenuItem("업데이트 확인...", null, OnCheckUpdateClicked));
        menu.Items.Add(new ToolStripMenuItem("실시간 연동 로그 열기...", null, OnOpenRealtimeLogClicked));
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
        _breakBoardService.Start();
        _scheduleReminderService.Start();
        _workJournalService.ApplySettings();
        RegisterHotkeys();
    }

    /// <summary>단축키 설정이 바뀔 때(환경설정 저장 시)마다 전부 해제 후 다시 등록합니다.</summary>
    private void RegisterHotkeys()
    {
        _hotkeyService.UnregisterAll();
        _hotkeyService.Register(_settings.ScheduleViewHotkey, ToggleScheduleListWindow);
        _hotkeyService.Register(_settings.DutyStatusViewHotkey, ToggleDutyStatusWindow);
        _hotkeyService.Register(_settings.TaskJournalViewHotkey, () => _workJournalService.ToggleVisible());
    }

    private void ToggleScheduleListWindow()
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_scheduleHotkeyForm is null || _scheduleHotkeyForm.IsDisposed)
        {
            _scheduleHotkeyForm = new ScheduleListForm(_api, _session);
            _scheduleHotkeyForm.FormClosing += (_, e) =>
            {
                e.Cancel = true;
                _scheduleHotkeyForm?.Hide();
            };
            _scheduleHotkeyForm.Show();
            return;
        }

        if (_scheduleHotkeyForm.Visible)
        {
            _scheduleHotkeyForm.Hide();
        }
        else
        {
            _scheduleHotkeyForm.Show();
            _scheduleHotkeyForm.Activate();
        }
    }

    private void ToggleDutyStatusWindow()
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "복무상황", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_dutyHotkeyForm is null || _dutyHotkeyForm.IsDisposed)
        {
            _dutyHotkeyForm = new DutyRegisterForm(_api);
            _dutyHotkeyForm.FormClosing += (_, e) =>
            {
                e.Cancel = true;
                _dutyHotkeyForm?.Hide();
            };
            _dutyHotkeyForm.Show();
            return;
        }

        if (_dutyHotkeyForm.Visible)
        {
            _dutyHotkeyForm.Hide();
        }
        else
        {
            _dutyHotkeyForm.Show();
            _dutyHotkeyForm.Activate();
        }
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

            _connectionStatusColor = state switch
            {
                ConnectionState.Connected => UiTheme.Success,
                ConnectionState.Connecting => UiTheme.TextSecondary,
                _ => UiTheme.Danger,
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

    private void OnScheduleReminderRaised(string title, string message)
    {
        RunOnUiThread(() => _trayIcon.ShowBalloonTip(6000, title, message, ToolTipIcon.Info));
    }

    private void OnLicenseStatusChanged(LicenseStatus status)
    {
        RunOnUiThread(() =>
        {
            var (text, color, mark) = status switch
            {
                LicenseStatus.Valid => ("라이센스: 인증됨", UiTheme.Success, (bool?)true),
                LicenseStatus.Invalid => ("라이센스: 인증 실패", UiTheme.Danger, (bool?)false),
                LicenseStatus.NotConfigured => ("라이센스: 미설정", UiTheme.TextSecondary, (bool?)null),
                _ => ("라이센스: 확인 전", UiTheme.TextSecondary, (bool?)null),
            };

            _licenseStatusItem.Text = text;
            _licenseStatusColor = color;
            _licenseStatusItem.Image?.Dispose();
            _licenseStatusItem.Image = UiTheme.CreateStatusIcon(color, mark);
        });
    }

    /// <summary>
    /// 학사 일정이 Windows 쪽에서 등록/수정/삭제되었을 때 호출됩니다. 배경화면 오버레이를
    /// 30분 타이머를 기다리지 않고 바로 갱신합니다. 웹 브라우저 쪽 새로고침은 별도로 알릴 필요가
    /// 없습니다 — 이 변경도 기존 HTTP API를 통해 이루어지므로, 서버가 DB 커밋 시점에 자동으로
    /// domain.event를 발행해 웹 클라이언트에게 전달합니다(웹소켓_데이터통신규칙.md 참고).
    /// </summary>
    private void OnScheduleChanged()
    {
        _scheduleOverlayService.RefreshNow();
        _workJournalService.RefreshNow();
    }

    /// <summary>
    /// 실시간 연동 서버로부터 "이 scope가 바뀌었다"는 알림을 받았을 때 호출됩니다(웹 브라우저 등
    /// 다른 클라이언트가 변경한 경우 포함). payload에는 실제 데이터가 없으므로, type을 보고
    /// 필요한 기존 화면/서비스만 즉시 다시 조회하도록 합니다.
    /// </summary>
    private void OnRealtimeScopeChanged(string scope, string type)
    {
        if (scope != "calendar")
        {
            // 현재 구독 중인 모듈은 calendar/notice뿐이며, notice는 별도 처리 화면이 없다.
            return;
        }

        RunOnUiThread(() =>
        {
            if (type.StartsWith("work.calendar.duty", StringComparison.Ordinal))
            {
                _dutyService.RefreshNow();
            }
            else
            {
                // work.calendar.event(학사 일정), work.calendar.todo(할일), resync(재동기화) 등은
                // 배경화면 학사달력 오버레이를 즉시 갱신한다.
                _scheduleOverlayService.RefreshNow();
            }

            // 할일/알림 대상 일정도 업무 일지에 반영되므로 항상 함께 갱신한다.
            _workJournalService.RefreshNow();
        });
    }

    /// <summary>
    /// domain.event 원본을 그대로 받아, 학사 일정의 담당업무(deptId)가 바뀌어 "내 업무"가
    /// 되었거나 빠졌는지 판단합니다(웹소켓_데이터통신규칙.md §7-A). 배경화면 오버레이·업무 일지
    /// 갱신 자체는 위 OnRealtimeScopeChanged에서 이미 이루어지므로, 여기서는 트레이 알림만 만듭니다.
    /// </summary>
    private void OnDomainEventReceived(DomainEventData ev)
    {
        RunOnUiThread(() => _myDutyChangeService.HandleDomainEvent(ev));
    }

    private void OnMyDutyChangeNotify(string message)
    {
        RunOnUiThread(() => _trayIcon.ShowBalloonTip(6000, "담당업무 변경 알림", message, ToolTipIcon.Info));
    }

    private void OnWorkSupportPageClicked(object? sender, EventArgs e)
    {
        var url = _settings.WorkSupportPageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("교무업무 페이지 주소가 설정되어 있지 않습니다. 환경설정 > 네트워크에서 설정해주세요.",
                "교무업무 페이지", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"교무업무 페이지를 열 수 없습니다: {ex.Message}",
                "교무업무 페이지", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnLoginClicked(object? sender, EventArgs e)
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
        // 업무 일지는 로그인 계정(담당업무) 기준으로 필터링하므로, 로그인 전에는 조회를
        // 건너뛰고 비어 있는 채로 남아 있었다. 로그인 직후 반드시 한 번 다시 조회해야 한다.
        _workJournalService.RefreshNow();
        _ = _licenseGuardService.CheckAsync();

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_settings.ShowStartupNoticeModal && _settings.StartupNoticeSuppressedDate != today)
        {
            using var summary = new StartupSummaryForm(_api);
            summary.ShowDialog();

            if (summary.SuppressToday)
            {
                _settings.StartupNoticeSuppressedDate = today;
                SettingsStore.Save(_settings);
            }
        }
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

    private void OnDutyRegisterClicked(object? sender, EventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "복무등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new DutyRegisterForm(_api);
        form.ShowDialog();
    }

    private void OnTodoRegisterClicked(object? sender, EventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            MessageBox.Show("먼저 로그인해주세요.", "할일등록", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new TodoRegisterForm(_api, _session);
        form.ShowDialog();
    }

    private bool RequireLoginForBasicInfo()
    {
        if (_session.IsLoggedIn)
        {
            return true;
        }

        MessageBox.Show("먼저 로그인해주세요.", "기본정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void OnRequestsClicked(object? sender, EventArgs e)
    {
        if (!RequireLoginForBasicInfo()) return;
        using var form = new RequestsForm(_api);
        form.ShowDialog();
    }

    private void OnSchoolInfoClicked(object? sender, EventArgs e)
    {
        if (!RequireLoginForBasicInfo()) return;
        using var form = new SchoolInfoForm(_api);
        form.ShowDialog();
    }

    private void OnSharedAccountsClicked(object? sender, EventArgs e)
    {
        if (!RequireLoginForBasicInfo()) return;
        using var form = new SharedAccountsForm(_api);
        form.ShowDialog();
    }

    private void OnMeetingsClicked(object? sender, EventArgs e)
    {
        if (!RequireLoginForBasicInfo()) return;
        using var form = new MeetingsForm(_api);
        form.ShowDialog();
    }

    private void OnGuideDocsClicked(object? sender, EventArgs e)
    {
        if (!RequireLoginForBasicInfo()) return;
        using var form = new GuideDocsForm(_api);
        form.ShowDialog();
    }

    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        using var form = new OptionsForm(_settings, _api);
        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            AutoStartManager.SetEnabled(_settings.AutoStartWithWindows);
            _scheduleOverlayService.ApplySettings();
            _workJournalService.ApplySettings();
            RegisterHotkeys();

            // 라이센스 인증키는 로그인 직후에만 검사했었는데, 이미 로그인한 상태에서
            // 환경설정으로 인증키를 새로 입력/수정하는 경우가 더 흔하므로 저장 시에도 다시 검사한다.
            if (_session.IsLoggedIn)
            {
                _ = _licenseGuardService.CheckAsync();
            }

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

    private void OnOpenRealtimeLogClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!System.IO.File.Exists(RealtimeLog.FilePath))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RealtimeLog.FilePath)!);
                System.IO.File.WriteAllText(RealtimeLog.FilePath, "");
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RealtimeLog.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"로그 파일을 열 수 없습니다: {ex.Message}\n경로: {RealtimeLog.FilePath}",
                "실시간 연동 로그", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _dutyService.Dispose();
        _scheduleOverlayService.Dispose();
        _autoPrintService.Dispose();
        _licenseGuardService.Dispose();
        _breakBoardService.Dispose();
        _scheduleReminderService.Dispose();
        _workJournalService.Dispose();
        _hotkeyService.Dispose();
        _scheduleHotkeyForm?.Dispose();
        _dutyHotkeyForm?.Dispose();
        _api.Dispose();
        _ = _wsClient.StopAsync();
        ExitThread();
    }
}
