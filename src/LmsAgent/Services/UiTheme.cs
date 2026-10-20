using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LmsAgent.Services;

/// <summary>
/// 프로그램 전체에서 쓰는 공통 디자인 테마입니다.
///
/// 2026-10 UI 전면 개편: 하늘색+오렌지 2색 테마를, 로그인·오늘의 알림·메인 창 목업에서
/// 쓰인 네이비 사이드바 + 파란색 단일 강조색 디자인으로 바꿨습니다. 프로그램의 거의
/// 모든 화면이 색상을 직접 지정하지 않고 이 클래스의 상수(Sky/Orange/...)와
/// StyleXxx 헬퍼를 통해서만 참조하는 기존 관례 덕분에, 아래 팔레트 "값"만 새 디자인에
/// 맞춰 바꾸는 것으로 거의 모든 화면이 한 번에 새 색으로 바뀝니다 — 상수 "이름"은
/// (호출부 30여 곳을 전부 고치지 않도록) 그대로 두었습니다. Orange 계열과 Sky 계열이
/// 이제 같은 파란색 계열을 가리키는 것은 실수가 아니라, 목업이 주요 액션(기존
/// Orange 역할)과 강조/링크(기존 Sky 역할)에 같은 파란색 하나만 쓰는 "단일 강조색"
/// 디자인이기 때문입니다.
/// WinForms 기본 컨트롤 위에 색상·글꼴·여백을 통일해 적용하는 얇은 헬퍼 모음입니다.
/// </summary>
public static class UiTheme
{
    // ── 팔레트(모던 네이비·블루 디자인) ──────────────────────
    public static readonly Color Sky = Color.FromArgb(31, 95, 191);        // #1F5FBF
    public static readonly Color SkyDark = Color.FromArgb(23, 74, 150);    // #174A96
    public static readonly Color SkyLight = Color.FromArgb(234, 241, 252); // #EAF1FC
    public static readonly Color SkyPale = Color.FromArgb(245, 247, 250);  // #F5F7FA

    public static readonly Color Orange = Color.FromArgb(31, 95, 191);        // #1F5FBF — Sky와 같은 단일 강조색
    public static readonly Color OrangeDark = Color.FromArgb(23, 74, 150);    // #174A96
    public static readonly Color OrangeLight = Color.FromArgb(234, 241, 252); // #EAF1FC

    public static readonly Color Background = SkyPale;
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(220, 226, 234);   // #DCE2EA
    public static readonly Color TextPrimary = Color.FromArgb(27, 36, 48); // #1B2430
    public static readonly Color TextSecondary = Color.FromArgb(91, 102, 118); // #5B6676
    public static readonly Color Success = Color.FromArgb(21, 128, 61);    // #15803D
    public static readonly Color Danger = Color.FromArgb(196, 43, 28);     // #C42B1C

    // ── 모던 디자인 전용 추가 색상(네이비 사이드바 등, 위 공용 팔레트로는 못 담는 색) ──
    public static readonly Color ModernNavy = Color.FromArgb(20, 33, 61);        // #14213D 사이드바 배경
    public static readonly Color ModernNavyActive = Color.FromArgb(44, 74, 134); // #2C4A86 선택된 메뉴/아바타
    public static readonly Color ModernNavyBorder = Color.FromArgb(38, 54, 89);  // #263659 사이드바 구분선
    public static readonly Color ModernNavyMuted = Color.FromArgb(169, 180, 198); // #A9B4C6 사이드바 보조 텍스트

    public static readonly Color ModernAccent = Color.FromArgb(31, 95, 191);       // #1F5FBF 주 강조색(파랑)
    public static readonly Color ModernAccentHover = Color.FromArgb(23, 74, 150);  // #174A96
    public static readonly Color ModernAccentTint = Color.FromArgb(47, 111, 207);  // #2F6FCF 강조 타일 아이콘 배경
    public static readonly Color ModernAccentSoft = Color.FromArgb(234, 241, 252); // #EAF1FC 연한 배지/배경

    public static readonly Color ModernBackground = Color.FromArgb(245, 247, 250); // #F5F7FA
    public static readonly Color ModernCardBorder = Color.FromArgb(220, 226, 234); // #DCE2EA 카드 테두리
    public static readonly Color ModernFieldBorder = Color.FromArgb(201, 209, 220); // #C9D1DC 입력창/창 테두리
    public static readonly Color ModernDivider = Color.FromArgb(227, 232, 239);    // #E3E8EF
    public static readonly Color ModernDividerLight = Color.FromArgb(238, 241, 245); // #EEF1F5

    public static readonly Color ModernText = Color.FromArgb(27, 36, 48);        // #1B2430
    public static readonly Color ModernTextMuted = Color.FromArgb(91, 102, 118); // #5B6676
    public static readonly Color ModernIconMuted = Color.FromArgb(58, 70, 86);   // #3A4656 (창 버튼 아이콘)

    public static readonly Color ModernSuccessDot = Color.FromArgb(21, 128, 61);  // #15803D
    public static readonly Color ModernSuccessText = Color.FromArgb(21, 98, 47);  // #15622F
    public static readonly Color ModernSuccessBg = Color.FromArgb(232, 245, 236); // #E8F5EC

    public static readonly Color ModernWarnText = Color.FromArgb(154, 69, 8);   // #9A4508
    public static readonly Color ModernWarnBg = Color.FromArgb(253, 243, 231);  // #FDF3E7

    public static readonly Color ModernCloseHover = Color.FromArgb(196, 43, 28); // #C42B1C 창 버튼(×) 호버 배경

    // ── 글꼴 ──────────────────────────────────────────────────
    public static readonly Font BaseFont = new("맑은 고딕", 9.5F);
    public static readonly Font BoldFont = new("맑은 고딕", 9.5F, FontStyle.Bold);
    public static readonly Font TitleFont = new("맑은 고딕", 13F, FontStyle.Bold);
    public static readonly Font SubTitleFont = new("맑은 고딕", 10F, FontStyle.Bold);

    // 모던 디자인 전용 글꼴(위 세 창에서만 사용).
    public static readonly Font ModernTinyFont = new("맑은 고딕", 8F);
    public static readonly Font ModernSmallFont = new("맑은 고딕", 8.5F);
    public static readonly Font ModernBodyFont = new("맑은 고딕", 9.5F);
    public static readonly Font ModernBodyBoldFont = new("맑은 고딕", 9.5F, FontStyle.Bold);
    public static readonly Font ModernLabelBoldFont = new("맑은 고딕", 9F, FontStyle.Bold);
    public static readonly Font ModernSubTitleFont = new("맑은 고딕", 10.5F, FontStyle.Bold);
    public static readonly Font ModernTitleFont = new("맑은 고딕", 15F, FontStyle.Bold);
    public static readonly Font ModernHeadlineFont = new("맑은 고딕", 13.5F, FontStyle.Bold);

    /// <summary>창 배경/기본 글꼴을 적용합니다. 각 폼 생성자 맨 앞에서 호출하세요.</summary>
    public static void ApplyForm(Form form)
    {
        form.BackColor = Background;
        form.Font = BaseFont;

        // Modern Flat UI: 폼이 실제로 뜨는 시점(모든 Controls.Add가 끝난 뒤)에 한 번,
        // 자식 컨트롤 전체를 훑어 표준 컨트롤의 3D 스타일을 평평하게 통일합니다.
        // 버튼/그리드/트리 등 이미 개별적으로 스타일을 입힌 컨트롤은 건드리지 않습니다.
        form.Load += (_, _) => ApplyFlatStyle(form);
    }

    /// <summary>
    /// 자식 컨트롤을 재귀적으로 훑으며 ComboBox/TextBox 등 표준 컨트롤의 기본 3D 테두리를
    /// 평평한 스타일로 바꿉니다. <see cref="ApplyForm"/>이 폼 로드 시 자동으로 호출합니다.
    /// </summary>
    public static void ApplyFlatStyle(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case ComboBox comboBox:
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;

                case TextBox textBox when textBox.BorderStyle != BorderStyle.None:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case NumericUpDown numericUpDown:
                    numericUpDown.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case CheckBox checkBox:
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.FlatAppearance.BorderSize = 1;
                    checkBox.FlatAppearance.BorderColor = Sky;
                    checkBox.FlatAppearance.CheckedBackColor = Orange;
                    break;

                case RadioButton radioButton:
                    radioButton.FlatStyle = FlatStyle.Flat;
                    radioButton.FlatAppearance.BorderSize = 0;
                    radioButton.FlatAppearance.CheckedBackColor = OrangeLight;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyFlatStyle(control);
            }
        }
    }

    /// <summary>등록/저장/로그인/확인 등 화면의 대표 액션 버튼(오렌지, 채움).</summary>
    public static void StylePrimaryButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = OrangeDark;
        button.FlatAppearance.MouseDownBackColor = OrangeDark;
        button.BackColor = Orange;
        button.ForeColor = Color.White;
        button.Font = BoldFont;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>취소/닫기 등 보조 액션 버튼(하늘색 아웃라인).</summary>
    public static void StyleSecondaryButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Sky;
        button.FlatAppearance.MouseOverBackColor = SkyLight;
        button.FlatAppearance.MouseDownBackColor = SkyLight;
        button.BackColor = Surface;
        button.ForeColor = SkyDark;
        button.Font = BaseFont;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>삭제 등 되돌리기 어려운 위험한 동작을 위한 버튼(빨간 아웃라인).</summary>
    public static void StyleDangerButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Danger;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 231, 227);
        button.BackColor = Surface;
        button.ForeColor = Danger;
        button.Font = BaseFont;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>작은 툴바형 버튼(달력의 &lt;/&gt;/오늘 등)을 위한 가벼운 스타일.</summary>
    public static void StyleFlatToolButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = SkyLight;
        button.BackColor = Surface;
        button.ForeColor = TextPrimary;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>
    /// 트레이 왼쪽 클릭 창(<see cref="LmsAgent.Forms.TrayControlPanelForm"/>)의 카테고리별
    /// 하위 항목을, 세로로 긴 목록 대신 모서리가 둥근 정사각형/직사각형 "타일" 버튼으로
    /// 보여주기 위한 스타일입니다. Button.Region을 둥근 사각형으로 잘라 그리는 것만으로
    /// 애니메이션 없이도 각지지 않은 "리퀴드" 느낌을 낼 수 있습니다 — 버튼 크기가 정해진
    /// 뒤(Width/Height 설정 후)에 호출해야 하며, 이후 크기가 바뀌어도(드문 경우) Resize에서
    /// 다시 잘라줍니다.
    /// </summary>
    public static void StyleLiquidTileButton(Button button, int cornerRadius = 18)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 2;
        button.FlatAppearance.BorderColor = Sky;
        button.FlatAppearance.MouseOverBackColor = Sky;
        button.FlatAppearance.MouseDownBackColor = SkyDark;
        button.BackColor = SkyLight;
        button.ForeColor = SkyDark;
        button.Font = BoldFont;
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;

        void Reclip(object? sender, EventArgs e) => ApplyRoundedRegion(button, cornerRadius);

        button.Resize += Reclip;
        Reclip(button, EventArgs.Empty);

        // 마우스가 올라오면 배경이 진해지는 것과 함께 글자색도 흰색으로 바꿔 눈에 띄게 한다
        // (FlatAppearance.MouseOverBackColor만으로는 글자색까지는 못 바꾸므로 직접 처리).
        button.MouseEnter += (_, _) => button.ForeColor = Color.White;
        button.MouseLeave += (_, _) => button.ForeColor = SkyDark;
    }

    public static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
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

    /// <summary>컨트롤을 둥근 사각형으로 잘라낸다(배지·아이콘 배지·카드 등). 크기가 바뀔 때마다
    /// 다시 호출해야 하면 Resize 이벤트에서 직접 불러 쓴다.</summary>
    public static void ApplyRoundedRegion(Control control, int radius)
    {
        using var path = RoundedRectPath(control.ClientRectangle, radius);
        control.Region = new Region(path);
    }

    /// <summary>테두리 없는 창(FormBorderStyle.None)에서, 지정한 컨트롤(제목표시줄 영역)을
    /// 드래그하면 창이 함께 움직이도록 한다. 로그인 창·오늘의 알림·메인 창이 공통으로 쓴다.</summary>
    public static void EnableDragMove(Control dragHandle, Form form)
    {
        var dragging = false;
        var dragStart = Point.Empty;

        dragHandle.MouseDown += (_, e) => { dragging = true; dragStart = e.Location; };
        dragHandle.MouseMove += (_, e) =>
        {
            if (dragging)
            {
                form.Location = new Point(form.Location.X + e.X - dragStart.X, form.Location.Y + e.Y - dragStart.Y);
            }
        };
        dragHandle.MouseUp += (_, _) => dragging = false;
    }

    /// <summary>로그인 창·오늘의 알림·메인 창에서 쓰는 꽉 찬 파랑 주요 액션 버튼
    /// (모서리를 둥글게 잘라낸다).</summary>
    public static void StyleModernPrimaryButton(Button button, int cornerRadius = 8)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ModernAccentHover;
        button.FlatAppearance.MouseDownBackColor = ModernAccentHover;
        button.BackColor = ModernAccent;
        button.ForeColor = Color.White;
        button.Font = ModernBodyBoldFont;
        button.Cursor = Cursors.Hand;

        void Reclip(object? sender, EventArgs e) => ApplyRoundedRegion(button, cornerRadius);
        button.Resize += Reclip;
        Reclip(button, EventArgs.Empty);
    }

    /// <summary>모던 디자인의 보조 액션 버튼(흰 배경, 옅은 회색 테두리).</summary>
    public static void StyleModernOutlineButton(Button button, int cornerRadius = 8)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = ModernFieldBorder;
        button.FlatAppearance.MouseOverBackColor = ModernAccentSoft;
        button.BackColor = Color.White;
        button.ForeColor = ModernText;
        button.Font = ModernBodyFont;
        button.Cursor = Cursors.Hand;

        void Reclip(object? sender, EventArgs e) => ApplyRoundedRegion(button, cornerRadius);
        button.Resize += Reclip;
        Reclip(button, EventArgs.Empty);
    }

    /// <summary>모던 창의 제목표시줄 아이콘 버튼(최소화/닫기). <paramref name="isClose"/>가
    /// true면 호버 시 빨간 배경(윈도우 표준 닫기 동작)으로 바뀐다.</summary>
    public static void StyleModernWindowButton(Button button, bool isClose = false)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Color.Transparent;
        button.ForeColor = ModernIconMuted;
        button.Cursor = Cursors.Hand;

        if (isClose)
        {
            button.FlatAppearance.MouseOverBackColor = ModernCloseHover;
            button.MouseEnter += (_, _) => button.ForeColor = Color.White;
            button.MouseLeave += (_, _) => button.ForeColor = ModernIconMuted;
        }
        else
        {
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 234, 240); // #E6EAF0
        }
    }

    /// <summary>모던 디자인의 둥근 알약(pill) 모양 상태 배지(연결됨/배지 카운트 등)를 그린다.</summary>
    public static void StyleModernPill(Label label, Color background, Color foreColor, int cornerRadius = 10)
    {
        label.BackColor = background;
        label.ForeColor = foreColor;
        label.Font = ModernLabelBoldFont;
        label.TextAlign = ContentAlignment.MiddleCenter;

        void Reclip(object? sender, EventArgs e) => ApplyRoundedRegion(label, cornerRadius);
        label.Resize += Reclip;
        Reclip(label, EventArgs.Empty);
    }

    public static void StyleHeaderLabel(Label label)
    {
        label.Font = TitleFont;
        label.ForeColor = SkyDark;
    }

    public static void StyleSubHeaderLabel(Label label)
    {
        label.Font = SubTitleFont;
        label.ForeColor = TextPrimary;
    }

    public static void StyleHintLabel(Label label)
    {
        label.ForeColor = TextSecondary;
    }

    /// <summary>DataGridView를 카드형 배경과 하늘색 헤더로 통일합니다.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersDefaultCellStyle.BackColor = Surface;
        grid.ColumnHeadersHeight = 34;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Sky;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = BoldFont;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = OrangeLight;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Font = BaseFont;
        grid.DefaultCellStyle.Padding = new Padding(6, 2, 0, 2);
        grid.AlternatingRowsDefaultCellStyle.BackColor = SkyPale;
        grid.RowTemplate.Height = 30;
    }

    /// <summary>DataGridView 안의 버튼 컬럼(수정/삭제/보기 등)을 테마 색으로 통일합니다.</summary>
    public static void StyleGridButtonColumn(DataGridViewButtonColumn column, bool primary = false)
    {
        column.FlatStyle = FlatStyle.Flat;
        column.DefaultCellStyle.BackColor = primary ? Orange : SkyLight;
        column.DefaultCellStyle.ForeColor = primary ? Color.White : SkyDark;
        column.DefaultCellStyle.Font = BoldFont;
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        column.DefaultCellStyle.SelectionBackColor = primary ? OrangeDark : Sky;
        column.DefaultCellStyle.SelectionForeColor = Color.White;
    }

    /// <summary>TreeView 배경/글꼴을 통일하고, 선택 항목을 오렌지색으로 직접 그립니다
    /// (WinForms 기본 파란색 시스템 강조색 대신 테마 색을 쓰기 위한 최소한의 owner-draw).</summary>
    public static void StyleTree(TreeView tree)
    {
        tree.BackColor = Surface;
        tree.ForeColor = TextPrimary;
        tree.Font = BaseFont;
        tree.BorderStyle = BorderStyle.FixedSingle;
        tree.FullRowSelect = true;
        tree.HideSelection = false;
        tree.ItemHeight = 26;
        tree.DrawMode = TreeViewDrawMode.OwnerDrawText;

        tree.DrawNode -= OnDrawTreeNode;
        tree.DrawNode += OnDrawTreeNode;
    }

    private static void OnDrawTreeNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var selected = (e.State & TreeNodeStates.Selected) != 0;
        var backColor = selected ? Orange : Surface;
        var foreColor = selected ? Color.White : TextPrimary;

        using var backBrush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Node?.Text ?? "", BaseFont, e.Bounds, foreColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    /// <summary>트레이/드롭다운 메뉴에 테마 색을 입히는 렌더러. 생성 시 한 번만 만들어 재사용하세요.</summary>
    public static ToolStripRenderer CreateMenuRenderer() => new ToolStripProfessionalRenderer(new SkyOrangeColorTable());

    /// <summary>
    /// 메뉴 항목에 붙이는 작은 원형 상태 아이콘을 그립니다(예: 라이센스 인증 여부).
    /// checkmark가 true면 흰색 체크 표시를, false면 x 표시를 그 위에 그립니다(null이면 표시 없음).
    /// </summary>
    public static Bitmap CreateStatusIcon(Color color, bool? checkmark)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);

        if (checkmark is true)
        {
            using var pen = new Pen(Color.White, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLines(pen, new PointF[] { new(4, 8.5f), new(7, 11.5f), new(12, 5.5f) });
        }
        else if (checkmark is false)
        {
            using var pen = new Pen(Color.White, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(pen, 5, 5, 11, 11);
            g.DrawLine(pen, 11, 5, 5, 11);
        }

        return bmp;
    }

    private sealed class SkyOrangeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => OrangeLight;
        public override Color MenuItemSelectedGradientBegin => OrangeLight;
        public override Color MenuItemSelectedGradientEnd => OrangeLight;
        public override Color MenuItemBorder => Orange;
        public override Color MenuItemPressedGradientBegin => OrangeLight;
        public override Color MenuItemPressedGradientEnd => OrangeLight;
        public override Color MenuBorder => Border;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
    }
}
