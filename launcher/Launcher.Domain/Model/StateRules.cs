using Launcher.Protocol.Model;

namespace Launcher.Domain.Model;

/// <summary>
/// Règles normatives du cycle de vie (COMPONENTS.md §4.1) et de l'agrégation de santé
/// (OBSERVABILITY.md §3). Toutes les transitions passent par ici, jamais par les appelants.
/// </summary>
public static class StateRules
{
    /// <summary>Nombre d'intervalles de sonde manqués avant l'état Défaillant (règle « perte de contact »).</summary>
    public const int MissedProbeThreshold = 3;

    /// <summary>Le délai prime sur l'état : un dépassement produit toujours Défaillant, jamais un état indéterminé.</summary>
    public static HealthReport Timeout(ComponentState during, string cause, DateTimeOffset at, int observations = 1) => new()
    {
        State = ComponentState.Defaillant,
        Cause = $"délai {Name(during)} dépassé — {cause}",
        LastObservedAt = at,
        ConsecutiveObservations = observations,
    };

    /// <summary>Perte de contact : trois intervalles de sonde consécutifs sans réponse.</summary>
    public static HealthReport Unreachable(DateTimeOffset at, int missed) => new()
    {
        State = ComponentState.Defaillant,
        Cause = $"perte de contact — {missed} intervalles de sonde sans réponse",
        LastObservedAt = at,
        ConsecutiveObservations = missed,
    };

    /// <summary>Processus terminé de façon inattendue.</summary>
    public static HealthReport ProcessLost(DateTimeOffset at, int exitCode, string outcome) => new()
    {
        State = ComponentState.Defaillant,
        Cause = $"processus terminé de façon inattendue (code {exitCode}, {outcome})",
        LastObservedAt = at,
        ConsecutiveObservations = 1,
    };

    /// <summary>Processus vivant, sonde prête : cible Prêt ou Actif selon le contexte.</summary>
    public static HealthReport Ready(DateTimeOffset at, ComponentState target, string cause) => new()
    {
        State = target,
        Cause = cause,
        LastObservedAt = at,
        ConsecutiveObservations = 1,
    };

    /// <summary>Passe une observation de sonde réussie dans le même état : le compteur de sondes manquées repart de zéro.</summary>
    public static HealthReport Confirmed(HealthReport previous, DateTimeOffset at) => new()
    {
        State = previous.State,
        Cause = previous.Cause,
        LastObservedAt = at,
        ConsecutiveObservations = previous.ConsecutiveObservations + 1,
        MissedProbeCount = 0,
    };

    /// <summary>Passe une observation de sonde manquée. Trois manquées consécutives = perte de contact.</summary>
    public static HealthReport Missed(HealthReport previous, DateTimeOffset at)
    {
        var missed = previous.MissedProbeCount + 1;
        return missed >= MissedProbeThreshold
            ? Unreachable(at, missed)
            : new HealthReport
            {
                State = previous.State,
                Cause = $"sonde manquée ({missed}/{MissedProbeThreshold})",
                LastObservedAt = at,
                ConsecutiveObservations = previous.ConsecutiveObservations + 1,
                MissedProbeCount = missed,
            };
    }

    /// <summary>Libellé français d'un état, utilisé dans les causes et l'interface.</summary>
    public static string Name(ComponentState state) => state switch
    {
        ComponentState.Absent => "Absent",
        ComponentState.Inactif => "Inactif",
        ComponentState.Demarrage => "Démarrage",
        ComponentState.Pret => "Prêt",
        ComponentState.Actif => "Actif",
        ComponentState.Suspendu => "Suspendu",
        ComponentState.Arret => "Arrêt",
        ComponentState.Defaillant => "Défaillant",
        _ => state.ToString(),
    };

    /// <summary>
    /// Agrégation globale (OBSERVABILITY.md §3.2) : le pire état parmi les composants requis,
    /// adouci pour les optionnels. Renvoie l'état global et sa cause principale, toujours affichée.
    /// </summary>
    public static (string GlobalState, string MainCause) Aggregate(
        IEnumerable<(ComponentInstance Instance, bool Required)> components)
    {
        ComponentState worstRequiredState = ComponentState.Absent;
        string worstRequiredLabel = string.Empty;
        string worstRequiredCause = string.Empty;
        var hasRequired = false;
        string? worstOptionalLabel = null;
        string? worstOptionalCause = null;
        var anyStarted = false;

        foreach (var (instance, required) in components)
        {
            var name = instance.Installation.Manifest?.Name ?? instance.ComponentId;
            var cause = instance.Health.Cause is { Length: > 0 } c ? c : Name(instance.Health.State);
            var label = $"{name} : {cause}";
            anyStarted |= instance.Health.State is not (ComponentState.Absent or ComponentState.Inactif);

            if (required)
            {
                if (!hasRequired || Severity(instance.Health.State) > Severity(worstRequiredState))
                {
                    worstRequiredState = instance.Health.State;
                    worstRequiredLabel = label;
                    worstRequiredCause = cause;
                    hasRequired = true;
                }
            }
            else if (instance.Health.State is ComponentState.Defaillant && worstOptionalLabel is null)
            {
                worstOptionalLabel = label;
                worstOptionalCause = cause;
            }
        }

        if (hasRequired && worstRequiredState is not (ComponentState.Actif or ComponentState.Pret or ComponentState.Suspendu))
        {
            var global = worstRequiredState switch
            {
                ComponentState.Defaillant => "Hors service",
                ComponentState.Absent or ComponentState.Inactif => "Hors service",
                _ => "Dégradé",
            };
            // Cause principale formatée « SYNE : cadence de tick basse » (OBSERVABILITY.md §3.2).
            return (global, worstRequiredLabel);
        }

        if (worstOptionalLabel is not null)
        {
            return ("Dégradé", worstOptionalLabel);
        }

        return anyStarted
            ? ("Sain", "tous les composants requis sont opérationnels")
            : ("Inactif", "aucun composant démarré");
    }

    private static int Severity(ComponentState state) => state switch
    {
        ComponentState.Defaillant => 5,
        ComponentState.Arret => 3,
        ComponentState.Demarrage => 2,
        ComponentState.Suspendu => 1,
        ComponentState.Pret => 1,
        ComponentState.Actif => 0,
        ComponentState.Inactif => 0,
        ComponentState.Absent => 0,
        _ => 0,
    };
}
