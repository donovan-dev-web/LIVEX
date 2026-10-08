# ROADMAP-V01.md — Atteindre une V0.1 fonctionnelle

**Composant** : LIVEX (général)
**Statut** : [VALIDÉ] — plan d'exécution arbitré le 8 octobre 2026, en cours d'exécution
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `ROADMAP.md`, `VERSIONING.md`, `RAPPORT-ELEMENTS-OUVERTS.md`, `Livex-status.md`, `launcher/ROADMAP-V1.md`, `launcher/V1-CAPABILITY-MATRIX.md`
**Source Monographie** : Partie 9, Annexe J (jalons T0–T6)
**Machines** : **Lot L** = machine Linux actuelle (`devops-GL73-8SE`, Ubuntu, Intel Core i7-8750H / 12 cœurs / 14 Gio, dotnet 10.0.401, Node 20, Python 3) — **Lot W** = machine Windows (bascule prévue après le lot L, voir §8)

---

## 0. Arbitrages validés le 8 octobre 2026

Ces décisions font foi et remplacent les « points restés ouverts » correspondants.

| # | Sujet | Décision | Conséquence dans ce document |
| :-- | :-- | :-- | :-- |
| **A1** | Jalon T6 (`docker compose up` < 30 s) | **Supprimé** — plus de Docker : le Launcher assure désormais la distribution et l'installation ; retirer le jalon de `ROADMAP.md` et de tous les documents qui le portent | Étape 10 devient « retrait du jalon T6 » |
| **A2** | Stabilité long-run (12 h / 72 h) | **Hors périmètre V0.1** — éventuellement réalisé pour une V1 stable ; reporté en P2 | Étape 6 réduite aux critères restants (cadence, backpressure, lag, reprise worker, parcours UI) |
| **A3** | Machine de référence des benchmarks V5 | **Cette machine Linux** (i7-8750H, 12 cœurs) | Étape 7 exécutible immédiatement ici, résultats documentés dans `PERFORMANCE.md` |
| **A4** | Workflow git | **Un commit local par lot validé** — pas de push ni de PR sans demande explicite | Chaque étape cochée = un commit local |
| **A5** | Cadence de bascule Windows | **Après le lot Linux complet** | Voir §8 (liste précise de ce qui exige Windows) |

---

## 1. Objectif et périmètre de la V0.1

Une **V0.1 fonctionnelle** = *cœur scientifique + campagne de bout en bout* :

- **SYNE + ECHOS + Launcher (mode Console)** : campagne réelle complète —
  run → export de flux → ingestion → analyse → rapport archivé dans le paquet
  `.livexp` — vérifiée de bout en bout sur **Linux et Windows**.
- **PRISM / mode Immersion** : **hors périmètre** (porte d'intégration séparée,
  `launcher/V1-CAPABILITY-MATRIX.md` §4-7).
- Le mode **Développement** (`syne-mock`) reste une option de démonstration,
  jamais une preuve scientifique.
- **Docker / `docker compose`** : **hors périmètre définitif** (arbitrage A1) —
  la distribution se fait par artefacts natifs + Launcher (étapes 8 et 11).
- **Stabilité long-run** : **hors périmètre V0.1** (arbitrage A2) — reportée
  à une V1 stable (P2).

> Note de vocabulaire : le dépôt emploie « V0.1 » pour le cœur SYNE+ECHOS et
> « V1 » pour le Launcher (`launcher/ROADMAP-V1.md`). Cette roadmap les traite
> comme un seul objectif de sortie : **LIVEX V0.1**.

### État de départ (8 octobre 2026, commit `a51c95d5`)

| Composant | État | Verrou V0.1 |
| :-- | :-- | :-- |
| SYNE 0.15.0 | Complet, T0–T5 livrés, calibration B1 actée (ADR-016), V2′ 6/6 ✓ | Chemin `reset` non corrigé |
| ECHOS | API headless (`/ingest/run`, `/analysis/run|experiment|report`), schéma SQLite v6, UI retirée (ADR-007) | Validation de l'installation Linux |
| Launcher | J0–J2C franchis, campagne réelle archivée en `.livexp` | J3–J6 ouverts (reprise, archivage analyse, sécurité, distribution) |
| `syne-mock` | Stable, 44 tests | Aucun (démo uniquement) |
| PRISM | Plugin C++ sans test ni validation SYNE réel | **Hors périmètre V0.1** |
| Windows | Code Launcher partiellement multi-OS | Manifestes `linux` seuls, CI 100 % ubuntu |
| Tests | 1 086 verts, 0 échec, couverture SYNE 96,19 % | V3 et V5 non exécutées |

---

## 2. Règles de lecture

- **P0** : obligatoire — la V0.1 n'est pas déclarable sans.
- **P1** : fortement recommandé avant annonce.
- **P2** : facultatif, reporté en V0.2+.
- **Lot L** : exécutable sur la machine **Linux** actuelle.
- **Lot W** : exécutable uniquement sur la **machine Windows** (cf. §8).
- **L → W** : rédaction/écriture possible sur Linux, **validation finale sur Windows**.
- Chaque étape a un **critère de sortie vérifiable** (test, preuve CI, campagne
  ou document). Une étape n'est cochée qu'avec sa preuve liée (PR, workflow,
  test ou décision), conformément à `launcher/ROADMAP-V1.md` §7 — et, par
  arbitrage A4, chaque lot validé donne lieu à **un commit local**.

---

## 3. P0 — Bloquants V0.1 (obligatoire)

### Étape 1 — Corriger le chemin `reset` de SYNE — **Lot L**

**Problème** : `POST /api/control/reset` relance un run avec
`ConfigLoader.LoadDefaults()` au lieu du profil de référence → une campagne
pilotée longue par `reset` s'éteint artificiellement (mesuré : seed 424242,
extinction t300 par `reset` vs survie 1200/1200 par `prepare`).

- `syne/Simulation.Console/Control/ControlServer.cs:360-372` — `ResetAsync`
  ne transmet ni config ni `SimulationProfiles.ReferenceJson()`.
- `syne/Simulation.Console/Control/SimulationController.cs:412-440` —
  `ResetAsync` appelle `StartAsync(configJson: null)` → `PrepareCoreAsync` →
  `ConfigLoader.LoadDefaults()` (`SimulationFactory.cs:50-75`).
- `ControlServer.cs:276,324` impose déjà `ReferenceJson` sur les chemins
  `prepare` et `start` : le chemin `reset` doit suivre la même règle.

**Correctif attendu** : appliquer le même défaut de profil que `prepare` dans
`PrepareCoreAsync` quand `configJson` est null (proposition déjà inscrite dans
`RAPPORT-ELEMENTS-OUVERTS.md` §5.1, défaut 1).

**Critère de sortie** :
- [x] Test de non-régression : `reset` sans config produisant les mêmes
      options que `prepare` sans config (profil de référence).
      **Preuve** : `SimulationControllerRunLifecycleTests.
      Reset_WithoutConfig_BuildsTheSameReferenceProfileAsPrepare` (reset ≡
      prepare ≡ `SimulationProfiles.Reference()`).
- [x] `dotnet test` SYNE vert — **590 tests (514 Core + 76 Console), 0 échec**,
      oracles inchangés → `engineVersion` conservée (les défauts intégrés
      coïncident avec le profil depuis ADR-016 ; le correctif rend cette
      coïncidence contractuelle).
      **Preuve** : commit `ef878f21`.

### Étape 2 — Rejouer V2′ par le chemin `reset` — **Lot L**

**Preuve manquante** : la campagne V2′ du 07/10 (ADR-016, 6/6 ✓) n'a validé
que le chemin `prepare` + `start`.

**Critère de sortie** :
- [ ] Re-campagne 50 **et** 100 agents × 2500 ticks × 3 seeds, enchaînant les
      runs **via `POST /api/control/reset`**.
- [ ] Critères ADR-016 inchangés : |pente| énergie ≤ 0,005/tick, 0 extinction,
      population = initiale sur 6/6.
- [ ] Résultats consolidés versés dans `RAPPORT-ELEMENTS-OUVERTS.md` §5.1.

### Étape 3 — Lot de cohérence documentaire — **Lot L**

Incohérences constatées à corriger :

- [ ] `README.md:11,54` — version SYNE 0.13.0 → **0.15.0**.
- [ ] `Livex-status.md` — snapshot du 02/10 qui décrit encore `echos-ui`
      (`:266-269`) et « SYNE sans manifeste » (`:247-252`) : réaligner sur
      ADR-007 (05/10) et `launcher/V1-CAPABILITY-MATRIX.md`.
- [ ] `docs/PLAN-CORRECTIFS-CAMPAGNE-RUNS.md` — référencé 6 fois
      (`CHANGELOG.md:16`, `VERSIONING.md:70`,
      `RAPPORT-ELEMENTS-OUVERTS.md:6,24`, `docs/docs-echos/CHANGELOG.md:151`,
      `docs/docs-syne/adr/ADR-015`) mais **absent du dépôt et de l'historique
      git** : désactiver les références (le plan a été exécuté, ses résultats
      sont consolidés dans `RAPPORT-ELEMENTS-OUVERTS.md` §5.1).
- [ ] `docs/README.md:56` → `docs/docs_prototype/` inexistant ;
      `docs/README.md:22` décrit encore l'interface ECHOS (ADR-007).
- [ ] `ROADMAP.md:70` → `ISSUES.md` / `KANBAN.md` racine inexistants
      (ils sont dans `docs/governance/`).
- [ ] `ROADMAP.md:82` (U8) référence encore `ph8 (ECHOS-080…085)`, milestone
      retiré par ADR-007.
- [ ] `CHANGELOG.md:131-132` « Electron conservé, différé post-V0.1 » vs
      ADR-007 qui retire Electron ; compléter la section `Removed`.
- [ ] En-têtes de documents figés (`RAPPORT-ELEMENTS-OUVERTS.md`,
      `docs/docs-echos/ROADMAP.md`, `CHANGELOG.md`, `VERSIONING.md`) :
      dates de mise à jour réelles.
- [ ] `docs/docs-echos/ISSUES.md` / `ROADMAP.md:47-57` : points ouverts
      portant sur des composants supprimés.
- [ ] Compter le fichier `ROADMAP-V01.md` (non suivi) dans le premier commit
      du lot documentaire.

**Critère de sortie** : [x] Aucune référence cassée ni statut contredit
(`grep` sur les chemins ci-dessus = 0, hors tombstones annotés et snapshot
Monographie), commit local dédié (A4) — **preuve : commit `0c4d2237`**.
Étendue au-delà de la liste initiale : tombstones `docs/docs_prototype/`
(`ARCHITECTURE.md`, `ADR-002` PRISM, changelogs PRISM/racine) retiré en
PR #502.

### Étape 4 — Preuve de bout en bout J3 (campagne Console réelle) — **Lot L puis W**

Items encore ouverts de `launcher/ROADMAP-V1.md` Jalon 3 :

- [ ] Reprise de campagne **sans rejouer un run réussi** (reconnaissance et
      diagnostic des runs interrompus).
- [ ] Archivage des artefacts d'analyse et du rapport retournés dans le paquet.
- [ ] Collecte des artefacts uniquement depuis les chemins autorisés.
- [ ] Contrôle des erreurs composant par composant (échec ECHOS ne supprime
      pas les données de simulation).
- [ ] E2E réel complet : SYNE publié + ECHOS réel + rapport relu à l'identique
      depuis le paquet (au-delà de `PublishedSyneCampaignEndToEndTests.cs`).

**Critère de sortie** :
- [ ] E2E vert en CI Linux **et** en CI Windows (dépend de l'étape 5) sur le
      parcours : campagne → interruption → reprise → analyse → paquet.
- [ ] Cases du Jalon 3 cochées avec preuves liées dans `ROADMAP-V1.md`.

### Étape 5 — Support Windows (cible Linux + Windows) — **L → W**

Aujourd'hui : `runs-on: ubuntu-latest` dans toute la CI, manifestes sans clé
`executable.windows`.

**Rédigeable sur Linux (Lot L)** :
- [ ] `syne/component.json` : publication `win-x64` + clé `executable.windows`.
- [ ] `echos/component.json` : point d'entrée Windows (script/exe) + clé
      `executable.windows`.
- [ ] `syne-mock/component.json` : clé `executable.windows` (Node).
- [ ] Générateur de manifestes / validation du schéma pour les clés par OS
      (`.github/workflows/ci.yml` job « Validation des manifests »).
- [ ] Jobs CI Windows : SYNE (build+tests), ECHOS, mock, Launcher
      (unit + integration + E2E avec SYNE publié).

**À valider sur Windows (Lot W)** :
- [ ] `ManifestDetector`, `ProcessTreeKiller`, `ProcessRunExecutor` déjà
      multi-OS : vérifier les comportements Windows (chemins, arbre de
      processus, arrêt authentifié).
- [ ] `--check` / `EnvironmentChecker` validés sous Windows.

**Critère de sortie** : [ ] CI verte sur `ubuntu-latest` **et**
`windows-latest`, y compris l'E2E de campagne, **et** validations locales
Windows ci-dessus effectuées.

### Étape 6 — Validation transverse V3 (stabilité) — **Lot L**

Couvre les critères `ROADMAP.md` U7/U8 encore « non acceptés comme preuves de
release ». **La stabilité long-run est exclue** (arbitrage A2) :

- [ ] Cadence contrôlée, backpressure et lag mesurés (sinon affichage
      « non fourni », jamais de mesure fabriquée).
- [ ] Reprise du worker ECHOS après mort violente du flux SYNE
      (défaut résiduel 3 du `RAPPORT-ELEMENTS-OUVERTS.md` §5.1).
- [ ] Parcours UI complets du Launcher (états vides, erreurs, verrouillages).

~~Stabilité long-run ≥ 12 h~~ → **retirée du périmètre V0.1** (A2), voir P2.

**Critère de sortie** : [ ] Rapport de campagne V3 avec résultats attachés
(hors long-run).

### Étape 7 — Recalibrage des benchmarks (V5) — **Lot L**

`docs/docs-syne/PERFORMANCE.md:119-123` : les chiffres V0.1 doivent être
refaits **avant la validation v0.1**. Machine de référence : **cette machine
Linux** (arbitrage A3) — Intel Core i7-8750H, 12 cœurs, 14 Gio, Ubuntu.

- [ ] Figer la machine de référence dans `PERFORMANCE.md` (processeur /
      cœurs utilisés, OS, versions).
- [ ] Refaire les benchmarks aux jalons ph10 (T4), en Release.
- [ ] Comparer aux budgets de tick et documenter les écarts.

**Critère de sortie** : [ ] Section « Résultats V0.1 » de `PERFORMANCE.md`
mise à jour avec la machine et les mesures.

### Étape 8 — Publication multiplateforme — **L → W**

**Rédigeable sur Linux (Lot L)** :
- [ ] Artefacts `linux-x64` et `win-x64` produits en CI pour SYNE, ECHOS,
      `syne-mock` et le Launcher (aujourd'hui `release.yml` : ubuntu seul).
- [ ] Publication des manifestes alignés sur les artefacts produits.
- [ ] Tags composants posés selon `VERSIONING.md` §4.

**À valider sur Windows (Lot W)** :
- [ ] Installation des artefacts `win-x64` à partir des artefacts de CI sur la
      machine Windows propre.

**Critère de sortie** : [ ] Release candidate installable sur les deux OS à
partir des artefacts de CI.

---

## 4. P1 — Avant annonce (fortement recommandé)

### Étape 9 — Valider l'installation ECHOS Linux — **Lot L**

`launcher/ROADMAP-V1.md` §2B, case restée ouverte : versions Python, ressources
runtime, base analytique accessible — documenter et tester les prérequis.

**Critère de sortie** : [ ] Procédure d'installation ECHOS vérifiée sur une
machine propre, résultats dans `docs/docs-launcher/INTEGRATION_CONTRACT.md`.

### Étape 10 — Retrait du jalon T6 (Docker) — **Lot L**

**Décision A1** : plus de Docker — le Launcher fournit installation,
diagnostic et orchestration. Retirer toute promesse `docker compose` :

- [x] `ROADMAP.md:54` — supprimer le jalon **T6** (« `docker compose up`
      démarre en < 30 s ») et le mentionner comme retiré (motif : distribution
      par paquets natifs + Launcher).
- [x] `CI_CD.md` — retirer les sections « Conteneurisation Docker »
      (`:20`, `:59`, `:76`, `:86`) + secret GHCR (`:86`).
- [x] `ARCHITECTURE.md:127,147` — retirer Docker multi-stage / `compose.yml`.
- [x] `docs/docs-syne/ARCHITECTURE.md:82,133` — retirer le `Dockerfile` « à
      venir ».
- [x] `docs/docs-launcher/OBSERVABILITY.md:211` — retirer la piste
      `docker-compose` d'observabilité (`:26` est une mention négative, la
      conserver).
- [x] `README.md` et `INSTALLATION.md` — aucune occurrence (vérifié).
- [x] `CHANGELOG.md` :133 (section Unreleased, donc pas historique) réécrit ;
      entrée « T6/Docker retiré (A1) » ajoutée dans la section `Removed` ;
      `docs/docs-syne/CHANGELOG.md:371` laissé comme historique.
- [x] `.github/workflows/release.yml` : TODO de publication d'images Docker
      supprimé (YAML revalidé).
- [x] Ne **pas** modifier `LIVEX-Monographie_SnapV0-1.md/.pdf` (snapshot figé)
      ni les copies sous `bin/` (sorties de build).

**Critère de sortie** : [x] `grep -ri docker` sur les documents sources ne
remonte plus aucune promesse T6/compose (hors snapshot Monographie, bin/,
tombstones annotés et mentions négatives) — **preuve : commit du lot étape
10**.

### Étape 11 — Réalignement des statuts de jalons — **Lot L**

- [ ] `launcher/ROADMAP-V1.md` : les cases J3+ sont cohérentes avec l'état
      réel (certaines paraissent en retard sur la code base).
- [ ] `docs/docs-launcher/ROADMAP.md` : G5 « non franchi » (`README.md:24`)
      vs « réalisé contre stubs » (`CHANGELOG.md:149`) : trancher.
- [ ] `RAPPORT-ELEMENTS-OUVERTS.md` : rayer V2′/V3/V5 une fois faites, passer
      le statut à [SNAPSHOT] daté.
- [ ] `Livex-status.md` : réédition complète (état au 02/10 obsolète).

**Critère de sortie** : [ ] Un seul document fait foi pour l'état V0.1.

---

## 5. P2 — Reporté en V0.2+ (facultatif)

| Sujet | Source | Raison du report |
| :-- | :-- | :-- |
| PRISM & mode Immersion | `docs/docs-prism/ROADMAP.md` (6 étapes), V6 | Porte d'intégration séparée, chantier très lourd |
| Installateur graphique, mise à jour auto, `--check` exhaustif | `ROADMAP-V1.md` J6 | Les artefacts de l'étape 8 suffisent à la V0.1 |
| Stabilité long-run 12 h **et** 72 h | Arbitrage A2 (ex-étape 6), `ROADMAP-V1.md` J5 | **Hors périmètre V0.1** — éventuellement pour une V1 stable |
| Docker compose / jalon T6 | Arbitrage A1 (ex-étape 10) | **Supprimé définitivement** — plus de Docker, le Launcher couvre l'installation |
| ADR D4 (intentions partagées), D6 (institutionnalisation) | `RAPPORT §3.1` | Arbitrés reportés V2 |
| Primitive `Steal` | `RAPPORT §3.1` D3 | Arbitrage éthique requis (`ETHICS_AND_SCOPE.md`) |
| Rôles de groupe, conflits chiffrés, constructions agentiques, sources spatiales | `RAPPORT §4.2` | Jalons ultérieurs SYNE |
| `/communication-heatmap`, export CSV, séries Parquet, versionnage réponses API | `RAPPORT §4.3` | Périmètre UI/ECHOS étendu |
| Campagnes de démonstration avec `syne-mock` | `RAPPORT`, `ROADMAP-V1.md` §2C | Jamais une preuve scientifique |
| Redémarrage auto, IPv6/écoute distante, auth inter-composants | `V1-CAPABILITY-MATRIX.md` §5 | Comportements restrictifs assumés en V0.1 |
| Politique de rétention longue des runs | `docs/docs-syne/PERSISTENCE.md` §87 | Rotation `maxBackups` suffit |
| Calibration fine (seuils, mémoire, poids d'émergence) | `RAPPORT §3.4` | Confirmée en V5, jamais figée par accident |
| Questions de fond éthique/science | `docs/ETHICS_AND_SCOPE.md` §6 | Permanentes, hors cycle |

---

## 6. Séquence d'exécution (mise à jour après arbitrages)

```text
Ordre obligatoire — sans dates fermes — tags de machine

[L] P0-1 fix reset ──┐
                     ├─> [L] P0-2 rejouer V2′ par reset (preuve scientifique)
[L] P0-3 doc  <──────┘
        │
        v
[L] P0-10 retrait T6 (Docker)  ·  [L] P1-9 install ECHOS  ·  [L] P1-11 statuts
        │
        v
[L] P0-4 J3 E2E réel (partie Linux)  <── P0-5 rédaction Windows (manifestes + CI)
        │                                        │
        v                                        v
[L] P0-6 V3 (sans long-run)             ══ BASCULE WINDOWS (§8) ══
        │                                        │
        v                                        [W] validations locales
[L] P0-7 V5 benchmarks (machine i7-8750H)        [W] P0-5 validation + CI
        │                                        [W] P0-4 E2E Windows
        └──────────────> [L+W] P0-8 publication multiplateforme
                                  │
                                  v
                       DÉCLARATION « LIVEX V0.1 »
```

**Chemin critique** : étapes 4 (J3) et 5 (Windows) — les deux lots lourds.
Les étapes 1–3 sont rapides et lèvent les derniers blocages scientifiques.

---

## 7. Définition de « LIVEX V0.1 livrée »

La V0.1 peut être annoncée si et seulement si :

- [ ] Les 8 étapes P0 sont cochées avec preuves liées.
- [ ] Campagne Console réelle de bout en bout validée sur **Linux et
      Windows** : run → flux → ingestion → analyse → rapport dans le paquet,
      reprise sans rejouer les runs terminés.
- [ ] Chemin `reset` calibré et revalidé (V2′ par reset).
- [ ] V3 exécutée avec rapport (**hors long-run**, arbitrage A2) et V5
      exécutée sur la machine de référence Linux (arbitrage A3).
- [ ] Artefacts `linux-x64` et `win-x64` publiés par la CI.
- [ ] Documentation racine cohérente (état, versions, liens), y compris le
      retrait de toute promesse Docker/T6 (arbitrage A1).
- [ ] PRISM et mode Immersion explicitement déclarés **hors V0.1** dans le
      README et la matrice de capacités.

---

## 8. Bascule vers la machine Windows (arbitrage A5)

**Déclencheur** : le lot Linux ci-dessous est terminé, committé localement
(étapes 1, 2, 3, 4-Linux, 6, 7, 9, 10, 11 terminés + étape 5 rédigée).

À faire **uniquement sur la machine Windows** :

1. Restaurer l'environnement (dotnet 10 SDK, Node 20, Python 3) et cloner la
   branche de travail.
2. **Étape 5 (validation)** : `ManifestDetector`, `ProcessTreeKiller`,
   `ProcessRunExecutor` (chemins, arbre de processus, arrêt authentifié),
   `--check` / `EnvironmentChecker`, build + tests Launcher sous Windows.
3. **Étape 4 (E2E Windows)** : campagne → interruption → reprise → analyse →
   paquet `.livexp` contre SYNE publié `win-x64`.
4. **Étape 8 (installation)** : installer la release candidate `win-x64`
   à partir des artefacts de CI et vérifier le parcours complet.
5. Cocher les étapes 5, 4 et 8 avec les preuves CI `windows-latest`.

Ce qui **reste faisable sur Linux** pendant que la machine Windows travaille :
toute la partie rédactionnelle des étapes 5 et 8, le suivi des workflows CI
(`windows-latest` tourne sur GitHub, pas sur ta machine) et les correctifs de
bugs remontés.

---

## Points restés ouverts dans ce document

- **Bascule Windows** : la date n'est pas fixée — elle suivra la fin du lot
  Linux (§8), à convenir au moment des faits.
- Ce document est un plan, pas un constat : chaque case passe à
  `[x]` uniquement avec sa preuve (workflow, test ou décision) et un commit
  local (A4).
- `ROADMAP-V01.md` et `docs/docs-installer/` sont non suivis par git à la
  date de rédaction : à inclure dans le premier commit du lot documentaire
  (étape 3).
