using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 기본정보 &gt; 학교기본정보. 학교명/학교급/학교장·교감/주소/연락처와 학년별 학생 현황을 보여줍니다.
/// 조회 전용이며, 등록/수정은 관리자 웹 화면에서만 가능합니다.
/// </summary>
public sealed class SchoolInfoForm : Form
{
    private readonly WorkSupportApiClient _api;

    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly TableLayoutPanel _contentRow = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
    private readonly Panel _headerPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _studentGridPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 10, 10) };
    private readonly Panel _infoPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 20, 10) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Fill };

    private readonly PictureBox _logoBox = new()
    {
        Left = 20, Top = 20, Width = 64, Height = 64, SizeMode = PictureBoxSizeMode.Zoom,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly Label _nameLabel = new()
    {
        Left = 96, Top = 20, Width = 380, Height = 26, Font = new Font("맑은 고딕", 14F, FontStyle.Bold),
        ForeColor = UiTheme.SkyDark,
    };

    private readonly Label _badgeLabel = new() { Left = 96, Top = 50, Width = 380, Height = 20 };

    private readonly DataGridView _studentGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private readonly Label _infoLabel = new() { Dock = DockStyle.Fill };

    private readonly Button _closeButton = new()
    {
        Left = 496, Top = 10, Width = 100, Text = "닫기", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    public SchoolInfoForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "학교 기본정보";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(616, 370);
        MinimumSize = new Size(640, 480);

        BuildStudentGridColumns();
        UiTheme.StyleGrid(_studentGrid);
        UiTheme.StyleHintLabel(_badgeLabel);

        _headerPanel.Controls.Add(_logoBox);
        _headerPanel.Controls.Add(_nameLabel);
        _headerPanel.Controls.Add(_badgeLabel);
        _studentGridPanel.Controls.Add(_studentGrid);
        _infoPanel.Controls.Add(_infoLabel);

        _contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280f));
        _contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _contentRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _contentRow.Controls.Add(_studentGridPanel, 0, 0);
        _contentRow.Controls.Add(_infoPanel, 1, 0);

        _bottomPanel.Controls.Add(_closeButton);

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
        _root.Controls.Add(_headerPanel, 0, 0);
        _root.Controls.Add(_contentRow, 0, 1);
        _root.Controls.Add(_bottomPanel, 0, 2);

        Controls.Add(_root);

        UiTheme.StyleSecondaryButton(_closeButton);

        _closeButton.Click += (_, _) => Close();

        Load += async (_, _) => await LoadAsync();
    }

    private void BuildStudentGridColumns()
    {
        _studentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "학년", DataPropertyName = "Label", Width = 60 });
        _studentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "학급", DataPropertyName = "ClassCount", Width = 50 });
        _studentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "남", DataPropertyName = "Boys", Width = 45 });
        _studentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "여", DataPropertyName = "Girls", Width = 45 });
        _studentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "계", DataPropertyName = "Total", Width = 50 });
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var result = await _api.GetSchoolInfoAsync();
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "학교 정보를 불러오지 못했습니다." : result.ErrorMessage,
                    "학교 기본정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var info = result.Data.Info;
            _nameLabel.Text = string.IsNullOrWhiteSpace(info.SchoolName) ? "(학교명 미등록)" : info.SchoolName;

            var badgeParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(info.SchoolType)) badgeParts.Add(info.SchoolType!);
            if (!string.IsNullOrWhiteSpace(info.Principal)) badgeParts.Add($"학교장 {info.Principal}");
            if (!string.IsNullOrWhiteSpace(info.VicePrincipal)) badgeParts.Add($"교감 {info.VicePrincipal}");
            _badgeLabel.Text = string.Join("   ", badgeParts);

            _infoLabel.Text =
                $"학교명   {info.SchoolName}\n" +
                $"학교급   {info.SchoolType}\n" +
                $"학교장   {info.Principal}\n" +
                $"교감     {info.VicePrincipal}\n" +
                $"주소     {info.Address}\n" +
                $"전화     {info.Tel}\n" +
                $"홈페이지 {info.Homepage}\n\n" +
                (string.IsNullOrWhiteSpace(info.UpdatedAt) ? "" : $"최종 수정 {info.UpdatedAt} · {info.UpdatedName}");

            var studentRows = new System.Collections.Generic.List<StudentRow>();
            foreach (var s in result.Data.Students)
            {
                studentRows.Add(new StudentRow(s));
            }
            studentRows.Add(StudentRow.FromSum(result.Data.Sum));
            _studentGrid.DataSource = studentRows;

            if (info.HasLogo && !string.IsNullOrWhiteSpace(info.LogoUrl))
            {
                try
                {
                    var (bytes, _, _) = await _api.DownloadFileAsync(_api.ResolveServerPath(info.LogoUrl));
                    using var ms = new System.IO.MemoryStream(bytes);
                    using var loaded = Image.FromStream(ms);
                    _logoBox.Image = new Bitmap(loaded); // 스트림을 나중에 해제해도 안전하도록 복사본을 만든다.
                }
                catch
                {
                    // 로고를 불러오지 못해도 나머지 정보는 그대로 보여준다.
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"학교 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "학교 기본정보", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class StudentRow
    {
        public StudentRow(SchoolStudentRow row)
        {
            Label = row.Label ?? "";
            ClassCount = row.ClassCount;
            Boys = row.Boys;
            Girls = row.Girls;
            Total = row.Total;
        }

        private StudentRow(string label, int classCount, int boys, int girls, int total)
        {
            Label = label;
            ClassCount = classCount;
            Boys = boys;
            Girls = girls;
            Total = total;
        }

        public static StudentRow FromSum(SchoolStudentSum sum) =>
            new("합계", sum.ClassCount, sum.Boys, sum.Girls, sum.Total);

        public string Label { get; }
        public int ClassCount { get; }
        public int Boys { get; }
        public int Girls { get; }
        public int Total { get; }
    }
}
