using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace MemoryScanner.Models;

public sealed record IntegerTypeOption(string DisplayName, int ByteSize, bool IsSigned)
{
    public static IReadOnlyList<IntegerTypeOption> All { get; } =
    [
        new("1 字节 - Int8", 1, true),
        new("1 字节 - UInt8", 1, false),
        new("2 字节 - Int16", 2, true),
        new("2 字节 - UInt16", 2, false),
        new("4 字节 - Int32", 4, true),
        new("4 字节 - UInt32", 4, false),
        new("8 字节 - Int64", 8, true),
        new("8 字节 - UInt64", 8, false)
    ];
}

public sealed record ProcessOption(int Id, string Name, string WindowTitle, ImageSource? Icon)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(WindowTitle)
            ? $"{Name}.exe  (PID: {Id})"
            : $"{Name}.exe  (PID: {Id}) — {WindowTitle}";
}

public sealed class MemoryResult : INotifyPropertyChanged
{
    private string _value;

    public MemoryResult(nuint address, string value)
    {
        Address = address;
        _value = value;
    }

    public nuint Address { get; }
    public string AddressText => $"0x{Address:X16}";

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public readonly record struct ScanProgress(ulong BytesScanned, int MatchCount);
