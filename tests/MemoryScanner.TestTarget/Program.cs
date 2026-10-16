using System.Runtime.InteropServices;

const int chunkSize = 1024 * 1024;
const long marker = 0x1234_5678_2345_6789;

byte[] memory = new byte[chunkSize * 2 + 64];
GCHandle pin = GCHandle.Alloc(memory, GCHandleType.Pinned);

try
{
    IntPtr arrayAddress = pin.AddrOfPinnedObject();
    _ = NativeMethods.VirtualQuery(
        arrayAddress,
        out MemoryBasicInformation region,
        (nuint)Marshal.SizeOf<MemoryBasicInformation>());

    ulong arrayStart = unchecked((ulong)arrayAddress.ToInt64());
    ulong regionStart = unchecked((ulong)region.BaseAddress.ToInt64());
    ulong offsetInRegion = arrayStart - regionStart;
    ulong nextBoundary = ((offsetInRegion / chunkSize) + 1) * chunkSize;
    int markerOffset = checked((int)(nextBoundary - offsetInRegion - 2));
    if (markerOffset < 0 || markerOffset + sizeof(long) > memory.Length)
    {
        markerOffset = chunkSize - 2;
    }

    IntPtr markerAddress = IntPtr.Add(arrayAddress, markerOffset);
    Marshal.WriteInt64(markerAddress, marker);
    Console.WriteLine(unchecked((ulong)markerAddress.ToInt64()));
    Console.Out.Flush();
    Console.ReadLine();
}
finally
{
    pin.Free();
}

static class NativeMethods
{
    [DllImport("kernel32.dll")]
    public static extern nuint VirtualQuery(
        IntPtr address,
        out MemoryBasicInformation buffer,
        nuint length);
}

[StructLayout(LayoutKind.Sequential)]
struct MemoryBasicInformation
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public uint AllocationProtect;
    public ushort PartitionId;
    public nuint RegionSize;
    public uint State;
    public uint Protect;
    public uint Type;
}
