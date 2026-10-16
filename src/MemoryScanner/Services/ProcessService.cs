using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MemoryScanner.Interop;
using MemoryScanner.Models;

namespace MemoryScanner.Services;

internal sealed class ProcessService : IDisposable
{
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiSmallIcon = 0x00000001;

    private static readonly Dictionary<string, ImageSource?> IconCache =
        new(StringComparer.OrdinalIgnoreCase);

    private SafeProcessHandle? _handle;

    public SafeProcessHandle Handle =>
        _handle is { IsInvalid: false, IsClosed: false }
            ? _handle
            : throw new InvalidOperationException("尚未选择目标进程。");

    public int? ProcessId { get; private set; }

    public IReadOnlyList<ProcessOption> GetProcesses()
    {
        int ownId = Environment.ProcessId;
        List<ProcessOption> result = [];

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != ownId && !process.HasExited)
                    {
                        result.Add(new ProcessOption(
                            process.Id,
                            process.ProcessName,
                            process.MainWindowTitle,
                            GetProcessIcon(process)));
                    }
                }
                catch
                {
                    // A process can exit or become inaccessible while it is being enumerated.
                }
            }
        }

        return result
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    private static ImageSource? GetProcessIcon(Process process)
    {
        string? executablePath;
        try
        {
            executablePath = process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        if (IconCache.TryGetValue(executablePath, out ImageSource? cached))
        {
            return cached;
        }

        try
        {
            IntPtr result = SHGetFileInfo(
                executablePath,
                0,
                out ShFileInfo info,
                (uint)Marshal.SizeOf<ShFileInfo>(),
                ShgfiIcon | ShgfiSmallIcon);
            if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
            {
                IconCache[executablePath] = null;
                return null;
            }

            try
            {
                BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(
                    info.IconHandle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                image.Freeze();
                IconCache[executablePath] = image;
                return image;
            }
            finally
            {
                _ = DestroyIcon(info.IconHandle);
            }
        }
        catch
        {
            IconCache[executablePath] = null;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        out ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    public void Open(ProcessOption process)
    {
        SafeProcessHandle newHandle = NativeMethods.OpenForMemoryAccess(process.Id);
        _handle?.Dispose();
        _handle = newHandle;
        ProcessId = process.Id;
    }

    public bool IsTargetAlive()
    {
        if (ProcessId is not int id)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        ProcessId = null;
    }
}
