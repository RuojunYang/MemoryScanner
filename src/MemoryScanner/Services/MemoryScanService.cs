using System.ComponentModel;
using System.Runtime.InteropServices;
using MemoryScanner.Interop;
using MemoryScanner.Models;

namespace MemoryScanner.Services;

internal sealed class MemoryScanService
{
    private const int ChunkSize = 1024 * 1024;
    private const int MaximumMatchCount = 10_000_000;

    public Task<List<nuint>> FirstScanAsync(
        SafeProcessHandle process,
        byte[] pattern,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => FirstScan(process, pattern, progress, cancellationToken), cancellationToken);

    public Task<List<nuint>> NextScanAsync(
        SafeProcessHandle process,
        IReadOnlyList<nuint> candidates,
        byte[] pattern,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => NextScan(process, candidates, pattern, progress, cancellationToken), cancellationToken);

    public string ReadValue(SafeProcessHandle process, nuint address, IntegerTypeOption type)
    {
        byte[] buffer = new byte[type.ByteSize];
        if (!NativeMethods.ReadProcessMemory(process, ToIntPtr(address), buffer, (nuint)buffer.Length, out nuint read) ||
            read != (nuint)buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法读取地址 0x{address:X}。");
        }

        return IntegerValueCodec.Decode(buffer, type);
    }

    private static List<nuint> FirstScan(
        SafeProcessHandle process,
        byte[] pattern,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<nuint> matches = [];
        ulong bytesScanned = 0;
        ulong queryAddress = 0;
        nuint infoSize = (nuint)Marshal.SizeOf<MemoryBasicInformation>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nuint queryResult = NativeMethods.VirtualQueryEx(
                process,
                ToIntPtr((nuint)queryAddress),
                out MemoryBasicInformation region,
                infoSize);

            if (queryResult == 0)
            {
                break;
            }

            ulong baseAddress = unchecked((ulong)region.BaseAddress.ToInt64());
            ulong regionSize = region.RegionSize;
            if (regionSize == 0)
            {
                break;
            }

            if (region.State == MemoryState.Commit && IsReadable(region.Protect))
            {
                ScanRegion(process, baseAddress, regionSize, pattern, matches, ref bytesScanned, progress, cancellationToken);
            }

            ulong nextAddress = baseAddress + regionSize;
            if (nextAddress <= queryAddress)
            {
                break;
            }

            queryAddress = nextAddress;
        }

        progress?.Report(new ScanProgress(bytesScanned, matches.Count));
        return matches;
    }

    private static void ScanRegion(
        SafeProcessHandle process,
        ulong baseAddress,
        ulong regionSize,
        byte[] pattern,
        List<nuint> matches,
        ref ulong bytesScanned,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        for (ulong offset = 0; offset < regionSize; offset += ChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong startsInChunk = Math.Min((ulong)ChunkSize, regionSize - offset);
            ulong requested = Math.Min(startsInChunk + (ulong)pattern.Length - 1, regionSize - offset);
            byte[] buffer = new byte[checked((int)requested)];
            nuint chunkAddress = (nuint)(baseAddress + offset);

            bool success = NativeMethods.ReadProcessMemory(
                process,
                ToIntPtr(chunkAddress),
                buffer,
                (nuint)buffer.Length,
                out nuint bytesRead);

            int validLength = checked((int)bytesRead);
            if ((success || validLength > 0) && validLength >= pattern.Length)
            {
                int maximumStart = checked((int)Math.Min(startsInChunk, (ulong)(validLength - pattern.Length + 1)));
                ReadOnlySpan<byte> data = buffer.AsSpan(0, validLength);
                for (int index = 0; index < maximumStart; index++)
                {
                    if (data.Slice(index, pattern.Length).SequenceEqual(pattern))
                    {
                        matches.Add(chunkAddress + (nuint)index);
                        if (matches.Count > MaximumMatchCount)
                        {
                            throw new InvalidOperationException(
                                $"匹配结果超过 {MaximumMatchCount:N0} 个。请换用更具体的数值或更大的整数类型。");
                        }
                    }
                }
            }

            bytesScanned += bytesRead;
            progress?.Report(new ScanProgress(bytesScanned, matches.Count));
        }
    }

    private static List<nuint> NextScan(
        SafeProcessHandle process,
        IReadOnlyList<nuint> candidates,
        byte[] pattern,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<nuint> matches = new(Math.Min(candidates.Count, 1_000_000));
        byte[] buffer = new byte[pattern.Length];
        ulong bytesScanned = 0;

        for (int index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nuint address = candidates[index];
            bool success = NativeMethods.ReadProcessMemory(
                process,
                ToIntPtr(address),
                buffer,
                (nuint)buffer.Length,
                out nuint bytesRead);

            if (success && bytesRead == (nuint)pattern.Length && buffer.AsSpan().SequenceEqual(pattern))
            {
                matches.Add(address);
            }

            bytesScanned += bytesRead;
            if ((index & 0xFFF) == 0)
            {
                progress?.Report(new ScanProgress(bytesScanned, matches.Count));
            }
        }

        progress?.Report(new ScanProgress(bytesScanned, matches.Count));
        return matches;
    }

    private static bool IsReadable(MemoryProtection protection)
    {
        if ((protection & (MemoryProtection.Guard | MemoryProtection.NoAccess)) != 0)
        {
            return false;
        }

        MemoryProtection baseProtection = protection & (MemoryProtection)0xFF;
        return baseProtection is MemoryProtection.ReadOnly
            or MemoryProtection.ReadWrite
            or MemoryProtection.WriteCopy
            or MemoryProtection.ExecuteRead
            or MemoryProtection.ExecuteReadWrite
            or MemoryProtection.ExecuteWriteCopy;
    }

    private static IntPtr ToIntPtr(nuint address) => unchecked((IntPtr)(long)(ulong)address);
}
