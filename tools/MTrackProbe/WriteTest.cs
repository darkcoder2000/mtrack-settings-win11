using MTrack.Core.Interop;
static class WriteTest
{
    public static void Run(uint id, uint value)
    {
        var setB = new Guid("55A8C0F3-67A0-4AF8-9C0D-EC86AE3F6789");
        using var dev = KsDevice.Open(KsDevice.FindDevicePaths()[0]);
        Console.WriteLine($"before B[{id}]={dev.GetUInt32(setB, id)}");
        dev.SetUInt32(setB, id, value);
        Console.WriteLine($"after  B[{id}]={dev.GetUInt32(setB, id)}");
    }
}
