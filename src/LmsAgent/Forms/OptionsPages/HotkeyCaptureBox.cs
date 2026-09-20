using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>
/// 클릭 후 키를 누르면 그 조합을 그대로 캡처해서 보여주는 읽기 전용 텍스트박스입니다.
/// 특수키(Ctrl/Alt/Shift) 없이 일반 키만 누르면 무시하고 안내 문구를 보여줍니다(요청사항:
/// 단축키는 반드시 특수키 + 다른 키의 조합이어야 함). Esc를 누르면 설정을 지웁니다.
/// </summary>
public sealed class HotkeyCaptureBox : TextBox
{
    private const string PlaceholderText = "클릭 후 키 입력...";
    private const string NeedsModifierText = "Ctrl/Alt/Shift 중 하나 이상과 함께 눌러주세요";

    public HotkeyBinding? Binding { get; private set; }

    public HotkeyCaptureBox()
    {
        ReadOnly = true;
        Text = PlaceholderText;
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode == Keys.Escape)
        {
            Binding = null;
            Text = PlaceholderText;
            return;
        }

        // 특수키만 누른 상태(아직 일반 키가 눌리지 않음)는 조합이 완성된 게 아니므로 무시한다.
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        var candidate = new HotkeyBinding
        {
            Enabled = true,
            Ctrl = e.Control,
            Alt = e.Alt,
            Shift = e.Shift,
            Key = e.KeyCode,
        };

        if (!candidate.HasModifier)
        {
            Binding = null;
            Text = NeedsModifierText;
            return;
        }

        Binding = candidate;
        Text = candidate.ToString();
    }

    public void SetBinding(HotkeyBinding binding)
    {
        if (binding.Key == Keys.None)
        {
            Binding = null;
            Text = PlaceholderText;
        }
        else
        {
            Binding = binding;
            Text = binding.ToString();
        }
    }
}
