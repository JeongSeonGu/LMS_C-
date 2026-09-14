using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>대분류 "일반" — 학교명을 우선 설정합니다.</summary>
public sealed class GeneralOptionsPage : UserControl, IOptionsPage
{
    private readonly TextBox _schoolNameBox = new() { Left = 130, Top = 20, Width = 260 };

    private readonly CheckBox _autoStartBox = new()
    {
        Left = 20, Top = 90, Width = 360, Text = "Windows 시작 시 자동 실행",
    };

    private readonly CheckBox _startupNoticeBox = new()
    {
        Left = 20, Top = 120, Width = 380, Text = "첫 시작 시 공지사항 알림",
    };

    private readonly CheckBox _breakBoardEnabledBox = new()
    {
        Left = 20, Top = 202, Width = 380, Text = "쉬는 시간 전자칠판 페이지 활성화",
    };

    private readonly ComboBox _breakBoardMonitorBox = new()
    {
        Left = 130, Top = 230, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _breakEndCountdownBox = new()
    {
        Left = 20, Top = 262, Width = 380, Text = "쉬는 시간 종료안내",
    };

    public string CategoryName => "일반";

    public GeneralOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "일반" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 27, Width = 100, Text = "학교명" });
        Controls.Add(_schoolNameBox);

        var schoolNameHint = new Label
        {
            Left = 20, Top = 59, Width = 380, Height = 30,
            Text = "화면 표시와 인쇄물 제목 등에 사용됩니다. 로그인하면 서버에 등록된 학교명으로 자동 채워집니다.",
        };
        UiTheme.StyleHintLabel(schoolNameHint);
        Controls.Add(schoolNameHint);

        Controls.Add(_autoStartBox);
        Controls.Add(_startupNoticeBox);

        var startupHint = new Label
        {
            Left = 20, Top = 148, Width = 380, Height = 45,
            Text = "체크 시 로그인 직후 오늘의 일정, 요청사항, 안내 및 공지, 내가 해야 할 일,\n" +
                   "요청된 법정연수 이수 등록, 처리 중인 협의사항을 모아 보여줍니다. (기본값: 미체크)",
        };
        UiTheme.StyleHintLabel(startupHint);
        Controls.Add(startupHint);

        Controls.Add(_breakBoardEnabledBox);
        Controls.Add(new Label { Left = 20, Top = 233, Width = 100, Text = "출력 모니터" });
        Controls.Add(_breakBoardMonitorBox);
        Controls.Add(_breakEndCountdownBox);

        var breakBoardHint = new Label
        {
            Left = 20, Top = 292, Width = 380, Height = 55,
            Text = "환경설정 > 차시에서 계산된 쉬는 시간에 맞춰 환경설정 > 네트워크의\n" +
                   "\"전자칠판 페이지\"를 선택한 모니터에 전체화면으로 띄우고, 쉬는 시간이\n" +
                   "끝나면 자동으로 닫습니다. 종료안내를 켜면 끝나기 2분 전에 카운트다운을 보여줍니다.",
        };
        UiTheme.StyleHintLabel(breakBoardHint);
        Controls.Add(breakBoardHint);

        foreach (var option in DisplayHelper.GetMonitorOptions())
        {
            _breakBoardMonitorBox.Items.Add(option);
        }
    }

    public void LoadFrom(AppSettings settings)
    {
        _schoolNameBox.Text = settings.SchoolName;
        _autoStartBox.Checked = settings.AutoStartWithWindows;
        _startupNoticeBox.Checked = settings.ShowStartupNoticeModal;
        _breakBoardEnabledBox.Checked = settings.BreakBoardEnabled;
        _breakEndCountdownBox.Checked = settings.BreakEndCountdownEnabled;
        ScheduleOptionsPage.SelectMonitor(_breakBoardMonitorBox, settings.BreakBoardMonitorIndex);
    }

    public void SaveTo(AppSettings settings)
    {
        settings.SchoolName = _schoolNameBox.Text.Trim();
        settings.AutoStartWithWindows = _autoStartBox.Checked;
        settings.ShowStartupNoticeModal = _startupNoticeBox.Checked;
        settings.BreakBoardEnabled = _breakBoardEnabledBox.Checked;
        settings.BreakEndCountdownEnabled = _breakEndCountdownBox.Checked;
        settings.BreakBoardMonitorIndex = (_breakBoardMonitorBox.SelectedItem as MonitorOption)?.Index ?? 0;
    }
}
