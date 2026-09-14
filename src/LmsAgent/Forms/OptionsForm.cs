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

    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 0) };
    private readonly Panel _bottomPanel = new() { Dock = DockStyle.Bottom, Height = 52 };

    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Left,
        Width = 140,
        HideSelection = false,
    };

    private readonly Panel _pageHost = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(12, 0, 0, 0),
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly Button _okButton = new()
    {
        Left = 336, Top = 10, Width = 80, Text = "확인", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly Button _cancelButton = new()
    {
        Left = 422, Top = 10, Width = 80, Text = "취소", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly Button _applyButton = new()
    {
        Left = 508, Top = 10, Width = 80, Text = "적용", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

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

        _contentPanel.Controls.Add(_pageHost);
        _contentPanel.Controls.Add(_tree);
        _bottomPanel.Controls.Add(_okButton);
        _bottomPanel.Controls.Add(_cancelButton);
        _bottomPanel.Controls.Add(_applyButton);

        Controls.Add(_contentPanel);
        Controls.Add(_bottomPanel);

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
