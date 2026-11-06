using Peeko.Core.Lyrics;

namespace Peeko.Core.Tests;

public class RomanizerTests
{
    [Theory]
    [InlineData("사랑해", "saranghae")]
    [InlineData("안녕하세요", "annyeonghaseyo")]
    [InlineData("밥을 먹어요", "babeul meogeoyo")]
    [InlineData("한국", "hanguk")]
    [InlineData("Love you 너를", "Love you neoreul")]
    public void Korean(string hangul, string expected) => Assert.Equal(expected, Romanizer.Romanize(hangul));

    [Theory]
    [InlineData("ありがとう", "arigatou")]
    [InlineData("きって", "kitte")]
    [InlineData("しゃしん", "shashin")]
    [InlineData("きんえん", "kin'en")]
    [InlineData("ラーメン", "raamen")]
    [InlineData("マッチ", "matchi")]
    [InlineData("君の名は", "君no名ha")]
    public void Japanese_kana(string kana, string expected) => Assert.Equal(expected, Romanizer.Romanize(kana));

    [Fact]
    public void Other_text_is_untouched()
    {
        Assert.False(Romanizer.Applies("Kuberuntung jadi anakmu"));
        Assert.Equal("Bunda", Romanizer.Romanize("Bunda"));
    }
}
