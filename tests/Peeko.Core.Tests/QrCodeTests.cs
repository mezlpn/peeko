using Peeko.Core.Conversion;

namespace Peeko.Core.Tests;

public class QrCodeTests
{
    [Fact]
    public void Reed_solomon_matches_hello_world_1M_example()
    {
        // Contoh "HELLO WORLD" versi 1-M dari tutorial QR Thonky.
        byte[] data = [32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17];
        Assert.Equal(new byte[] { 196, 35, 39, 119, 235, 215, 231, 226, 93, 23 }, QrCode.ReedSolomon(data, 10));
    }

    [Fact]
    public void Format_and_version_bits_match_the_standard_tables()
    {
        Assert.Equal(0b101010000010010, QrCode.FormatBits(0));
        Assert.Equal(0b100000011001110, QrCode.FormatBits(5));
        Assert.Equal(0b100101010100000, QrCode.FormatBits(7));
        Assert.Equal(0b000111110010010100, QrCode.VersionBits(7));
    }

    [Fact]
    public void Picks_smallest_version_and_draws_finders()
    {
        var short1 = QrCode.Encode("http://192.168.1.5:8765/a1b2c3");
        Assert.Equal(3, short1.Version); // 31 byte > 26 (v2-M), muat di v3
        Assert.Equal(29, short1.Size);
        // Finder kiri atas: pojok gelap, cincin terang, tengah gelap.
        Assert.True(short1[0, 0]);
        Assert.False(short1[1, 1]);
        Assert.True(short1[3, 3]);
        Assert.False(short1[7, 7]);
        // Modul gelap tetap.
        Assert.True(short1[8, short1.Size - 8]);
        Assert.Equal(1, QrCode.Encode("hai").Version);
        Assert.Throws<ArgumentException>(() => QrCode.Encode(new string('x', 300)));
    }
}
