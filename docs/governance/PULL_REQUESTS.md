# PULL_REQUESTS.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `GITFLOW.md`, `CI_CD.md`
**Source Monographie** : —

---

## 1. Objectif

Ce document définit le processus de revue et de fusion des Pull Requests (PR) du dépôt LIVEX, adapté au modèle de branche `GITFLOW.md` et au pipeline `CI_CD.md`.

## 2. Règles générales

- Toute modification du code, hors hotfix trivial, passe par une PR.
- **Une PR = une unité logique** (une fonctionnalité, un bug, un document, un ADR). Ne pas mélanger.
- La PR référence au moins une issue (ou documente explicitement pourquoi aucune).
- La PR doit cibler la bonne branche : `develop` (flux normal), `main` (release/hotfix).

## 3. Contenu obligatoire (template `.github/PULL_REQUEST_TEMPLATE.md`)

1. **Résumé** : quoi, pourquoi, référence Monographie (`§`).
2. **Changements** : liste des fichiers/pertinences par composant.
3. **Tests effectués** : commandes, résultats, couverture.
4. **Impact contrat** : tout changement de contrat de transport (`WorldSnapshot`, `ExternalEvent`, API REST) ou de persistance doit être explicite — il impose une révision de la partie concernée de `VERSIONING.md`.
5. **Déterminisme** (si concerne SYNE) : impact sur la reproductibilité bit-à-bit, version moteur.

## 4. Critères de fusion

Une PR est fusionnable **uniquement** si :

1. Le pipeline GitHub Actions est vert (build, tests, couverture ≥ objectif, lint).
2. La revue est validée (actuellement : revue solo via relecture systématique ; à l'arrivée d'un contributeur, au moins 1 approbation d'une personne distincte de l'auteur).
3. Les conflits sont résolus.
4. Les contract-breaking changes sont identifiés et documentés dans le changelog.

## 5. Revue

- Relire **la logique, pas seulement la syntaxe** : conformité à la Monographie, cohérence des contrats, anti-déterminisme non documenté, introduction d'`System.Random` interdit (Monographie §3.6.3).
- Utiliser les conversations GitHub (résolution systématique avant fusion).
- Interdire la fusion avec des conversations ouvertes non résolues.

## 6. Stratégie de fusion

- `develop` : fusion **squash** (un commit propre dans l'historique d'intégration).
- `release/*` → `main` : fusion **merge** (conserver l'historique de stabilisation).
- `hotfix/*` : fusion **merge**, puis répercussion dans `develop` (cf. GITFLOW).

## 7. ADR

Les PR de type ADR suivent le format `docs/adr/0000-template.md` : une proposition qui documente Contexte / Décision / Conséquences / Alternatives. La fusion d'un ADR est conditionnée à la validation de la décision ; un ADR `Proposed` reste documenté mais non suivi par le code tant qu'il n'est pas `Accepted`.

---

## Points restés ouverts dans ce document
- Aucun — document stabilisé. Le nombre de reviewers obligatoires sera fixé à 1 dès l'arrivée des premiers contributeurs.