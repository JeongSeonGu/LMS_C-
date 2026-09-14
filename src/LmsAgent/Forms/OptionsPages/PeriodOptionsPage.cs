using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "차시" — 하루 시간표(교시/점심시간)를 등록합니다. "차시 추가"를 누르면 이전 교시
/// 다음으로 새 교시가 추가되고, 4교시 다음에는 점심시간이 자동으로 먼저 추가됩니다.
/// 등록한 교시/점심시간 사이의 빈 시간은 쉬는 시간으로 자동 계산되어 아래에 표시되고,
/// 이 값이 "쉬는 시간 전자칠판" 기능(대분류 "일반")이 사용하는 쉬는 시간입니다.
/// </summary>
public sealed class PeriodOptionsPage : UserControl, IOptionsPage
{
    private static readonly TimeOnly DefaultFirstStart = new(9, 0);
    private const int ClassMinutes = 40;
    private const int BreakMinutes = 10;
    private const int LunchMinutes = 50;

    private readonly BindingList<PeriodRow> _rows = new();

    private readonly DataGridView _grid = new()
    {
        Left = 20, Top = 30, Width = 380, Height = 190,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private readonly Button _addButton = new() { Left = 20, Top = 228, Width = 90, Text = "차시 추가" };
    private readonly Button _removeButton = new() { Left = 116, Top = 228, Width = 90, Text = "선택 삭제" };

    private readonly Label _breaksLabel = new()
    {
        Left = 20, Top = 264, Width = 380, Height = 80,
    };

    public string CategoryName => "차시";

    public PeriodOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "차시" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        BuildColumns();
        UiTheme.StyleGrid(_grid);
        _grid.DataSource = _rows;

        UiTheme.StylePrimaryButton(_addButton);
        UiTheme.StyleSecondaryButton(_removeButton);
        UiTheme.StyleHintLabel(_breaksLabel);

        Controls.Add(_grid);
        Controls.Add(_addButton);
        Controls.Add(_removeButton);
        Controls.Add(_breaksLabel);

        var hint = new Label
        {
            Left = 20, Top = 348, Width = 380, Height = 40,
            Text = "시작/종료 시간은 칸을 눌러 \"HH:mm\" 형식으로 직접 수정할 수 있습니다.\n" +
                   "4교시 다음 \"차시 추가\"를 누르면 점심시간이 자동으로 먼저 추가됩니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);

        _addButton.Click += (_, _) => OnAddClicked();
        _removeButton.Click += (_, _) => OnRemoveClicked();
        _rows.ListChanged += (_, _) => UpdateBreaksLabel();
        _grid.CellEndEdit += (_, _) => UpdateBreaksLabel();
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "구분", DataPropertyName = nameof(PeriodRow.Label), Width = 90, ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "시작", DataPropertyName = nameof(PeriodRow.Start), Width = 90,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "종료", DataPropertyName = nameof(PeriodRow.End), Width = 90,
        });
    }

    private void OnAddClicked()
    {
        var periods = _rows.Select(r => r.ToSetting()).ToList();

        var nonLunchCount = periods.Count(p => !p.IsLunch);
        var hasLunch = periods.Any(p => p.IsLunch);

        if (nonLunchCount == 4 && !hasLunch)
        {
            var lunchStart = NextStart(periods);
            periods.Add(new PeriodSetting
            {
                IsLunch = true,
                Start = lunchStart.ToString("HH:mm"),
                End = lunchStart.AddMinutes(LunchMinutes).ToString("HH:mm"),
            });
        }

        var start = NextStart(periods);
        var end = start.AddMinutes(ClassMinutes);
        periods.Add(new PeriodSetting
        {
            IsLunch = false,
            Start = start.ToString("HH:mm"),
            End = end.ToString("HH:mm"),
        });

        Renumber(periods);
        Rebind(periods);
    }

    private static TimeOnly NextStart(List<PeriodSetting> periods)
    {
        var sorted = PeriodScheduleHelper.SortedValid(periods);
        if (sorted.Count == 0)
        {
            return DefaultFirstStart;
        }

        var lastEnd = TimeOnly.Parse(sorted[^1].End);
        return lastEnd.AddMinutes(BreakMinutes);
    }

    private static void Renumber(List<PeriodSetting> periods)
    {
        var index = 1;
        foreach (var p in PeriodScheduleHelper.SortedValid(periods))
        {
            if (!p.IsLunch)
            {
                p.Index = index++;
            }
        }
    }

    private void OnRemoveClicked()
    {
        if (_grid.CurrentRow?.DataBoundItem is PeriodRow row)
        {
            _rows.Remove(row);
        }
    }

    private void Rebind(List<PeriodSetting> periods)
    {
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        foreach (var p in PeriodScheduleHelper.SortedValid(periods).Concat(periods.Except(PeriodScheduleHelper.SortedValid(periods))))
        {
            _rows.Add(new PeriodRow(p));
        }
        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
        UpdateBreaksLabel();
    }

    private void UpdateBreaksLabel()
    {
        var periods = _rows.Select(r => r.ToSetting()).ToList();
        var breaks = PeriodScheduleHelper.ComputeBreaks(periods);

        _breaksLabel.Text = breaks.Count == 0
            ? "쉬는 시간: 계산할 항목이 2개 이상 필요합니다."
            : "쉬는 시간(자동 계산):\n" + string.Join("\n", breaks.Select(b => $"  {b.Start:HH\\:mm} ~ {b.End:HH\\:mm}"));
    }

    public void LoadFrom(AppSettings settings)
    {
        var periods = settings.Periods.Count > 0
            ? settings.Periods.Select(Clone).ToList()
            : new List<PeriodSetting>();

        Rebind(periods);
    }

    public void SaveTo(AppSettings settings)
    {
        var periods = _rows.Select(r => r.ToSetting()).ToList();
        Renumber(periods);
        settings.Periods = PeriodScheduleHelper.SortedValid(periods);
    }

    public string? ValidateSettings()
    {
        foreach (var row in _rows)
        {
            if (!PeriodScheduleHelper.TryParseTime(row.Start, out var start))
            {
                return $"{row.Label}의 시작 시간 형식이 올바르지 않습니다(예: 09:00).";
            }

            if (!PeriodScheduleHelper.TryParseTime(row.End, out var end))
            {
                return $"{row.Label}의 종료 시간 형식이 올바르지 않습니다(예: 09:40).";
            }

            if (end <= start)
            {
                return $"{row.Label}의 종료 시간은 시작 시간보다 늦어야 합니다.";
            }
        }

        var sorted = _rows.Select(r => r.ToSetting()).ToList();
        var ordered = PeriodScheduleHelper.SortedValid(sorted);
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var end = TimeOnly.Parse(ordered[i].End);
            var nextStart = TimeOnly.Parse(ordered[i + 1].Start);
            if (nextStart < end)
            {
                return $"{ordered[i].Label}과(와) {ordered[i + 1].Label}의 시간이 겹칩니다.";
            }
        }

        return null;
    }

    private static PeriodSetting Clone(PeriodSetting p) => new()
    {
        Index = p.Index, Start = p.Start, End = p.End, IsLunch = p.IsLunch,
    };

    private sealed class PeriodRow
    {
        private readonly PeriodSetting _setting;

        public PeriodRow(PeriodSetting setting)
        {
            _setting = setting;
        }

        public string Label => _setting.Label;

        public string Start
        {
            get => _setting.Start;
            set => _setting.Start = value;
        }

        public string End
        {
            get => _setting.End;
            set => _setting.End = value;
        }

        public PeriodSetting ToSetting() => _setting;
    }
}
