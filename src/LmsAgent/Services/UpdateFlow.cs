using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LmsAgent.Forms;

namespace LmsAgent.Services;

/// <summary>
/// "새 버전이 있습니다" 안내 → 사용자 동의 → 진행률 표시 → 적용까지의 전체 UX를 한 곳에서
/// 처리합니다. <see cref="UpdateService.CheckForUpdateAsync"/>가 <see cref="UpdateCheckResult.Available"/>을
/// 반환했을 때만 호출하세요.
///
/// Program.cs(아직 Application.Run을 부르지 않은 시점)와 트레이 메뉴("업데이트 확인...",
/// 이미 메시지 루프가 돌고 있는 시점) 양쪽에서 안전하게 쓸 수 있도록, async/await 대신
/// Application.DoEvents()로 직접 메시지 루프를 펌프하는 방식을 씁니다 — 이렇게 하면
/// 진행률 창이 실제로 그려지고 응답하는 것을, 메시지 루프가 아직 시작되지 않은 상황에서도
/// 보장할 수 있습니다.
/// </summary>
public static class UpdateFlow
{
    /// <summary>
    /// 안내 창을 보여주고, 사용자가 "지금 업데이트"를 선택하면 진행률 창을 띄운 채로
    /// 다운로드~적용까지 진행합니다. 적용에 성공하면 true를 반환하며, 이 경우 호출자는
    /// 트레이 아이콘을 숨기고 즉시 프로세스를 종료해야 합니다(내부적으로는 이미
    /// <see cref="UpdateService.DownloadAndApplyAsync"/>가 새 프로세스를 띄운 뒤이므로,
    /// 그냥 프로세스가 끝나 버려도 자연스럽습니다).
    /// </summary>
    public static bool Run(UpdateService service, UpdateCheckOutcome outcome)
    {
        var manifest = outcome.Manifest ?? throw new InvalidOperationException(
            "UpdateFlow.Run은 outcome.Result == Available일 때만 호출해야 합니다.");

        using var confirmForm = new UpdateAvailableForm(manifest, outcome.CurrentVersion!, outcome.LatestVersion!);
        if (confirmForm.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        using var progressForm = new UpdateProgressForm();
        _ = progressForm.Handle; // 핸들을 미리 만들어 BeginInvoke가 즉시 동작하도록 한다.
        progressForm.Show();
        progressForm.Refresh();

        var applyResult = UpdateCheckResult.DownloadFailed;
        var task = Task.Run(async () =>
        {
            applyResult = await service.DownloadAndApplyAsync(manifest, percent =>
            {
                if (!progressForm.IsDisposed)
                {
                    progressForm.BeginInvoke(() =>
                    {
                        if (!progressForm.IsDisposed)
                        {
                            progressForm.SetProgress(percent);
                        }
                    });
                }
            }).ConfigureAwait(false);
        });

        // 적용에 성공하면 UpdateService가 새 프로세스를 띄운 뒤 Environment.Exit(0)을 호출해
        // 이 프로세스 자체가 끝나 버리므로, 이 루프는 실패한 경우에만 실제로 빠져나온다.
        while (!task.IsCompleted)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }

        if (applyResult == UpdateCheckResult.Applied)
        {
            return true;
        }

        progressForm.Close();

        var message = applyResult == UpdateCheckResult.ChecksumMismatch
            ? "내려받은 파일의 체크섬이 manifest.json과 일치하지 않아 적용하지 않았습니다."
            : "새 버전 파일을 내려받지 못했습니다.";

        MessageBox.Show(
            message + "\n자세한 원인은 트레이 메뉴 \"실시간 연동 로그 열기...\"에 남아 있습니다.",
            "업데이트 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
}
