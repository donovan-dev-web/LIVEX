# ADR-001 : Choix de Godot (édition .NET) pour le prototype PRISM

**Composant** : PRISM
**Statut** : [Superseded]
**Décision historique** : 17 septembre 2026
**Supersédé le** : 27 septembre 2026
**Remplacé par** : [`ADR-002-choix-unreal-prism-ldk.md`](ADR-002-choix-unreal-prism-ldk.md)
**Dépend de** : —
**Source Monographie** : §5.2.3

---

> **Historique uniquement.** Cette décision concernait le prototype Godot et
> n'est plus la direction actuelle de PRISM. Elle est **supersédée par l'ADR-002**
> (Unreal Engine 5.8 + plugin PRISM-LDK, module technique `PrismLdk`), qui
> formalise ce choix. Voir [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

## Contexte

Le modèle de simulation ne doit pas être lié à un moteur graphique. PRISM est un **framework intermédiaire** entre le modèle LIVEX (SYNE) et le moteur graphique. Il faut un moteur pour le prototype de visualisation.

## Décision historique

Utiliser **Godot 4.7.2 édition .NET** (piste **[HÉRITÉ]**) avec **C#** comme langage. Le moteur **définitif reste volontairement ouvert** ; le choix interviendra après comparaison des besoins de PRISM, du pipeline d'assets, des performances et des contraintes de développement.

## Conséquences

### Positives
- Cohérence **monorepo .NET** (typage fort, build/débogage via `dotnet`, tests communs).
- Léger et adapté à un prototype de visualisation.
- Séparation framework/moteur garantie : si le moteur change, seuls les adaptateurs changent.

### Négatives
- Godot n'est pas le moteur définitif → coût de migration éventuel (limité par le framework intermédiaire).

### Risques
- Les abstractions PRISM doivent rester indépendantes de Godot (modèle propre) — principe invariant.

## Alternatives considérées

- **GDScript** : rompt la cohérence .NET → écarté.
- **Godot non-.NET** : incompatible avec le workflow C# → écarté.
- **Three.js** : alternative pour un rendu 2D/3D dans l'interface ECHOS ; non retenu pour PRISM.
- **Unity/Unreal** : surdimensionnés pour un prototype de visualisation.

## Statut actuel

- Décision **supersédée par l'ADR-002** : PRISM est le projet Unreal final de LIVEX et intègre le plugin PRISM-LDK (`PrismLdk`).
- L'affirmation « Unity/Unreal surdimensionnés pour un prototype de visualisation » portait sur le périmètre du prototype, pas sur celui du projet final ; elle n'est pas opposable à l'ADR-002.
- Les choix internes de rendu du projet Unreal PRISM relèvent de ce projet ; ils ne sont pas prescrits par cet ADR historique.
- Le principe invariant énoncé ici reste valable : le modèle de simulation ne doit pas dépendre du moteur graphique, et seul l'adaptateur change si le moteur change.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 27 septembre 2026 | Marquée supersédée par l'ADR-002 | Adoption d'Unreal et du plugin PRISM-LDK |
| 17 septembre 2026 | Création | — |
