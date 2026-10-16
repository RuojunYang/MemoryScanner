using System.Diagnostics;
using System.Globalization;
using MemoryScanner.Interop;
using MemoryScanner.Models;
using MemoryScanner.Services;

namespace MemoryScanner.Tests;

public class MemoryIntegrationTests
{
    [Fact(Timeout = 30_000)]
    public async Task Scanner_FindsCrossChunkValue_RefinesAndWritesChildProcess()
    {
        string configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? "Debug"
            : "Release";
        string targetDll = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "MemoryScanner.TestTarget",
            "bin",
            configuration,
            "net8.0",
            "MemoryScanner.TestTarget.dll"));
        Assert.True(File.Exists(targetDll), $"找不到测试辅助进程：{targetDll}");

        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(targetDll);

        using Process target = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动测试辅助进程。");
        try
        {
            string addressLine = await target.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))
                ?? throw new InvalidOperationException("测试辅助进程没有返回地址。");
            nuint expectedAddress = (nuint)ulong.Parse(addressLine, CultureInfo.InvariantCulture);

            IntegerTypeOption type = new("Int64", sizeof(long), true);
            const long initialValue = 0x1234_5678_2345_6789;
            const long replacementValue = 0x0FED_CBA9_1357_2468;
            byte[] initialPattern = IntegerValueCodec.Encode(
                initialValue.ToString(CultureInfo.InvariantCulture),
                type);
            byte[] replacementPattern = IntegerValueCodec.Encode(
                replacementValue.ToString(CultureInfo.InvariantCulture),
                type);

            using SafeProcessHandle handle = NativeMethods.OpenForMemoryAccess(target.Id);
            MemoryScanService scanner = new();
            MemoryWriteService writer = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

            List<nuint> firstResults = await scanner.FirstScanAsync(handle, initialPattern, null, timeout.Token);
            Assert.Contains(expectedAddress, firstResults);

            List<nuint> refined = await scanner.NextScanAsync(
                handle,
                [expectedAddress],
                initialPattern,
                null,
                timeout.Token);
            Assert.Equal([expectedAddress], refined);

            string verifiedValue = writer.WriteAndVerify(
                handle,
                expectedAddress,
                replacementValue.ToString(CultureInfo.InvariantCulture),
                type);
            Assert.Equal(replacementValue.ToString(CultureInfo.InvariantCulture), verifiedValue);

            List<nuint> oldValueResults = await scanner.NextScanAsync(
                handle,
                [expectedAddress],
                initialPattern,
                null,
                timeout.Token);
            List<nuint> newValueResults = await scanner.NextScanAsync(
                handle,
                [expectedAddress],
                replacementPattern,
                null,
                timeout.Token);
            Assert.Empty(oldValueResults);
            Assert.Equal([expectedAddress], newValueResults);
        }
        finally
        {
            if (!target.HasExited)
            {
                await target.StandardInput.WriteLineAsync("exit");
                if (!target.WaitForExit(2_000))
                {
                    target.Kill(entireProcessTree: true);
                }
            }
        }
    }
}
