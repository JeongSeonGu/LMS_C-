using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "출력" — 사용할 프린터와, 나의 일간 일정 자동 인쇄 여부를 설정합니다.
/// 체크하면 평일 08:30~10:00 사이 프로그램 최초 실행 시 선택한 프린터로
/// 그날 나의 학사 업무 일정을 자동 인쇄합니다.
/// </summary>
public sealed class PrintOptionsPage : UserControl, IOptionsPage
{
    private readonly ComboBox _printerBox = new()
    {
        Left = 130, Top = 20, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly CheckBox _autoPrintBox = new()
    {
        Left = 130, Top = 55, Width = 300, Text = "나의 일간 일정 자동 출력",
    };

    public string CategoryName => "출력";

    public PrintOptionsPage()
    {
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Left = 16, Top = 0, Width = 400, Height = 20,
            Text = "출력",
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label { Left = 20, Top = 23, Width = 100, Text = "프린터" });
        Controls.Add(_printerBox);
        Controls.Add(_autoPrintBox);
        Controls.Add(new Label
        {
            Left = 20, Top = 85, Width = 380, Height = 40,
            ForeColor = Color.Gray,
            Text = "체크 시 평일 08:30~10:00 사이 프로그램을 처음 실행할 때 선택한 프린터로\n" +
                   "그날 나의 학사 업무 일정을 자동으로 출력합니다.",
        });

        foreach (var printer in PrinterSettings.InstalledPrinters)
        {
            _printerBox.Items.Add(printer.ToString());
        }
    }

    public void LoadFrom(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PrinterName) && _printerBox.Items.Contains(settings.PrinterName))
        {
            _printerBox.SelectedItem = settings.PrinterName;
        }
        else if (_printerBox.Items.Count > 0)
        {
            _printerBox.SelectedIndex = 0;
        }

        _autoPrintBox.Checked = settings.PrintDailyScheduleEnabled;
    }

    public void SaveTo(AppSettings settings)
    {
        settings.PrinterName = _printerBox.SelectedItem as string;
        settings.PrintDailyScheduleEnabled = _autoPrintBox.Checked;
    }

    public string? ValidateSettings()
    {
        if (_autoPrintBox.Checked && _printerBox.SelectedItem is null)
        {
            return "자동 출력을 사용하려면 프린터를 선택하세요.";
        }

        return null;
    }
}
