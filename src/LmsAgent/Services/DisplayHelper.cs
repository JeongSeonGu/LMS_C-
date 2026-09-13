using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LmsAgent.Services;

/// <summary>다중 모니터 선택 콤보박스에 쓰이는 항목.</summary>
public sealed record MonitorOption(int Index, string Label)
{
    public override string ToString() => Label;
}

public static class DisplayHelper
{
    public static List<MonitorOption> GetMonitorOptions()
    {
        var screens = Screen.AllScreens;
        var options = new List<MonitorOption>(screens.Length);

        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var suffix = s.Primary ? " - 주 모니터" : "";
            options.Add(new MonitorOption(i, $"모니터 {i + 1} ({s.Bounds.Width}x{s.Bounds.Height}){suffix}"));
        }

        return options;
    }

    /// <summary>설정된 인덱스가 현재 연결된 모니터 수를 벗어나면 주 모니터로 안전하게 보정합니다.</summary>
    public static Screen ResolveScreen(int index)
    {
        var screens = Screen.AllScreens;
        if (index >= 0 && index < screens.Length)
        {
            return screens[index];
        }

        return Screen.PrimaryScreen ?? screens[0];
    }
}
