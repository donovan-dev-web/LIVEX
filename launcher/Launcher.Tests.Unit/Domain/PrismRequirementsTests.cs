using Launcher.Domain.Model;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Domain;

/// <summary>
/// Exigences publiées de PRISM au manifeste (INTEGRATION_CONTRACT.md §11.1) : chaque
/// non-conformité produit la cause exacte du tableau normatif, jamais une vague généralité.
/// </summary>
public sealed class PrismRequirementsTests
{
    [Fact]
    public void Manifeste_conforme_n_expose_aucune_cause()
    {
        Assert.Empty(PrismRequirements.Validate(Conformant()));
    }

    [Fact]
    public void Mode_declare_par_contribues_to_sans_type_immersion()
    {
        var manifest = Conformant();
        manifest.Type = "service";
        manifest.ContributesTo = ["immersion"];

        Assert.Empty(PrismRequirements.Validate(manifest));
    }

    [Fact]
    public void Mode_non_declare_produit_la_cause_exacte()
    {
        var manifest = Conformant();
        manifest.Type = "service";
        manifest.ContributesTo = ["analyse"];

        Assert.Equal(["PRISM ne se déclare pas porteur du mode Immersion"], PrismRequirements.Validate(manifest));
    }

    [Fact]
    public void Capacites_manquantes_produisent_leurs_causes_exactes()
    {
        var manifest = Conformant();
        manifest.Capabilities = [];

        Assert.Equal(
        [
                "capacité « snapshotStream » absente du manifeste",
                "capacité « renderCadence » absente du manifeste",
            ],
            PrismRequirements.Validate(manifest));
    }

    [Fact]
    public void Point_de_controle_absent_ou_desactive_produit_sa_cause()
    {
        var sansControle = Conformant();
        sansControle.Endpoints = null;
        Assert.Contains("point d'accès de contrôle non déclaré", PrismRequirements.Validate(sansControle));

        var desactive = Conformant();
        desactive.Endpoints = new Dictionary<string, JsonEndpoint> { ["control"] = new() { Enabled = false } };
        Assert.Contains("point d'accès de contrôle non déclaré", PrismRequirements.Validate(desactive));
    }

    private static ComponentManifest Conformant() => new()
    {
        Schema = 1,
        Id = "prism",
        Name = "PRISM",
        Type = "immersion",
        Version = "0.1.0",
        Capabilities = ["snapshotStream", "renderCadence"],
        Endpoints = new Dictionary<string, JsonEndpoint> { ["control"] = new() { Transport = "http" } },
        ContributesTo = ["immersion"],
    };
}
