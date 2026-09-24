using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 새 버전이 있을 때(<see cref="UpdateCheckResult.Available"/>) 사용자에게 물어보는 안내
/// 창입니다. manifest.json의 notes를 그대로 보여주고, "지금 업데이트"를 눌러야만 실제
/// 다운로드가 시작됩니다(자동으로 조용히 받아서 적용하던 예전 방식과 달리, 사용자가 지금
/// 업데이트할지 스스로 선택할 수 있습니다). manifest.mandatory가 true면 "나중에"를 감추고
/// 강제로 업데이트하도록 안내합니다.
/// </summary>
public sealed class UpdateAvailableForm : Form
{
    private readonly Label _versionLabel = new()
    {
        Left = 24, Top = 56, Width = 372, Height = 24, Font = UiTheme.BoldFont, ForeColor = UiTheme.SkyDark,
    };

    private readonly Label _mandatoryLabel = new()
    {
        Left = 24, Top = 84, Width = 372, Height = 20, Font = UiTheme.BoldFont, ForeColor = UiTheme.Danger,
        Text = "⚠ 이 업데이트는 필수입니다.", Visible = false,
    };

    private readonly TextBox _notesBox = new()
    {
        Left = 24, Top = 112, Width = 372, Height = 140,
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.FixedSingle, BackColor = UiTheme.SkyPale,
    };

    private readonly Button _laterButton = new() { Left = 196, Top = 264, Width = 90, Height = 32, Text = "나중에" };
    private readonly Button _updateButton = new() { Left = 296, Top = 264, Width = 100, Height = 32, Text = "지금 업데이트" };

    public UpdateAvailableForm(UpdateManifest manifest, Version currentVersion, Version latestVersion)
    {
        Text = "업데이트 확인";
        Icon = AppIconProvider.Icon;
        UiTheme.ApplyForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 316);

        var header = new Label
        {
            Left = 24, Top = 16, Width = 372, Height = 28, Text = "새 버전이 있습니다",
        };
        UiTheme.StyleHeaderLabel(header);
        Controls.Add(header);

        _versionLabel.Text = $"현재 버전 {currentVersion}  →  새 버전 {latestVersion}";
        Controls.Add(_versionLabel);

        if (manifest.Mandatory)
        {
            _mandatoryLabel.Visible = true;
            _laterButton.Visible = false;
            _updateButton.Left = 296;
        }
        Controls.Add(_mandatoryLabel);

        _notesBox.Text = string.IsNullOrWhiteSpace(manifest.Notes) ? "(업데이트 설명이 없습니다.)" : manifest.Notes;
        Controls.Add(_notesBox);

        UiTheme.StyleSecondaryButton(_laterButton);
        UiTheme.StylePrimaryButton(_updateButton);
        _laterButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _updateButton.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_laterButton);
        Controls.Add(_updateButton);

        AcceptButton = _updateButton;
        CancelButton = manifest.Mandatory ? null : _laterButton;
    }
}
