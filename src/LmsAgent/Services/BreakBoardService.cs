using System;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Forms;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 차시에서 계산된 쉬는 시간에 맞춰 전자칠판 페이지를 자동으로 열고 닫습니다.
/// 환경설정 &gt; 일반의 "쉬는 시간 전자칠판 페이지 활성화"가 켜져 있어야 동작하며,
/// "쉬는 시간 종료안내"가 켜져 있으면 쉬는 시간이 끝나기 2분 전에 카운트다운을 보여줍니다.
/// </summary>
public sealed class BreakBoardService : IDisposable
{
    private static readonly TimeSpan CountdownLeadTime = TimeSpan.FromMinutes(2);

    private readonly AppSettings _settings;
    private readonly Timer _timer;

    private BreakBoardForm? _board;
    private BreakCountdownForm? _countdown;
    private (TimeOnly Start, TimeOnly End)? _currentBreak;
    private bool _countdownShownForCurrentBreak;

    public BreakBoardService(AppSettings settings)
    {
        _settings = settings;
        _timer = new Timer { Interval = 15_000 };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _timer.Start();
        Tick();
    }

    public void Stop()
    {
        _timer.Stop();
        HideAll();
    }

    private void HideAll()
    {
        _board?.Hide();
        _countdown?.HideCountdown();
        _currentBreak = null;
    }

    private void Tick()
    {
        if (!_settings.BreakBoardEnabled
            || string.IsNullOrWhiteSpace(_settings.SmartBoardPageUrl)
            || _settings.Periods.Count == 0)
        {
            HideAll();
            return;
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var activeBreak = PeriodScheduleHelper.FindCurrentBreak(_settings.Periods, now);

        if (activeBreak is null)
        {
            HideAll();
            return;
        }

        if (_currentBreak != activeBreak)
        {
            _currentBreak = activeBreak;
            _countdownShownForCurrentBreak = false;
        }

        _board ??= new BreakBoardForm();
        _board.ShowOn(DisplayHelper.ResolveScreen(_settings.BreakBoardMonitorIndex), _settings.SmartBoardPageUrl!);

        if (_settings.BreakEndCountdownEnabled && !_countdownShownForCurrentBreak)
        {
            var breakEndAt = DateTime.Today + activeBreak.Value.End.ToTimeSpan();
            var remaining = breakEndAt - DateTime.Now;

            if (remaining > TimeSpan.Zero && remaining <= CountdownLeadTime)
            {
                _countdownShownForCurrentBreak = true;
                _countdown ??= new BreakCountdownForm();
                _countdown.ShowCountdown(DisplayHelper.ResolveScreen(_settings.BreakBoardMonitorIndex), breakEndAt);
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _board?.Dispose();
        _countdown?.Dispose();
    }
}
