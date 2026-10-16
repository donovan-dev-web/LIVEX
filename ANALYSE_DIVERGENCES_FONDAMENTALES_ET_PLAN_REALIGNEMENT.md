# Analyse des divergences fondamentales entre vision originelle et projet actuel

*Date : 16 octobre 2026*

## 1) Résumé exécutif

Le constat est sans ambiguïté : **le projet s'est écarté de son objectif fondamental**.

L'objectif originel n'était **pas** de "créer un simulateur multi-agents fonctionnel", mais de **construire un dispositif expérimental** permettant de répondre à une question précise :

> *Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?*

Pour y répondre valablement, le projet exige : **lois locales explicites, autonomie fonctionnelle traçable, observabilité partielle stricte, déterminisme reproductible bit-à-bit, séparation rigoureuse décider/mesurer/montrer/archiver**.

L'état actuel penche trop vers un **"monde qui semble vivant"** (théâtre) au détriment d'un **"dispositif traçable et rejouable"** (microscope). L'écart est donc **philosophique avant d'être technique**.

**Recommandation :** Ne pas réécrire l'intégralité du dépôt. **Réaligner le cœur** (cognition BDI explicite + perception partielle + croyances faillibles + ECHOS lecture-seule stricte). Conserver ce qui est déjà bien découplé (transport WebSocket, structure modulaire, headless).

## 2) Objectif réel du projet

### 2.1 Question directrice

Extraite de `LIVEX-Monographie_SnapV0-1.md` et confirmée par `docs/monographie/01-introduction.md` :

> **Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?**

### 2.2 Exigences découlant de cette question

D'après `docs/monographie/02b-question-decomposee.md`, chaque terme impose des exigences concrètes et testables :

| Terme | Exigence | Signification pratique |
|---|---|---|
| **Environnement virtuel** | Espace à conséquences | Tout déplacement/action coûte. Ressources limitées. Rien n'est gratuit. |
| **Entités autonomes** | Décision sans commande externe, explicable | Besoin génère un **Objectif**, pas une action. L'utilité tranche entre candidats. Traçable. |
| **Lois fondamentales** | Locales, identiques pour tous, sans mémoire du collectif | Aucune règle ne référence `population`, `nb_groupes`, `tendance globale`. |
| **Comportements/structures non explicitement programmés** | Émergence **faible** traçable & rejouable | Doit pouvoir remonter `action <- intention <- objectif <- besoin <- croyance <- mémoire <- perception`. Rejouable à graine identique. |

### 2.3 Posture méthodique

`docs/monographie/00-AVANT-PROPOS.md` insiste lourdement sur :

- **Séparer fait / interprétation / hypothèse**. Un groupe observé = fait. L'appeler "société" = interprétation. Tirer une loi générale = hypothèse.
- **Distinguer "observable" de "démontré"**. Le dispositif doit **pouvoir** observer. L'observation effective doit être prouvée.
- **Distinguer "zéro observé" / "zéro non-mesuré"**. Ne jamais combler un trou de mesure.
- **Traçabilité > spectaculaire**. Mieux vaut modeste et expliqué qu'impressionnant et opaque.
- **Reproductibilité scientifique**. Déterminisme exigé.

## 3) Vision architecturale prévue

`docs/monographie/04b-projet-vision-architecture.md` formalise 4 responsabilités avec **interdits** non-négociables.

### 3.1 Séparation stricte

| Composant | Rôle | Interdit fondamental |
|---|---|---|
| **SYNE** | *Décider* – détient l'unique vérité du monde | Être mis en scène. Être influencé par rendu/analyse. |
| **ECHOS** | *Mesurer sans toucher* – observatoire en lecture seule | Modifier le monde. Interpoler. Inventer des données. |
| **PRISM** | *Montrer sans décider* – couche visuelle | Calculer décisions. Modifier état. Extrapoler pour "lisser". |
| **Launcher** | *Archiver sans relayer* – orchestration + scellement | Relayer le flux WebSocket. S'interposer entre SYNE et consommateurs. |

### 3.2 Principe d'indépendance

> *"SYNE peut exister sans ECHOS et PRISM. ECHOS et PRISM peuvent évoluer indépendamment tant qu'ils respectent les contrats définis avec SYNE."*

Le flux doit rester **unidirectionnel** : `SYNE -> (snapshot + events) -> ECHOS` et `SYNE -> PRISM`. Le pilotage est séparé via port de contrôle.

### 3.3 Les 7 principes fondamentaux

1. **Pas de narration imposée** – aucun `if tick == X`
2. **Autonomie fonctionnelle** – pas de télécommande comportementale
3. **Observabilité partielle** – imperfection moteur de diversité
4. **Déterminisme reproductible** – bit-à-bit, vérifiable
5. **Persistance** – sauvegarde/reprise sans rupture
6. **Indépendance du rendu** – SYNE headless
7. **Observabilité scientifique** – traçable, sans perturbation

## 4) Analyse comparative : snapshot vs monographie vs prototype vs état actuel

### 4.1 Comparaison philosophique

| Critère | `LIVEX-Monographie_SnapV0-1.md` | `docs/monographie/` (actuelle) | `SSE-emergent-simulation/` (prototype) | Tendance "projet actuel" |
|---|---|---|---|---|
| **Clarté de l'objectif** | Bonne (question posée) | **Excellente** (décomposée, testable) | Correcte (V1 fonctionnel) | **Floue** – glisse vers "simulateur" |
| **Méthodologie (fait/interprétation)** | Implicite | **Très explicite** | Peu explicite | Souvent absente |
| **Refus des raccourcis** | Présent | **Systématique** | Modéré | **Trop laxiste** (vouloir "que ça fonctionne") |
| **Accent sur traçabilité** | Fort | **Très fort** | Moyen | Variable selon module |
| **Honnêteté sur limites** | Présent | **Remarquable** | Correct | Moins affirmé |

### 4.2 Comparaison technique

| Aspect | Snapshot V0.1 | `docs/monographie/` visé | Prototype SSE | État à réaligner |
|---|---|---|---|---|
| **Découplage SYNE/ECHOS/PRISM** | Idée claire | **Exigence stricte** | **Assez bon** | Doit **renforcer** (interdits effectifs) |
| **Transport WebSocket** | Prévu | Prévu + contrats | **Implémenté** | À conserver |
| **Déterminisme** | Exigé | Exigé + vérifiable | Evoqué (xoshiro256**) | **À rendre testable systématiquement** |
| **BDI explicite** | Mentionné (BDI) | **BDI opérationnalisé + traçable** | **Utility AI V1** | **À refondre partiellement** |
| **Observabilité partielle** | **Centrale** | **Centrale + faillible** | Implicite/faible | **À imposer strictement** |
| **Croyances faillibles** | Oui | **Oui + révisables** | Souvent "omniscient" | **À corriger** |
| **Mémoire à décroissance** | Prévue | **Explicite (exp(-λ*t))** | Présente mais variable | **À formaliser** |
| **Communication dégradable** | Prévue | **Locale + dégradable** | Absente/naïve | **À implémenter correctement** |
| **Groupes bottom-up** | Prévu | **Détaillé** | Peu/absent | À aligner |
| **ECHOS lecture-seule** | Sous-entendu | **Absolu + provenance** | Correct dans esprit | **À durcir** |
| **Launcher "sans relayer"** | Idée | **Très explicite** | Moins marqué | **À respecter** |
| **Persistance + PRNG** | Exigé | **Exigé + sérialisable** | Présent (JSON) | À vérifier exhaustif |

## 5) Divergences fondamentales exhaustives

### 5.1 Divergences philosophiques

| # | Divergence | Impact | Priorité |
|---|---|---|---|
| **D-P1** | Passage "dispositif expérimental" -> "simulateur fonctionnel". L'objectif devient "que ça tourne bien" au lieu "que ça soit traçable". | **Fondamental** | **CRITIQUE** |
| **D-P2** | Confusion "semble émergent" vs "est traçable et rejouable". | **Fondamental** | **CRITIQUE** |
| **D-P3** | Oubli du principe "fait/interprétation/hypothèse". Tendance à sur-interpréter visuellement. | **Fondamental** | **CRITIQUE** |
| **D-P4** | Acceptation implicite de raccourcis pour obtenir de "jolis" comportements. | **Fondamental** | **CRITIQUE** |

### 5.2 Divergences architecturales

| # | Divergence | Détails | Priorité |
|---|---|---|---|
| **D-A1** | **Fuite d'autorité SYNE** | D'autres composants peuvent influencer la logique ou "compenser". SYNE doit rester **seul** juge. | **CRITIQUE** |
| **D-A2** | **ECHOS pas strictement lecture-seule** | Interpolations, valeurs par défaut, "remplissage" de séries. **Interdit** par monographie. | **CRITIQUE** |
| **D-A3** | **PRISM trop "intelligent"** | Lissage, prédiction, animation qui masque l'état brut. Doit refléter, pas interpréter. | **MAJEUR** |
| **D-A4** | **Couplage indirect** | Risque que Launcher relaie flux ou que modules partagent état non-autoritaire. | **MAJEUR** |
| **D-A5** | **Manque de respect "sans relayer"** | Si Launcher devient proxy WS -> viole l'interdit. | **MAJEUR** |
| **D-A6** | **Contrats pas appliqués à la lettre** | Tolérance excessive vs "tolérer champs inconnus mais refuser incohérences". | **MAJEUR** |

### 5.3 Divergences cognition / autonomie

| # | Divergence | Détails | Priorité |
|---|---|---|---|
| **D-C1** | **Observabilité partielle trop laxiste** | Perception trop large, ou accès à voisins "globaux". Doit être **bornée** et **strictement locale**. | **CRITIQUE** |
| **D-C2** | **Besoin -> Action direct** | Au lieu de `Besoin -> Goal -> Délibération -> Intention`. C'est un **script déguisé**. | **CRITIQUE** |
| **D-C3** | **Croyances = copie** | L'entité connaît ce qu'elle voit? Ou **croit** ce qu'elle infère? Différencier est fondamental. | **CRITIQUE** |
| **D-C4** | **BDI non-explicite** | `Beliefs/Desires/Intentions` mal séparés. Difficile d'expliquer "pourquoi j'ai choisi ça". | **CRITIQUE** |
| **D-C5** | **Utilité non-traçable** | Formule doit être décomposable (bénéfice-coût-risque-confiance-urgence-alignement). | **CRITIQUE** |
| **D-C6** | **Mémoire trop persistante** | Pas de décroissance exponentielle. Oubli trop faible ou inexistant. | **MAJEUR** |
| **D-C7** | **Confiance statique** | Messages reçus traités comme fiables. Doit pouvoir évoluer. | **MAJEUR** |
| **D-C8** | **Hétérogénéité sous-exploitée** | Traits/temperaments existent-ils réellement comme **facteurs** dans utilité ? | **MAJEUR** |

### 5.4 Divergences monde / lois / émergence

| # | Divergence | Détails | Priorité |
|---|---|---|---|
| **D-M1** | **Lois non-strictement locales** | Tout accès à "moyenne", "compte", "stats globales" dans logique décisionnelle = **interdit**. | **CRITIQUE** |
| **D-M2** | **Ordre causal flou** | Parallélisation risquée. L'ordre `perception->mémoire->croyances->besoins->objectifs->délibération->intention->action` doit être **figé**. | **CRITIQUE** |
| **D-M3** | **Coûts trop faibles** | Si se déplacer/socialiser/coûte rien -> pression disparaît -> convergence artificielle. | **MAJEUR** |
| **D-M4** | **Communication trop parfaite** | Portée large + 100% fiable -> diffusion instantanée, tue partialité. | **MAJEUR** |
| **D-M5** | **Ressources trop permissives** | Régénération/coûts mal calibrés -> survie trop facile. | **MAJEUR** |
| **D-M6** | **Événements déguisés** | "Timers", "spawners conditionnels", triggers à intervalle -> frôle narration. | **MAJEUR** |
| **D-M7** | **Groupes top-down-friendly** | Formation doit être **strictement bottom-up** (rencontres répétées + interactions). | **MAJEUR** |

### 5.5 Divergences reproductibilité / instrumentation

| # | Divergence | Détails | Priorité |
|---|---|---|---|
| **D-R1** | **Déterminisme non-vérifié** | Manque de test systématique "seed identique -> bit-à-bit". Doit exister et passer. | **CRITIQUE** |
| **D-R2** | **État PRNG incomplet** | Sauvegarde ne contient pas forcément **tout** l'état xoshiro256**. Reprise peut diverger. | **CRITIQUE** |
| **D-R3** | **Absence de DecisionTrace** | Impossible de justifier un choix a posteriori. Traçabilité incomplète. | **CRITIQUE** |
| **D-R4** | **Provenance absente** | ECHOS ignore d'où vient la donnée. "zéro observé" indistinguable. | **CRITIQUE** |
| **D-R5** | **Gap detection inexistant** | Trous ingérés silencieusement. Fausse lecture des métriques. | **CRITIQUE** |
| **D-R6** | **Archivage non-scelle** | `.livexp` modifiable après fin ? Doit être **immuable** + hashé. | **MAJEUR** |
| **D-R7** | **Config non-centralisée** | Valeurs éparpillées -> impossible de garantir config_hash cohérent. | **MAJEUR** |

## 6) Points problématiques concrets à corriger

### SYNE

- [ ] **Perception** : interdire tout accès `GetAllAgents()` dans phase décisionnelle. Uniquement via grille + rayon.
- [ ] **BeliefStore** : implémenter clairement "ce que je crois" vs "ce que je sais via perception courante". Vieillissement des croyances.
- [ ] **Goal arbitration** : plusieurs objectifs concurrents. L'urgence seule ne doit **pas** forcer. L'utilité tranche.
- [ ] **Action candidates** : générer candidats **systématiquement** depuis état cognitif, pas filtrer par "logique métier".
- [ ] **No global lookups** : bannir `world.Agents.Where(...)` dans `Decide()`.
- [ ] **Causal order test** : unité de test vérifiant l'ordre strict.

### ECHOS

- [ ] **Strict read-only** : CI pourrait vérifier via analyse statique (pas d'assignation à état SYNE).
- [ ] **Never interpolate** : remplacer tout `?? default` par marquage `missing`.
- [ ] **Track gaps** : `expected_tick = last+1`, si != -> incrémenter `gap_count`.
- [ ] **Enforce provenance** : chaque enregistrement doit avoir `source` + `ingestion_tick`.
- [ ] **Reject invented values** : métriques ne doivent **jamais** "remplir" trous.

### PRISM

- [ ] **No decision imports** : interdire imports vers `Decision`, `BDI`, `Utility`.
- [ ] **ViewModel only** : convertir snapshot -> DTO visuel pur.
- [ ] **No time extrapolation** : afficher `tick` reçu. Pas de `lerp` temporel entre ticks non-reçus.
- [ ] **Contract strictness** : valider champs requis. Logger clairement divergences.

### Intégration

- [ ] **Determinism harness** : script/test "2 runs same seed -> sha256(state_t=N) identique".
- [ ] **Headless smoke** : `SYNE --headless --ticks=500 --seed=123` doit produire events cohérents sans PRISM.
- [ ] **Config hash** : calculer depuis config normalisée (triée) et l'inclure partout.

## 7) Liste exhaustive : à reprendre / modifier / corriger / réaligner

Classé par module et priorité.

### A) SYNE – Cœur décisionnel

| Action | Détail | Priorité |
|---|---|---|
| **REPRENDRE** | **PerceptionSystem** : strictement locale (rayon + éventuelle occlusion). Interdire global scan. | **CRITIQUE** |
| **REFAIRE** | **BeliefStore** : séparé de perception. Crédulité? Révision à la baisse. Vieillissement. | **CRITIQUE** |
| **REFAIRE** | **MemorySystem** : traces + `last_seen` + décroissance `confidence *= exp(-decay*age)`. Filtrage seuil. | **CRITIQUE** |
| **REPRENDRE** | **GoalSystem** : `Need -> Goal` (avec `reason`, `urgency`, `created_tick`, `source`). | **CRITIQUE** |
| **REFAIRE** | **DecisionSystem** : rendre utilité **décomposable** + produire `DecisionTrace` complet. | **CRITIQUE** |
| **REPRENDRE** | **Intention** : engagement explicite. Peut être abandonné si plus utile ? (à définir clairement). | **MAJEUR** |
| **MODIFIER** | **ActionSystem** : candidats depuis BDI uniquement. Pas de raccourcis. | **MAJEUR** |
| **MODIFIER** | **Grid/Spatial** : seul moyen autorisé pour "qui vois-je". | **MAJEUR** |
| **VÉRIFIER** | **SimulationLoop** : ordre causal figé, immuable. | **CRITIQUE** |

### B) SYNE – Monde & persistance

| Action | Détail | Priorité |
|---|---|---|
| **VÉRIFIER** | **PRNG** : état complet sérialisé. Test reprise stricte. | **CRITIQUE** |
| **CORRIGER** | **Save/Load** : inclure `prngState`, `tick`, `configHash`, `schemaVersion`, `engineVersion`. | **CRITIQUE** |
| **MODIFIER** | **Resources** : s'assurer que consommation/regen suivent logique pure. Pas d'effet global caché. | **MAJEUR** |
| **MODIFIER** | **Obstacles/Pathfinding** : neutre vis-à-vis cognition. Jamais utilisé pour "aider" décision. | **MINEUR** |

### C) ECHOS – Ingestion & métriques

| Action | Détail | Priorité |
|---|---|---|
| **REFONDRE** | **Ingestor** : lecture seule. Refuser tout write vers SYNE. | **CRITIQUE** |
| **AJOUTER** | **GapDetector** : `expected = last_tick+1`. Log + compteur. | **CRITIQUE** |
| **AJOUTER** | **Provenance** : obligatoire sur toute entité métrique. | **CRITIQUE** |
| **SUPPRIMER/REMPLACER** | **Interpolations** : bannir. Remplacer par `missing` flag. | **CRITIQUE** |
| **MODIFIER** | **Métriques** : 100% descriptives. Aucune dérive prédictive. | **MAJEUR** |
| **MODIFIER** | **API** : exposer `gaps`, `missing_ratio`, `last_tick` clairement. | **MAJEUR** |
| **VÉRIFIER** | **Schéma DB** : aligner avec `05-contrats.md`. | **MAJEUR** |

### D) PRISM – Rendu

| Action | Détail | Priorité |
|---|---|---|
| **RESTRICTION** | **Couche** : interdire dépendance vers `Simulation.Core.Decision*`. | **CRITIQUE** |
| **REFONDRE** | **ViewModel mapping** : pur transform. Zero business logic. | **MAJEUR** |
| **SUPPRIMER** | **Extrapolation temporelle** | **MAJEUR** |
| **MODIFIER** | **Sync** : suivre `tick` strictement. Afficher "stale" si trop vieux. | **MINEUR** |

### E) Launcher – Orchestration/Archivage

| Action | Détail | Priorité |
|---|---|---|
| **VÉRIFIER** | **No-relay** : WS direct effectif. Pas de proxy. | **CRITIQUE** |
| **AJOUTER** | **Sealing**: `sealed_at`, `sha256`, `sealed=true` immutable. | **CRITIQUE** |
| **AJOUTER** | **Deterministic manifest**: tri + normalisation. | **MAJEUR** |
| **MODIFIER** | **Campagnes**: graines dérivées explicites (`base_seed + run_index*k`). | **MAJEUR** |
| **AJOUTER** | **Run metadata**: `config_hash`, `engine_version`, `git_sha?` si disponible. | **MAJEUR** |

### F) Tests & Outillage

| Action | Détail | Priorité |
|---|---|---|
| **AJOUTER** | **Determinism test**: 2 exécutions mêmes params -> état identique après N ticks. | **CRITIQUE** |
| **AJOUTER** | **Partiality test**: agent A hors rayon B -> B invisible. Message hors portée ignoré. | **CRITIQUE** |
| **AJOUTER** | **Causality test**: action T n'influence perception T+1 uniquement selon ordre. | **CRITIQUE** |
| **AJOUTER** | **Save/Load roundtrip**: save->load->tick -> même résultat que continu. | **CRITIQUE** |
| **AJOUTER** | **Goal-vs-Action test**: besoin génère goal, pas action directe. | **CRITIQUE** |
| **AJOUTER** | **No-global-lookup test**: static analysis ou test guard. | **MAJEUR** |
| **AJOUTER** | **ECHOS read-only test**: s'assurer aucun effet moteur. | **MAJEUR** |
| **AJOUTER** | **Golden run**: petit run figé + hash attendu. | **MAJEUR** |

## 8) Plan d'action pragmatique

L'objectif est de **réaligner sans tout jeter**. Prioriser le strict nécessaire.

### Phase 1 : Diagnostic & verrouillage (1-2 jours)
- [ ] Relire `docs/monographie/05-syne/*` intégralement (contrats + boucles + cognition)
- [ ] Relire `docs/monographie/06-echos/*` (ingestion + métriques)
- [ ] Cartographier code actuel vs contrats (mapping fichier/classe)
- [ ] Lister **toutes** violations "global lookup" dans SYNE
- [ ] Vérifier état PRNG save/load

### Phase 2 : SYNE – Cognition minimale (3-5 jours)
- [ ] **Durcir perception** : forcer grille + rayon. Refuser global.
- [ ] **Extraire BeliefStore** explicite (si fusionné)
- [ ] **Formaliser Memory** avec décroissance
- [ ] **Introduire Goal** systématique (besoin->goal)
- [ ] **Rendre Decision traçable** + `DecisionTrace`
- [ ] **Figer ordre causal** + tests

**Cible** : pouvoir répondre "pourquoi X a choisi Y ?" avec données concrètes.

### Phase 3 : ECHOS – Lecture-seule stricte (1-2 jours)
- [ ] **Bannir écritures** vers SYNE
- [ ] **Ajouter gap detection + provenance**
- [ ] **Supprimer toutes interpolations** -> `missing`
- [ ] **Exposer métadonnées** (gaps, last_tick)
- [ ] **Tests read-only**

### Phase 4 : Reproductibilité (1-2 jours)
- [ ] **Test déterminisme bit-à-bit**
- [ ] **Vérifier sérialisation PRNG complète**
- [ ] **Save/load roundtrip**
- [ ] **Golden run minimal**

### Phase 5 : PRISM + Launcher (1 jour)
- [ ] **Durcir frontières** (pas d'import décisionnel)
- [ ] **Vérifier "sans relayer" effectif**
- [ ] **Scellement `.livexp` + hash**

### Phase 6 : Validation méthodique (1-2 jours)
- [ ] Lancer headless 500-1000 ticks seed fixe -> stable
- [ ] Vérifier partialité stricte
- [ ] Relire métriques ECHOS : aucun "remplissage"
- [ ] Confirmer traçabilité sur 10 décisions tirées au hasard

**Total indicatif** : 8-12 jours de réalignement ciblé.

## 9) Recommandation finale

**Oui, l'ampleur des divergences justifie un réalignement sérieux du cœur.**

Mais **non, ce n'est pas "tout recommencer à zéro"**.

### À garder
- **Structure découplée** SYNE/ECHOS/PRISM – bonne intuition, à renforcer
- **Transport WebSocket** – fonctionnel, contrats à durcir
- **Headless-first** – parfaitement aligné avec vision
- **.NET stack** cohérent
- **Prototype SSE** – utile comme **référence structurelle**, pas comme source de vérité

### À corriger prioritairement
1. **Cognition** (BDI explicite + goal system)
2. **Perception partielle stricte**
3. **Croyances faillibles + mémoire à décroissance**
4. **ECHOS lecture-seule + provenance + gap detection**
5. **Déterminisme vérifiable + PRNG complet**

### Le mot d'ordre

**"Microscope, pas théâtre".**

Accepte que visuellement, le monde devienne **plus sobre** après réalignement. C'est le prix normal pour obtenir ce que tu recherches réellement : **un dispositif capable d'observer honnêtement des structures non-programmées, traçables et rejouables**.

C'est exactement ce que formalise si bien `docs/monographie/`. **C'est cette feuille de route qu'il faut suivre à la lettre**, pas s'en éloigner.

## 10) Annexes – Références utilisées

- `D:\LIVEX\LIVEX-Monographie_SnapV0-1.md` – Snapshot conception initiale
- `D:\LIVEX\docs\monographie\00-AVANT-PROPOS.md` – Posture méthodique
- `D:\LIVEX\docs\monographie\01-introduction.md` – Question directrice
- `D:\LIVEX\docs\monographie\02-contexte-philosophie.md` – Limites mondes scriptés
- `D:\LIVEX\docs\monographie\02b-question-decomposee.md` – Décomposition testable
- `D:\LIVEX\docs\monographie\04b-projet-vision-architecture.md` – Architecture + interdits
- `D:\LIVEX\docs\monographie\05-syne\*.md` – SYNE détaillé
- `D:\LIVEX\SSE-emergent-simulation\README.md` – Prototype historique
- `D:\LIVEX\SSE-emergent-simulation\docs\V2\01-VISION.md` – Vision V2 prototype