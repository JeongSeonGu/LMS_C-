using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Interop;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사달력을 배경화면처럼 상시 표시하는 오버레이 창.
/// 주 단위일 때는 선택한 모니터 하단의 얇은 띠, 월 단위일 때는 화면 전체를 채웁니다.
/// 다른 창들의 Z-order 최하단으로 내려서 "배경화면처럼" 보이게 합니다.
/// </summary>
public sealed class ScheduleOverlayForm : Form
{
    private static readonly Color DefaultDeptColor = Color.FromArgb(120, 130, 140);

    private Dictionary<int, Color> _deptColors = new();
    private ScheduleOutputUnit _unit = ScheduleOutputUnit.Week;
    private DateTime _periodStart = DateTime.Today;
    private List<SchoolEvent> _events = new();
    private string _headerTitle = "";

    public ScheduleOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = Color.FromArgb(16, 28, 40);
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void Configure(Screen screen, ScheduleOutputUnit unit, string schoolName)
    {
        _unit = unit;
        _headerTitle = string.IsNullOrWhiteSpace(schoolName) ? "학사달력" : $"{schoolName} 학사달력";

        if (unit == ScheduleOutputUnit.Month)
        {
            // screen.Bounds는 작업 표시줄 영역까지 포함해 화면 전체를 덮어버린다. WorkingArea를
            // 기준으로 하고, 최대 1024×768로 제한해 화면 가운데에 표시한다.
            var area = screen.WorkingArea;
            var width = Math.Min(1024, area.Width);
            var height = Math.Min(768, area.Height);
            var left = area.Left + (area.Width - width) / 2;
            var top = area.Top + (area.Height - height) / 2;
            Bounds = new Rectangle(left, top, width, height);
        }
        else
        {
            const int stripHeight = 190;
            var area = screen.Bounds;
            Bounds = new Rectangle(area.Left, area.Bottom - stripHeight, area.Width, stripHeight);
        }

        Invalidate();
    }

    public void UpdateEvents(DateTime periodStart, List<SchoolEvent> events)
    {
        _periodStart = periodStart;
        _events = events;
        Invalidate();
    }

    /// <summary>DB(school_departments.color)에 저장된 실제 담당업무 색상으로 갱신합니다.</summary>
    public void SetDepartmentColors(Dictionary<int, Color> colors)
    {
        _deptColors = colors;
        Invalidate();
    }

    /// <summary>환경설정 &gt; 학사일정의 투명도(0~100%)를 적용합니다.</summary>
    public void SetOpacityPercent(int percent)
    {
        Opacity = Math.Clamp(percent, 10, 100) / 100.0;
    }

    /// <summary>다른 창들 뒤로 보내 배경화면처럼 보이게 합니다 (근사 구현).</summary>
    public void SendToBottom()
    {
        if (IsHandleCreated)
        {
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_unit == ScheduleOutputUnit.Week)
        {
            DrawWeek(e.Graphics);
        }
        else
        {
            DrawMonth(e.Graphics);
        }
    }

    private void DrawWeek(Graphics g)
    {
        using var headerFont = new Font("맑은 고딕", 11F, FontStyle.Bold);
        using var dayFont = new Font("맑은 고딕", 10F, FontStyle.Bold);
        using var eventFont = new Font("맑은 고딕", 8.5F);
        using var headerBrush = new SolidBrush(Color.White);

        g.DrawString($"{_headerTitle} · {_periodStart:yyyy-MM-dd} 주간일정", headerFont, headerBrush, 12, 8);

        var top = 34f;
        var columnWidth = (float)Width / 7;

        for (var i = 0; i < 7; i++)
        {
            var day = _periodStart.AddDays(i);
            var x = i * columnWidth;

            using var separator = new Pen(Color.FromArgb(60, UiTheme.Sky));
            g.DrawLine(separator, x, top, x, Height - 8);

            var isToday = day.Date == DateTime.Today;
            using var dayBrush = new SolidBrush(isToday ? UiTheme.Orange : Color.White);
            g.DrawString($"{day:MM/dd (ddd)}", dayFont, dayBrush, x + 6, top + 2);

            var dayEvents = _events
                .Where(ev => ev.StartDateTime.Date <= day.Date && ev.EndDateTime.Date >= day.Date)
                .OrderBy(ev => ev.StartDateTime)
                .Take(5)
                .ToList();

            var y = top + 24;
            foreach (var ev in dayEvents)
            {
                using var chip = new SolidBrush(DeptColor(ev.DeptId));
                var rect = new RectangleF(x + 4, y, columnWidth - 8, 18);
                g.FillRectangle(chip, rect);
                g.DrawString(ev.Title, eventFont, Brushes.White, rect.X + 3, rect.Y + 1);
                y += 20;
            }
        }
    }

    private void DrawMonth(Graphics g)
    {
        using var headerFont = new Font("맑은 고딕", 20F, FontStyle.Bold);
        using var weekdayFont = new Font("맑은 고딕", 11F, FontStyle.Bold);
        using var dayFont = new Font("맑은 고딕", 11F, FontStyle.Bold);
        using var eventFont = new Font("맑은 고딕", 9F);

        g.DrawString($"{_headerTitle} · {_periodStart:yyyy년 M월}", headerFont, Brushes.White, 24, 20);

        var gridTop = 80f;
        var gridHeight = Height - gridTop - 20;
        var columnWidth = (float)Width / 7;

        string[] weekdayNames = { "월", "화", "수", "목", "금", "토", "일" };
        using var weekdayBrush = new SolidBrush(UiTheme.SkyLight);
        for (var i = 0; i < 7; i++)
        {
            g.DrawString(weekdayNames[i], weekdayFont, weekdayBrush, i * columnWidth + 8, gridTop - 26);
        }

        var firstOfMonth = new DateTime(_periodStart.Year, _periodStart.Month, 1);
        var leadingBlanks = ((int)firstOfMonth.DayOfWeek + 6) % 7; // 월요일 시작 기준 오프셋
        var daysInMonth = DateTime.DaysInMonth(_periodStart.Year, _periodStart.Month);
        var totalCells = leadingBlanks + daysInMonth;
        var rows = (int)Math.Ceiling(totalCells / 7.0);
        var rowHeight = gridHeight / Math.Max(rows, 1);

        using var gridPen = new Pen(Color.FromArgb(50, UiTheme.Sky));

        for (var cell = 0; cell < rows * 7; cell++)
        {
            var col = cell % 7;
            var row = cell / 7;
            var cellRect = new RectangleF(col * columnWidth, gridTop + row * rowHeight, columnWidth, rowHeight);
            g.DrawRectangle(gridPen, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

            var dayNumber = cell - leadingBlanks + 1;
            if (dayNumber < 1 || dayNumber > daysInMonth) continue;

            var date = new DateTime(_periodStart.Year, _periodStart.Month, dayNumber);
            var isToday = date.Date == DateTime.Today;

            using var dayBrush = new SolidBrush(isToday ? UiTheme.Orange : Color.White);
            g.DrawString(dayNumber.ToString(), dayFont, dayBrush, cellRect.X + 6, cellRect.Y + 4);

            var dayEvents = _events
                .Where(ev => ev.StartDateTime.Date <= date.Date && ev.EndDateTime.Date >= date.Date)
                .OrderBy(ev => ev.StartDateTime)
                .Take(3)
                .ToList();

            var y = cellRect.Y + 26;
            foreach (var ev in dayEvents)
            {
                using var chip = new SolidBrush(DeptColor(ev.DeptId));
                var chipRect = new RectangleF(cellRect.X + 4, y, cellRect.Width - 8, 16);
                if (chipRect.Bottom > cellRect.Bottom - 2) break;
                g.FillRectangle(chip, chipRect);
                g.DrawString(ev.Title, eventFont, Brushes.White, chipRect.X + 3, chipRect.Y);
                y += 18;
            }
        }
    }

    private Color DeptColor(int? deptId)
    {
        if (deptId is int id && _deptColors.TryGetValue(id, out var color))
        {
            return color;
        }

        return DefaultDeptColor;
    }
}
