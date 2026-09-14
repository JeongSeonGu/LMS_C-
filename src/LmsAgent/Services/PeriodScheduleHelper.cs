using System;
using System.Collections.Generic;
using System.Linq;
using LmsAgent.Configuration;

namespace LmsAgent.Services;

/// <summary>
/// 환경설정 &gt; 차시에 등록된 교시/점심시간 블록으로부터 쉬는 시간(빈 구간)을 계산합니다.
/// 환경설정 화면의 안내 표시와, 쉬는 시간 전자칠판 기능(BreakBoardService)이 함께 사용합니다.
/// </summary>
public static class PeriodScheduleHelper
{
    public static bool TryParseTime(string? value, out TimeOnly time)
    {
        if (!string.IsNullOrWhiteSpace(value) && TimeOnly.TryParse(value, out time))
        {
            return true;
        }

        time = default;
        return false;
    }

    /// <summary>시작 시각 기준으로 정렬한 블록 목록을 반환합니다. 시간 형식이 잘못된 항목은 제외합니다.</summary>
    public static List<PeriodSetting> SortedValid(IEnumerable<PeriodSetting> periods)
    {
        return periods
            .Where(p => TryParseTime(p.Start, out _) && TryParseTime(p.End, out _))
            .OrderBy(p => TimeOnly.Parse(p.Start))
            .ToList();
    }

    /// <summary>정렬된 블록들 사이의 빈 시간을 쉬는 시간으로 계산합니다(겹치거나 붙어 있으면 제외).</summary>
    public static List<(TimeOnly Start, TimeOnly End)> ComputeBreaks(IEnumerable<PeriodSetting> periods)
    {
        var sorted = SortedValid(periods);
        var breaks = new List<(TimeOnly Start, TimeOnly End)>();

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var end = TimeOnly.Parse(sorted[i].End);
            var nextStart = TimeOnly.Parse(sorted[i + 1].Start);
            if (nextStart > end)
            {
                breaks.Add((end, nextStart));
            }
        }

        return breaks;
    }

    /// <summary>지정한 시각이 쉬는 시간 안에 있으면 그 쉬는 시간 구간을 반환합니다.</summary>
    public static (TimeOnly Start, TimeOnly End)? FindCurrentBreak(IEnumerable<PeriodSetting> periods, TimeOnly now)
    {
        foreach (var b in ComputeBreaks(periods))
        {
            if (now >= b.Start && now < b.End)
            {
                return b;
            }
        }

        return null;
    }
}
