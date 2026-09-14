using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 기본정보 &gt; 협의사항. 서버가 Google 시트를 그대로 읽어오는 조회 전용 화면입니다
/// (등록/수정/삭제 API가 없어 이 프로그램에서도 조회만 제공합니다).
/// 새 안건 등록은 관리자가 설정해 둔 외부 등록 링크를 기본 브라우저로 엽니다.
/// </summary>
public sealed class MeetingsForm : Form
{
    private readonly WorkSupportApiClient _api;
    private string? _appscriptUrl;
    private string? _sheetViewUrl;

    private readonly Panel _topPanel = new() { Dock = DockStyle.Top, Height = 50 };
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 8) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Bottom, Height = 50 };

    private readonly ComboBox _sourceBox = new()
    {
        Left = 20, Top = 14, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly Button _registerButton = new()
    {
        Left = 400, Top = 12, Width = 100, Text = "안건 등록...", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly Button _viewSheetButton = new()
    {
        Left = 508, Top = 12, Width = 90, Text = "시트 보기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AllowUserToResizeRows = false,
    };

    private readonly Label _guideLabel = new()
    {
        Left = 20, Top = 12, Width = 460, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };

    private readonly Button _closeButton = new()
    {
        Left = 498, Top = 10, Width = 100, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    public MeetingsForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "협의사항";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(618, 470);
        MinimumSize = new Size(640, 480);

        _sourceBox.Items.AddRange(new object[] { "협의안건 전달사항", "협의안건 전달내용 보관" });
        _sourceBox.SelectedIndex = 0;

        UiTheme.StyleGrid(_grid);
        UiTheme.StyleHintLabel(_guideLabel);
        UiTheme.StylePrimaryButton(_registerButton);
        UiTheme.StyleSecondaryButton(_viewSheetButton);
        UiTheme.StyleSecondaryButton(_closeButton);

        _topPanel.Controls.Add(_sourceBox);
        _topPanel.Controls.Add(_registerButton);
        _topPanel.Controls.Add(_viewSheetButton);
        _contentPanel.Controls.Add(_grid);
        _bottomPanel.Controls.Add(_guideLabel);
        _bottomPanel.Controls.Add(_closeButton);

        Controls.Add(_contentPanel);
        Controls.Add(_topPanel);
        Controls.Add(_bottomPanel);

        _sourceBox.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _registerButton.Click += (_, _) => OpenUrl(_appscriptUrl, "안건 등록 링크가 설정되어 있지 않습니다.");
        _viewSheetButton.Click += (_, _) => OpenUrl(_sheetViewUrl, "시트 바로가기 주소가 설정되어 있지 않습니다.");
        _closeButton.Click += (_, _) => Close();

        Load += async (_, _) => await LoadAsync();
    }

    private static void OpenUrl(string? url, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(emptyMessage, "협의사항", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("링크를 열 수 없습니다.", "협의사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var source = _sourceBox.SelectedIndex == 1 ? "archive" : "recent";
        _grid.DataSource = null;
        _grid.Columns.Clear();

        try
        {
            var result = await _api.GetMeetingsAsync(source);
            if (!result.Ok || result.Data is null)
            {
                _guideLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : result.ErrorMessage;
                return;
            }

            var data = result.Data;
            _appscriptUrl = data.Config.AppscriptUrl;
            _sheetViewUrl = data.Config.SheetViewUrl;
            _guideLabel.Text = data.Config.Guide ?? "";

            if (!data.Ready)
            {
                MessageBox.Show("아직 이 시트가 연결되지 않았습니다. 관리자에게 문의해 주세요.",
                    "협의사항", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 시트 열 구성이 학교마다 다를 수 있어, 머리글을 그대로 DataTable 컬럼으로 만들고
            // DataGridView가 자동으로 열을 생성하도록 한다(AutoGenerateColumns 기본값 true).
            var table = new System.Data.DataTable();
            foreach (var header in data.Headers)
            {
                table.Columns.Add(header);
            }
            foreach (var row in data.Rows)
            {
                var newRow = table.NewRow();
                foreach (var header in data.Headers)
                {
                    newRow[header] = row.TryGetValue(header, out var value) ? value : "";
                }
                table.Rows.Add(newRow);
            }

            _grid.DataSource = table;
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.ReadOnly = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"협의사항을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "협의사항", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
