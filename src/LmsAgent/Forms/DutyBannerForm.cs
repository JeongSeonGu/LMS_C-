using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LmsAgent.Interop;
using LmsAgent.Services;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Forms;

/// <summary>
/// 복무 변동사항(출장/연가) 알림 배너. 화면 우측 상단에 항상 위로 고정 표시되는
/// 모던 스타일의 카드형 토스트입니다. 포커스를 가져가지 않도록 WS_EX_NOACTIVATE를 사용하고,
/// CS_DROPSHADOW로 실제 창 그림자를, 둥근 모서리는 Region으로 직접 그립니다.
/// 클릭하면(또는 우상단 닫기 표시를 누르면) 바로 닫힙니다.
/// </summary>
public sealed class DutyBannerForm : Form
{
    private const int CornerRadius = 16;
    private const int IconDiameter = 36;
    private const int Margin = 14;

    private static readonly Font HeadlineFont = new("맑은 고딕", 10.5F, FontStyle.Bold);
    private static readonly Font DetailFont = new("맑은 고딕", 9.5F);
    private static readonly Font CloseFont = new("맑은 고딕", 9F, FontStyle.Bold);
    private static readonly Font GlyphFont = new("맑은 고딕", 15F, FontStyle.Bold);

    private readonly Timer _fadeTimer;
    private double _targetOpacity = 1.0;
    private Rectangle _closeRect;

    private string _headline = "";
    private string _detail = "";
    private Color _accentColor = UiTheme.Sky;

    public DutyBannerForm()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(360, 78);
        BackColor = UiTheme.Surface;
        Cursor = Cursors.Hand;
        Opacity = 0;

        _fadeTimer = new Timer { Interval = 15 };
        _fadeTimer.Tick += OnFadeTick;

        VisibleChanged += OnVisibleChanged;
        MouseClick += OnMouseClickAnywhere;
        Resize += (_, _) => ApplyRoundedRegion();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW — 테두리 없는 팝업에 실제 창 그림자를 입힌다.
            return cp;
        }
    }

    /// <param name="urgent">true면 "당일 부재중"(강한 경고색), false면 "하루 전 예정"(안내색)으로 표시합니다.</param>
    public void UpdateContent(string text, bool urgent)
    {
        _headline = urgent ? "오늘 부재 안내" : "내일 부재 예정";
        _detail = text;
        _accentColor = urgent ? UiTheme.Danger : UiTheme.Sky;
        Invalidate();
    }

    public void PositionTopRight(Screen screen)
    {
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Top + 16);
    }

    /// <summary>환경설정 &gt; 복무의 투명도(0~100%)를 적용합니다.</summary>
    public void SetOpacityPercent(int percent)
    {
        _targetOpacity = Math.Clamp(percent, 5, 100) / 100.0;
        if (Visible && !_fadeTimer.Enabled)
        {
            Opacity = _targetOpacity;
        }
    }

    private void OnVisibleChanged(object? sender, EventArgs e)
    {
        if (!Visible)
        {
            _fadeTimer.Stop();
            return;
        }

        Opacity = 0;
        _fadeTimer.Start();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var next = Opacity + 0.12;
        if (next >= _targetOpacity)
        {
            Opacity = _targetOpacity;
            _fadeTimer.Stop();
        }
        else
        {
            Opacity = next;
        }
    }

    private void OnMouseClickAnywhere(object? sender, MouseEventArgs e)
    {
        Hide();
    }

    private void ApplyRoundedRegion()
    {
        using var path = RoundedRectPath(ClientRectangle, CornerRadius);
        Region?.Dispose();
        Region = new Region(path);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bounds = ClientRectangle;

        using (var bg = new SolidBrush(UiTheme.Surface))
        {
            g.FillRectangle(bg, bounds);
        }

        // 왼쪽 강조 바 — 긴급도에 따라 위험/안내 색을 그대로 보여줍니다.
        var accentRect = new Rectangle(0, 0, 6, bounds.Height);
        using (var accentBrush = new SolidBrush(_accentColor))
        {
            g.FillRectangle(accentBrush, accentRect);
        }

        // 아이콘 배지
        var iconRect = new Rectangle(Margin + 6, (bounds.Height - IconDiameter) / 2, IconDiameter, IconDiameter);
        using (var iconBrush = new SolidBrush(_accentColor))
        {
            g.FillEllipse(iconBrush, iconRect);
        }

        var glyph = _accentColor == UiTheme.Danger ? "!" : "i";
        TextRenderer.DrawText(g, glyph, GlyphFont, iconRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // 닫기(×) 표시
        _closeRect = new Rectangle(bounds.Width - 28, 8, 20, 20);
        TextRenderer.DrawText(g, "×", CloseFont, _closeRect, UiTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // 텍스트 영역
        var textLeft = iconRect.Right + 12;
        var textWidth = _closeRect.Left - textLeft - 8;
        var headlineRect = new Rectangle(textLeft, 14, textWidth, 22);
        var detailRect = new Rectangle(textLeft, 36, textWidth, bounds.Height - 44);

        TextRenderer.DrawText(g, _headline, HeadlineFont, headlineRect, UiTheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, _detail, DetailFont, detailRect, UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
