using System.Globalization;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Analyse des arguments de ligne de commande (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// Les valeurs étaient analysées avec <c>int.Parse</c>/<c>ulong.Parse</c>, en
/// culture courante : sous une culture à virgule décimale, un seeding correct
/// pouvait être rejeté, et les <c>FormatException</c>/<c>OverflowException</c>
/// resultant n'étaient pas filtrées par le point d'entrée — l'utilisateur
/// obtenait une trace d'appels. En outre <c>--world-size 500</c> (hauteur
/// manquante) consommait le flag suivant comme hauteur, <c>--help</c> levait
/// « flag inconnu », et un port hors plage échouait bien plus tard, sans
/// message.
/// </para>
/// </summary>
public sealed class CliArgsTests
{
    [Fact]
    public void Help_IsRecognised_AndDoesNotThrow()
    {
        CliOptions cli = CliOptions.Parse(["--help"]);

        Assert.True(cli.Help);
    }

    [Fact]
    public void ShortHelp_IsRecognised()
    {
        Assert.True(CliOptions.Parse(["-h"]).Help);
    }

    [Fact]
    public void HelpUsage_mentionsEveryMode()
    {
        string usage = CliOptions.Usage;

        foreach (string flag in new[] { "--seed", "--max-ticks", "--world-size", "--config", "--observe", "--serve", "--benchmark", "--help" })
        {
            Assert.Contains(flag, usage, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("--seed", "abc")]
    [InlineData("--seed", "")]
    [InlineData("--seed", "1.5")]
    [InlineData("--max-ticks", "abc")]
    [InlineData("--benchmark-ticks", "12x")]
    public void NonNumericValue_ThrowsArgumentException_NotFormatException(string flag, string value)
    {
        // Le message doit être exploitable : ArgumentException est filtrée par
        // Main (code 2), FormatException ne l'est pas.
        ArgumentException error = Assert.Throws<ArgumentException>(() => CliOptions.Parse([flag, value]));

        Assert.Contains(flag, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeSeed_ThrowsArgumentException_NotOverflowException()
    {
        // ulong.Parse("-1") levait OverflowException, non filtrée par Main.
        ArgumentException error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--seed", "-1"]));

        Assert.Contains("--seed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedAboveUInt64_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--seed", "18446744073709551616"]));
    }

    /// <summary>
    /// Les drapeaux entiers sont analysés explicitement en culture invariante.
    ///
    /// <para>
    /// Ce test vérifie la <b>stabilité</b> du comportement sous une culture non
    /// invariante, pas une différence observable : pour des drapeaux entiers,
    /// <c>NumberStyles.Integer</c> refuse déjà les séparateurs de milliers quelle
    /// que soit la culture, donc aucune valeur entière ne distingue aujourd'hui
    /// les deux appels. La garantie reste utile — elle fige le contrat si un
    /// drapeau réel ou flottant est ajouté — mais elle n'est pas, seule,
    /// discriminante.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void IntegerFlags_ParseStably_UnderAnyCulture(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);

            CliOptions cli = CliOptions.Parse(["--max-ticks", "1000", "--seed", "42"]);

            Assert.Equal(1000, cli.MaxTicks);
            Assert.Equal(42ul, cli.Seed);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DigitGroups_AreRejectedRegardlessOfCulture()
    {
        // « 1 000 » et « 1.000 » ne doivent pas être acceptés comme 1000 :
        // c'est le piège classique de l'analyse en culture courante.
        foreach (string value in new[] { "1 000", "1.000", "1,000" })
        {
            Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--max-ticks", value]));
        }
    }

    [Fact]
    public void WorldSize_ReadsBothValues()
    {
        CliOptions cli = CliOptions.Parse(["--world-size", "320", "240"]);

        Assert.NotNull(cli.WorldSize);
        Assert.Equal(320, cli.WorldSize!.Value.Width);
        Assert.Equal(240, cli.WorldSize.Value.Height);
    }

    [Fact]
    public void WorldSize_WithMissingHeight_DoesNotSwallowTheNextFlag()
    {
        // Avant : « --max-ticks » était analysé comme une hauteur, et l'erreur
        // citait « --max-ticks » comme valeur invalide — il faut donc exiger le
        // message « deux valeurs », pas seulement le nom du flag.
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CliOptions.Parse(["--world-size", "500", "--max-ticks", "10"]));

        Assert.Contains("deux valeurs", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--max-ticks", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSize_AtTheEndOfTheLine_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--world-size", "500"]));
    }

    [Fact]
    public void MissingValue_IsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--seed"]));

        Assert.Contains("--seed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FlagFollowedByAnotherFlag_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--seed", "--headless"]));
    }

    [Fact]
    public void UnknownFlag_MentionsHelp()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--turbo"]));

        Assert.Contains("--turbo", error.Message, StringComparison.Ordinal);
        Assert.Contains("--help", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    public void OutOfRangePort_IsRejectedImmediately(string port)
    {
        // 0 ferait écouter sur un port aléatoire en silence.
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CliOptions.Parse(["--serve", "--serve-port", port]));

        Assert.Contains("--serve-port", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PortWithoutItsMode_IsRejected_InsteadOfBeingIgnored()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CliOptions.Parse(["--serve-port", "6000"]));

        Assert.Contains("--serve", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservePortWithoutAnyConsumingMode_IsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CliOptions.Parse(["--observe-port", "6000"]));

        Assert.Contains("--observe-port", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Le port d'observation est consommé par deux modes. En <c>--serve</c>,
    /// <c>Program</c> démarre l'observability sur ce port : c'est le lancement
    /// du contrat d'intégration SYNE → ECHOS
    /// (<c>--serve --serve-port P --observe-port Q</c>). Refuser cette
    /// combinaison fait échouer le job d'intégration U8 avec un refus
    /// d'arguments, pas avec un bug du moteur.
    /// </summary>
    [Fact]
    public void ObservePortWithServe_IsAccepted()
    {
        CliOptions cli = CliOptions.Parse(
            ["--serve", "--serve-port", "6000", "--observe-port", "6001"]);

        Assert.True(cli.Serve);
        Assert.Equal(6000, cli.ServePort);
        Assert.Equal(6001, cli.ObservePort);
    }

    [Fact]
    public void ValidFlags_AreParsed()
    {
        CliOptions cli = CliOptions.Parse(
        [
            "--seed", "7", "--max-ticks", "10", "--world-size", "100", "100",
            "--headless", "--observe", "--observe-port", "6001",
        ]);

        Assert.Equal(7ul, cli.Seed);
        Assert.Equal(10, cli.MaxTicks);
        Assert.Equal((100, 100), cli.WorldSize);
        Assert.True(cli.Headless);
        Assert.True(cli.Observe);
        Assert.Equal(6001, cli.ObservePort);
    }

    [Fact]
    public void EmptyArguments_AreAccepted()
    {
        CliOptions cli = CliOptions.Parse([]);

        Assert.Null(cli.Seed);
        Assert.False(cli.Help);
    }
}
