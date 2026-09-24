using System;

namespace LmsAgent.Models;

/// <summary>
/// 사용자가 직접 기록하는 개인일정 한 건. 학사 일정과 달리 서버(DB)에 전혀 저장되지 않고
/// 이 PC의 로컬 파일(<see cref="LmsAgent.Services.PersonalScheduleStore"/>)에만 남습니다.
/// </summary>
public sealed class PersonalScheduleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Date { get; set; }

    /// <summary>특정 시각이 있는 일정이면 시작 시각. 종일 일정이면 null.</summary>
    public TimeSpan? Time { get; set; }

    public string Title { get; set; } = "";

    public string? Note { get; set; }
}
