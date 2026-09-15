using System;
using System.Drawing;
using System.Windows.Forms;

namespace LmsAgent.Services;

/// <summary>
/// 프로그램 전체에서 쓰는 공통 디자인 테마입니다. 밝은 하늘색을 기본색으로,
/// 오렌지색을 주요 액션(등록/저장/로그인 등) 강조색으로 사용합니다.
/// WinForms 기본 컨트롤 위에 색상·글꼴·여백을 통일해 적용하는 얇은 헬퍼 모음입니다.
/// </summary>
public static class UiTheme
{
    // ── 팔레트 ────────────────────────────────────────────────
    public static readonly Color Sky = Color.FromArgb(41, 171, 226);
    public static readonly Color SkyDark = Color.FromArgb(15, 133, 186);
    public static readonly Color SkyLight = Color.FromArgb(224, 244, 253);
    public static readonly Color SkyPale = Color.FromArgb(241, 250, 254);

    public static readonly Color Orange = Color.FromArgb(255, 141, 60);
    public static readonly Color OrangeDark = Color.FromArgb(232, 111, 26);
    public static readonly Color OrangeLight = Color.FromArgb(255, 231, 209);

    public static readonly Color Background = SkyPale;
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 231, 240);
    public static readonly Color TextPrimary = Color.FromArgb(38, 50, 56);
    public static readonly Color TextSecondary = Color.FromArgb(112, 128, 136);
    public static readonly Color Success = Color.FromArgb(43, 158, 111);
    public static readonly Color Danger = Color.FromArgb(224, 86, 66);

    // ── 글꼴 ──────────────────────────────────────────────────
    public static readonly Font BaseFont = new("맑은 고딕", 9.5F);
    public static readonly Font BoldFont = new("맑은 고딕", 9.5F, FontStyle.Bold);
    public static readonly Font TitleFont = new("맑은 고딕", 13F, FontStyle.Bold);
    public static readonly Font SubTitleFont = new("맑은 고딕", 10F, FontStyle.Bold);

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
