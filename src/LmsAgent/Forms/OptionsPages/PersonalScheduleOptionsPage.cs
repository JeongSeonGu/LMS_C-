using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Services;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 대분류 "학사일정" 바로 아래의 "개인일정" — 사용자 정보 &gt; 개인일정 등록으로 기록하는
/// 개인일정의 표시 색상·아이콘과 저장 폴더를 설정합니다. 개인일정은 서버(DB)나 웹 화면과
/// 전혀 연동되지 않는, 이 PC에만 있는 로컬 전용 기록입니다.
/// </summary>
public sealed class PersonalScheduleOptionsPage : UserControl, IOptionsPage
{
    private static readonly string[] IconChoices = { "📍", "⭐", "🏠", "✈️", "🎯", "💊", "🎂", "🔖", "🧾", "🚗" };

    private readonly Button _colorButton = new() { Left = 130, Top = 20, Width = 60, Height = 28, Text = "" };

    private readonly ComboBox _iconBox = new()
    {
        Left = 130, Top = 58, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly TextBox _folderBox = new() { Left = 130, Top = 96, Width = 260 };

    private readonly Button _browseButton = new() { Left = 396, Top = 94, Width = 30, Text = "..." };
    private readonly Button _openFolderButton = new() { Left = 130, Top = 128, Width = 120, Text = "폴더 열기" };

    private Color _selectedColor = ColorHelper.ParseHexOrDefault("#7C5CFF", Color.MediumPurple);

    public string CategoryName => "개인일정";

    public PersonalScheduleOptionsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Font = UiTheme.BaseFont;

        var header = new Label { Left = 16, Top = 0, Width = 400, Height = 24, Text = "개인일정" };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        Controls.Add(new Label { Left = 20, Top = 27, Width = 100, Text = "표시 색상" });
        Controls.Add(_colorButton);
        Controls.Add(new Label { Left = 20, Top = 63, Width = 100, Text = "아이콘" });
        Controls.Add(_iconBox);
        Controls.Add(new Label { Left = 20, Top = 101, Width = 100, Text = "저장 폴더" });
        Controls.Add(_folderBox);
        Controls.Add(_browseButton);
        Controls.Add(_openFolderButton);

        foreach (var icon in IconChoices)
        {
            _iconBox.Items.Add(icon);
        }

        UiTheme.StyleFlatToolButton(_colorButton);
        UiTheme.StyleSecondaryButton(_browseButton);
        UiTheme.StyleSecondaryButton(_openFolderButton);

        var hint = new Label
        {
            Left = 20, Top = 164, Width = 400, Height = 40,
            Text = "저장 폴더를 비워두면 이 PC의 %AppData%\\LmsAgent\\ 를 사용합니다. 폴더를\n" +
                   "바꿔도 기존 파일이 자동으로 옮겨지지는 않습니다.",
        };
        UiTheme.StyleHintLabel(hint);
        Controls.Add(hint);

        // Width가 문구 실제 폭보다 좁으면 줄바꿈된 두 번째 줄이 Height 밖으로 잘려 보인다
        // (개인일정 등록 목록 창에서 실제로 겪은 문제) — 넉넉한 Width에 두 줄 분량의
        // Height를 같이 줘서, 혹시 줄바꿈되더라도 잘리지 않게 한다.
        var disclaimer = new Label
        {
            Left = 20, Top = 220, Width = 460, Height = 40,
            Text = "개인일정은 로컬에만 기록될 뿐 학사 일정과 연동이 되지 않습니다.",
        };
        UiTheme.StyleHintLabel(disclaimer);
        Controls.Add(disclaimer);

        _colorButton.Click += OnColorButtonClicked;
        _browseButton.Click += OnBrowseClicked;
        _openFolderButton.Click += OnOpenFolderClicked;
    }

    private void OnColorButtonClicked(object? sender, EventArgs e)
    {
        using var dialog = new ColorDialog { Color = _selectedColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplyColor(dialog.Color);
        }
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "개인일정을 저장할 폴더를 선택하세요.",
            SelectedPath = string.IsNullOrWhiteSpace(_folderBox.Text) ? PersonalScheduleStore.DefaultFolder : _folderBox.Text,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderBox.Text = dialog.SelectedPath;
        }
    }

    private void OnOpenFolderClicked(object? sender, EventArgs e)
    {
        var folder = string.IsNullOrWhiteSpace(_folderBox.Text) ? PersonalScheduleStore.DefaultFolder : _folderBox.Text;
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"폴더를 열 수 없습니다: {ex.Message}", "개인일정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyColor(Color color)
    {
        _selectedColor = color;
        _colorButton.BackColor = color;
        _colorButton.FlatAppearance.MouseOverBackColor = color;
    }

    public void LoadFrom(AppSettings settings)
    {
        ApplyColor(ColorHelper.ParseHexOrDefault(settings.PersonalScheduleColor, Color.MediumPurple));

        var iconIndex = Array.IndexOf(IconChoices, settings.PersonalScheduleIcon);
        _iconBox.SelectedIndex = iconIndex >= 0 ? iconIndex : 0;

        _folderBox.Text = settings.PersonalScheduleStorageFolder ?? "";
    }

    public void SaveTo(AppSettings settings)
    {
        settings.PersonalScheduleColor = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
        settings.PersonalScheduleIcon = _iconBox.SelectedItem as string ?? "📍";

        var folder = _folderBox.Text.Trim();
        settings.PersonalScheduleStorageFolder = folder.Length > 0 ? folder : null;
    }
}
