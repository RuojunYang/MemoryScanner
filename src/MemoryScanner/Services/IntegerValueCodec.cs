using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using MemoryScanner.Models;

namespace MemoryScanner.Services;

public static class IntegerValueCodec
{
    public static byte[] Encode(string text, IntegerTypeOption type)
    {
        if (!BigInteger.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger value))
        {
            throw new FormatException("请输入有效的十进制整数。");
        }

        int bits = checked(type.ByteSize * 8);
        BigInteger min = type.IsSigned ? -(BigInteger.One << (bits - 1)) : BigInteger.Zero;
        BigInteger max = type.IsSigned
            ? (BigInteger.One << (bits - 1)) - 1
            : (BigInteger.One << bits) - 1;

        if (value < min || value > max)
        {
            throw new OverflowException($"{type.DisplayName} 的范围是 {min} 到 {max}。");
        }

        byte[] result = new byte[type.ByteSize];
        switch ((type.ByteSize, type.IsSigned))
        {
            case (1, true):
                result[0] = unchecked((byte)(sbyte)value);
                break;
            case (1, false):
                result[0] = (byte)value;
                break;
            case (2, true):
                BinaryPrimitives.WriteInt16LittleEndian(result, (short)value);
                break;
            case (2, false):
                BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)value);
                break;
            case (4, true):
                BinaryPrimitives.WriteInt32LittleEndian(result, (int)value);
                break;
            case (4, false):
                BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)value);
                break;
            case (8, true):
                BinaryPrimitives.WriteInt64LittleEndian(result, (long)value);
                break;
            case (8, false):
                BinaryPrimitives.WriteUInt64LittleEndian(result, (ulong)value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), "仅支持 1、2、4、8 字节整数。");
        }

        return result;
    }

    public static string Decode(ReadOnlySpan<byte> bytes, IntegerTypeOption type)
    {
        if (bytes.Length < type.ByteSize)
        {
            throw new ArgumentException("字节数不足。", nameof(bytes));
        }

        return (type.ByteSize, type.IsSigned) switch
        {
            (1, true) => unchecked((sbyte)bytes[0]).ToString(CultureInfo.InvariantCulture),
            (1, false) => bytes[0].ToString(CultureInfo.InvariantCulture),
            (2, true) => BinaryPrimitives.ReadInt16LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            (2, false) => BinaryPrimitives.ReadUInt16LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            (4, true) => BinaryPrimitives.ReadInt32LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            (4, false) => BinaryPrimitives.ReadUInt32LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            (8, true) => BinaryPrimitives.ReadInt64LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            (8, false) => BinaryPrimitives.ReadUInt64LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public static IReadOnlyList<int> FindMatches(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> pattern)
    {
        if (pattern.IsEmpty)
        {
            throw new ArgumentException("匹配值不能为空。", nameof(pattern));
        }

        List<int> matches = [];
        for (int offset = 0; offset <= buffer.Length - pattern.Length; offset++)
        {
            if (buffer.Slice(offset, pattern.Length).SequenceEqual(pattern))
            {
                matches.Add(offset);
            }
        }

        return matches;
    }
}
