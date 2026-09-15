using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Configuration;
using LmsAgent.Forms.OptionsPages;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 환경설정 창. Visual Studio의 옵션 창처럼 왼쪽에는 대분류 트리를,
/// 오른쪽에는 선택한 대분류의 설정 항목을 보여줍니다.
/// </summary>
public sealed class OptionsForm : Form
{
    private readonly AppSettings _settings;
    private readonly IOptionsPage[] _pages;

    // TableLayoutPanel을 쓰는 이유: Panel + Dock(Top/Bottom/Left/Fill) 조합은 어느 컨트롤을
    // 먼저 Controls에 추가하느냐에 따라 레이아웃 우선순위가 달라지는데(z-order 의존),
    // 이 프로젝트에서 그 순서를 두 번이나 잘못 적용해 확인/취소/적용 버튼이 안 보이는 사고가
    // 반복됐습니다. TableLayoutPanel은 행/열 크기를 RowStyles/ColumnStyles로 명시적으로
    // 지정하므로 추가 순서와 무관하게 항상 같은 자리에 배치됩니다.
    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
    private readonly TableLayoutPanel _contentRow = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };

    // 버튼을 Left/Top 절대 좌표로 두면 폼 폭이나 DPI 배율이 달라질 때 패널 밖으로 밀려날 수 있으므로,
    // 오른쪽 정렬 FlowLayoutPanel(Dock=Right, AutoSize)로 항상 패널 안쪽에 붙어 보이도록 한다.
    private readonly FlowLayoutPanel _bottomPanel = new()
    {
        Dock = DockStyle.Right,
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = new Padding(0),
    };

    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
    };

    private readonly Panel _pageHost = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(12, 0, 0, 0),
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly Button _okButton = new() { Width = 80, Height = 30, Text = "확인", Margin = new Padding(0, 10, 8, 10) };
    private readonly Button _cancelButton = new() { Width = 80, Height = 30, Text = "취소", Margin = new Padding(0, 10, 8, 10) };
    private readonly Button _applyButton = new() { Width = 80, Height = 30, Text = "적용", Margin = new Padding(0, 10, 12, 10) };

    public OptionsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "환경설정";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 420);
        MinimumSize = new Size(640, 480);

        _pages = new IOptionsPage[]
        {
            new GeneralOptionsPage(),
            new ScheduleOptionsPage(),
            new PeriodOptionsPage(),
            new DutyOptionsPage(),
            new PrintOptionsPage(),
            new NetworkOptionsPage(),
            new LicenseOptionsPage(),
        };

        foreach (var page in _pages)
        {
            var control = (Control)page;
            control.Visible = false;
            _pageHost.Controls.Add(control);
            page.LoadFrom(_settings);

            _tree.Nodes.Add(new TreeNode(page.CategoryName) { Tag = page });
        }

        var treeHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 0, 12) };
        treeHost.Controls.Add(_tree);

        var pageHostWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 12, 12) };
        pageHostWrapper.Controls.Add(_pageHost);

        _contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152f));
        _contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _contentRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _contentRow.Controls.Add(treeHost, 0, 0);
        _contentRow.Controls.Add(pageHostWrapper, 1, 0);

        _bottomPanel.Controls.Add(_okButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_applyButton);
        var bottomHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        bottomHost.Controls.Add(_bottomPanel);

        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        _root.Controls.Add(_contentRow, 0, 0);
        _root.Controls.Add(bottomHost, 0, 1);

        Controls.Add(_root);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        UiTheme.StyleTree(_tree);
        _pageHost.BackColor = UiTheme.Surface;
        UiTheme.StylePrimaryButton(_okButton);
        UiTheme.StyleSecondaryButton(_applyButton);
        UiTheme.StyleSecondaryButton(_cancelButton);

        _tree.AfterSelect += (_, e) => ShowPage(e.Node?.Tag as IOptionsPage);
        _okButton.Click += (_, _) =>
        {
            if (SaveAll())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        _applyButton.Click += (_, _) => SaveAll();
        _cancelButton.Click += (_, _) => Close();

        _tree.SelectedNode = _tree.Nodes[0];
        ShowPage(_pages[0]);
    }

    private void ShowPage(IOptionsPage? page)
    {
        foreach (Control control in _pageHost.Controls)
        {
            control.Visible = ReferenceEquals(control, page);
        }
    }

    private bool SaveAll()
    {
        foreach (var page in _pages)
        {
            var error = page.ValidateSettings();
            if (error is not null)
            {
                MessageBox.Show(error, "환경설정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        foreach (var page in _pages)
        {
            page.SaveTo(_settings);
        }

        return true;
    }
}
