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

    private readonly TreeView _tree = new()
    {
        Left = 12,
        Top = 12,
        Width = 140,
        Height = 360,
        HideSelection = false,
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
    };

    private readonly Panel _pageHost = new()
    {
        Left = 164,
        Top = 12,
        Width = 420,
        Height = 360,
        BorderStyle = BorderStyle.FixedSingle,
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
    };

    private readonly Button _okButton = new()
    {
        Left = 336, Top = 384, Width = 80, Text = "확인", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    private readonly Button _cancelButton = new()
    {
        Left = 422, Top = 384, Width = 80, Text = "취소", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    private readonly Button _applyButton = new()
    {
        Left = 508, Top = 384, Width = 80, Text = "적용", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
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
            control.Bounds = new Rectangle(0, 0, _pageHost.Width, _pageHost.Height);
            control.Visible = false;
            _pageHost.Controls.Add(control);
            page.LoadFrom(_settings);

            _tree.Nodes.Add(new TreeNode(page.CategoryName) { Tag = page });
        }

        Controls.Add(_tree);
        Controls.Add(_pageHost);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
        Controls.Add(_applyButton);

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
