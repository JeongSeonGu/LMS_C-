using System;
using System.Drawing;
using System.Windows.Forms;
using LmsAgent.Interop;
using LmsAgent.Services;

namespace LmsAgent.Forms;

/// <summary>
/// 업무 일지와는 완전히 별개의, 실제 Windows 스티커 메모 같은 독립 창입니다. 업무 일지 창
/// 위에 겹쳐 그려지는 것이 아니라 그 자체로 하나의 떠 있는 창이며, 여러 개를 동시에 띄울 수
/// 있습니다. 내용은 서버와 무관하게 이 PC에만 저장됩니다(StickyNoteService).
/// </summary>
public sealed class StickyNoteForm : Form
{
    private static readonly Color NoteColor = Color.FromArgb(255, 247, 168);
    private static readonly Color HeaderColor = Color.FromArgb(255, 232, 140);
    private static readonly Color PlaceholderColor = Color.FromArgb(150, 130, 70);
    private static readonly Color TextColor = Color.FromArgb(60, 45, 10);
    private const string PlaceholderText = "메모를 작성하세요...";

    public Guid Id { get; }

    /// <summary>헤더의 + 버튼 — 새 메모를 하나 더 만들어 달라는 요청.</summary>
    public event EventHandler? NewNoteRequested;

    /// <summary>이 메모를 완전히 삭제해 달라는 요청("…" 메뉴 &gt; 메모 삭제, 확인 후).</summary>
    public event EventHandler? DeleteRequested;

    /// <summary>내용·위치·크기가 바뀔 때마다 발생합니다(디바운스 저장용).</summary>
    public event EventHandler? ContentOrBoundsChanged;

    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 30, BackColor = HeaderColor };
    private readonly Panel _toolbar = new() { Dock = DockStyle.Bottom, Height = 30, BackColor = HeaderColor };

    private readonly Button _newButton = new()
    {
        Dock = DockStyle.Left, Width = 30, Text = "+", FlatStyle = FlatStyle.Flat, ForeColor = TextColor,
    };

    private readonly Button _menuButton = new()
    {
        Dock = DockStyle.Right, Width = 30, Text = "…", FlatStyle = FlatStyle.Flat, ForeColor = TextColor,
    };

    private readonly Button _closeButton = new()
    {
        Dock = DockStyle.Right, Width = 30, Text = "×", FlatStyle = FlatStyle.Flat, ForeColor = TextColor,
    };

    private readonly RichTextBox _textBox = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = NoteColor,
        Font = new Font("맑은 고딕", 10F),
        AcceptsTab = true,
    };

    private bool _showingPlaceholder;
    private bool _dragging;
    private Point _dragStart;

    public StickyNoteForm(Guid id)
    {
        Id = id;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(260, 300);
        BackColor = NoteColor;

        UiTheme.StyleFlatToolButton(_newButton);
        _newButton.BackColor = HeaderColor;
        _newButton.FlatAppearance.BorderSize = 0;
        _newButton.Font = new Font(_newButton.Font.FontFamily, 12F, FontStyle.Bold);

        _menuButton.FlatAppearance.BorderSize = 0;
        _menuButton.BackColor = HeaderColor;
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.BackColor = HeaderColor;

        _header.Controls.Add(_newButton);
        _header.Controls.Add(_closeButton);
        _header.Controls.Add(_menuButton);

        BuildToolbar();

        Controls.Add(_textBox);
        Controls.Add(_toolbar);
        Controls.Add(_header);

        _newButton.Click += (_, _) => NewNoteRequested?.Invoke(this, EventArgs.Empty);
        _closeButton.Click += (_, _) => Hide();
        _menuButton.Click += OnMenuClicked;

        // 헤더를 드래그해서 창을 옮긴다(실제 스티커 메모처럼).
        _header.MouseDown += (_, e) => { _dragging = true; _dragStart = e.Location; };
        _header.MouseMove += (_, e) =>
        {
            if (_dragging)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            }
        };
        _header.MouseUp += (_, _) => { _dragging = false; ContentOrBoundsChanged?.Invoke(this, EventArgs.Empty); };

        ShowPlaceholderIfEmpty();
        _textBox.Enter += (_, _) => ClearPlaceholder();
        _textBox.Leave += (_, _) => ShowPlaceholderIfEmpty();
        _textBox.TextChanged += (_, _) =>
        {
            if (!_showingPlaceholder)
            {
                ContentOrBoundsChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Move += (_, _) => ContentOrBoundsChanged?.Invoke(this, EventArgs.Empty);
        Resize += (_, _) => ContentOrBoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    private void BuildToolbar()
    {
        var bold = NewToolButton("B", new Font("맑은 고딕", 9F, FontStyle.Bold));
        var italic = NewToolButton("I", new Font("맑은 고딕", 9F, FontStyle.Italic));
        var underline = NewToolButton("U", new Font("맑은 고딕", 9F, FontStyle.Underline));
        var strike = NewToolButton("S", new Font("맑은 고딕", 9F, FontStyle.Strikeout));
        var list = NewToolButton("≡", UiTheme.BaseFont);
        var image = NewToolButton("🖼", UiTheme.BaseFont);

        bold.Click += (_, _) => ToggleFontStyle(FontStyle.Bold);
        italic.Click += (_, _) => ToggleFontStyle(FontStyle.Italic);
        underline.Click += (_, _) => ToggleFontStyle(FontStyle.Underline);
        strike.Click += (_, _) => ToggleFontStyle(FontStyle.Strikeout);
        list.Click += (_, _) => ToggleBulletList();
        image.Click += (_, _) => InsertImage();

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
        };
        flow.Controls.Add(bold);
        flow.Controls.Add(italic);
        flow.Controls.Add(underline);
        flow.Controls.Add(strike);
        flow.Controls.Add(list);
        flow.Controls.Add(image);
        _toolbar.Controls.Add(flow);
    }

    private static Button NewToolButton(string text, Font font) => new()
    {
        Width = 30, Height = 26, Text = text, Font = font,
        FlatStyle = FlatStyle.Flat, ForeColor = TextColor, BackColor = HeaderColor,
        Margin = new Padding(1),
    };

    /// <summary>선택 영역(또는 선택이 없으면 이후 입력될 글자)의 서식을 토글합니다.</summary>
    private void ToggleFontStyle(FontStyle style)
    {
        _textBox.Focus();
        ClearPlaceholder();

        var current = _textBox.SelectionFont ?? _textBox.Font;
        var toggled = current.Style.HasFlag(style) ? current.Style & ~style : current.Style | style;
        _textBox.SelectionFont = new Font(current, toggled);
    }

    /// <summary>선택한 줄들 앞에 "• "를 붙이거나 뗀다(간단한 글머리 기호 토글).</summary>
    private void ToggleBulletList()
    {
        _textBox.Focus();
        ClearPlaceholder();
        var selStart = _textBox.SelectionStart;
        var selEnd = selStart + _textBox.SelectionLength;
        var firstLine = _textBox.GetLineFromCharIndex(selStart);
        var lastLine = _textBox.GetLineFromCharIndex(selEnd);

        // 줄 개수는 바뀌지 않으므로(접두사만 넣거나 빼므로), 줄 번호로 매번 다시 인덱스를 구하면
        // 앞 줄 편집으로 뒤 줄의 문자 위치가 밀려도 항상 정확한 위치를 가리킨다.
        for (var i = firstLine; i <= lastLine; i++)
        {
            var idx = _textBox.GetFirstCharIndexFromLine(i);
            if (idx < 0)
            {
                continue;
            }

            var nextIdx = i + 1 < _textBox.Lines.Length ? _textBox.GetFirstCharIndexFromLine(i + 1) : _textBox.TextLength;
            var lineText = _textBox.Text.Substring(idx, Math.Max(0, nextIdx - idx)).TrimEnd('\r', '\n');

            _textBox.Select(idx, lineText.Length);
            _textBox.SelectedText = lineText.StartsWith("• ", StringComparison.Ordinal)
                ? lineText[2..]
                : "• " + lineText;
        }
    }

    private void InsertImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            Title = "메모에 넣을 이미지 선택",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var image = Image.FromFile(dialog.FileName);
            using var bitmap = new Bitmap(image);
            ClearPlaceholder();
            Clipboard.SetImage(bitmap);
            _textBox.Focus();
            _textBox.Paste();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"이미지를 넣지 못했습니다: {ex.Message}", "쪽지", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        // ContextMenuStrip.Show()는 비동기(사용자가 닫을 때까지 열려 있음)이므로 using으로
        // 즉시 Dispose하면 안 된다 — 닫힐 때(Closed) 스스로 정리하도록 한다.
        var menu = new ContextMenuStrip();
        menu.Closed += (_, _) => menu.Dispose();
        menu.Items.Add("메모 삭제", null, (_, _) =>
        {
            var confirm = MessageBox.Show(this, "이 메모를 삭제하시겠습니까? 되돌릴 수 없습니다.",
                "메모 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                DeleteRequested?.Invoke(this, EventArgs.Empty);
            }
        });
        menu.Show(_menuButton, new Point(0, _menuButton.Height));
    }

    private void ClearPlaceholder()
    {
        if (!_showingPlaceholder)
        {
            return;
        }

        _showingPlaceholder = false;
        _textBox.Clear();
        _textBox.ForeColor = TextColor;
        _textBox.Font = new Font("맑은 고딕", 10F);
    }

    private void ShowPlaceholderIfEmpty()
    {
        if (_textBox.TextLength > 0)
        {
            return;
        }

        _showingPlaceholder = true;
        _textBox.ForeColor = PlaceholderColor;
        _textBox.Font = new Font("맑은 고딕", 10F, FontStyle.Italic);
        _textBox.Text = PlaceholderText;
    }

    /// <summary>서식(RTF)째로 저장/복원합니다 — 굵게·기울임·삽입한 이미지가 그대로 유지됩니다.</summary>
    public void LoadRtf(string? rtf)
    {
        if (string.IsNullOrEmpty(rtf))
        {
            ShowPlaceholderIfEmpty();
            return;
        }

        try
        {
            _showingPlaceholder = false;
            _textBox.Rtf = rtf;
            _textBox.ForeColor = TextColor;
            if (_textBox.TextLength == 0)
            {
                ShowPlaceholderIfEmpty();
            }
        }
        catch
        {
            // 손상된 RTF는 무시하고 빈 메모로 시작한다.
        }
    }

    /// <summary>현재 내용을 RTF로 반환합니다. 플레이스홀더만 있는 상태면 null(저장할 내용 없음).</summary>
    public string? SaveRtf() => _showingPlaceholder ? null : _textBox.Rtf;
}
