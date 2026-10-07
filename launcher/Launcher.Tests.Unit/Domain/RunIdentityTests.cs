using Launcher.Domain.Model;
using Xunit;

namespace Launcher.Tests.Unit.Domain;

/// <summary>
/// Identité analytique d'un run (<c>{campagne}-{run}</c>). C'est la clé sous
/// laquelle ECHOS enregistre les runs de toutes les campagnes d'une
/// installation : sa stabilité conditionne la rejouabilité des paquets.
/// </summary>
public sealed class RunIdentityTests
{
    [Fact]
    public void L_identite_combine_la_campagne_et_le_run()
    {
        Assert.Equal("EXP-2026-001-RUN-0001", RunIdentity.For("EXP-2026-001", "RUN-0001"));
    }

    [Fact]
    public void Deux_run_de_campagnes_differentes_ne_se_confondent_pas()
    {
        // Sans identité composite, deux « RUN-0001 » se chevaucheraient dans la
        // base analytique et le second run écraserait le premier.
        var first = RunIdentity.For("EXP-A", "RUN-0001");
        var second = RunIdentity.For("EXP-B", "RUN-0001");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void L_identite_est_deterministe()
    {
        Assert.Equal(
            RunIdentity.For("EXP-2026-001", "RUN-0042"),
            RunIdentity.For("EXP-2026-001", "RUN-0042"));
    }

    [Fact]
    public void L_identite_respecte_la_longueur_maximale_du_moteur()
    {
        var campaign = new string('a', 128);

        var failure = Assert.Throws<ArgumentException>(() => RunIdentity.For(campaign, "RUN-0001"));
        Assert.Contains("128", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_identite_juste_a_la_limite_est_acceptee()
    {
        var campaign = new string('a', RunIdentity.MaxLength - "RUN-0001".Length - 1);

        var identity = RunIdentity.For(campaign, "RUN-0001");

        Assert.Equal(RunIdentity.MaxLength, identity.Length);
    }

    [Theory]
    [InlineData("", "RUN-0001")]
    [InlineData("EXP-2026-001", "")]
    public void Un_identifiant_manquant_est_refuse(string campaign, string run)
    {
        Assert.Throws<ArgumentException>(() => RunIdentity.For(campaign, run));
    }

    [Fact]
    public void La_variante_sans_echec_renvoie_null_pour_une_identite_impossible()
    {
        Assert.Null(RunIdentity.TryFor(new string('a', 128), "RUN-0001"));
        Assert.Equal("EXP-RUN-0001", RunIdentity.TryFor("EXP", "RUN-0001"));
    }
}