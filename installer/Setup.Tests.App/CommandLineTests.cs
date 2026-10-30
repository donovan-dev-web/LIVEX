// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.App.CommandLine;
using Setup.Core.Simulation;
using Xunit;

namespace Setup.Tests.App;

/// <summary>
/// Parseur strict de la ligne de commande : une option ou une valeur inconnue
/// doit être signalée (code 1), jamais ignorée en silence.
/// </summary>
public class CommandLineTests
{
    [Theory]
    [InlineData("--dmeo")]
    [InlineData("--log")]
    [InlineData("--log=C:/tmp/livex.log")]
    [InlineData("--inconnu")]
    public void OptionInconnue_EstSignalee(string option)
    {
        var options = AppOptions.Parse(new[] { option });

        Assert.NotEmpty(options.Unknown);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("(vide)")]
    public void ScenarioInconnu_EstSignale(string scenario)
    {
        var options = AppOptions.Parse(new[] { "--demo-scenario", scenario });

        Assert.NotEmpty(options.Unknown);
    }

    [Fact]
    public void ScenarioSansValeur_EstSignale()
    {
        var options = AppOptions.Parse(new[] { "--demo-scenario" });

        Assert.NotEmpty(options.Unknown);
    }

    [Theory]
    [InlineData("fresh", DemoScenario.FreshInstall)]
    [InlineData("neuve", DemoScenario.FreshInstall)]
    [InlineData("nouvelle", DemoScenario.FreshInstall)]
    [InlineData("existing", DemoScenario.ExistingInstall)]
    [InlineData("existante", DemoScenario.ExistingInstall)]
    [InlineData("maintenance", DemoScenario.ExistingInstall)]
    [InlineData("prism", DemoScenario.PrismAbsent)]
    [InlineData("sans-prism", DemoScenario.PrismAbsent)]
    [InlineData("failure", DemoScenario.FailureDuringWrite)]
    [InlineData("échec", DemoScenario.FailureDuringWrite)]
    public void ScenarioConnu_EstAccepte(string name, DemoScenario expected)
    {
        var options = AppOptions.Parse(new[] { "--demo-scenario", name });

        Assert.Empty(options.Unknown);
        Assert.True(options.Demo);
        Assert.Equal(expected, options.DemoScenario);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    public void PorteeConnue_EstAcceptee(string scope)
    {
        var options = AppOptions.Parse(new[] { "--uninstall", "--scope", scope });

        Assert.Empty(options.Unknown);
        Assert.Equal(scope, options.Core.Scope);
    }

    [Theory]
    [InlineData("d")]
    [InlineData("")]
    public void PorteeInconnue_EstSignalee(string scope)
    {
        var options = AppOptions.Parse(new[] { "--uninstall", "--scope", scope });

        Assert.NotEmpty(options.Unknown);
    }

    [Fact]
    public void Aide_EstReconnue()
    {
        var options = AppOptions.Parse(new[] { "--help" });

        Assert.True(options.Help);
        Assert.Empty(options.Unknown);
    }
}
