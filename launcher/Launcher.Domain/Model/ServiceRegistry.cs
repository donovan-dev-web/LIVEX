using Launcher.Protocol.Model;

namespace Launcher.Domain.Model;

/// <summary>
/// Compte-rendu de santé d'un composant (COMPONENTS.md §2) : état, cause principale,
/// date de dernière observation et compteur d'observations consécutives.
/// Un état vert sans cause est interdit (COMPONENTS.md §4.1).
/// </summary>
public sealed class HealthReport
{
    public ComponentState State { get; init; }

    /// <summary>Cause principale, lisible, obligatoire pour tout état autre qu'Inactif.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>Date de dernière observation, UTC.</summary>
    public DateTimeOffset LastObservedAt { get; init; }

    /// <summary>Nombre d'observations consécutives dans cet état.</summary>
    public int ConsecutiveObservations { get; init; }

    /// <summary>Nombre de sondes manquées consécutives ; repart de zéro à chaque sonde réussie.</summary>
    public int MissedProbeCount { get; init; }
}

/// <summary>Installation d'un composant : présence physique sur le poste (COMPONENTS.md §2).</summary>
public sealed class ComponentInstallation
{
    /// <summary>Identifiant de type (ex. « syne »).</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>Chemin de l'installation.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Manifeste lu à la détection. Nul si aucun manifeste trouvé (état Absent).</summary>
    public ComponentManifest? Manifest { get; init; }

    /// <summary>Cause de non-démarrabilité : « manifeste invalide », « binaire absent », etc.</summary>
    public string? DetectionCause { get; init; }

    /// <summary>Le binaire déclaré est présent sur le disque.</summary>
    public bool BinaryPresent { get; init; }

    /// <summary>Le manifeste est valide et de schéma connu.</summary>
    public bool ManifestValid => Manifest is not null && DetectionCause is null;
}

/// <summary>
/// Instance : un processus en cours d'exécution, piloté par le Launcher,
/// rattaché à une installation (COMPONENTS.md §2).
/// </summary>
public sealed class ComponentInstance
{
    /// <summary>Identifiant d'instance : « &lt;composant&gt;-&lt;nnnn&gt; » (NETWORK.md §6.1).</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Identifiant de type du composant.</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>Installation dont l'instance est issue.</summary>
    public ComponentInstallation Installation { get; init; } = new();

    /// <summary>Instance adoptée : lancée hors du Launcher, supervisée mais non possédée (COMPONENTS.md §11).</summary>
    public bool Adopted { get; init; }

    /// <summary>Compte-rendu de santé courant.</summary>
    public HealthReport Health { get; set; } = new()
    {
        State = ComponentState.Inactif,
        Cause = string.Empty,
        LastObservedAt = DateTimeOffset.MinValue,
        ConsecutiveObservations = 0,
    };

    /// <summary>PID du processus, si lancé.</summary>
    public int? ProcessId { get; set; }

    /// <summary>Points d'accès actifs résolus.</summary>
    public Dictionary<string, ResolvedEndpoint> Endpoints { get; } = new();

    /// <summary>Date de démarrage du processus.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Date d'arrêt du processus.</summary>
    public DateTimeOffset? StoppedAt { get; set; }
}

/// <summary>Point d'accès résolu : la seule forme d'adresse que le domaine connaisse (NETWORK.md §4.1).</summary>
public sealed record ResolvedEndpoint(string Kind, string Url, int Port);

/// <summary>
/// Registre de services : table des instances vivantes, unique source pour l'interface,
/// la supervision et la résolution des appels de contrôle (COMPONENTS.md §5).
/// En mémoire, reconstruit par détection, jamais persisté.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ComponentInstance> _instances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ComponentInstallation>> _installations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComponentInstallation> _activeByType = new(StringComparer.Ordinal);

    /// <summary>Compteur d'instances par composant, pour l'identifiant « &lt;composant&gt;-&lt;nnnn&gt; ».</summary>
    private readonly Dictionary<string, int> _instanceCounters = new(StringComparer.Ordinal);

    /// <summary>Enregistre une installation détectée.</summary>
    public void RegisterInstallation(ComponentInstallation installation)
    {
        lock (_gate)
        {
            if (!_installations.TryGetValue(installation.ComponentId, out var list))
            {
                list = new List<ComponentInstallation>();
                _installations[installation.ComponentId] = list;
            }

            list.RemoveAll(i => string.Equals(i.Location, installation.Location, StringComparison.OrdinalIgnoreCase));
            list.Add(installation);
            if (_activeByType.TryGetValue(installation.ComponentId, out var active)
                && string.Equals(active.Location, installation.Location, StringComparison.OrdinalIgnoreCase))
            {
                _activeByType[installation.ComponentId] = installation;
            }
        }
    }

    /// <summary>Choisit l'installation active d'un type (la première valide, par ordre de détection).</summary>
    public ComponentInstallation? GetActiveInstallation(string componentId)
    {
        lock (_gate)
        {
            if (_activeByType.TryGetValue(componentId, out var chosen))
            {
                return chosen;
            }

            if (!_installations.TryGetValue(componentId, out var list) || list.Count == 0)
            {
                return null;
            }

            var selected = list.FirstOrDefault(i => i.ManifestValid) ?? list[0];
            _activeByType[componentId] = selected;
            return selected;
        }
    }

    /// <summary>Installations détectées pour un type de composant.</summary>
    public IReadOnlyList<ComponentInstallation> GetInstallations(string componentId)
    {
        lock (_gate)
        {
            return _installations.TryGetValue(componentId, out var list)
                ? list.ToArray()
                : Array.Empty<ComponentInstallation>();
        }
    }

    /// <summary>Force l'installation active d'un type (choix explicite de l'utilisateur).</summary>
    public void SetActiveInstallation(string componentId, ComponentInstallation installation)
    {
        lock (_gate)
        {
            if (!string.Equals(componentId, installation.ComponentId, StringComparison.Ordinal)
                || !installation.ManifestValid
                || !_installations.TryGetValue(componentId, out var list))
            {
                throw new ArgumentException("l'installation ne peut pas être sélectionnée pour ce composant", nameof(installation));
            }

            var registered = list.FirstOrDefault(item =>
                string.Equals(item.Location, installation.Location, StringComparison.OrdinalIgnoreCase));
            if (registered is null || !registered.ManifestValid)
            {
                throw new ArgumentException("l'installation n'est pas enregistrée ou son manifeste est invalide", nameof(installation));
            }

            _activeByType[componentId] = registered;
        }
    }

    /// <summary>Alloue l'identifiant d'instance suivant pour un composant.</summary>
    public string NextInstanceId(string componentId)
    {
        lock (_gate)
        {
            var next = _instanceCounters.TryGetValue(componentId, out var count) ? count + 1 : 1;
            _instanceCounters[componentId] = next;
            return $"{componentId}-{next:D4}";
        }
    }

    /// <summary>Ajoute une instance au registre.</summary>
    public void Add(ComponentInstance instance)
    {
        lock (_gate)
        {
            _instances[instance.InstanceId] = instance;
        }
    }

    /// <summary>Retire une instance du registre.</summary>
    public bool Remove(string instanceId)
    {
        lock (_gate)
        {
            return _instances.Remove(instanceId);
        }
    }

    /// <summary>Retrouve une instance par identifiant.</summary>
    public ComponentInstance? Find(string instanceId)
    {
        lock (_gate)
        {
            return _instances.GetValueOrDefault(instanceId);
        }
    }

    /// <summary>Retrouve une instance d'un composant donné, par son identifiant de type.</summary>
    public ComponentInstance? FindByComponent(string componentId)
    {
        lock (_gate)
        {
            return _instances.Values.FirstOrDefault(i => i.ComponentId == componentId);
        }
    }

    /// <summary>Toutes les instances vivantes.</summary>
    public IReadOnlyList<ComponentInstance> All()
    {
        lock (_gate)
        {
            return _instances.Values.ToList();
        }
    }
}
