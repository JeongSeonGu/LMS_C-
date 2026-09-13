using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;

namespace LmsAgent.Forms;

/// <summary>
/// 기본정보 &gt; 길라잡이 조회. 교무학사 길라잡이 문서함(대분류 &gt; 소분류 &gt; 문서)을 트리로 보여주고,
/// 선택한 문서를 내려받을 수 있습니다. 다운로드는 로그인 세션이 필요해 프로그램이 직접
/// 인증된 요청으로 내려받은 뒤 저장합니다(외부 브라우저로 열면 로그인 세션이 없어 실패합니다).
/// </summary>
public sealed class GuideDocsForm : Form
{
    private readonly WorkSupportApiClient _api;

    private readonly TreeView _tree = new() { Left = 20, Top = 20, Width = 300, Height = 400 };

    private readonly Label _selectedLabel = new()
    {
        Left = 336, Top = 20, Width = 260, Height = 60, Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
    };

    private readonly Button _downloadButton = new() { Left = 336, Top = 90, Width = 110, Text = "다운로드...", Enabled = false };
    private readonly Button _openButton = new() { Left = 336, Top = 126, Width = 110, Text = "열어보기", Enabled = false };
    private readonly Button _closeButton = new() { Left = 336, Top = 400, Width = 110, Text = "닫기" };

    private GuideDoc? _selectedDoc;

    public GuideDocsForm(WorkSupportApiClient api)
    {
        _api = api;

        Text = "길라잡이 조회";
        Icon = AppIconProvider.Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(616, 440);

        Controls.Add(_tree);
        Controls.Add(_selectedLabel);
        Controls.Add(_downloadButton);
        Controls.Add(_openButton);
        Controls.Add(_closeButton);

        _tree.AfterSelect += OnTreeSelect;
        _downloadButton.Click += async (_, _) => await DownloadAsync(openAfter: false);
        _openButton.Click += async (_, _) => await DownloadAsync(openAfter: true);
        _closeButton.Click += (_, _) => Close();

        Load += async (_, _) => await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        _tree.Nodes.Clear();

        try
        {
            var result = await _api.GetGuideCatalogAsync();
            if (!result.Ok || result.Data is null)
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(result.ErrorMessage) ? "길라잡이 목록을 불러오지 못했습니다." : result.ErrorMessage,
                    "길라잡이", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var major in result.Data.Majors)
            {
                var majorNode = new TreeNode(major.Name);
                foreach (var sub in major.Subs)
                {
                    var subNode = new TreeNode(sub.Title);
                    foreach (var doc in sub.Docs)
                    {
                        subNode.Nodes.Add(new TreeNode(doc.Title) { Tag = doc });
                    }
                    majorNode.Nodes.Add(subNode);
                }
                _tree.Nodes.Add(majorNode);
            }

            _tree.ExpandAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"길라잡이 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}",
                "길라잡이", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnTreeSelect(object? sender, TreeViewEventArgs e)
    {
        _selectedDoc = e.Node?.Tag as GuideDoc;

        var hasFile = _selectedDoc?.CurrentVersion?.File is not null;
        _downloadButton.Enabled = hasFile;
        _openButton.Enabled = hasFile;

        _selectedLabel.Text = _selectedDoc is null
            ? ""
            : $"{_selectedDoc.Title}\n{(string.IsNullOrWhiteSpace(_selectedDoc.UpdatedAt) ? "" : $"수정일: {_selectedDoc.UpdatedAt}")}";
    }

    private async System.Threading.Tasks.Task DownloadAsync(bool openAfter)
    {
        var file = _selectedDoc?.CurrentVersion?.File;
        if (file is null || string.IsNullOrWhiteSpace(file.DownloadUrl))
        {
            return;
        }

        _downloadButton.Enabled = false;
        _openButton.Enabled = false;

        try
        {
            var uri = _api.ResolveServerPath(file.DownloadUrl);
            var (bytes, _, serverFileName) = await _api.DownloadFileAsync(uri);

            var ext = string.IsNullOrWhiteSpace(file.Ext) ? "" : $".{file.Ext}";
            var suggestedName = string.IsNullOrWhiteSpace(serverFileName)
                ? $"{_selectedDoc!.Title}{ext}"
                : serverFileName;

            string savePath;
            if (openAfter)
            {
                // 미리 보기용으로는 임시 폴더에 저장해 바로 연다.
                var tempDir = Path.Combine(Path.GetTempPath(), "LmsAgentGuideDocs");
                Directory.CreateDirectory(tempDir);
                savePath = Path.Combine(tempDir, suggestedName);
                await File.WriteAllBytesAsync(savePath, bytes);

                Process.Start(new ProcessStartInfo(savePath) { UseShellExecute = true });
                return;
            }

            using var dialog = new SaveFileDialog { FileName = suggestedName };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            savePath = dialog.FileName;
            await File.WriteAllBytesAsync(savePath, bytes);

            MessageBox.Show("다운로드가 완료되었습니다.", "길라잡이", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"다운로드 중 오류가 발생했습니다: {ex.Message}",
                "길라잡이", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _downloadButton.Enabled = true;
            _openButton.Enabled = true;
        }
    }
}
