# RAPPORT — Éléments ouverts du projet LIVEX

**Composant** : LIVEX (transverse)
**Statut** : [SNAPSHOT] — état au 30 septembre 2026, **mis à jour en session** le même jour : V1/V2 exécutées (§5.1) et les 9 ADR cognitifs arbitrés (§3.1)
**Périmètre** : tous les ADR, tous les « Points restés ouverts » de la documentation, les roadmaps/issues des 3 composants, le plan de correctifs campagne-runs et les suites de tests
**Dépend de** : `docs/adr/`, `docs/docs-syne/`, `docs/docs-echos/`, `docs/docs-prism/`, `docs/ETHICS_AND_SCOPE.md`, `docs/PLAN-CORRECTIFS-CAMPAGNE-RUNS.md`, `ROADMAP.md`

---

## 1. Objectif et méthode

Ce rapport recense **tout ce qui n'est pas clos** dans le projet, et le classe en deux
catégories disjointes :

- **Décision à prendre** : un choix humain reste à faire (accepter/rejeter une piste,
  trancher une valeur, figer un périmètre). Rien n'avance sans arbitrage.
- **Fonctionnalité ouverte** : le besoin est identifié et documenté, la décision de
  principe existe (ou n'est pas requise) ; ce qui manque est du travail d'implémentation
  ou de validation.

Sources balayées : les 10 ADR de `docs/adr/`, les 15 ADR SYNE, les 3 ADR ECHOS, les
2 ADR PRISM, les sections « Points restés ouverts » des ~45 documents, les backlogs
`docs-syne/ISSUES.md` / `docs/docs-echos/ISSUES.md`, les ROADMAPs (racine, SYNE, ECHOS,
PRISM), `docs/PLAN-CORRECTIFS-CAMPAGNE-RUNS.md` (§7 jalons et critères de re-campagne),
ainsi qu'un balayage `TODO`/`FIXME` du code (aucune occurrence : le code ne porte aucun
dette explicite non tracée).

> Règle du projet (Monographie §9.6.4) : une valeur non tranchée reste ouverte et se
> calibre plus tard, **jamais figée par accident**. Ce rapport applique cette règle à
> l'envers : il liste ce qui attend encore un arbitrage ou une réalisation.

---

## 2. Synthèse

| Catégorie | Nombre | Effort global |
| :-- | :-- | :-- |
| Décisions à prendre — ADR cognitifs ouverts | **0** — 9 arbitrées le 30/09/2026 : D2/D3/D5/D7/D8 **acceptées puis implémentées** (engineVersion 0.14.0, drapeaux inerte par défaut), D4/D6 **reportées V2**, D1 **rejetée**, note affective **écartée** | Réalisé |
| Décisions à prendre — points reportés SYNE | **7** | Faible à moyen |
| Décisions à prendre — choix produit/tech/calibration | **12** (14 moins les 2 closes par les réalignements du 30/09) | Faible unitairement |
| Fonctionnalités — mécanismes cognitifs (liés aux ADR) | **0 restante des ADR acceptés** (D7, D8, D3, D5, D2 implémentés le 30/09) | Réalisé |
| Fonctionnalités — moteur SYNE | **6** | Moyen |
| Fonctionnalités — ECHOS / API / UI | **6** | Faible à moyen |
| Fonctionnalités — PRISM / Unreal | **2 chantiers** (6 étapes) | Très lourd |
| Validations à exécuter (campagnes de runs, jalons) | V1 **exécutée et validée** ; V2 **exécutée** (critères 1/4 ✓, 2/3 ✗ → itération B1) ; **V2' exécutée** le 07/10 (ADR-016 : défauts B1 validés 50 et 100 agents × 2500 ticks, 6/6 ✓) ; restent V3–V6 | Moyen |
| Questions de fond permanentes (éthique/science) | **3 familles** | Hors cycle |

Les campagnes de runs de validation (§5) ont été **exécutées le 30/09/2026** (§5.1) :
le contrat SYNE↔ECHOS corrigé est prouvé en conditions réelles (seeds, ticks, rapports),
le déterminisme bit-à-bit est confirmé à l'échelle 1200 ticks, et la calibration B1
devait être **itérée** (critères 2 et 3 non atteints au seuil strict) — **iterée le
07/10/2026 sur les défauts intégrés du moteur** (ADR-016, `engineVersion` 0.15.0) :
critère d'énergie atteint sur 2500 ticks (6/6 runs, 50 et 100 agents), critère Eat/Drink
requalifié (faim moyenne jamais > 70).

---

## 3. Décisions à prendre

### 3.1 ADR cognitifs (`docs/adr/`) — **arbitrés le 30/09/2026**

Les huit ADR et la note ont été **arbitrés en session le 30/09/2026** ; les statuts sont
posés en tête de chaque fichier, avec motif. Récapitulatif :

| # | ADR | Décision | Motif / conséquence |
| :-- | :-- | :-- | :-- |
| D1 | ADR-Perception-Evenements.md | **Rejetée** | L'application directe des événements localisés est assumée comme exception à l'observabilité partielle (§6.11). Conséquence enregistrée : D2 fondera sa saillance sur les signaux internes, sans observation `Event` dédiée |
| D2 | ADR-Politique-Reconsideration.md | **Acceptée** | Filtre de saillance pré-délibération ; implémentation après la calibration de survie (itération B1) ; dépendance D1 réorientée (voir D1) |
| D3 | ADR-Means-End-Reasoning.md | **Acceptée (2 temps)** | 1) réintroduction d'`Attack` ; 2) bibliothèque de plans par objectif. `Buy` suit D7/D8 ; `Steal` arbitré avec `docs/ETHICS_AND_SCOPE.md` avant ajout |
| D4 | ADR-Intentions-Partagees.md | **Reportée V2** | Chantier social lourd ; à rouvrir après calibration validée et ADR socle réalisés |
| D5 | ADR-Engagements-Communicationnels.md | **Acceptée** | `Commitment` → impact `TrustLevel` ; implémentation après les ADR socle |
| D6 | ADR-Institutionnalisation-Emergence.md | **Reportée V2** | Le plus ambitieux (SYNE+ECHOS) ; à rouvrir sur base calibrée |
| D7 | ADR-XXX primitives-actions.md | **Acceptée** | Primitives atomiques ; corrige l'incohérence documentaire et débloque les composites `Trade`/`Buy` |
| D8 | ADR-XXX systeme-inventaire.md | **Acceptée** | Inventaire poids/slots, à implémenter conjointement avec D7 |
| — | Note-Couche-Affective.md | **Écartée** | L'état affectif n'entrera pas dans le moteur ; note conservée pour mémoire et traçabilité |

Ordre d'implémentation retenu : D7+D8 (socle économie) → D3 temps 1 puis 2 → D5 → D2 ;
D4 et D6 restent en attente V2.

Les sections 3.2 (reportés SYNE), 3.3 (choix produit/tech) et 3.4 (calibration)
restent **inchangées et ouvertes** — la campagne V1/V2 (§5.1) fournit les premières
données réelles de calibration sans trancher les valeurs.

### 3.2 Décisions reportées côté SYNE (`docs/docs-syne/ROADMAP.md` §6)

Chiffrages et mécaniques **consciemment reportés** à la réalisation — ils ne bloquent
rien mais restent formellement ouverts :

| Sujet | Où le trancher | État |
| :-- | :-- | :-- |
| Latence des pulsations de communication (vitesse de signal) | Paramètre configurable optionnel (implémentation communication) | Ouvert |
| Résolution **chiffrée** des conflits (probabilités, dégâts — ADR-009) | Implémentation `Interaction/` | Ouvert (`SYSTEMS_SPEC.md` §103) |
| Modèle d'héritage / fusion consentie (logique cognitive exacte — décision n°17) | Implémentation cycle de vie | Ouvert (`SYSTEMS_SPEC.md` §105) |
| Mécanique **agentique** des constructions : coût, matériaux, qui construit (décision n°20) | Post-SYNE-071 | Ouvert (les obstacles statiques sont livrés, pas la construction par les entités) |
| « Instruire » (SYNE-042) : ingestion en mémoire épisodique | Jalon ph7 (sources spatiales) | Ouvert |
| Sources spatiales de ressources (décision n°4) | Jalon ph7 (SYNE-071/072) | Hors V0.1, à rouvrir |
| Fréquences numériques exactes du scheduler V0.1 | Valeurs [HÉRITÉ] conservées | À confirmer en calibration |

### 3.3 Choix produit / technique à trancher

| Sujet | Source | Pourquoi c'est ouvert |
| :-- | :-- | :-- |
| Périmètre exact des écrans web (React/Vite) au-delà des parties 4 et 5 de la Monographie | `docs/docs-echos/ROADMAP.md` §55, `docs/docs-echos/ISSUES.md` §155 | Marqué `[OUVERT]` explicitement |
| Bibliothèque de visualisation par vue (ECharts vs Plotly) | `docs/docs-echos/ARCHITECTURE.md` §111 | « À affiner à l'implémentation » — ECharts est utilisé en pratique (Écran C), le choix par vue n'est pas formalisé |
| Pixel-perfect vs lint visuel (tests snapshot UI) | `docs/docs-echos/UI_DESIGN.md` §125 | Choix « à trancher en implémentation (phase 6) » |
| Contraste exact des états de graphe D3, à valider avec le rendu PRISM (éclair rouge = Warning…) | `docs/docs-echos/UI_DESIGN.md` §126 | Dépend de PRISM |
| Normalisation de `SystemComplexity` (échelles hétérogènes) | `docs/docs-echos/EMERGENCE_INDICATORS.md` §81 | Dérivée possible, non tranchée |
| Authentification entre composants | `COMMUNICATION.md` §132 | Non requis en local V0.1 ; **à réévaluer si ECHOS n'est plus local** |
| Politique de compression/archivage des vieux runs (rétention) | `docs/docs-syne/PERSISTENCE.md` §87 | Rotation simple `maxBackups` en V0.1, pas de politique à long terme |
| Clés de configuration par espèce : format final | `docs/docs-syne/CONFIGURATION.md` §382 | « À stabiliser avec le modèle de paramétrages » |
| Obstacles rectangles + attribut `Passable` | `docs/docs-syne/DATA_MODEL.md` §169-170 | Reporté à la navigation V2 |
| Politique de tri de sévérité P0..P3 | `docs/governance/ISSUES.md` §75 | À affiner avec la première fréquence réelle d'issues |
| Rythme de pose des tags `livex-v` (release assemblée) | `VERSIONING.md` fin | À affiner à la première release CI/CD réelle |
| Divergence documentaire saisons | `docs/docs-syne/SYSTEMS_SPEC.md` §107 vs `docs/docs-syne/ROADMAP.md` ph7 | SYSTEMS_SPEC indique « mécanique V2 (saisons, événements globaux) non activée en V0.1 » alors que les saisons sont livrées (SYNE-072, 0.9.0) — le doc doit être réaligné ou le périmètre V0.1 clarifié |

### 3.4 Calibration chiffrée (valeurs à confirmer sur runs réels)

Toutes ces valeurs sont **configurables et tracées** (jamais figées par accident) ;
les campagnes du 30/09/2026 (§5.1) fournissent les premières données réelles — la
pente d'énergie résiduelle (≈ −0,039/tick) et la part Eat/Drink (1,9–3,2 %) encadrent
directement l'itération B1 :

| Valeur | Source | État |
| :-- | :-- | :-- |
| Seuils de besoins (défauts actés 50/50/70, décision n°4) | `docs/docs-syne/COGNITIVE_ARCHITECTURE.md` §106, `DATA_MODEL.md` §167 | Calibration générale à faire |
| Coûts/bénéfices d'actions hors Eat/Drink (énergie, temps, risque) [HÉRITÉ] | `COGNITIVE_ARCHITECTURE.md` §107 | À réévaluer — Eat/Drink viennent d'être calibrés (ADR-015, 0.13.0), le reste suit |
| Bonus d'alignement ×1.2 et `actionSwitchMargin` 0.05 | `COGNITIVE_ARCHITECTURE.md` §108 | Valeurs optimales à confirmer |
| Plage de décroissance mémoire [HÉRITÉ] | `DATA_MODEL.md` §168 | À confirmer |
| Fenêtres/seuils des moteurs ECHOS (100 ticks, fréquence > 2, amplification > 1,5) [HÉRITÉ] | `docs/docs-echos/TESTING.md` §314, `METRICS_SPEC.md` §200 | À confirmer en calibration (constantes stables pour les goldens) |
| Poids du score d'émergence (0.15/0.10/0.20/0.25) [HÉRITÉ] | `docs/docs-echos/EMERGENCE_INDICATORS.md` §80 | Sensibilité à étudier sans changer la formule |
| Budgets de profilage sur sims réelles | `docs/docs-echos/LOGGING_INSTRUMENTATION.md` §154 | Valeurs V0.1 en garde-fou CI |
| Profondeur max des chaînes causales affichables | `docs/docs-echos/CAUSAL_ANALYSIS.md` §84 | Calibrage à l'implémentation de l'outil de reconstruction |

---

## 4. Fonctionnalités ouvertes

### 4.1 Mécanismes cognitifs (implémentation des ADR §3.1)

Les 5 ADR acceptés sont **implémentés le 30/09/2026** (engineVersion 0.14.0, 569 tests
verts, checksums dorés inchangés) — tous sous drapeaux désactivés par défaut :

1. **Primitives d'actions atomiques** (D7) — `Take`/`Give`/`Trade`/`Attack`/`Defend`
   ajoutés en queue de `DesireKind`, exécution atomique (`Blocked` sans effet partiel).
2. **Inventaire** (D8) — capacité de poids 20, `TryTake`/`TryGive` tout-ou-rien,
   snapshot + persistance additifs.
3. **Means-end reasoning** (D3, 2 temps) — `Attack` au catalogue (jamais généré V0.1) ;
   `PlanLibrary` (candidats `Take`/`Trade`). `Steal` en attente d'arbitrage éthique.
4. **Engagements communicationnels** (D5) — `Commitment` → objectif candidat pondéré
   par la confiance → `TrustLevel ± bonus/penalty` (version minimale).
5. **Politique de reconsidération** (D2) — étape 3bis : saillance (besoin franchi,
   condition critique = force) + filet de sécurité périodique (50 ticks).

(Perception des événements D1 : rejeté. Intentions partagées D4 et Institutionnalisation
D6 : reportés V2 — inchangés.)

### 4.2 Moteur SYNE

| Fonctionnalité | Source | Note |
| :-- | :-- | :-- |
| Rôles de groupe au-delà du leader (attribution d'actions, ressources de groupe) | `docs/docs-syne/SOCIAL_NETWORK.md` §114 | Jalons ultérieurs |
| Interaction groupes ↔ conflits | `SOCIAL_NETWORK.md` §116 | Lié à la résolution chiffrée des conflits (§3.2) |
| Révision des croyances par messages (intégration cognitive complète) ; production réelle de `Request`/`Warning`/`Trading` avec les besoins sociaux | `docs/docs-syne/COMMUNICATION_PROTOCOL.md` §105 | V0.1 consomme la file comme trace d'activité |
| Effet cognitif de la lecture des livres | `docs/docs-syne/ISSUES.md` SYNE-121 | Différé au futur moteur mémoire (hors U8) |
| Mécanique agentique des constructions (coût, matériaux, constructeurs — décision n°20) | `docs/docs-syne/ROADMAP.md` §6 | Les obstacles statiques configurables sont livrés (SYNE-071) |
| Sources spatiales de ressources | `docs/docs-syne/ROADMAP.md` ph7 | Hors V0.1, à rouvrir avec la navigation V2 |

### 4.3 ECHOS / API / UI

| Fonctionnalité | Source | Note |
| :-- | :-- | :-- |
| Endpoint `/api/communication-heatmap` | `docs/docs-echos/API_REST.md` §257 | Non implémenté en V0.1 (périmètre UI) |
| Export **CSV** des traces de décision par l'API | `docs/docs-echos/LOGGING_INSTRUMENTATION.md` §153 | Format documentaire §7 existe, pas l'endpoint |
| Série Parquet « séries lourdes » | `docs/docs-echos/EXPERIMENT_COMPARISON.md` §56 | Optionnel, V0.1+ |
| Versionnage des réponses API aligné sur `VERSIONING.md` | `docs/docs-echos/API_REST.md` §258 | Évolutions additives = MINOR, à formaliser dans les réponses |
| Console de débogage (profilage live) | `docs/docs-echos/ROADMAP.md` ph5 | Hors périmètre ECHOS — spécifiée côté console SYNE, non réalisée |
| Validation UX des personas / parcours utilisateur | `docs/docs-echos/USER_STORIES.md` §78 | Tests utilisateurs à venir en phase 8 |

### 4.4 PRISM / Unreal (composant le plus en retard)

Tout le volet PRISM est ouvert — `docs/docs-prism/ROADMAP.md` (statut DRAFT) pose 6
étapes, aucune n'est validée :

1. Stabiliser les contrats/types Blueprint et l'intégration de PRISM-LDK dans le projet PRISM.
2. Valider avec **SYNE réel** le cycle `Prepare` → `world_initialized` → `Ready` → `Start`, puis snapshots/deltas/événements.
3. Génération de présentation à partir du monde SYNE et mise à jour stable par ID.
4. Inspection, vues de groupe/relations et indicateurs de run.
5. Mesure des coûts (snapshots, entités, rendu) et optimisation dans PRISM.
6. Tests interop, erreurs de transport, compatibilité de contrat, valeurs inconnues.

Points associés : pas de **matrice CI dédiée** au plugin Unreal
(`ARCHITECTURE.md` §176) ; palette de terrains et marqueurs de génération du
`WorldDescription` non encore branchés (`PRISM_UNREAL_IMPLEMENTATION.md` §169/417) ;
aucun ADR supplémentaire requis — les décisions de transport et de rôle sont prises
(ADR-002 PRISM [Accepted]).

### 4.5 Long terme (vision racine, non planifié)

`ROADMAP.md` §5 : mode joueur-habitant (incarner une entité), représentation
constructions/territoires/saisons/météo dans PRISM, intégration ECHOS dans la scène
Unreal. Non datés, volontairement.

---

## 5. Validations à exécuter (code prêt, preuve manquante)

### 5.1 Campagnes exécutées le 30/09/2026 (V1 et V2)

Exécutées depuis une session de code (machine de développement : dotnet 10, Python 3.14,
Node 20) — le prérequis « machine dédiée » ne s'applique pas à ces campagnes. Procédure :
SYNE `--serve` (HTTP :5181 + WS :5180) + worker `echos.dev_ingest` ; runs pilotés via
`prepare` → `ready` → `start` ; fin de campagne = **arrêt de SYNE** (le worker vivant
flush le dernier segment et écrit le rapport du dernier run). Bases : `docs/campaign-runs/`
(hors git).

**V1 — campagne pilote J-C1 : VALIDÉE**

| Critère plan §7 | Résultat |
| :-- | :-- |
| Seeds réels portés | ✓ `run-<seed>-<12hex>`, `runs.seed` renseigné (12345 / 424242) |
| 0 tick perdu, 200/200 les deux côtés | ✓ 1..200 complet sur les 2 runs, aucun FK error |
| 2 rapports de calibration | ✓ écrits à la transition de run (A2) et à la fin de flux |

**V2 — campagne de validation 3 × 1200 ticks : PARTIELLE (itération B1 requise)**

Runs calibrés (profil de référence via `prepare`, Eat/Drink recovery 2.0/1.0) :
seeds 12345 / 424242 / 999 + un second run 12345 pour le déterminisme.

| Critère plan §4-B1 | Résultat |
| :-- | :-- |
| 1. Zéro extinction, population 100/100 | ✓ sur les 3 seeds (chemin calibré) |
| 2. Énergie stable \|pente\| < 0,005/tick (t1001–1200) | ✗ pente ≈ **−0,039/tick** (E finale ≈ 78–80) — mort lente résiduelle |
| 3. Part Eat/Drink ≥ 15 % si faim > 70 | ✗ **1,9–3,2 %** (vs 9,7 % pré-correctif) |
| 4. Empreintes bit-à-bit identiques même seed | ✓ **0 divergence / 169 200 valeurs** (1200 summaries + 120 000 traces décision + 48 000 métriques moteurs) |

**Défauts résiduels découverts pendant les campagnes** :

1. 🔴 **Le chemin `POST /api/control/reset` n'applique pas le profil de référence** :
   `ResetAsync` appelle `StartAsync(seed, configJson: null)` → `PrepareCoreAsync` avec
   `ConfigLoader.LoadDefaults()` (sans Eat/Drink recovery) — alors que `prepare` applique
   `SimulationProfiles.ReferenceJson()`. Effet mesuré : seed 424242 → extinction t300 par
   `reset` vs survie 1200/1200 par `prepare`. Tout run piloté long doit passer par
   `prepare`+`start` en attendant correctif (proposer : même défaut de profil que
   `prepare` dans `PrepareCoreAsync` quand `configJson` est null).
2. 🟠 **Aucun flush au `finished`** : la fin d'un run ne flushera pas le dernier segment ni
   le rapport ; tout atterrit à la fermeture du flux (arrêt SYNE / reconnexion). Cohérent
   avec le §4.3, mais la lecture « rapport écrit à la fin de chaque run » du plan A2 ne
   tient en exécution que pour les runs suivis d'un reset consommé.
3. 🟡 Le worker ECHOS ne détecte pas la mort violente du flux SYNE tant qu'aucune trame
   n'arrive (bloqué en `recv`) — sa reconnexion prend effet à la prochaine émission.

**V3–V6 restent à exécuter** (voir table ci-dessous).

### 5.2 Validations restantes

Ces chantiers ne demandent **pas de décision ni de nouveau code** (ou très peu).
V1 et V2 ont été exécutées le 30/09/2026 (§5.1) ; V2 devra être **rejouée** après le
correctif du chemin `reset` et l'itération B1.

| # | Validation | Critère | Source |
| :-- | :-- | :-- | :-- |
| V2' | **Re-campagne 3 × 1200 ticks** après correctif `reset` + itération B1 | **EXÉCUTÉE le 07/10/2026 en périmètre élargi** (ADR-016) : défauts intégrés recalibrés B1, 50 **et** 100 agents × **2500 ticks** × 3 seeds — énergie stable **✓** (|pente| ≤ 0,0037/tick < 0,005, 0 extinction, 0 mort, population = initiale sur 6/6) ; part Eat/Drink ≥ 15 % **vide, pas remplie** : la faim moyenne ne dépasse jamais 70 avec ces défauts (critère requalifié, voir ADR-016 §Validation (c)). Reste à rejouer V2' sur le chemin `reset` une fois le correctif §5.1-D1 fait | Plan §4-B1/§7 ; `ADR-016` |
| V3 | **Jalons U7/U8 comme jalons transverses** | Cadence contrôlée, backpressure, lag, parcours UI complets ; stabilité long-run, reprise worker | `ROADMAP.md` §6 (U7 « validation produit partielle », U8 « non accepté comme jalon transverse ») |
| V4 | Ingestion **réelle** de deux runs SYNE en CI (preuve J2/J3 ECHOS) | Les 2 tests skippés (binaire SYNE Release / serveur syne-mock) passent en continu | `docs/docs-echos/TESTING.md` §315 ; suite ECHOS : 2 skipped |
| V5 | Recalibrage complet des benchmarks | Refaits après implémentation, aux jalons ph10 (T4) et avant validation v0.1 | `docs/docs-syne/PERFORMANCE.md` §118-124 |
| V6 | Validation du plugin PRISM contre SYNE réel | Étapes 2 et 6 de la roadmap PRISM ; build CI dans la version d'Unreal ciblée | `docs/docs-prism/ROADMAP.md`, `ARCHITECTURE.md` §176 |

---

## 6. Questions de fond (permanentes, hors cycle de livraison)

`docs/ETHICS_AND_SCOPE.md` §6 — conservées ouvertes volontairement :

- **Scientifiques** : fiabilité d'un « degré d'émergence » ; suffisance des primitives
  pour des phénomènes complexes ; production de **culture** (normes, rituels, symboles) ;
  coopération stable sans mécanisme central ; distinguer émergence vs complexité programmée.
- **Techniques** : au-delà de 1000 entités (10 000+ par décomposition/LOD) ;
  parallélisme distribué déterministe ; BDI + LLM sans perte de déterminisme ;
  mondes multiples parallèles.
- **Philosophiques** : frontière simulation/modèle ; une entité simulée peut-elle avoir
  une « fin » éthique ; la distinction vérité/croyances est-elle une bonne abstraction.

---

## 7. Lecture prioritaire suggérée (mise à jour 30/09/2026)

1. **Corriger le défaut du chemin `reset`** (défaut 1 du §5.1) puis **itérer B1**
   (`EnergyRecovery` comme levier isolé, cf. plan §4-B1) et relancer V2 — les critères
   2 et 3 encadrent l'itération ; les données de la campagne du 30/09 servent de référence.
2. **Lancer l'implémentation des ADR acceptés** dans l'ordre D7+D8 → D3 (temps 1 puis 2)
   → D5 → D2 — plus court chemin vers l'économie (`Trade`/`Buy`) et un arbitrage
   utilitaire plus riche (l'itération B1 en profitera pour s'exercer sur le catalogue refondu).
3. **Rejouer V2 après itération B1** pour clore les critères chiffrés §3.4 ; la preuve
   de déterminisme bit-à-bit (0 divergence / 169 200 valeurs) rend l'itération sûre.
4. **Lancer le chantier PRISM** (étapes 1–2) — le contrat 0.2.1 a désormais été consommé
   en conditions réelles par V1/V2 ; PRISM reste le seul composant sans validation contre
   SYNE réel.
5. ~~Réaligner les documents en retard~~ **fait le 30/09/2026** : `SYSTEMS_SPEC.md`
   (saisons livrées/désactivées), `API_CONTRACTS.md` (sections « à figer » closes de fait),
   `TESTING.md` (état réel 556 tests).

---

## Points restés ouverts dans ce rapport

- Ce rapport est un instantané au 30/09/2026 — **actualisé en session le même jour**
  (campagnes §5.1, arbitrages §3.1, réalignements documentaires §7.5). Prochaine
  révision conseillée : après le correctif du chemin `reset` et l'itération B1, ou à
  chaque ADR implémenté.
- Le périmètre « écrans web » (§3.3) étant `[OUVERT]` par décision, la liste des
  fonctionnalités UI de §4.3 ne prétend pas être exhaustive.
- Les bases de campagnes (`docs/campaign-runs/`) sont hors git ; les résultats
  consolidés de §5.1 font foi en cas de purge locale.
