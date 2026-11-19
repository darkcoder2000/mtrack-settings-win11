using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MTrackPanel.Services;

/// <summary>
/// Hidden top-level window with the class name the M-Audio ASIO driver looks for.
/// When a DAW presses "ASIO Control Panel", the driver calls
/// FindWindowA("{CE869237-...}_DDPanel") followed by ShowWindow(SW_SHOW) and SetForegroundWindow.
/// We react to that by showing the real settings window and hiding this one again.
/// </summary>
internal sealed class ActivationWindow : IDisposable
{
    public const string ClassName = "{CE869237-0350-49E7-BE8D-57C8EA29C0FD}_DDPanel";

    private const uint WM_SHOWWINDOW = 0x0018;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_HIDE = 0, SW_SHOW = 5;

    private readonly WndProc _wndProc; // keep delegate alive
    private readonly IntPtr _hwnd;
    private readonly Dispatcher _dispatcher;

    public event Action? ActivationRequested;

    public ActivationWindow()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _wndProc = WindowProc;
        var hInstance = GetModuleHandleW(null);

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = ClassName,
        };
        if (RegisterClassExW(ref wc) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassEx failed");

        _hwnd = CreateWindowExW(WS_EX_TOOLWINDOW, ClassName, "M-Track Settings", WS_POPUP,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed");
    }

    /// <summary>Used by a second instance to wake up the first one.</summary>
    public static void ActivateExistingInstance()
    {
        var hwnd = FindWindowW(ClassName, null);
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
    }

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SHOWWINDOW && wParam != IntPtr.Zero)
        {
            _dispatcher.BeginInvoke(() =>
            {
                ShowWindow(_hwnd, SW_HIDE);
                ActivationRequested?.Invoke();
            });
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
        UnregisterClassW(ClassName, GetModuleHandleW(null));
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClassW(string cls, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string cls, string title, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string cls, string? title);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);
}
