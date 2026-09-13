using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>대분류 "일반" — 학교명을 우선 설정합니다.</summary>
public sealed class GeneralOptionsPage : UserControl, IOptionsPage
{
    private readonly TextBox _schoolNameBox = new() { Left = 130, Top = 20, Width = 260 };

    private readonly CheckBox _autoStartBox = new()
    {
        Left = 20, Top = 90, Width = 360, Text = "Windows 시작 시 자동 실행",
    };

    public string CategoryName => "일반";

    public GeneralOptionsPage()
    {
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Left = 16, Top = 0, Width = 400, Height = 20,
            Text = "일반",
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "학교명" });
        Controls.Add(_schoolNameBox);
        Controls.Add(new Label
        {
            Left = 20, Top = 55, Width = 380, Height = 30,
            ForeColor = Color.Gray,
            Text = "화면 표시와 인쇄물 제목 등에 사용됩니다. 로그인하면 서버에 등록된 학교명으로 자동 채워집니다.",
        });
        Controls.Add(_autoStartBox);
    }

    public void LoadFrom(AppSettings settings)
    {
        _schoolNameBox.Text = settings.SchoolName;
        _autoStartBox.Checked = settings.AutoStartWithWindows;
    }

    public void SaveTo(AppSettings settings)
    {
        settings.SchoolName = _schoolNameBox.Text.Trim();
        settings.AutoStartWithWindows = _autoStartBox.Checked;
    }
}
