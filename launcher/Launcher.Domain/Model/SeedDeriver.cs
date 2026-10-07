using System.Security.Cryptography;
using System.Text;

namespace Launcher.Domain.Model;

/// <summary>
/// Dérivation des graines (EXPERIMENTS.md §5). Le Launcher n'invente jamais de graine :
/// une campagne sans stratégie explicite est refusée à la création.
/// </summary>
public static class SeedDeriver
{
    /// <summary>Dérive la graine du run n (indexé à partir de 0).</summary>
    public static long Derive(SeedStrategy strategy, long baseSeed, int runNumber, IReadOnlyList<long>? explicitSeeds = null)
    {
        return strategy switch
        {
            SeedStrategy.Derived => baseSeed + runNumber,
            SeedStrategy.Explicit => explicitSeeds is not null && runNumber < explicitSeeds.Count
                ? explicitSeeds[runNumber]
                : throw new ArgumentException($"stratégie « explicit » : aucune graine fournie pour le run {runNumber}"),
            SeedStrategy.DerivedHashed => Hash(baseSeed, runNumber),
            // « random » n'est dérivable que par la liste tirée puis enregistrée dans la
            // définition (DATA_FLOW.md §4.3) : c'est elle qui rend le run rejouable à graines
            // identiques. Sans elle, il n'y a rien à dériver et la campagne est invalide —
            // refusée à la création par ExperimentDefinition.Validate, jamais en pleine boucle.
            SeedStrategy.Random => explicitSeeds is not null && runNumber < explicitSeeds.Count
                ? explicitSeeds[runNumber]
                : throw new ArgumentException(
                    "la stratégie « random » exige les graines tirées enregistrées dans la définition (DATA_FLOW.md §4.3)"),
            _ => throw new ArgumentException($"stratégie de graine inconnue : {strategy}"),
        };
    }

    /// <summary>Hash stable SHA-256 de (baseSeed, n), ramené sur 63 bits positifs.</summary>
    private static long Hash(long baseSeed, int runNumber)
    {
        var payload = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{baseSeed}:{runNumber}"));
        var digest = SHA256.HashData(payload);
        var value = BitConverter.ToUInt64(digest, 0) & 0x7FFF_FFFF_FFFF_FFFF;
        return unchecked((long)value);
    }
}
