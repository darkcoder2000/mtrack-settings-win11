using System.Drawing;
using System.Windows.Forms;

namespace MTrackPanel.Services;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action open, Action exit)
    {
        var menu = new ContextMenuStrip();
        var openItem = menu.Items.Add("Open M-Track Settings", null, (_, _) => open());
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add("Windows Sound Settings", null, (_, _) => Shell.OpenSoundSettings());
        menu.Items.Add("Sound Control Panel", null, (_, _) => Shell.OpenSoundControlPanel());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _icon = new NotifyIcon
        {
            Icon = new Icon(stream, SystemInformation.SmallIconSize),
            Text = "M-Audio M-Track Settings",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) open(); };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

internal static class Shell
{
    public static void OpenSoundSettings() => Start("ms-settings:sound");

    public static void OpenSoundControlPanel() => Start("control.exe", "mmsys.cpl");

    private static void Start(string file, string? args = null)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, args ?? "") { UseShellExecute = true });
        }
        catch (Exception) { /* nothing sensible to do */ }
    }
}
