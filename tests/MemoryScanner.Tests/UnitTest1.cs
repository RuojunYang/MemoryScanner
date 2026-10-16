using MemoryScanner.Models;
using MemoryScanner.Services;

namespace MemoryScanner.Tests;

public class IntegerValueCodecTests
{
    public static TheoryData<int, bool, string, string> EncodingCases => new()
    {
        { 1, true, "-128", "80" },
        { 1, false, "255", "FF" },
        { 2, true, "-32768", "0080" },
        { 2, false, "65535", "FFFF" },
        { 4, true, "-2147483648", "00000080" },
        { 4, false, "4294967295", "FFFFFFFF" },
        { 8, true, "-9223372036854775808", "0000000000000080" },
        { 8, false, "18446744073709551615", "FFFFFFFFFFFFFFFF" }
    };

    [Theory]
    [MemberData(nameof(EncodingCases))]
    public void Encode_UsesLittleEndianAndSupportsBoundaries(
        int byteSize,
        bool isSigned,
        string value,
        string expectedHex)
    {
        IntegerTypeOption type = new("test", byteSize, isSigned);

        byte[] encoded = IntegerValueCodec.Encode(value, type);

        Assert.Equal(expectedHex, Convert.ToHexString(encoded));
        Assert.Equal(value, IntegerValueCodec.Decode(encoded, type));
    }

    [Theory]
    [InlineData(1, true, "128")]
    [InlineData(1, false, "-1")]
    [InlineData(2, false, "65536")]
    [InlineData(4, true, "2147483648")]
    [InlineData(8, false, "18446744073709551616")]
    public void Encode_RejectsValuesOutsideSelectedType(int byteSize, bool isSigned, string value)
    {
        IntegerTypeOption type = new("test", byteSize, isSigned);
        Assert.Throws<OverflowException>(() => IntegerValueCodec.Encode(value, type));
    }

    [Fact]
    public void FindMatches_FindsUnalignedAndOverlappingValues()
    {
        byte[] data = [9, 1, 1, 1, 8];
        byte[] pattern = [1, 1];

        IReadOnlyList<int> matches = IntegerValueCodec.FindMatches(data, pattern);

        Assert.Equal([1, 2], matches);
    }
}
