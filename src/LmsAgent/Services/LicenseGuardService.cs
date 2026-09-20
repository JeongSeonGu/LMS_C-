using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Networking;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>트레이 메뉴의 라이센스 상태 아이콘 표시에 사용하는 인증 결과입니다.</summary>
public enum LicenseStatus
{
    /// <summary>아직 확인하지 않았습니다(로그인 전 등).</summary>
    Unknown,

    /// <summary>서버(학교)에 인증키가 설정되어 있지 않아 검사를 건너뛰었습니다.</summary>
    NotConfigured,

    /// <summary>인증키가 일치합니다.</summary>
    Valid,

    /// <summary>인증키가 일치하지 않습니다.</summary>
    Invalid,
}

/// <summary>
/// 환경설정 &gt; 라이센스에 입력한 인증키를 서버의 학교 정보(auth_key)와 대조합니다.
/// 서버에 인증키가 설정되어 있지 않으면 검사하지 않고, 값이 다르면 경고 후 3분 뒤
/// 프로그램을 자동 종료합니다. 로그인 직후 호출하도록 되어 있습니다.
/// </summary>
public sealed class LicenseGuardService : IDisposable
{
    private readonly WorkSupportApiClient _api;
    private readonly AppSettings _settings;
    private readonly Timer _exitTimer;

    public LicenseStatus Status { get; private set; } = LicenseStatus.Unknown;

    /// <summary>상태가 바뀔 때마다 발생합니다. 트레이 메뉴의 상태 아이콘 갱신에 사용합니다.</summary>
    public event Action<LicenseStatus>? StatusChanged;

    public LicenseGuardService(WorkSupportApiClient api, AppSettings settings)
    {
        _api = api;
        _settings = settings;
        _exitTimer = new Timer { Interval = (int)TimeSpan.FromMinutes(3).TotalMilliseconds };
        _exitTimer.Tick += (_, _) =>
        {
            _exitTimer.Stop();
            Application.Exit();
        };
    }

    public async Task CheckAsync()
    {
        try
        {
            var result = await _api.GetSchoolInfoAsync().ConfigureAwait(true);
            var serverKey = result.Ok ? result.Data?.Info.AuthKey : null;

            if (string.IsNullOrWhiteSpace(serverKey))
            {
                // 서버(학교)에 인증키가 설정되어 있지 않으면 라이센스 검사를 하지 않습니다.
                SetStatus(LicenseStatus.NotConfigured);
                return;
            }

            if (string.Equals(serverKey.Trim(), _settings.LicenseKey?.Trim(), StringComparison.Ordinal))
            {
                _exitTimer.Stop();
                SetStatus(LicenseStatus.Valid);
                return;
            }

            MessageBox.Show(
                "인증키가 일치하지 않습니다. 환경설정 > 라이센스에서 올바른 인증키를 입력해주세요.\n" +
                "인증키가 확인되지 않으면 3분 후 프로그램이 자동으로 종료됩니다.",
                "라이센스 인증 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            _exitTimer.Stop();
            _exitTimer.Start();
            SetStatus(LicenseStatus.Invalid);
        }
        catch (Exception ex)
        {
            // 조회 실패(네트워크 오류 등)만으로는 강제 종료하지 않고, 상태도 그대로 둡니다.
            // 다만 원인을 알 수 없어 계속 "확인 전"으로 보이는 문제를 진단할 수 있도록 로그는 남긴다.
            RealtimeLog.Write($"[라이센스] 학교 정보 조회 실패: {ex.Message}");
        }
    }

    private void SetStatus(LicenseStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    public void Dispose() => _exitTimer.Dispose();
}
