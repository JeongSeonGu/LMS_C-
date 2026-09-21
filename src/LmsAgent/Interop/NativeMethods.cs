using System;
using System.Runtime.InteropServices;

namespace LmsAgent.Interop;

/// <summary>
/// 학사달력 배경화면 오버레이 창을 다른 창들 뒤로 보내
/// "배경화면처럼" 보이게 하기 위한 최소한의 Win32 P/Invoke.
/// (실제 바탕화면(WorkerW)에 자식으로 삽입하는 것이 아니라, Z-order 최하단으로
///  내려서 흉내 내는 근사적인 구현입니다.)
/// </summary>
internal static class NativeMethods
{
    public static readonly IntPtr HWND_BOTTOM = new(1);
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
