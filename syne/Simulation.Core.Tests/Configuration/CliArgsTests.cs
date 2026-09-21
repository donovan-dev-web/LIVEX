using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

public class CliArgsTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsDefaults()
    {
        var cli = CliOptions.Parse([]);

        Assert.Null(cli.Seed);
        Assert.Null(cli.MaxTicks);
        Assert.Null(cli.WorldSize);
        Assert.Null(cli.Headless);
        Assert.Null(cli.ConfigPath);
    }

    [Fact]
    public void Parse_AllFlags()
    {
        var cli = CliOptions.Parse(["--seed", "999", "--max-ticks", "42", "--world-size", "300", "400", "--headless", "--config", "conf.json"]);

        Assert.Equal(999UL, cli.Seed);
        Assert.Equal(42, cli.MaxTicks);
        Assert.Equal((300, 400), cli.WorldSize);
        Assert.True(cli.Headless);
        Assert.Equal("conf.json", cli.ConfigPath);
    }

    [Fact]
    public void Parse_UnknownFlag_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--bogus"]));
        Assert.Contains("--bogus", ex.Message);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--max-ticks")]
    [InlineData("--config")]
    [InlineData("--world-size")]
    public void Parse_MissingValue_Throws(string flag)
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse([flag]));
        Assert.Contains(flag, ex.Message);
    }

    [Fact]
    public void Parse_SeedAcceptsUInt64Range()
    {
        var cli = CliOptions.Parse(["--seed", "18446744073709551615"]);
        Assert.Equal(ulong.MaxValue, cli.Seed);
    }
}