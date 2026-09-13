using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 기본정보 &gt; 공통계정. 학교 공통 계정(서비스명/아이디/비밀번호/바로가기)을 조회합니다.
/// 등록/수정/삭제는 관리자 전용이라 이 화면에서는 목록 조회와 비밀번호 "보기"만 제공합니다.
/// </summary>
public sealed class SharedAccountsForm : Form
{
    private readonly WorkSupportApiClient _api;

    private readonly DataGridView _grid = new()
    {
        Left = 20, Top = 20, Width = 560, Height = 380,
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private readonly Button _closeButton = new() { Left = 480, Top = 410, Width = 100, Text = "닫기" };

    private DataGridViewButtonColumn _viewColumn = null!;
    private DataGridViewButtonColumn _openColumn = null!;

    public SharedAccountsForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "공통 계정 정보";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 440);

        BuildColumns();
        UiTheme.StyleGrid(_grid);
        UiTheme.StyleGridButtonColumn(_viewColumn, primary: true);
        UiTheme.StyleGridButtonColumn(_openColumn);

        Controls.Add(_grid);
        Controls.Add(_closeButton);

        UiTheme.StyleSecondaryButton(_closeButton);

        _closeButton.Click += (_, _) => Close();
        _grid.CellContentClick += OnCellContentClick;

        Load += async (_, _) => await LoadAsync();
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "분류", DataPropertyName = "Category", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "서비스명", DataPropertyName = "Title", Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "아이디", DataPropertyName = "AccountId", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "비밀번호", DataPropertyName = "DisplayPassword", Width = 110 });

        _viewColumn = new DataGridViewButtonColumn { HeaderText = "", Text = "보기", UseColumnTextForButtonValue = true, Width = 60 };
        _openColumn = new DataGridViewButtonColumn { HeaderText = "", Text = "바로가기", UseColumnTextForButtonValue = true, Width = 70 };
        _grid.Columns.Add(_viewColumn);
        _grid.Columns.Add(_openColumn);
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var result = await _api.GetSharedAccountsAsync();
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "공통 계정 정보를 불러오지 못했습니다." : result.ErrorMessage,
                    "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rows = new List<AccountRow>();
            foreach (var item in result.Data.Items)
            {
                rows.Add(new AccountRow(item));
            }
            _grid.DataSource = rows;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"공통 계정 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not AccountRow row) return;

        if (e.ColumnIndex == _openColumn.Index)
        {
            if (string.IsNullOrWhiteSpace(row.Account.SiteUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo(row.Account.SiteUrl) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show("바로가기 주소를 열 수 없습니다.", "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (e.ColumnIndex == _viewColumn.Index)
        {
            if (!row.Account.HasPassword)
            {
                MessageBox.Show("등록된 비밀번호가 없습니다.", "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var result = await _api.RevealSharedAccountAsync(row.Account.Id);
                if (result.Ok && result.Data is not null)
                {
                    MessageBox.Show(
                        $"서비스: {result.Data.Title}\n아이디: {result.Data.AccountId}\n비밀번호: {result.Data.AccountPw}",
                        "비밀번호 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(result.ErrorMessage) ? "열람 권한이 없습니다." : result.ErrorMessage,
                        "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비밀번호를 확인하는 중 오류가 발생했습니다: {ex.Message}",
                    "공통 계정 정보", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private sealed class AccountRow
    {
        public AccountRow(SharedAccount account)
        {
            Account = account;
        }

        public SharedAccount Account { get; }
        public string? Category => Account.Category;
        public string Title => Account.Title;
        public string? AccountId => Account.AccountId;
        public string DisplayPassword => Account.HasPassword ? "••••••••" : "-";
    }
}
