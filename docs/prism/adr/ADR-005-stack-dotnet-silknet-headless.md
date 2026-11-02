# ADR-005 : Stack .NET, rendu Silk.NET OpenGL, noyau headless

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004, `../../syne/adr/ADR-006-prng-reproductible.md`
**Source Monographie** : —

---

## Contexte

SYNE (.NET 10) et le Launcher (.NET, Avalonia) sont en C#. La simulation de PRISM doit tourner sans fenêtre ni GPU pour les campagnes multi-runs, et doit rester bit-à-bit reproductible. Le rendu doit pouvoir exister sans que la simulation en dépende.

## Décision

**PRISM est une solution .NET en trois couches, dont seule la dernière référence une bibliothèque graphique.**

| Projet | Contenu | Dépendances |
| :-- | :-- | :-- |
| `Prism.Core` | Monde, génération, tick, entités, navigation, recettes, hachage, PRNG | BCL uniquement |
| `Prism.Contracts` | Messages, versionnement, codecs | BCL |
| `Prism.Host` | Exécutable headless : serveur de contrôle HTTP, liaison WebSocket, persistance | `Prism.Core`, `Prism.Contracts`, SQLite |
| `Prism.Viewer` | Fenêtre, meshing, caméra, interface | `Silk.NET` (OpenGL, Windowing, Input), UI immédiate |
| `Prism.Bots` | Agents de test (marche aléatoire, glouton, planificateur simple) | `Prism.Contracts` |

Règles :
- **Aucune référence** de `Prism.Core` vers Silk.NET ni vers du code `float`/`double` qui influence l'état (analyseur Roslyn en CI).
- Le visualiseur est un **processus client** connecté au flux de lecture ; un mode `--embedded` l'héberge dans le même processus pour le développement, avec le tick sur un thread dédié.
- OpenGL 3.3 *core* est le minimum ciblé ; le rendu n'est jamais requis par `Prism.Host`.
- Cibles : Windows x64 et Linux x86-64 (comme le reste de la pile), publication autonome.

## Conséquences

### Positives
- Un seul écosystème de build, de tests et de packaging ; manifestes Launcher inchangés dans leur forme.
- Campagnes de runs sans GPU ; CI sans carte graphique pour tout sauf le rendu.
- Frontière rendu/simulation vérifiable par les références de projets.

### Négatives
- Il faut écrire le rendu voxel (meshing, lumière, interface) que Unreal fournissait.
- OpenGL dépend des pilotes ; Linux sous Wayland à valider (GLFW).
- Les licences des dépendances (Silk.NET, bibliothèque d'interface) sont à ajouter aux notices tierces.

### Risques
- Dette de rendu : le visualiseur peut absorber le temps du noyau. Mitigation : phase P11 après le gel du contrat.

## Alternatives considérées

- **Rester sur Unreal** : voir ADR-004.
- **Godot (.NET)** : refusé, rendu et simulation seraient à nouveau mêlés dans un moteur de jeu.
- **Vulkan via Silk.NET** : repoussé ; OpenGL suffit pour un monde de 4 M blocs et élargit la compatibilité.

## Validation / rejet

Validé au jalon G2 (noyau sans référence graphique, visualiseur de debug opérationnel). Réouvert si le meshing OpenGL ne tient pas le budget de rendu (PERFORMANCE.md §6).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
