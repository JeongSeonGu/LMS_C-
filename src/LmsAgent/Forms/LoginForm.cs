using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>사용자 정보 &gt; 사용자 로그인 메뉴에서 열리는 로그인 창입니다. 네이비·블루 모던
/// 디자인 목업(로그인 화면)을 그대로 옮긴 테두리 없는 커스텀 창입니다.</summary>
public sealed class LoginForm : Form
{
    private const int FormWidth = 400;
    private const int FormHeight = 580;

    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 36, BackColor = Color.White };
    private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 44, BackColor = UiTheme.ModernBackground };

    private readonly TextBox _idBox = new() { BorderStyle = BorderStyle.None, Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText };
    private readonly TextBox _passwordBox = new() { BorderStyle = BorderStyle.None, PasswordChar = '*', Font = UiTheme.ModernBodyFont, ForeColor = UiTheme.ModernText };
    private readonly Panel _passwordWrap = new() { BackColor = UiTheme.ModernAccent, Height = 44 };
    private readonly Button _togglePasswordButton = new() { Width = 40, Height = 40, Text = "👁", FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };

    private readonly Label _capsLockWarning = new()
    {
        AutoSize = true, Text = "⚠ Caps Lock이 켜져 있습니다.", ForeColor = UiTheme.ModernWarnText, Font = UiTheme.ModernSmallFont, Visible = false,
    };

    private readonly CheckBox _saveIdBox = new() { AutoSize = true, Text = "아이디 저장", Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernText, Checked = true };
    private readonly CheckBox _autoLoginBox = new() { AutoSize = true, Text = "Windows 시작 시 자동 로그인", Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernText };

    private readonly Button _loginButton = new() { Height = 48, Text = "로그인" };

    // 진단 메시지(응답 스니펫 포함)가 길어질 수 있어 스크롤/복사가 가능한 읽기 전용 텍스트박스로 표시한다.
    private readonly TextBox _statusLabel = new()
    {
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.White,
        ScrollBars = ScrollBars.Vertical,
        ForeColor = UiTheme.Danger,
        Font = UiTheme.ModernSmallFont,
        TabStop = false,
    };

    private readonly Label _connectionDot = new() { Width = 8, Height = 8, BackColor = UiTheme.ModernSuccessDot };
    private readonly Label _footerSchoolLabel = new() { AutoSize = true, ForeColor = UiTheme.ModernTextMuted, Font = UiTheme.ModernSmallFont };

    public bool SaveLoginId => _saveIdBox.Checked;
    public bool AutoLoginEnabled => _autoLoginBox.Checked;
    public string LoginId => _idBox.Text.Trim();

    /// <summary>자동 로그인 저장용 — 로그인에 성공했고 자동 로그인 체크박스가 켜져 있을 때만
    /// 호출자(TrayApplicationContext)가 이 값을 DPAPI로 암호화해 저장합니다.</summary>
    public string EnteredPassword => _passwordBox.Text;

    public LoginForm(WorkSupportApiClient api, SessionManager session, string? savedLoginId, bool autoLoginEnabled, string? schoolName)
    {
        _api = api;
        _session = session;

        Text = "사용자 로그인";
        Icon = AppIconProvider.Icon;
        BackColor = UiTheme.ModernFieldBorder; // 바깥쪽 1px 테두리처럼 보이는 배경색
        Padding = new Padding(1);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(FormWidth, FormHeight);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        Font = UiTheme.ModernBodyFont;

        BuildHeader();
        BuildBody();
        BuildFooter(schoolName);

        // Fill부터 먼저 추가해야 Top/Bottom 도킹 패널이 각자의 가장자리를 정확히 차지한다.
        Controls.Add(_body);
        Controls.Add(_footer);
        Controls.Add(_header);

        if (!string.IsNullOrWhiteSpace(savedLoginId))
        {
            _idBox.Text = savedLoginId;
            _saveIdBox.Checked = true;
        }

        _autoLoginBox.Checked = autoLoginEnabled;

        AcceptButton = _loginButton;

        _loginButton.Click += OnLoginClicked;

        UiTheme.EnableDragMove(_header, this);

        Shown += (_, _) => UpdateCapsLockWarning();
    }

    private void BuildHeader()
    {
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "LMS 연동 프로그램",
            ForeColor = UiTheme.ModernTextMuted,
            Font = UiTheme.ModernSmallFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
        };

        var minimizeButton = new Button { Width = 44, Height = 36, Text = "─", Margin = new Padding(0) };
        var closeButton = new Button { Width = 44, Height = 36, Text = "×", Font = new Font(UiTheme.BaseFont.FontFamily, 12F, FontStyle.Bold), Margin = new Padding(0) };
        UiTheme.StyleModernWindowButton(minimizeButton);
        UiTheme.StyleModernWindowButton(closeButton, isClose: true);
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();

        var rightCluster = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
        };
        rightCluster.Controls.Add(closeButton);
        rightCluster.Controls.Add(minimizeButton);

        _header.Controls.Add(titleLabel);
        _header.Controls.Add(rightCluster);

        UiTheme.EnableDragMove(titleLabel, this);
    }

    // ⚠ Panel.Padding은 Dock/Anchor/Flow로 배치되는 자식에게만 적용되고, 아래처럼 Left/Top을
    // 직접 지정하는 자식에게는 전혀 영향을 주지 않는다(조용히 무시된다) — 그래서 _body에
    // Padding(40, 16, ...)을 줘도 Left=0인 컨트롤은 패딩 없이 진짜 왼쪽 끝(x=0)에 붙어
    // 전체 내용이 왼쪽으로 쏠려 보였다. Padding에 기대지 않고, 아래 모든 Left 값에
    // ContentLeft(=40)를 직접 더해 오프셋을 준다.
    private const int ContentLeft = 40;
    private const int ContentTop = 16;
    private const int ContentWidth = 320;

    private void BuildBody()
    {
        var y = ContentTop;

        var iconBadge = new Panel { Left = ContentLeft, Top = y, Width = 48, Height = 48, BackColor = UiTheme.ModernAccent };
        UiTheme.ApplyRoundedRegion(iconBadge, 12);
        iconBadge.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Text = "🎓", Font = new Font(UiTheme.BaseFont.FontFamily, 18F), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter,
        });
        _body.Controls.Add(iconBadge);
        y += 48 + 14;

        var titleLabel = new Label
        {
            Left = ContentLeft, Top = y, AutoSize = true, Text = "교무학사 LMS 로그인", Font = UiTheme.ModernTitleFont, ForeColor = UiTheme.ModernText,
        };
        _body.Controls.Add(titleLabel);
        y += 30;

        var subtitleLabel = new Label
        {
            Left = ContentLeft, Top = y, Width = ContentWidth, Height = 38, Text = "웹 LMS와 같은 계정으로 로그인하면\n일정·알림이 이 PC로 자동 연동됩니다.",
            Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernTextMuted,
        };
        _body.Controls.Add(subtitleLabel);
        y += 38 + 28;

        var idLabel = new Label { Left = ContentLeft, Top = y, AutoSize = true, Text = "아이디", Font = UiTheme.ModernLabelBoldFont, ForeColor = UiTheme.ModernText };
        _body.Controls.Add(idLabel);
        y += 22;

        var idWrap = new Panel { Left = ContentLeft, Top = y, Width = ContentWidth, Height = 40, BackColor = UiTheme.ModernFieldBorder, Padding = new Padding(1) };
        _idBox.Dock = DockStyle.Fill;
        _idBox.BackColor = Color.White;
        var idInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(11, 11, 11, 0) };
        idInner.Controls.Add(_idBox);
        idWrap.Controls.Add(idInner);
        _body.Controls.Add(idWrap);
        y += 40 + 16;

        var pwLabel = new Label { Left = ContentLeft, Top = y, AutoSize = true, Text = "비밀번호", Font = UiTheme.ModernLabelBoldFont, ForeColor = UiTheme.ModernText };
        _body.Controls.Add(pwLabel);
        y += 22;

        _passwordWrap.Left = ContentLeft;
        _passwordWrap.Top = y;
        _passwordWrap.Width = ContentWidth;
        _passwordWrap.Padding = new Padding(2);
        var pwInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _passwordBox.Left = 10;
        _passwordBox.Top = 10;
        _passwordBox.Width = _passwordWrap.Width - 4 - 10 - 40;
        // pwInner는 아직 부모에 추가되지 않아 Dock=Fill이 적용되기 전이므로(기본 Width=100),
        // pwInner.Width를 그대로 읽으면 잘못된 값이 된다 — 이미 알고 있는 _passwordWrap의
        // 고정 폭(320, 테두리 Padding 2px씩 제외)을 기준으로 직접 계산한다.
        _togglePasswordButton.Left = _passwordWrap.Width - 4 - _togglePasswordButton.Width;
        _togglePasswordButton.Top = 0;
        _togglePasswordButton.FlatAppearance.BorderSize = 0;
        _togglePasswordButton.ForeColor = UiTheme.ModernTextMuted;
        _togglePasswordButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _togglePasswordButton.Click += (_, _) =>
        {
            _passwordBox.PasswordChar = _passwordBox.PasswordChar == '\0' ? '*' : '\0';
            _togglePasswordButton.Text = _passwordBox.PasswordChar == '\0' ? "🙈" : "👁";
        };
        pwInner.Controls.Add(_passwordBox);
        pwInner.Controls.Add(_togglePasswordButton);
        _passwordWrap.Controls.Add(pwInner);
        _body.Controls.Add(_passwordWrap);
        y += 40 + 4;

        _capsLockWarning.Left = ContentLeft;
        _capsLockWarning.Top = y;
        _body.Controls.Add(_capsLockWarning);
        _passwordBox.KeyUp += (_, _) => UpdateCapsLockWarning();
        y += 22;

        _saveIdBox.Left = ContentLeft;
        _saveIdBox.Top = y;
        _body.Controls.Add(_saveIdBox);

        var forgotLink = new LinkLabel
        {
            AutoSize = true, Top = y, Text = "비밀번호를 잊으셨나요?", Font = UiTheme.ModernSmallFont, LinkColor = UiTheme.ModernAccent,
        };
        forgotLink.Left = ContentLeft + ContentWidth - forgotLink.PreferredWidth;
        forgotLink.LinkClicked += (_, _) =>
            _statusLabel.Text = "비밀번호 재설정은 교무학사 웹 LMS 로그인 화면에서 진행해주세요.";
        _body.Controls.Add(forgotLink);
        y += 26;

        _autoLoginBox.Left = ContentLeft;
        _autoLoginBox.Top = y;
        _body.Controls.Add(_autoLoginBox);
        y += 26 + 10;

        _loginButton.Left = ContentLeft;
        _loginButton.Top = y;
        _loginButton.Width = ContentWidth;
        UiTheme.StyleModernPrimaryButton(_loginButton);
        _body.Controls.Add(_loginButton);
        y += 48 + 12;

        _statusLabel.Left = ContentLeft;
        _statusLabel.Top = y;
        _statusLabel.Width = ContentWidth;
        _statusLabel.Height = FormHeight - 36 - 44 - 16 - y;
        _body.Controls.Add(_statusLabel);
    }

    private void BuildFooter(string? schoolName)
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(20, 14, 0, 0),
        };

        UiTheme.ApplyRoundedRegion(_connectionDot, 4);
        var connectionLabel = new Label { AutoSize = true, Text = "LMS 서버 연결 가능", Font = UiTheme.ModernSmallFont, ForeColor = UiTheme.ModernTextMuted, Margin = new Padding(6, 2, 0, 0) };
        flow.Controls.Add(_connectionDot);
        flow.Controls.Add(connectionLabel);

        _footerSchoolLabel.Text = string.IsNullOrWhiteSpace(schoolName) ? "v[버전]" : $"{schoolName} · v[버전]";
        var schoolHost = new FlowLayoutPanel { Dock = DockStyle.Right, WrapContents = false, AutoSize = true, Padding = new Padding(0, 16, 20, 0) };
        schoolHost.Controls.Add(_footerSchoolLabel);

        _footer.Controls.Add(flow);
        _footer.Controls.Add(schoolHost);
    }

    private void UpdateCapsLockWarning() => _capsLockWarning.Visible = Control.IsKeyLocked(Keys.CapsLock);

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var loginId = _idBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrEmpty(loginId) || string.IsNullOrEmpty(password))
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = "아이디와 비밀번호를 입력하세요.";
            return;
        }

        _loginButton.Enabled = false;
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = "로그인 중...";

        try
        {
            var result = await _api.LoginAsync(loginId, password);

            if (!result.Ok || result.Data?.User is null)
            {
                _statusLabel.ForeColor = UiTheme.Danger;
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "로그인에 실패했습니다."
                    : result.ErrorMessage;
                return;
            }

            _session.SetSession(result.Data.User);

            _statusLabel.ForeColor = UiTheme.Success;
            _statusLabel.Text = "담당업무 정보를 불러오는 중...";

            await _session.RefreshDepartmentContextAsync(_api);

            _statusLabel.Text = "로그인 성공";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusLabel.Text = $"로그인 오류: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }
    }
}
