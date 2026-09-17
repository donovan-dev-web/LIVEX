# CONTRIBUTING.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `GITFLOW.md`, `VERSIONING.md`, `CI_CD.md`, `docs/governance/*`

---

## 1. Principe

Même en développement solo, ces conventions servent au « futur toi » et à d'éventuels contributeurs. Elles sont volontairement légères mais **opposables** (les PR qui les violent sont refusées).

## 2. Prérequis

- Lire `README.md`, `VISION.md` et `ARCHITECTURE.md`.
- Comprendre le monorepo : 3 composants (`syne/`, `echos/`, `prism/`) versionnés indépendamment.
- Ne jamais modifier un **contrat de transport** sans relire `COMMUNICATION.md` et `docs/docs-*/API_CONTRACTS.md`/`API_REST.md`/`TRANSPORT_API.md`.

## 3. Workflow Git

Suivre `GITFLOW.md` :
- Branches `feature/<composant>-<sujet>`, `fix/<composant>-<sujet>`, `docs/<composant>-<sujet>`.
- Commit conventionnel : `feat(syne): ...`, `fix(echos): ...`, `docs(prism): ...`.
- PR vers `develop` (squash), releases par `release/<composant>-vX.Y.Z`.

## 4. Avant d'ouvrir une PR — checklist

- [ ] Issue référencée et rattachée à un composant + une phase de roadmap (`docs/governance/ISSUES.md`).
- [ ] Tests : nouveaux tests unitaires pour toute fonctionnalité ; **le déterminisme de SYNE n'est jamais cassé**.
- [ ] Couverture maintenue ≥ 80 % sur le composant touché.
- [ ] Documentation mise à jour (doc des composants + `CHANGELOG.md` + docs). **Une PR qui modifie le comportement sans MAJ de doc est refusée.**
- [ ] Style : respecter les conventions du langage (C# .editorconfig / lint TS / format python black, etc. — cf. `CI_CD.md`).
- [ ] Le contrat de transport n'est modifié que selon `VERSIONING.md` (evol adhesion `MINOR`, breaking `MAJOR`).

## 5. Revue

- Solo : **auto-revue documentée** (relire le diff à tête reposée, décrire les points vérifiés dans la PR).
- Contributions externes : au minimum 1 reviewer, sinon pas de merge.
- Règle : pas de merge direct sur `main`, protection de branches.

## 6. Signalement de bugs

- Ouvrir une issue avec le template `bug_report.md` (`.github/ISSUE_TEMPLATE/`).
- Pour un bug de déterminisme SYNE : toujours fournir seed + config + version moteur (reproductibilité exigée).

## 7. Définition de « fini »

| Critère | Définition |
| :-- | :-- |
| Code | Implémenté + testé (unitaire + intégration) |
| Déterminisme | Vérifié (checksums, `BitIdenticalPersistenceTest`) |
| Doc | Docs du composant + CHANGELOG à jour |
| CI | `.github/workflows/ci.yml` vert |
| Périmètre | Pas de `[OUVERT]` introduits sans trace dans `ROADMAP.md` |

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |