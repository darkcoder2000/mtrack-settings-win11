using MTrack.Core.Interop;

// Read-only probe of the M-Track driver's KS property sets.
var setA = new Guid("3961234E-B801-4B28-8FAA-5BB5DC0EF610"); // command channel (not probed here)
var setB = new Guid("55A8C0F3-67A0-4AF8-9C0D-EC86AE3F6789"); // standard driver properties

var paths = KsDevice.FindDevicePaths();
Console.WriteLine($"Found {paths.Count} device(s)");
foreach (var p in paths) Console.WriteLine("  " + p);
if (paths.Count == 0) return 1;

using var dev = KsDevice.Open(paths[0]);
Console.WriteLine($"Opened, PID=0x{dev.ProductId:X4}");

if (args.Length >= 1 && args[0] == "find") { FindTest.Run(args.Length > 1); return 0; }
if (args.Length == 3 && args[0] == "set") { WriteTest.Run(uint.Parse(args[1]), uint.Parse(args[2])); return 0; }
uint maxId = args.Length > 0 ? uint.Parse(args[0]) : 16;
var buf = new byte[512];
for (uint id = 0; id <= maxId; id++)
{
    Array.Clear(buf);
    if (dev.TryProperty(setB, id, KsDevice.KSPROPERTY_TYPE_GET, default, buf, out var n, out var err))
    {
        Console.WriteLine($"B[{id,2}] {n,3} bytes: {Convert.ToHexString(buf, 0, n)}");
        if (n == 4) Console.WriteLine($"         u32={BitConverter.ToUInt32(buf)}");
        else if (n % 4 == 0 && n > 0)
            Console.WriteLine("         u32s=" + string.Join(",", Enumerable.Range(0, n / 4).Select(i => BitConverter.ToUInt32(buf, i * 4))));
    }
    else Console.WriteLine($"B[{id,2}] error {err}");
}
return 0;
