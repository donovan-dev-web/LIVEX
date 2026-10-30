// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Setup.Core.Simulation;
using Xunit;

namespace Setup.Tests.Unit;

public class DemoWorldTests
{
    [Fact]
    public void Manifest_Demo_Est_Valide()
    {
        var manifest = DemoWorld.CreateManifest();

        Assert.Empty(ManifestValidator.Validate(manifest));
        Assert.Equal(DemoWorld.Product, manifest.Product);
        Assert.Equal("demo", manifest.Channel);
    }

    [Fact]
    public void Manifest_Contient_Les_Composants_Obligatoires_Et_Facultatifs()
    {
        var manifest = DemoWorld.CreateManifest();
        var mandatory = manifest.Components.Where(c => c.Mandatory).Select(c => c.Id).ToArray();

        Assert.Equal(new[] { "launcher", "syne", "echos", "contracts", "resources" }, mandatory);
        Assert.Contains(manifest.Components, c => c.Id == "prism");
        Assert.Contains(manifest.Components, c => c.Id == "prism-ldk");
        Assert.Contains(manifest.Components, c => c.Id == "docs");
        Assert.Contains(manifest.Components, c => c.Id == "syne-mock");
    }

    [Fact]
    public void Prism_Est_Selectionne_Par_Defaut()
    {
        var selection = DemoWorld.DefaultSelection();

        Assert.Contains("prism", selection);
        Assert.DoesNotContain("prism-ldk", selection);
    }

    [Fact]
    public void Scenario_PrismAbsent_Rend_Prism_Indisponible_Et_Non_Selectionne()
    {
        var manifest = DemoWorld.CreateManifest(DemoScenario.PrismAbsent);
        var prism = manifest.Components.Single(c => c.Id == "prism");

        Assert.Equal("unavailable", prism.Acquisition.Kind);
        Assert.Empty(prism.Files);
        Assert.Empty(ManifestValidator.Validate(manifest));
        Assert.DoesNotContain("prism", DemoWorld.DefaultSelection(DemoScenario.PrismAbsent));
    }

    [Fact]
    public void Les_Empreintes_Du_Monde_Fictif_Sont_Deterministes()
    {
        var first = DemoWorld.CreateManifest();
        var second = DemoWorld.CreateManifest();

        Assert.All(first.Files, file =>
        {
            var other = second.Files.Single(f => f.Path == file.Path);
            Assert.Equal(file.Sha256, other.Sha256);
            Assert.Equal(64, file.Sha256.Length);
        });
    }

    [Fact]
    public void Les_Fichiers_Des_Composants_Sont_Tous_Declares_Dans_Files()
    {
        var manifest = DemoWorld.CreateManifest();
        var declared = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        var componentFiles = manifest.Components
            .Where(c => c.Acquisition.Kind == "payload")
            .SelectMany(c => c.Files)
            .Select(f => f.Path);

        Assert.All(componentFiles, path => Assert.Contains(path, declared));
    }
}

public class ManifestValidatorTests
{
    [Fact]
    public void Refuse_Un_Chemin_Absolu_Ou_Remontant()
    {
        Assert.NotEmpty(ManifestValidator.PathIssues("/etc/passwd"));
        Assert.NotEmpty(ManifestValidator.PathIssues("..\\secret.txt"));
        Assert.NotEmpty(ManifestValidator.PathIssues("C:\\Windows\\system32\\a.dll"));
        Assert.NotEmpty(ManifestValidator.PathIssues(""));
        Assert.Empty(ManifestValidator.PathIssues("components/syne/syne.exe"));
    }

    [Fact]
    public void Refuse_Un_Composant_Obligatoire_Dependant_D_un_Facultatif()
    {
        var manifest = WithComponents(new[]
        {
            Component("core", mandatory: true, requires: new[] { "extra" }),
            Component("extra", mandatory: false)
        });

        var issues = ManifestValidator.Validate(manifest);

        Assert.Contains(issues, issue => issue.Contains("obligatoire", StringComparison.Ordinal));
    }

    [Fact]
    public void Refuse_Une_Dependance_Absente_Du_Manifeste()
    {
        var manifest = WithComponents(new[] { Component("core", mandatory: true, requires: new[] { "ghost" }) });

        var issues = ManifestValidator.Validate(manifest);

        Assert.Contains(issues, issue => issue.Contains("ghost", StringComparison.Ordinal));
    }

    [Fact]
    public void Refuse_Un_Actif_Sans_Empreinte_Ni_Taille()
    {
        var asset = new ComponentDescriptor(
            "module", "1.0.0", false, "Module", "Module", "d", "d", "GPL-3.0-or-later",
            Array.Empty<string>(), Array.Empty<FileEntry>(),
            new AcquisitionDescriptor("release-asset", "a.zip", "https://example.invalid/a.zip", null, null, "zip", null, null));

        var issues = ManifestValidator.Validate(WithComponents(new[] { asset }));

        Assert.Contains(issues, issue => issue.Contains("taille", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("empreinte", StringComparison.Ordinal));
    }

    [Fact]
    public void Refuse_Un_Composant_Indisponible_Avec_Des_Fichiers()
    {
        var component = new ComponentDescriptor(
            "module", "1.0.0", false, "Module", "Module", "d", "d", "GPL-3.0-or-later",
            Array.Empty<string>(), new[] { new FileEntry("a/b.dll", 10, Hash("a/b.dll")) },
            new AcquisitionDescriptor("unavailable", null, null, null, null, null, null, "absent"));

        var issues = ManifestValidator.Validate(WithComponents(new[] { component }));

        Assert.Contains(issues, issue => issue.Contains("indisponible", StringComparison.Ordinal));
    }

    private static DistributionManifest WithComponents(IReadOnlyList<ComponentDescriptor> components) =>
        new(1, "LIVEX", "1.0.0", "dev", "core", "win-x64", DateTimeOffset.UtcNow,
            components,
            components.SelectMany(c => c.Files).ToArray(),
            null);

    private static ComponentDescriptor Component(string id, bool mandatory, IReadOnlyList<string>? requires = null) =>
        new(id, "1.0.0", mandatory, id, id, "d", "d", "GPL-3.0-or-later",
            requires ?? Array.Empty<string>(), Array.Empty<FileEntry>(),
            new AcquisitionDescriptor("payload", null, null, null, null, null, null, null));

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
