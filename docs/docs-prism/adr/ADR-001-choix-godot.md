# ADR-001 : Choix de Godot (édition .NET) pour PRISM

**Composant** : PRISM
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : §5.2.3

---

## Contexte

Le modèle de simulation ne doit pas être lié à un moteur graphique. PRISM est un **framework intermédiaire** entre le modèle LIVEX (SYNE) et le moteur graphique. Il faut un moteur pour le prototype de visualisation.

## Décision

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

## Validation / rejet

- Réouverture au moment du choix du moteur graphique définitif (migration via adaptateurs PRISM).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |