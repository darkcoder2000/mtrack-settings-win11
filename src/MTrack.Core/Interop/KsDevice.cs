using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MTrack.Core.Interop;

/// <summary>
/// Thin wrapper around the M-Track driver's KS topology filter.
/// All communication with the driver goes through IOCTL_KS_PROPERTY on this handle.
/// </summary>
public sealed class KsDevice : IDisposable
{
    public const uint IOCTL_KS_PROPERTY = 0x002F0003;
    public const uint IOCTL_KS_ENABLE_EVENT = 0x002F0007;
    public const uint IOCTL_KS_DISABLE_EVENT = 0x002F000B;

    public const uint KSPROPERTY_TYPE_GET = 1;
    public const uint KSPROPERTY_TYPE_SET = 2;

    /// <summary>KSCATEGORY_TOPOLOGY.</summary>
    public static readonly Guid KsCategoryTopology = new("DDA54A40-1E4C-11D1-A050-405705C10000");

    /// <summary>Reference string of the M-Track topology filter (KSNAME_TopoFilter in the INF).</summary>
    public const string TopoFilterRefString = "{ce869237-0350-49e7-be8d-57c8ea29c0fd}";

    private readonly SafeFileHandle _handle;

    public string Path { get; }

    private KsDevice(string path, SafeFileHandle handle)
    {
        Path = path;
        _handle = handle;
    }

    /// <summary>Finds the device-interface paths of all connected M-Track / M-Track Plus units.</summary>
    public static IReadOnlyList<string> FindDevicePaths()
    {
        var result = new List<string>();
        Guid cat = KsCategoryTopology;
        uint len;
        if (Native.CM_Get_Device_Interface_List_SizeW(out len, ref cat, null, 0) != 0 || len <= 1)
            return result;

        var buffer = new char[len];
        if (Native.CM_Get_Device_Interface_ListW(ref cat, null, buffer, len, 0) != 0)
            return result;

        foreach (var p in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var lower = p.ToLowerInvariant();
            if (lower.Contains("vid_0763&pid_400") && lower.EndsWith(TopoFilterRefString))
                result.Add(p);
        }
        return result;
    }

    public static KsDevice Open(string path)
    {
        var h = Native.CreateFileW(path,
            Native.GENERIC_READ | Native.GENERIC_WRITE,
            Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
            IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open {path}");
        return new KsDevice(path, h);
    }

    /// <summary>USB product id (0x4000 = M-Track, 0x4001 = M-Track Plus) parsed from the path.</summary>
    public int ProductId
    {
        get
        {
            var i = Path.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
            return i >= 0 && int.TryParse(Path.AsSpan(i + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out var pid) ? pid : 0;
        }
    }

    /// <summary>
    /// Sends IOCTL_KS_PROPERTY. The input buffer is KSPROPERTY followed by <paramref name="extraIn"/>.
    /// Returns the number of bytes written to <paramref name="output"/>; throws on failure.
    /// </summary>
    public int Property(Guid set, uint id, uint flags, ReadOnlySpan<byte> extraIn, Span<byte> output)
    {
        Span<byte> inBuf = stackalloc byte[24 + extraIn.Length];
        set.TryWriteBytes(inBuf[..16]);
        BitConverter.TryWriteBytes(inBuf[16..20], id);
        BitConverter.TryWriteBytes(inBuf[20..24], flags);
        extraIn.CopyTo(inBuf[24..]);
        return Ioctl(IOCTL_KS_PROPERTY, inBuf, output);
    }

    /// <summary>Same as <see cref="Property"/> but returns false instead of throwing.</summary>
    public bool TryProperty(Guid set, uint id, uint flags, ReadOnlySpan<byte> extraIn, Span<byte> output, out int bytesReturned, out int error)
    {
        try
        {
            bytesReturned = Property(set, id, flags, extraIn, output);
            error = 0;
            return true;
        }
        catch (Win32Exception ex)
        {
            bytesReturned = 0;
            error = ex.NativeErrorCode;
            return false;
        }
    }

    public uint GetUInt32(Guid set, uint id)
    {
        Span<byte> o = stackalloc byte[4];
        var n = Property(set, id, KSPROPERTY_TYPE_GET, default, o);
        if (n < 4) throw new InvalidDataException($"Property {id} returned {n} bytes");
        return BitConverter.ToUInt32(o);
    }

    public void SetUInt32(Guid set, uint id, uint value)
    {
        // KS SET: the value is passed in the *output* buffer (METHOD_NEITHER semantics).
        Span<byte> v = stackalloc byte[4];
        BitConverter.TryWriteBytes(v, value);
        Ioctl(IOCTL_KS_PROPERTY, BuildProp(set, id, KSPROPERTY_TYPE_SET), v);
    }

    private static byte[] BuildProp(Guid set, uint id, uint flags)
    {
        var b = new byte[24];
        set.TryWriteBytes(b.AsSpan(0, 16));
        BitConverter.TryWriteBytes(b.AsSpan(16, 4), id);
        BitConverter.TryWriteBytes(b.AsSpan(20, 4), flags);
        return b;
    }

    public unsafe int Ioctl(uint code, ReadOnlySpan<byte> input, Span<byte> output)
    {
        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            if (!Native.DeviceIoControl(_handle, code, pIn, (uint)input.Length, pOut, (uint)output.Length, out var returned, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return (int)returned;
        }
    }

    /// <summary>
    /// Enables a KS event with KSEVENTF_EVENT_HANDLE notification. Returns the KSEVENTDATA buffer,
    /// which must be kept alive and passed to <see cref="DisableEvent"/>.
    /// </summary>
    public unsafe byte[] EnableEvent(Guid set, uint id, SafeWaitHandle evt)
    {
        // KSEVENT { GUID Set; ULONG Id; ULONG Flags; }  Flags = KSEVENT_TYPE_ENABLE (1)
        var ksEvent = BuildProp(set, id, 1);
        // KSEVENTDATA (x64: 32 bytes) { ULONG NotificationType; union { struct { HANDLE Event; ULONG_PTR Reserved[2]; } } }
        var data = new byte[32];
        BitConverter.TryWriteBytes(data.AsSpan(0, 4), 1u); // KSEVENTF_EVENT_HANDLE
        BitConverter.TryWriteBytes(data.AsSpan(8, 8), evt.DangerousGetHandle().ToInt64());
        Ioctl(IOCTL_KS_ENABLE_EVENT, ksEvent, data);
        return data;
    }

    public void DisableEvent(byte[] eventData)
    {
        try { Ioctl(IOCTL_KS_DISABLE_EVENT, eventData, default); } catch (Win32Exception) { }
    }

    public void Dispose() => _handle.Dispose();

    private static unsafe class Native
    {
        public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle h, uint code, void* inBuf, uint inLen, void* outBuf, uint outLen, out uint returned, IntPtr overlapped);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_Interface_List_SizeW(out uint len, ref Guid cls, string? deviceId, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_Interface_ListW(ref Guid cls, string? deviceId, [Out] char[] buffer, uint len, uint flags);
    }
}
