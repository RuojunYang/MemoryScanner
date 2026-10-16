using System.ComponentModel;
using System.Runtime.InteropServices;
using MemoryScanner.Interop;
using MemoryScanner.Models;

namespace MemoryScanner.Services;

internal sealed class MemoryWriteService
{
    public string WriteAndVerify(
        SafeProcessHandle process,
        nuint address,
        string value,
        IntegerTypeOption type)
    {
        byte[] bytes = IntegerValueCodec.Encode(value, type);
        if (!NativeMethods.WriteProcessMemory(
                process,
                unchecked((IntPtr)(long)(ulong)address),
                bytes,
                (nuint)bytes.Length,
                out nuint written) ||
            written != (nuint)bytes.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法写入地址 0x{address:X}。");
        }

        byte[] verification = new byte[bytes.Length];
        if (!NativeMethods.ReadProcessMemory(
                process,
                unchecked((IntPtr)(long)(ulong)address),
                verification,
                (nuint)verification.Length,
                out nuint read) ||
            read != (nuint)verification.Length ||
            !verification.AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidOperationException("写入已执行，但回读校验失败。");
        }

        return IntegerValueCodec.Decode(verification, type);
    }
}
