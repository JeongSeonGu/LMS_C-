using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 학사 일정 &gt; 일정 목록 창입니다. 구글 캘린더 스타일의 월간 달력으로 보여주며,
/// 담당업무 색상은 서버(DB)에 등록된 색상을 그대로 사용합니다.
/// 빈 날짜를 클릭하면 그 날짜로 새 일정을, 일정 칩을 클릭하면 상세/수정/삭제 팝업을 엽니다.
/// 자신의 담당업무와 관련된 일정만 수정/삭제할 수 있습니다(관리자는 전체 가능).
/// </summary>
public sealed class ScheduleListForm : Form
{
    private readonly WorkSupportApiClient _api;
    private readonly SessionManager _session;

    private readonly Button _prevMonthButton = new() { Left = 20, Top = 15, Width = 30, Text = "<" };
    private readonly Button _todayButton = new() { Left = 55, Top = 15, Width = 55, Text = "오늘" };
    private readonly Label _monthLabel = new()
    {
        Left = 115, Top = 18, Width = 140, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
    };
    private readonly Button _nextMonthButton = new() { Left = 255, Top = 15, Width = 30, Text = ">" };

    private readonly Button _addButton = new()
    {
        Left = 560, Top = 15, Width = 90, Text = "새 일정...", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly MonthCalendarView _calendar = new()
    {
        Left = 20, Top = 50, Width = 630, Height = 460,
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
    };

    private readonly Button _closeButton = new()
    {
        Left = 560, Top = 520, Width = 90, Text = "닫기", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    public ScheduleListForm(WorkSupportApiClient api, SessionManager session)
    {
        _api = api;
        _session = session;

        Text = "학사 일정 목록";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 700);
        MinimumSize = new Size(680, 520);

        Controls.Add(_prevMonthButton);
        Controls.Add(_todayButton);
        Controls.Add(_monthLabel);
        Controls.Add(_nextMonthButton);
        Controls.Add(_addButton);
        Controls.Add(_calendar);
        Controls.Add(_closeButton);

        UiTheme.StyleFlatToolButton(_prevMonthButton);
        UiTheme.StyleFlatToolButton(_todayButton);
        UiTheme.StyleFlatToolButton(_nextMonthButton);
        UiTheme.StylePrimaryButton(_addButton);
        UiTheme.StyleSecondaryButton(_closeButton);
        UiTheme.StyleSubHeaderLabel(_monthLabel);
        _monthLabel.ForeColor = UiTheme.SkyDark;

        _prevMonthButton.Click += (_, _) => ChangeMonth(-1);
        _nextMonthButton.Click += (_, _) => ChangeMonth(1);
        _todayButton.Click += (_, _) => GoToMonth(DateTime.Today);
        _addButton.Click += OnAddClicked;
        _closeButton.Click += (_, _) => Close();

        _calendar.DayClicked += OnDayClicked;
        _calendar.EventClicked += OnEventClicked;

        Load += async (_, _) => await LoadAsync();
    }

    private void ChangeMonth(int delta) => GoToMonth(_calendar.Month.AddMonths(delta));

    private void GoToMonth(DateTime month)
    {
        _calendar.SetMonth(month);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var month = _calendar.Month;
        _monthLabel.Text = $"{month.Year}년 {month.Month}월";

        await _session.EnsureDepartmentsLoadedAsync(_api);
        _calendar.SetDepartmentColors(_session.GetDepartmentColorMap());

        try
        {
            var result = await _api.GetEventsAsync(month.Year, month.Month);
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "일정을 불러오지 못했습니다." : result.ErrorMessage,
                    "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _calendar.SetEvents(result.Data);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"일정을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string DeptName(int? deptId)
    {
        if (deptId is null) return "관련 업무 없음";
        return _session.Departments.FirstOrDefault(d => d.Id == deptId)?.Name ?? $"업무 #{deptId}";
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new ScheduleRegisterForm(_api, _session);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnDayClicked(DateTime day)
    {
        using var form = new ScheduleRegisterForm(_api, _session, editing: null, initialDate: day);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnEventClicked(SchoolEvent ev)
    {
        var canEdit = _session.CanUseDept(ev.DeptId);
        var when = ev.AllDay
            ? $"{ev.StartDateTime:yyyy-MM-dd} (종일)"
            : $"{ev.StartDateTime:yyyy-MM-dd HH:mm} ~ {ev.EndDateTime:yyyy-MM-dd HH:mm}";

        var lines = new List<string>
        {
            when,
            $"담당업무: {DeptName(ev.DeptId)}",
        };
        if (!string.IsNullOrWhiteSpace(ev.Location)) lines.Add($"장소: {ev.Location}");
        if (!string.IsNullOrWhiteSpace(ev.Note)) lines.Add("");
        if (!string.IsNullOrWhiteSpace(ev.Note)) lines.Add(ev.Note!);

        using var detail = new EventDetailForm(ev.Title, string.Join("\n", lines), canEdit);
        if (detail.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        switch (detail.SelectedAction)
        {
            case EventDetailForm.DetailAction.Edit:
                using (var form = new ScheduleRegisterForm(_api, _session, ev))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        await LoadAsync();
                    }
                }
                break;

            case EventDetailForm.DetailAction.Delete:
                await DeleteEventAsync(ev);
                break;
        }
    }

    private async Task DeleteEventAsync(SchoolEvent ev)
    {
        var confirm = MessageBox.Show($"'{ev.Title}' 일정을 삭제하시겠습니까?",
            "학사 일정 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _api.DeleteEventAsync(ev.Id);
            if (result.Ok)
            {
                await LoadAsync();
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "삭제에 실패했습니다." : result.ErrorMessage,
                    "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                "학사 일정", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
