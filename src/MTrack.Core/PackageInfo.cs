using Microsoft.Win32;

namespace MTrack.Core;

public static class PackageInfo
{
    /// <summary>Version of the installed "M-Audio M-Track Driver" MSI package, e.g. "1.0.6".</summary>
    public static string GetDriverPackageVersion()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstall = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var k = uninstall.OpenSubKey(name);
                    if (k?.GetValue("DisplayName") is string dn &&
                        dn.StartsWith("M-Audio M-Track", StringComparison.OrdinalIgnoreCase) &&
                        k.GetValue("DisplayVersion") is string ver)
                        return ver;
                }
            }
            catch (Exception) { /* ignore and try next view */ }
        }
        return "?";
    }
}
