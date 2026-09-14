using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 구글 캘린더의 월간 보기를 본뜬, 담당업무 색상이 칠해진 이벤트 칩을 보여주는 월간 달력 컨트롤입니다.
/// 빈 날짜 칸을 클릭하면 <see cref="DayClicked"/>가, 이벤트 칩을 클릭하면 <see cref="EventClicked"/>가 발생합니다.
/// </summary>
public sealed class MonthCalendarView : UserControl
{
    private static readonly string[] WeekdayNames = { "월", "화", "수", "목", "금", "토", "일" };
    private const int HeaderHeight = 22;
    private const int ChipHeight = 18;

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<SchoolEvent> _events = new();
    private Dictionary<int, Color> _deptColors = new();
    private HashSet<DateTime> _dutyDates = new();

    private readonly List<(RectangleF Rect, SchoolEvent? Event, DateTime Day)> _hitAreas = new();

    public event Action<DateTime>? DayClicked;
    public event Action<SchoolEvent>? EventClicked;

    public DateTime Month => _month;

    public MonthCalendarView()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void SetMonth(DateTime month)
    {
        _month = new DateTime(month.Year, month.Month, 1);
        Invalidate();
    }

    public void SetDepartmentColors(Dictionary<int, Color> colors)
    {
        _deptColors = colors;
        Invalidate();
    }

    public void SetEvents(List<SchoolEvent> events)
    {
        _events = events;
        Invalidate();
    }

    /// <summary>교장/교감/교무부장/행정실장의 복무(연가·출장·조퇴) 기록이 있는 날짜 집합을 설정합니다.
    /// 해당 날짜의 칸에 작은 복무 표시 아이콘을 그립니다.</summary>
    public void SetDutyDates(IEnumerable<DateTime> dates)
    {
        _dutyDates = new HashSet<DateTime>(dates.Select(d => d.Date));
        Invalidate();
    }

    private Color ColorFor(int? deptId)
    {
        if (deptId is int id && _deptColors.TryGetValue(id, out var color))
        {
            return color;
        }

        return Color.FromArgb(154, 160, 166); // 관련 업무 없음/색상 미지정 기본값
    }

    /// <summary>
    /// 교장/교감/교무부장/행정실장의 복무(연가·출장·조퇴) 기록이 있는 날짜의 셀 오른쪽 위에
    /// 그리는 작은 클립보드 모양 표시입니다. 날짜 숫자와 겹치지 않도록 셀 오른쪽 끝에 붙입니다.
    /// </summary>
    private static void DrawDutyIcon(Graphics g, float cellRight, float cellTop)
    {
        const float size = 11f;
        var rect = new RectangleF(cellRight - size - 4, cellTop + 3, size, size);

        using var badgeBrush = new SolidBrush(UiTheme.OrangeDark);
        g.FillRectangle(badgeBrush, rect.X, rect.Y + 1, rect.Width, rect.Height - 1);

        var tabRect = new RectangleF(rect.X + size * 0.28f, rect.Y - 1, size * 0.44f, 2.5f);
        g.FillRectangle(badgeBrush, tabRect);

        using var linePen = new Pen(Color.White, 1f);
        g.DrawLine(linePen, rect.X + 2, rect.Y + 5, rect.Right - 2, rect.Y + 5);
        g.DrawLine(linePen, rect.X + 2, rect.Y + 8, rect.Right - 3, rect.Y + 8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        _hitAreas.Clear();

        using var headerFont = new Font("맑은 고딕", 9F, FontStyle.Bold);
        using var dayFont = new Font("맑은 고딕", 9F);
        using var eventFont = new Font("맑은 고딕", 8F);
        using var gridPen = new Pen(UiTheme.Border);
        using var weekendBrush = new SolidBrush(UiTheme.Danger);
        using var weekdayBrush = new SolidBrush(UiTheme.SkyDark);

        // 헤더 띠(하늘색)로 요일 행을 구분한다.
        using (var headerBandBrush = new SolidBrush(UiTheme.SkyLight))
        {
            g.FillRectangle(headerBandBrush, 0, 0, Width, HeaderHeight);
        }

        var colWidth = (float)Width / 7;

        for (var i = 0; i < 7; i++)
        {
            g.DrawString(WeekdayNames[i], headerFont, i >= 5 ? weekendBrush : weekdayBrush, i * colWidth + 4, 3);
        }

        var leadingBlanks = ((int)_month.DayOfWeek + 6) % 7; // 월요일 시작 기준 오프셋
        var daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        var totalCells = leadingBlanks + daysInMonth;
        var rows = Math.Max(1, (int)Math.Ceiling(totalCells / 7.0));
        var gridHeight = Math.Max(1, Height - HeaderHeight);
        var rowHeight = (float)gridHeight / rows;

        for (var cell = 0; cell < rows * 7; cell++)
        {
            var col = cell % 7;
            var row = cell / 7;
            var cellRect = new RectangleF(col * colWidth, HeaderHeight + row * rowHeight, colWidth, rowHeight);
            g.DrawRectangle(gridPen, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

            var dayNumber = cell - leadingBlanks + 1;
            if (dayNumber < 1 || dayNumber > daysInMonth)
            {
                continue;
            }

            var date = new DateTime(_month.Year, _month.Month, dayNumber);
            _hitAreas.Add((cellRect, null, date)); // 빈 영역 클릭 시 해당 날짜로 새 일정 등록

            var isToday = date == DateTime.Today;
            if (isToday)
            {
                using var todayBrush = new SolidBrush(UiTheme.Orange);
                g.FillEllipse(todayBrush, cellRect.X + 3, cellRect.Y + 2, 18, 18);
                g.DrawString(dayNumber.ToString(), dayFont, Brushes.White, cellRect.X + 7, cellRect.Y + 3);
            }
            else
            {
                using var dayBrush = new SolidBrush(UiTheme.TextPrimary);
                g.DrawString(dayNumber.ToString(), dayFont, dayBrush, cellRect.X + 4, cellRect.Y + 2);
            }

            if (_dutyDates.Contains(date))
            {
                DrawDutyIcon(g, cellRect.Right, cellRect.Y);
            }

            var dayEvents = _events
                .Where(ev => ev.StartDateTime.Date <= date && ev.EndDateTime.Date >= date)
                .OrderBy(ev => ev.StartDateTime)
                .ToList();

            var maxVisible = Math.Max(1, (int)((cellRect.Bottom - (cellRect.Y + 22)) / ChipHeight));
            var y = cellRect.Y + 22;

            for (var i = 0; i < dayEvents.Count; i++)
            {
                var isLastSlot = i == maxVisible - 1 && dayEvents.Count > maxVisible;
                if (i >= maxVisible)
                {
                    break;
                }

                if (isLastSlot)
                {
                    var moreRect = new RectangleF(cellRect.X + 2, y, cellRect.Width - 4, ChipHeight);
                    g.DrawString($"+{dayEvents.Count - i}개 더보기", eventFont, Brushes.Gray, moreRect.X + 2, moreRect.Y + 1);
                    break;
                }

                var ev = dayEvents[i];
                var chipRect = new RectangleF(cellRect.X + 2, y, cellRect.Width - 4, ChipHeight - 2);
                using var chipBrush = new SolidBrush(ColorFor(ev.DeptId));
                g.FillRectangle(chipBrush, chipRect);
                g.DrawString(ev.Title, eventFont, Brushes.White, chipRect.X + 3, chipRect.Y + 1);

                _hitAreas.Add((chipRect, ev, date));
                y += ChipHeight;
            }
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        // 이벤트 칩이 날짜 배경보다 나중에 추가되므로, 뒤에서부터 검사해야 칩이 우선 선택된다.
        for (var i = _hitAreas.Count - 1; i >= 0; i--)
        {
            var (rect, ev, day) = _hitAreas[i];
            if (!rect.Contains(e.Location))
            {
                continue;
            }

            if (ev is not null)
            {
                EventClicked?.Invoke(ev);
            }
            else
            {
                DayClicked?.Invoke(day);
            }

            return;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}
