using System.ComponentModel;
using MTrack.Core.Interop;

namespace MTrack.Core;

/// <summary>Snapshot of the device state as reported by the driver.</summary>
public sealed record MTrackState(
    string ModelName,
    int ProductId,
    uint SampleRate,
    uint BufferSize,
    bool ClockLocked,
    string DriverVersion,
    string FirmwareVersion);

/// <summary>
/// High-level access to an M-Track / M-Track Plus through the driver's KS property set
/// (see docs/protocol.md). Not thread-safe; use from one thread.
/// </summary>
public sealed class MTrackDevice : IDisposable
{
    /// <summary>"Standard driver properties" set used by the original M-Audio panel.</summary>
    public static readonly Guid PropSet = new("55A8C0F3-67A0-4AF8-9C0D-EC86AE3F6789");

    public const uint PropVersions = 1;    // GET, 36 bytes
    public const uint PropSampleRate = 4;  // GET/SET, ULONG Hz
    public const uint PropBufferSize = 5;  // GET/SET, ULONG samples
    public const uint PropClockLocked = 10; // GET, ULONG (1 = locked)

    /// <summary>Values offered by the original panel (hard-coded there as well).</summary>
    public static readonly IReadOnlyList<uint> SupportedSampleRates = [44100, 48000];
    public static readonly IReadOnlyList<uint> SupportedBufferSizes = [128, 256, 512, 1024, 2048, 4096];

    private readonly KsDevice _ks;

    private MTrackDevice(KsDevice ks) => _ks = ks;

    public string Path => _ks.Path;

    /// <summary>Opens the first connected unit, or returns null if none is present.</summary>
    public static MTrackDevice? TryOpenFirst()
    {
        foreach (var path in KsDevice.FindDevicePaths())
        {
            try { return new MTrackDevice(KsDevice.Open(path)); }
            catch (Win32Exception) { /* try next */ }
        }
        return null;
    }

    public MTrackState ReadState()
    {
        var pid = _ks.ProductId;
        var (drv, fw) = ReadVersions();
        bool locked;
        try { locked = _ks.GetUInt32(PropSet, PropClockLocked) != 0; }
        catch (Win32Exception) { locked = true; }

        return new MTrackState(
            pid == 0x4001 ? "M-Track Plus" : "M-Track",
            pid,
            _ks.GetUInt32(PropSet, PropSampleRate),
            _ks.GetUInt32(PropSet, PropBufferSize),
            locked,
            drv,
            fw);
    }

    public void SetSampleRate(uint hz) => _ks.SetUInt32(PropSet, PropSampleRate, hz);

    public void SetBufferSize(uint samples) => _ks.SetUInt32(PropSet, PropBufferSize, samples);

    private (string driver, string firmware) ReadVersions()
    {
        var b = new byte[36];
        try
        {
            var n = _ks.Property(PropSet, PropVersions, KsDevice.KSPROPERTY_TYPE_GET, default, b);
            if (n < 32) return ("?", "?");
            uint U(int i) => BitConverter.ToUInt32(b, i * 4);
            // Layout observed: [0..3] driver a.b.c.d, [4] reserved, [5..7] firmware a.b.c, [8] reserved
            return ($"{U(0)}.{U(1)}.{U(2)}.{U(3)}", $"{U(5)}.{U(6)}.{U(7)}");
        }
        catch (Win32Exception)
        {
            return ("?", "?");
        }
    }

    public void Dispose() => _ks.Dispose();
}
