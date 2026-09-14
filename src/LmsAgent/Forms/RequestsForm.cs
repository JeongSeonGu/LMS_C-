using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 기본정보 &gt; 요청사항. 조회·등록이 자유롭고, 본인이 작성한 글만 수정/삭제할 수 있습니다.
/// (대상은 항상 전체 공개로 등록되며, 개별 수신자 지정은 제공하지 않습니다.)
/// </summary>
public sealed class RequestsForm : Form
{
    private readonly WorkSupportApiClient _api;
    private RequestMeta _meta = new();

    private readonly ComboBox _scopeBox = new()
    {
        Left = 20, Top = 16, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly Button _addButton = new()
    {
        Left = 470, Top = 14, Width = 90, Text = "등록...", Anchor = AnchorStyles.Top | AnchorStyles.Right,
    };

    private readonly DataGridView _grid = new()
    {
        Left = 20, Top = 50, Width = 620, Height = 380,
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
    };

    private readonly Button _editButton = new()
    {
        Left = 20, Top = 440, Width = 80, Text = "수정...", Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
    };

    private readonly Button _deleteButton = new()
    {
        Left = 108, Top = 440, Width = 80, Text = "삭제", Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
    };

    private readonly Button _refreshButton = new()
    {
        Left = 470, Top = 440, Width = 80, Text = "새로고침", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    private readonly Button _closeButton = new()
    {
        Left = 560, Top = 440, Width = 80, Text = "닫기", Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    public RequestsForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "요청사항";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(660, 490);
        MinimumSize = new Size(640, 480);

        _scopeBox.Items.AddRange(new object[] { "전체", "내가 쓴 글", "나에게 온 글", "처리 대기" });
        _scopeBox.SelectedIndex = 0;

        BuildColumns();
        UiTheme.StyleGrid(_grid);
        UiTheme.StylePrimaryButton(_addButton);
        UiTheme.StyleSecondaryButton(_editButton);
        UiTheme.StyleDangerButton(_deleteButton);
        UiTheme.StyleSecondaryButton(_refreshButton);
        UiTheme.StyleSecondaryButton(_closeButton);

        Controls.Add(_scopeBox);
        Controls.Add(_addButton);
        Controls.Add(_grid);
        Controls.Add(_editButton);
        Controls.Add(_deleteButton);
        Controls.Add(_refreshButton);
        Controls.Add(_closeButton);

        _scopeBox.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _addButton.Click += OnAddClicked;
        _editButton.Click += OnEditClicked;
        _deleteButton.Click += OnDeleteClicked;
        _refreshButton.Click += async (_, _) => await LoadAsync();
        _closeButton.Click += (_, _) => Close();
        _grid.SelectionChanged += (_, _) => UpdateButtonStates();

        Load += async (_, _) => await InitializeAsync();
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "분류", DataPropertyName = "Category", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "제목", DataPropertyName = "Title", Width = 220 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "우선순위", DataPropertyName = "PriorityLabel", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "상태", DataPropertyName = "StatusLabel", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "작성자", DataPropertyName = "CreatedName", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "기한", DataPropertyName = "DueDate", Width = 80 });
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        var metaResult = await _api.GetRequestMetaAsync();
        if (metaResult.Ok && metaResult.Data is not null)
        {
            _meta = metaResult.Data;
        }

        await LoadAsync();
    }

    private string CurrentScope => _scopeBox.SelectedIndex switch
    {
        1 => "mine",
        2 => "tome",
        3 => "open",
        _ => "all",
    };

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var result = await _api.GetRequestsAsync(CurrentScope);
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "요청사항을 불러오지 못했습니다." : result.ErrorMessage,
                    "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _grid.DataSource = result.Data.Items;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"요청사항을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private RequestItem? SelectedItem =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as RequestItem : null;

    private void UpdateButtonStates()
    {
        var canEdit = SelectedItem?.IsMine ?? false;
        _editButton.Enabled = canEdit;
        _deleteButton.Enabled = canEdit;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        using var form = new RequestEditForm(_api, _meta);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var item = SelectedItem;
        if (item is null || !item.IsMine)
        {
            MessageBox.Show("본인이 작성한 요청사항만 수정할 수 있습니다.", "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var detailResult = await _api.GetRequestDetailAsync(item.Id);
        if (!detailResult.Ok || detailResult.Data is null)
        {
            MessageBox.Show(
                string.IsNullOrWhiteSpace(detailResult.ErrorMessage) ? "상세 정보를 불러오지 못했습니다." : detailResult.ErrorMessage,
                "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new RequestEditForm(_api, _meta, detailResult.Data.Request);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var item = SelectedItem;
        if (item is null || !item.IsMine)
        {
            MessageBox.Show("본인이 작성한 요청사항만 삭제할 수 있습니다.", "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show($"'{item.Title}' 요청사항을 삭제하시겠습니까?",
            "요청사항 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _api.DeleteRequestAsync(item.Id);
            if (result.Ok)
            {
                await LoadAsync();
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "삭제에 실패했습니다." : result.ErrorMessage,
                    "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                "요청사항", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
