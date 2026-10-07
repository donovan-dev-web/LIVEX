namespace Launcher.Domain.Model;

/// <summary>
/// Identité d'un run dans la base analytique ECHOS.
///
/// Le Launcher nomme ses runs <c>RUN-0001</c> : cet identifiant n'est unique
/// qu'à l'intérieur d'une campagne. Or ECHOS enregistre les runs dans une base
/// unique, partagée par toutes les campagnes d'une installation, où deux
/// <c>RUN-0001</c> se chevaucheraient et le second run écraserait le premier.
///
/// L'identité analytique est donc le couple <c>{campagne}-{run}</c>, calculé ici
/// en un seul endroit et réutilisé par les deux côtés de la chaîne :
///
/// - le Launcher le passe à SYNE via <c>--run-id</c>, il est écrit dans le flux ;
/// - ECHOS lit cette valeur dans le flux exporté et l'utilise comme clé ;
/// - le Launcher la réutilise pour l'analyse, sans table de correspondance.
///
/// <para>Le format est figé par le contrat d'intégration v1 : un run enregistré
/// sous une autre forme ne serait plus retrouvable par les analyses.</para>
/// </summary>
public static class RunIdentity
{
    /// <summary>Séparateur entre l'identifiant de campagne et celui du run.</summary>
    public const char Separator = '-';

    /// <summary>Longueur maximale d'un identifiant de run SYNE.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Identifiant analytique du run : <c>{experimentId}-{runId}</c>.
    /// </summary>
    public static string For(string experimentId, string runId)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
        {
            throw new ArgumentException("identifiant de campagne manquant", nameof(experimentId));
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("identifiant de run manquant", nameof(runId));
        }

        var identity = $"{experimentId}{Separator}{runId}";
        if (identity.Length > MaxLength)
        {
            // Limite de SYNE (<c>--run-id</c>) et du magasin ECHOS : on refuse ici
            // plutôt que de voir le moteur rejeter la ligne de commande du run.
            throw new ArgumentException(
                $"identité de run trop longue ({identity.Length} > {MaxLength}) : "
                + $"« {experimentId} » + « {runId} »",
                nameof(runId));
        }

        return identity;
    }

    /// <summary>
    /// Variante non bloquante : renvoie <see langword="null"/> si l'identité ne peut
    /// pas être construite dans le format accepté, au lieu de lever.
    /// </summary>
    public static string? TryFor(string experimentId, string runId)
    {
        try
        {
            return For(experimentId, runId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}