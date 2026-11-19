using System.Runtime.InteropServices;
static class FindTest
{
    [DllImport("user32.dll", CharSet = CharSet.Ansi, EntryPoint = "FindWindowA")] static extern IntPtr FA(string? c, string? t);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")] static extern IntPtr FW(string? c, string? t);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")] static extern IntPtr FEx(IntPtr p, IntPtr a, string? c, string? t);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    public static void Run(bool activate)
    {
        const string cls = "{CE869237-0350-49E7-BE8D-57C8EA29C0FD}_DDPanel";
        Console.WriteLine($"Shell_TrayWnd W={FW("Shell_TrayWnd", null)}");
        Console.WriteLine($"A={FA(cls, null)} W={FW(cls, null)} Ex={FEx(IntPtr.Zero, IntPtr.Zero, cls, null)} byTitle={FW(null, "M-Track Settings")}");
        var h = FA(cls, null);
        if (activate && h != IntPtr.Zero) { ShowWindow(h, 5); SetForegroundWindow(h); Console.WriteLine("activated"); }
    }
}
