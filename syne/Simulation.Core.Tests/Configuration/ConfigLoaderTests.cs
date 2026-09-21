using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

public class ConfigLoaderTests
{
    [Fact]
    public void Defaults_RespectAnnexeH()
    {
        var options = ConfigLoader.LoadDefaults();

        Assert.Equal(500, options.Simulation.WorldWidth);
        Assert.Equal(500, options.Simulation.WorldHeight);
        Assert.Equal(1_000_000, options.Simulation.MaxTicks);
        Assert.Equal(100, options.Agents.InitialCount);
        Assert.Equal(8, options.Agents.Traits.Count);
        Assert.All(options.Agents.Traits.Values, value => Assert.Equal(1.0, value));
        Assert.Equal(30, options.Agents.Perception.Radius);
        Assert.Equal(12_345UL, options.Random.Seed);
        Assert.Equal("xoshiro256**", options.Random.Engine);
        Assert.Equal(100, options.Resources.Food.Initial);
        Assert.Equal(100, options.Resources.Food.DegradationTick);
        Assert.Equal(5.0, options.Resources.Water.RegenerationRate);
    }

    [Fact]
    public void PartialFile_KeepsDefaultsForMissingSections()
    {
        string path = WriteTempConfig("""
            {
              "simulation": { "worldWidth": 800, "maxTicks": 250 }
            }
            """);

        var options = ConfigLoader.LoadFile(path);

        Assert.Equal(800, options.Simulation.WorldWidth);
        Assert.Equal(500, options.Simulation.WorldHeight);
        Assert.Equal(250, options.Simulation.MaxTicks);
        Assert.Equal(12_345UL, options.Random.Seed);
        Assert.Equal(30, options.Agents.Perception.Radius);
    }

    [Fact]
    public void PartialResourceSection_PreservesSiblingDefaults()
    {
        string path = WriteTempConfig("""
            {
              "resources": { "water": { "initial": 2000 } }
            }
            """);

        var options = ConfigLoader.LoadFile(path);

        Assert.Equal(2000, options.Resources.Water.Initial);
        Assert.Equal(5.0, options.Resources.Water.RegenerationRate);
        Assert.Equal(100, options.Resources.Food.Initial);
        Assert.Equal(100, options.Resources.Food.DegradationTick);
    }

    [Fact]
    public void InvalidJson_ThrowsWithExplicitMessage()
    {
        string path = WriteTempConfig("{ \"simulation\": { worldWidth: } }");

        var ex = Assert.Throws<InvalidDataException>(() => ConfigLoader.LoadFile(path));
        Assert.Contains("JSON mal formé", ex.Message);
        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void MissingFile_ThrowsFileNotFoundException()
    {
        string path = Path.Combine(Path.GetTempPath(), "absente-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Throws<FileNotFoundException>(() => ConfigLoader.LoadFile(path));
    }

    private static string WriteTempConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "syne-config-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}