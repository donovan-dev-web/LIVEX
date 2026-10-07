using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// Fusion « surcouche partielle » de la configuration (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// La fusion s'effectuait sur deux objets <see cref="SimulationOptions"/> déjà
/// désérialisés. Or un <c>SimulationOptions</c> désérialisé est toujours
/// <b>complet</b> : toute clé absente de la surcouche prend la valeur par défaut
/// du type. La surcouche écrasait donc <b>tout</b> le profil de base, y compris
/// les sections qu'elle ne voulait pas toucher. Un client HTTP envoyant
/// <c>{"config":{"simulation":{"worldWidth":800}}}</c> s'attendreait à ne changer
/// que la largeur ; en réalité toutes les ressources, tous les seuils et tous les
/// budgets de communication retombaient sur les valeurs par défaut codées en dur.
///
/// </para>
/// <para>
/// La correction fusionne le <b>JSON brut</b> : seules les clés présentes
/// écrasent, conformément à Annexe H §5.
/// </para>
/// </summary>
public class ConfigLoaderMergeJsonTests
{
    [Fact]
    public void MergeJson_OverridesOnlyThePresentKeys()
    {
        SimulationOptions @base = SimulationProfiles.Reference();
        @base.Communication.MaxSendsPerTick = 7;

        SimulationOptions merged = ConfigLoader.MergeJson(@base, """{"simulation":{"worldWidth":800}}""");

        // La clé présente est appliquée…
        Assert.Equal(800, merged.Simulation.WorldWidth);
        // …et tout le reste du profil est conservé.
        Assert.Equal(7, merged.Communication.MaxSendsPerTick);
        Assert.Equal(20_000, merged.Resources.Food.Initial);
        Assert.False(merged.Communication.RelayEnabled);
        Assert.Equal(1.5, merged.Agents.Actions.RestEnergyGain, 10);
    }

    [Fact]
    public void MergeJson_PreservesNestedSectionsThatAreNotMentioned()
    {
        SimulationOptions @base = SimulationProfiles.Reference();

        SimulationOptions merged = ConfigLoader.MergeJson(@base, """{"agents":{"actions":{"moveEnergyCost":0.9}}}""");

        Assert.Equal(0.9, merged.Agents.Actions.MoveEnergyCost, 10);
        // Les autres réglages du même bloc et des blocs voisins survivent.
        Assert.Equal(1.5, merged.Agents.Actions.RestEnergyGain, 10);
        Assert.Equal(2.0, merged.Agents.Actions.RestFatigueRecovery, 10);
        Assert.Equal(@base.Agents.Perception.Radius, merged.Agents.Perception.Radius);
        Assert.Equal(20_000, merged.Resources.Water.Initial);
    }

    [Fact]
    public void MergeJson_OverwritesListsWholesale()
    {
        SimulationOptions @base = SimulationProfiles.Reference();
        @base.World.Territories.Zones.Add(new TerritoryZoneDefinition { Id = "a", CenterX = 1, CenterY = 1, Radius = 1 });

        SimulationOptions merged = ConfigLoader.MergeJson(@base, """{"world":{"territories":{"zones":[]}}}""");

        // Une liste ne peut pas être fusionnée élément par élément : elle est
        // remplacée, sinon on ne pourrait jamais retirer une zone.
        Assert.Empty(merged.World.Territories.Zones);
    }

    [Fact]
    public void MergeJson_EmptyOverlay_LeavesTheProfileIntact()
    {
        SimulationOptions @base = SimulationProfiles.Reference();
        SimulationOptions merged = ConfigLoader.MergeJson(@base, "{}");

        Assert.Equal(20_000, merged.Resources.Food.Initial);
        Assert.False(merged.Communication.RelayEnabled);
        Assert.Equal(@base.Simulation.WorldWidth, merged.Simulation.WorldWidth);
    }

    [Fact]
    public void ReferenceJson_MergedOverDefaults_ReproducesTheProfileExactly()
    {
        // Invariant critique du lanceur HTTP : « pas de config » doit produire
        // exactement le profil de référence, pas une approximation.
        SimulationOptions reference = SimulationProfiles.Reference();
        SimulationOptions merged = ConfigLoader.MergeJson(ConfigLoader.LoadDefaults(), SimulationProfiles.ReferenceJson());

        Assert.Equal(ConfigLoader.ToJson(reference), ConfigLoader.ToJson(merged));
    }

    [Fact]
    public void MergeJson_JsonElement_BehavesLikeTheStringOverload()
    {
        SimulationOptions @base = SimulationProfiles.Reference();

        using var document = System.Text.Json.JsonDocument.Parse("""{"simulation":{"worldWidth":640}}""");
        SimulationOptions merged = ConfigLoader.MergeJson(@base, document.RootElement);

        Assert.Equal(640, merged.Simulation.WorldWidth);
        Assert.Equal(20_000, merged.Resources.Food.Initial);
    }

    [Fact]
    public void MergeJson_MalformedJson_Throws_InsteadOfSilentlyResetting()
    {
        SimulationOptions @base = SimulationProfiles.Reference();

        Assert.Throws<InvalidDataException>(() => ConfigLoader.MergeJson(@base, "{ not json"));
    }

    [Fact]
    public void ToJson_ThenMergeJson_IsIdempotent()
    {
        SimulationOptions original = SimulationProfiles.Reference();

        SimulationOptions round = ConfigLoader.MergeJson(original, ConfigLoader.ToJson(original));

        Assert.Equal(ConfigLoader.ToJson(original), ConfigLoader.ToJson(round));
    }

    [Fact]
    public void LoadFile_And_MergeJson_Agree_OnTheSamePartialOverlay()
    {
        // Les deux chemins (fichier --config et corps HTTP) doivent produire la
        // même configuration : c'est le contrat d'Annexe H §5.
        const string partial = """{"simulation":{"worldWidth":777},"communication":{"maxSendsPerTick":3}}""";
        string path = Path.Combine(Path.GetTempPath(), $"syne-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, partial);

        try
        {
            SimulationOptions fromFile = ConfigLoader.LoadFile(path);
            SimulationOptions fromJson = ConfigLoader.MergeJson(ConfigLoader.LoadDefaults(), partial);

            Assert.Equal(ConfigLoader.ToJson(fromFile), ConfigLoader.ToJson(fromJson));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DeserializingAPartialOverlay_ProducesACompleteObject_WhichIsWhyJsonMergingIsRequired()
    {
        // Documente le piège qui a motivé la correction : désérialiser une
        // surcouche partielle en <see cref="SimulationOptions"/> ne laisse AUCUN
        // trou — les clés absentes reçoivent la valeur par défaut du type, donc
        // la surcouche devient indistinguable d'un remplacement complet. Fusionner
        // deux objets de ce type revient donc à écraser le profil entier.
        const string partial = """{"simulation":{"worldWidth":800}}""";

        SimulationOptions deserialized = System.Text.Json.JsonSerializer.Deserialize<SimulationOptions>(
            partial,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;

        // La clé demandée est là…
        Assert.Equal(800, deserialized.Simulation.WorldWidth);
        // …mais tout le reste a été rempli par la valeur par défaut du type — ici
        // comparaison au défaut AMENÉ de la même surcouche, donc strictement égale :
        // la désérialisation seule n'est jamais un profil, d'où la fusion JSON.
        SimulationOptions expected = ConfigLoader.LoadDefaults();
        expected.Simulation.WorldWidth = 800;
        Assert.Equal(ConfigLoader.ToJson(expected), ConfigLoader.ToJson(deserialized));

        // D'où la fusion sur le JSON brut : MergeJson ne voit que les clés
        // réellement présentes.
        SimulationOptions merged = ConfigLoader.MergeJson(SimulationProfiles.Reference(), partial);
        Assert.Equal(20_000, merged.Resources.Food.Initial);
        Assert.False(merged.Communication.RelayEnabled);
    }
}
