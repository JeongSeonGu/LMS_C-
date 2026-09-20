using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LmsAgent.Configuration;

namespace LmsAgent.Services;

/// <summary>
/// Win32 RegisterHotKey 기반 전역 단축키 서비스입니다. 등록된 조합키를 누르면 프로그램이
/// 포커스를 갖고 있지 않아도(트레이 상주 상태에서도) 콜백이 실행됩니다.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class HotkeyWindow : NativeWindow
    {
        public event Action<int>? HotkeyPressed;

        public HotkeyWindow() => CreateHandle(new CreateParams());

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke(m.WParam.ToInt32());
            }

            base.WndProc(ref m);
        }
    }

    private readonly HotkeyWindow _window = new();
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public GlobalHotkeyService()
    {
        _window.HotkeyPressed += id =>
        {
            if (_actions.TryGetValue(id, out var action))
            {
                action();
            }
        };
    }

    /// <summary>등록에 성공하면 true. 조합키가 비활성/미설정/특수키 없음이거나, 다른 프로그램이
    /// 이미 같은 조합을 선점했으면 false를 반환합니다(예외를 던지지 않습니다).</summary>
    public bool Register(HotkeyBinding binding, Action onPressed)
    {
        if (!binding.IsUsable)
        {
            return false;
        }

        uint modifiers = 0;
        if (binding.Ctrl) modifiers |= MOD_CONTROL;
        if (binding.Alt) modifiers |= MOD_ALT;
        if (binding.Shift) modifiers |= MOD_SHIFT;

        var id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, modifiers, (uint)binding.Key))
        {
            return false;
        }

        _actions[id] = onPressed;
        return true;
    }

    /// <summary>등록해 둔 단축키를 모두 해제합니다. 설정이 바뀌어 다시 등록할 때 먼저 호출하세요.</summary>
    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_window.Handle, id);
        }

        _actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.DestroyHandle();
    }
}
