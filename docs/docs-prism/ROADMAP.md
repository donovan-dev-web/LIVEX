# ROADMAP — PRISM

**Composant** : PRISM
**Statut** : DRAFT
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`../../ROADMAP.md`](../../ROADMAP.md), [`TRANSPORT_API.md`](TRANSPORT_API.md)

---

## 1. Principes

- PRISM est le projet Unreal final de LIVEX et intègre le plugin PRISM-LDK
  (*LIVEX Development Kit*, module technique `PrismLdk`).
- SYNE conserve les décisions et l'état canonique.
- Le C++ du plugin expose des contrats, types et événements Blueprint et
  reste mince. Le projet PRISM porte l'expérience et le rendu.
- Faire évoluer les contrats en cohérence avec la documentation SYNE et le
  versionnage transverse.

## 2. Étapes d'évolution

| Étape | Objectif |
| :-- | :-- |
| 1 | Stabiliser les contrats/types Blueprint et l'intégration de PRISM-LDK dans le projet PRISM |
| 2 | Valider avec SYNE réel le cycle `Prepare` → `world_initialized` → `Ready` → `Start`, puis les snapshots/deltas/événements |
| 3 | Développer dans PRISM la génération de présentation à partir du monde SYNE et la mise à jour stable par ID |
| 4 | Ajouter inspection, vues de groupe/relations et indicateurs de run, selon les données réellement exposées |
| 5 | Mesurer le coût des snapshots, du nombre d'entités et du rendu ; optimiser dans PRISM sans déplacer la logique décisionnelle |
| 6 | Maintenir les tests interop, les erreurs de transport, la compatibilité de contrat et les valeurs inconnues |

Ces étapes sont des objectifs, non une déclaration que les fonctions visuelles
sont déjà implémentées par le plugin.

## 3. Risques et réponse

| Risque | Réponse |
| :-- | :-- |
| PRISM confond état visuel et état simulé | Conserver SYNE comme autorité ; documenter toute projection Unreal comme visuelle |
| Contrat modifié sans coordination | Aligner les évolutions sur `docs-syne/API_CONTRACTS.md` et le versionnage partagé |
| Traitement double d'une mutation via snapshot et événement | Utiliser le snapshot comme état courant ; réserver les événements aux notifications |
| Les tests mock sont pris pour une preuve d'équivalence | Exécuter les vérifications décisionnelles et trajectoires contre SYNE réel |
| Le plugin devient un moteur de rendu ou de décision | Garder sa frontière limitée à l'intégration C++/Blueprint ; placer expérience et rendu dans PRISM |
