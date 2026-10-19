// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Verrou « un seul jeu de valeurs » (ADR-016) : défauts = profil de référence = configs/simulation/reference.json (BL-35 / A0-2).

using System.Text.Json;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

/// <summary>
/// BL-35 / A0-2 — verrou de désynchronisation du profil de référence.
///
/// <para>
/// Le défaut constaté par BL-35 était une <b>resynchronisation silencieuse</b> :
/// <c>configs/simulation/reference.json</c> portait encore les anciennes dérives
/// B1 (<c>0,0002</c> / <c>0,0005</c>) alors que <c>SimulationProfiles.Reference()</c>
/// appliquait les valeurs réalignées (<c>0,003</c> / <c>0,002</c>) — deux jeux de
/// valeurs vivant côte à côte, sans rien qui crie. L'invariante d'ADR-016 (« un
/// seul jeu de valeurs à maintenir : défauts = profil de référence =
/// reference.json ») n'avait pas de gardien exécutable ; ces tests le deviennent.
/// </para>
/// <para>
/// Le premier test compare le <b>fichier réel du dépôt</b> (surcouche JSON
/// fusionnée sur les défauts intégrés) au profil de référence. Le deuxième
/// verrouille la moitié « défauts = profil » de l'invariante. Le troisième rend
/// explicite la décision d'arbitrage A0-2 (modèle localisé sous gate de
/// viabilité publié, <c>docs/realignment-evidence/BL35-GATE-VIABILITE-LOCALISE.md</c>).
/// </para>
/// <para>
/// La comparaison se fait sur la <b>forme canonique</b> (clés d'objet triées), pas
/// sur la chaîne brute : la fusion reconstruit les dictionnaires (catalogue
/// d'actions) avec les clés de la surcouche en tête, si bien que l'<b>ordre</b>
/// des entrées diffère sans que la <b>valeur</b> diffère. Cet ordre n'est pas
/// comportemental : le catalogue n'est lu que par clé (<c>Entries["eat"]</c>),
/// la seule itération étant celle du validateur.
/// </para>
/// </summary>
public sealed class ReferenceProfileParityTests
{
    [Fact]
    public void ReferenceJsonFile_ReplaysExactlyTheReferenceProfile()
    {
        string overlay = File.ReadAllText(LocateReferenceJson());

        SimulationOptions fromFile = ConfigLoader.MergeJson(ConfigLoader.LoadDefaults(), overlay);

        Assert.Equal(
            CanonicalJson(ConfigLoader.ToJson(SimulationProfiles.Reference())),
            CanonicalJson(ConfigLoader.ToJson(fromFile)));
    }

    [Fact]
    public void BuiltInDefaults_EqualTheReferenceProfile()
    {
        Assert.Equal(
            CanonicalJson(ConfigLoader.ToJson(SimulationProfiles.Reference())),
            CanonicalJson(ConfigLoader.ToJson(ConfigLoader.LoadDefaults())));
    }

    [Fact]
    public void ReferenceProfile_DeclaresTheLocalizedResourceModel()
    {
        // A0-2 / BL-35 : le profil de référence tourne en modèle localisé,
        // franchissant le gate de viabilité publié le 17/10. Un passage muet en
        // réserve globale serait une contradiction d'arbitrage, pas un réglage.
        Assert.True(SimulationProfiles.Reference().Resources.Localized);
    }

    /// <summary>
    /// Localise <c>configs/simulation/reference.json</c> en remontant depuis le
    /// répertoire de sortie du test : le fichier vit à la racine du dépôt, pas
    /// dans l'assembly.
    /// </summary>
    private static string LocateReferenceJson()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "configs", "simulation", "reference.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "configs/simulation/reference.json introuvable en remontant depuis " + AppContext.BaseDirectory);
    }

    /// <summary>Forme canonique : mêmes valeurs, clés d'objet triées par ordre ordinal.</summary>
    private static string CanonicalJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Canonicalize(document.RootElement);
    }

    private static string Canonicalize(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(
            ",",
            element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonicalize(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonicalize)) + "]",
        _ => element.GetRawText(),
    };
}
