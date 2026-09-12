using System;

namespace LmsAgent.Models;

public sealed class ScheduleItem
{
    public string Title { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "일반";
}
