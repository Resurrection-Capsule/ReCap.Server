using ReCap.Server.Config;

namespace ReCap.Tests.Config;

public class LocaleSettingsTests
{
    private sealed class FakeSource(string? value) : ILocaleSource
    {
        public string? Read() => value;
    }

    [Fact]
    public void CliCodeWinsOverRegistry()
    {
        var s = LocaleSettings.Resolve("pt-br", new FakeSource("en-us"));
        Assert.Equal("pt-br", s.Code);
    }

    [Fact]
    public void RegistryUsedWhenNoCli()
    {
        var s = LocaleSettings.Resolve(null, new FakeSource("pt-br"));
        Assert.Equal("pt-br", s.Code);
    }

    [Fact]
    public void FallsBackToEnUsWhenNoneValid()
    {
        var s = LocaleSettings.Resolve("garbage", new FakeSource("also-bad"));
        Assert.Equal("en-us", s.Code);
    }

    [Fact]
    public void InvalidCliFallsToRegistry()
    {
        var s = LocaleSettings.Resolve("xx-yy", new FakeSource("pt-br"));
        Assert.Equal("pt-br", s.Code);
    }

    [Theory]
    [InlineData("PT-BR", "pt-br")]
    [InlineData("pt_br", "pt-br")]
    [InlineData("  en-US ", "en-us")]
    public void NormalizesCasingAndUnderscore(string raw, string expected)
    {
        var s = LocaleSettings.Resolve(raw, new FakeSource(null));
        Assert.Equal(expected, s.Code);
    }

    [Theory]
    [InlineData("en-us", 0x656E5553u)]
    [InlineData("pt-br", 0x70744252u)]
    public void DerivesBlazeIdBigEndianLangLowerRegionUpper(string code, uint expected)
    {
        var s = LocaleSettings.Resolve(code, new FakeSource(null));
        Assert.Equal(expected, s.BlazeId);
    }

    [Fact]
    public void TextPackagePathIsLocaleCodeText()
    {
        var s = LocaleSettings.Resolve("pt-br", new FakeSource(null));
        Assert.Equal(System.IO.Path.Combine("Locale", "pt-br", "Text.package"), s.TextPackageRelativePath);
    }
}
