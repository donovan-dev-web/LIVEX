namespace Launcher.Protocol.Model;

/// <summary>Type fonctionnel d'un composant piloté (COMPONENTS.md §2).</summary>
public enum ComponentType
{
    /// <summary>Moteur de simulation (SYNE).</summary>
    Engine,
    /// <summary>Analyse (ECHOS).</summary>
    Analysis,
    /// <summary>Immersion (PRISM).</summary>
    Immersion,
    /// <summary>Utilitaire (type prévu, non attribué en V0.1 — ISSUES.md O-27).</summary>
    Utility,
}

/// <summary>Mode d'utilisation servi par un composant (COMPONENTS.md §3, champ contributesTo).</summary>
public enum UsageMode
{
    /// <summary>Mode Contrôle : socle, porte par le moteur.</summary>
    Control,
    /// <summary>Mode Analyse : porte par ECHOS.</summary>
    Analysis,
    /// <summary>Mode Immersion : porte par PRISM.</summary>
    Immersion,
}

/// <summary>Type de session (COMPONENTS.md §8.1) : change ce que l'interface expose, pas les composants.</summary>
public enum SessionKind
{
    Production,
    Experience,
    Developpement,
}

/// <summary>États du cycle de vie d'un composant piloté (COMPONENTS.md §4).</summary>
public enum ComponentState
{
    /// <summary>Aucune installation détectée.</summary>
    Absent,
    /// <summary>Installation présente, aucun processus.</summary>
    Inactif,
    /// <summary>Processus lancé, attente de la sonde de santé.</summary>
    Demarrage,
    /// <summary>Sonde au vert, pas encore sollicité.</summary>
    Pret,
    /// <summary>En service, sollicité par le mode.</summary>
    Actif,
    /// <summary>Vivant mais inactif, prêt à reprendre.</summary>
    Suspendu,
    /// <summary>Arrêt demandé, attente de terminaison.</summary>
    Arret,
    /// <summary>Sonde au rouge ou processus perdu.</summary>
    Defaillant,
}
