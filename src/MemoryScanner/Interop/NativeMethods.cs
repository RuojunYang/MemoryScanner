using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MemoryScanner.Interop;

[Flags]
internal enum ProcessAccess : uint
{
    VirtualMemoryOperation = 0x0008,
    VirtualMemoryRead = 0x0010,
    VirtualMemoryWrite = 0x0020,
    QueryInformation = 0x0400
}

[Flags]
internal enum MemoryProtection : uint
{
    NoAccess = 0x01,
    ReadOnly = 0x02,
    ReadWrite = 0x04,
    WriteCopy = 0x08,
    Execute = 0x10,
    ExecuteRead = 0x20,
    ExecuteReadWrite = 0x40,
    ExecuteWriteCopy = 0x80,
    Guard = 0x100
}

internal enum MemoryState : uint
{
    Commit = 0x1000
}

[StructLayout(LayoutKind.Sequential)]
internal struct MemoryBasicInformation
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public MemoryProtection AllocationProtect;
    public ushort PartitionId;
    public nuint RegionSize;
    public MemoryState State;
    public MemoryProtection Protect;
    public uint Type;
}

internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeProcessHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(
        ProcessAccess desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(
        SafeProcessHandle process,
        IntPtr address,
        out MemoryBasicInformation buffer,
        nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        SafeProcessHandle process,
        IntPtr baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(
        SafeProcessHandle process,
        IntPtr baseAddress,
        byte[] buffer,
        nuint size,
        out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    internal static SafeProcessHandle OpenForMemoryAccess(int processId)
    {
        const ProcessAccess access =
            ProcessAccess.QueryInformation |
            ProcessAccess.VirtualMemoryOperation |
            ProcessAccess.VirtualMemoryRead |
            ProcessAccess.VirtualMemoryWrite;

        SafeProcessHandle handle = OpenProcess(access, false, processId);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, $"无法打开 PID {processId}。可能需要以管理员身份运行。");
        }

        return handle;
    }
}
