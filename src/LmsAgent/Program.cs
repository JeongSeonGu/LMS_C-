using System;
using System.Threading;
using System.Windows.Forms;
using LmsAgent.App;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent;

internal static class Program
{
    // 중복 실행 방지용 뮤텍스 이름 (프로그램 전용 GUID)
    private const string SingleInstanceMutexName = "Global\\LmsAgent-6C1B8E1E-6C3B-4B0F-9C0B-70B6F5B6B0B0";

    [STAThread]
    private static void Main(string[] args)
    {
        using var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("LMS 연동 프로그램이 이미 실행 중입니다.", "LMS Agent",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settings = SettingsStore.Load();

        // 매 실행마다 업데이트 서버를 확인합니다. 새 버전이 있으면 안내 창을 띄워 사용자가
        // 지금 업데이트할지 직접 선택하게 하고, 동의하면 진행률 창을 보여주며 내려받아
        // 적용한 뒤 갱신된 프로그램을 재시작합니다. 이 경우 현재 프로세스는 바로 종료합니다.
        var updateService = new UpdateService(settings);
        try
        {
            var outcome = updateService.CheckForUpdateAsync().GetAwaiter().GetResult();
            if (outcome.Result == UpdateCheckResult.Available && UpdateFlow.Run(updateService, outcome))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            RealtimeLog.Write($"[업데이트] 시작 시 확인 중 예외 발생: {ex.Message}");
        }

        // 업데이트가 없으면 트레이(최소화 상태)로 상시 실행합니다.
        Application.Run(new TrayApplicationContext(settings));
    }
}
