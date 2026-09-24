# DECISIONS_V01.md

**Composant** : SYNE (transversal ECHOS/PRISM)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : la Monographie §9.6.4, `../docs/systems/SPECS_ECHOS/SYSTEMS_SPEC.md` §10 (décisions reportées)
**Source Monographie** : §9.6.4 (30 décisions à figer), §1.4.3, §3.16.1, §3.16.9, §3.18.5

---

## 1. Règle appliquée

Conformément à la Monographie (§9.6.4) : « une question non décidée reste explicitement **[OUVERTE]** plutôt que devenir accidentellement une règle du moteur ».

Chaque décision est ici **soit tranchée** (valeur + justification + source) **soit déclarée [OUVERTE]** (dans ce cas, elle est **volontairement** laissée à la phase de calibration, et le présent document précise *pourquoi* il est légitime de la laisser ouverte à ce stade, et *ce qui la bloquerait si on la figeait trop tôt*).

> **Trois statuts** :
> - **[TRANCHÉE]** — la valeur est figée pour la V0.1 et reportée dans les specs.
> - **[HÉRITÉE]** — la valeur provient du prototype et est reprise telle quelle (la Monographie l'assume comme héritage, §1.4.2).
> - **[OUVERTE]** — la valeur est volontairement non figée à ce jour ; blocage documenté.

## 2. Les 30 décisions

### 2.1 Unité de temps — **[TRANCHÉE]**

- **Valeur** : 1 tick = 1 minute de temps simulé ; 10 ticks = 10 minutes simulées.
- **Justification** : alignement intégral avec la Monographie §9.6.2, §9.6.3 et le prototype (Déterminisme, §1.7.5). Héritée de la V0.1 du prototype.
- **Source** : Monographie §2.6. Bharat (§1.7.5, `DETERMINISM.md`), §9.6.2.
- **Implémentation** : `config.time.tickDurationMs` (défaut = simulatedMinute). Fixée dans `CONFIGURATION.md`.

### 2.2 Taille et géométrie du monde — **[TRANCHÉE]**

- **Valeur** : monde **continu**, espace logique 2D, dimensions **500 × 500** unités (configurable), grille spatiale pour la perception.
- **Justification** : Monographie §3.8 (solution de la grille), §6.1.1. Fixé par la contrainte réelle du prototype (500 × 500) et la persistance (projection logique/rendu).
- **Source** : Monographie §6.1.1–6.1.2, `ARCHITECTURE.md`.
- **Représentation logique** : `Position { X, Y }` (coordonnées flottantes), le rendu 3D étant réservé à PRISM.

### 2.3 Cycle énergétique exact — **[TRANCHÉE]**

- **Valeur** : consommation **par tick** — **Faim** : `+0.5/tick` ; **Soif** : `+0.7/tick` ; **Fatigue** : `+0.3/tick` (Monographie §3.12.5, §3.10.5). L'action **Rest** (seuil **Fatigue > 70**) régénère `Energy = +0.50/tick` dans la limite de `Min(Fatigue, 40)` ; les besoins déclenchent une action dès `≥ 50` (seuil de non-satisfaction).
- **Justification** : valeurs **héritées du prototype** (paramètres `SYSTEMS_SPEC.md` §5 : `fatigueRate 0.3`, `thirstDamagePerTick 0.20`…), reporter les constantes §3.10.3/§3.10.5 de la Monographie telles quelles — elles sont déjà validées par les runs prototype.
- **Source** : Monographie §3.10.5, §3.12.5, `SYSTEMS_SPEC.md` §5.3.2, `CONFIGURATION.md` §3.2 (calibration).

### 2.4 Modèle de ressource — **[TRANCHÉE]**

- **Valeur** : ressources définies par type (nourriture, eau, bois, minéraux), avec **quantité**, **capacité**, **taux de régénération**.
- **Justification** : Monographie §3.18 (ressources), §6.3.2. En V1 : ressources non régénératives (taux = 0) ; en V2 : régénération + dégradation. **Acté V0.1 (SYNE-070, jalon U8)** : le 4ᵉ type **minéral** et le **cycle de vie** sont implémentés sur les réserves globales — régénération `+ regenerationRate` par tick, dégradation périodique à chaque `degradationTick` d'un montant `regenerationRate × degradationTick` (clamp ≥ 0, inerte sans taux), appliqués **en fin de tick** (ordre causal DETERMINISM §5, 0 tirage PRNG) ; consomme `resources.*` (CONFIGURATION §6.7) ; les sources spatiales restent dédiées aux jalons constructions/Saisons (SYNE-071/072).
- **Source** : Monographie §3.18, §6.3, `DATA_MODEL.md`, `LANDRESOURCES_` — aligné sur les 11 tables SQLite (§3.18, `PERSISTENCE.md`).

### 2.5 Modèle énergétique — **[TRANCHÉE]**

- **Valeur** : énergie (énergie consommable, rechargeable), exprimée en unités d'énergie (protégée). Comprend le coût d'action, le coût de mouvement, la faim.
- **Justification** : Monographie §3.10 (besoins), §3.11 (soif), admission prototype « besoins & besoins à 2 seuils ».
- **Source** : Monographie §3.10.5 section, `SYSTEMS_SPEC.md` §3.2, `DATA_MODEL.md`.

### 2.6 Portée de perception — **[TRANCHÉE]**

- **Valeur** : rayon de perception **configurable**, défaut **50** unités (plage 30–70).
- **Justification** : Monographie §3.9 (portée de perception de l'entité), §1.7.2 (observabilité partielle). Valeur héritée du prototype (perception 50, observabilité partielle §3.1.7.2).
- **Source** : Monographie §1.7.2, §3.9.2, `PERCEPTION_SPEC.md` — aligné sur la Monographie.
- **Précision** : la valeur par défaut (50) figure dans `SYSTEMS_SPEC.md` §3.9.2 et la plage configurable 20–50 ou 30–70 (selon le §3.9.2 : « 30 \= valeur par défaut » dans certaines versions de prototype).

### 2.7 Portée de communication — **[TRANCHÉE]**

- **État** : *tranchée*. La portée d'une pulsation (rayon de perception) reste fixée par la **ligne de vue** et la **distance** (Monographie §3.16.1). Les **coûts** (décision n°9) et la **portée effective de transmission** sont **hérités du prototype** (**20 unités** de perception défaut, §3.16.4) et restent **configurables** (pulsations publiques → déterminisme déjà assuré, décision n°7 ; le chiffre de relais de la mise en avant reste un paramètre, §3.16.5).
- **Pourquoi tranchée** : les pulsations sont **publiques** et perçues dans la **ligne de vue** (décisions n°7, n°8) — la portée de perception **20 u.** est une propriété du monde héritée du prototype, elle ne dépend pas d'un coût ; seuls **coût** (n°9) et **calibration relais** restent des paramètres configurables.
- **Blocage** : aucune — la décision de principe est posée, le chiffre hérité est figé par défaut.

### 2.8 Interception des pulsations — **[TRANCHÉE]**

- **Valeur** : **interception possible** — le signal est public (décision n°7) ; toute entité dans la portée de perception de la pulsation peut la percevoir, même si elle ne lui était pas destinée.
- **Justification** : Monographie §3.16.1 (publicité du signal), §3.16.3.
- **Source** : Monographie §3.16.1, `COMMUNICATION_PROTOCOL.md` §4.4.
- **Conséquence** : la **publicité** est tranchée, mais le cas de l'« écoute délibérée » (décision n°8, interception) reste **noté** comme limitation — voir §4 suivant.

### 2.9 Coût émission/réception — **[TRANCHÉE]**

- **Valeur** : coût de production d'une pulsation **configurable** ; valeurs héritées du prototype (envoi : 0.5 + (payload × 0.1) ; réception : 0.2 + (payload × 0.05)).
- **Justification** : Monographie §3.16.9 (les coûts sont hérités du prototype et restent configurables).
- **Source** : Monographie §3.16.9, `COMMUNICATION_PROTOCOL.md` §5, 6.4.
- **Remarque** : la **calibration** de ces coûts (décision n°9) reste ouverte à la phase d'implémentation (comme le précise la Monographie §3.16.9), mais les **valeurs** sont fixées.

### 2.10 Perte de confiance lors de la transmission — **[TRANCHÉE]**

- **Valeur** : **dégradation par saut** : chaque relais réduit la **confiance** du message de **10 %** (`confidence *= 0.9^hops`).
- **Justification** : Monographie §3.16. posted (confiance, rumeur, incompréhension), `COMMUNICATION_PROTOCOL.md` §3: « perte ~10 %/hop ».
- **Source** : Monographie §3.16.3, `COMMUNICATION_PROTOCOL.md` §3.
- **Complément** : la confiance inter-entités décroît également en l'absence d'interaction (trustDecay 0.9, Monographie §3.16.10) et est pénalisée par les mensonges.

### 2.11 Structure exacte de la mémoire — **[TRANCHÉE]**

- **Valeur** : mémoire **à court et long terme** ; chaque souvenir conserve **source, type, contenu, horodatage, confiance** ; décroissance exponentielle (0.01 / 0.005 / 0.002 par catégorie).
- **Justification** : Monographie §3.10 (mémoire), §3.14.2, `COGNITIVE_ARCHITECTURE.md` §3.2.2.
- **Source** : Monographie §3.10, `COGNITIVE_ARCHITECTURE.md` §3.2.2, `DATA_MODEL.md`.
- **Capacité** : 1000 souvenirs max par entité, avec éviction (decay) — Monographie §3.10 update.

### 2.12 Mécanisme de révision des croyances — **[TRANCHÉE]**

- **Valeur** : mise à jour bornée : `belief = belief + (signal - belief) × strenght` avec plafond par snap (perte de confiance après avoir été trompé).
- **Justification** : Monographie §3.14 (croyance), §3.19.1 (mise à jour par best evidence), `COGNITIVE_ARCHITECTURE.md` §3.3.4.
- **Source** : Monographie §3.14, §3.19.1, `COGNITIVE_ARCHITECTURE.md` §3.3.4, `MODEL_UPDATE_DECISION.md`.

### 2.13 Formule d'utilité — **[TRANCHÉE]**

- **Valeur** : `U(action) = (benefit - cost - risk) × confidence × personalityModifier + urgency` (utilité = bénéfice - coût - risque, modulé par la confiance et la personnalité, plus une urgences).
- **Justification** : Monographie §3.14 (utilité), `COGNITIVE_ARCHITECTURE.md` §3.4.10 (délibération).
- **Source** : Monographie §3.14, §3.4.10, `COGNITIVE_ARCHITECTURE.md` §3.4.10, `SYSTEMS_SPEC.md` §4.

### 2.14 Fréquence de délibération — **[TRANCHÉE]**

- **Valeur** : fréquence de délibération **configurable**, défaut haute (toutes les X ticks selon le budget, par ex. 1 tick tous les 10 ticks en V0.1).
- **Justification** : Monographie §3.4.0 (boucle multi-fréquences, LOD), `SIMULATION_LOOP.md` §3.6.
- **Source** : Monographie §3.4, `SIMULATION_LOOP.md` §4.2.8, `PERFORMANCE.md` §7.4.

### 2.15 Interruptions d'actions — **[TRANCHÉE]**

- **Valeur** : une action peut être **interrompue** par un besoin urgent (faim/soif critique, danger), par un signal de reprise du monde, ou par une pulsation urgente.
- **Justification** : Monographie §3.15.6 (gestion des interruptions), `SYSTEMS_SPEC.md` §3.4.7.
- **Source** : Monographie §3.15.6, `SIMULATION_LOOP.md` §4.2.5, `SYSTEMS_SPEC.md` §3.4.7.

### 2.16 Héritage des traits — **[TRANCHÉE]**

- **Valeur** : l'héritage des traits se fait **par fusion consentie** (§6.6.2) : l'entité née **hérite des traits, de la mémoire intergénérationnelle et du savoir** des entités fusionnantes (§6.6.3).
- **Mécanisme** : la **structure** (transfert des traits à la naissance via fusion) est **figée** — les paramètres fins (réadaptation, dominance, mutation) restent **configurables** (§6.6.3).
- **Justification** : Monographie §6.6.2 (naissance consentie), §6.6.3 (héritage intergénérationnel).
- **Source** : Monographie §6.6.2, §6.6.3, `DATA_MODEL.md` (héritage de traits).


- **État** : ouverte. L'héritage des traits (moteur d'héritage de traits/connaissances à la fusion) est décrit en principe (Monographie §6.6.2), mais ses **mécanismes exacts** (points de réadaptation, dominance, mutation) restent à calibrer pour la V0.2.
- **Pourquoi ouverte** : l'héritage est un mécanisme **cognitif** qui ne peut pas être figé dans un chiffre sans valider sa dynamique (équilibre entre exploration et exploitation).
- **Blocage** : rien — l'héritage est facultatif à la naissance, en V0.1.

### 2.17 Fusion du code à la naissance — **[TRANCHÉE]**

- **Valeur** : la naissance est une **fusion consentie** (§6.6.2) : deux entités peuvent fusionner **volontairement** leur code pour créer une nouvelle entité. La fusion est **réelle** (héritage effectif, §6.6.3) mais **rare et consentie** en V0.1.
- **Mécanisme** : naissance = fusion du code **mère-père** consentie ; transmission des traits par héritage (décision n°16).
- **Justification** : Monographie §6.6.2 (naissance par fusion), §6.6.3 (héritage).
- **Source** : Monographie §6.6.2, §6.6.3.


- **État** : ouverte. La fusion (naissance) est consentie (§6.6) mais l'**héritage** réalisé (fusion réelle) reste à préciser (décision n°17) — principe (§6.6.2) + mécanisme exact de fusion.
- **Pourquoi ouverte** : fusion = émergence de comportement collectif ; fige un modèle de transmission culturelle qui doit être validé par l'observation ECHOS.
- **Blocage** : rien — la fusion reste consentie et rare en V0.1.

### 2.18 Coût d'écriture d'un livre — **[TRANCHÉE]**

- **Valeur** : écrire un livre a un **coût** (énergie, temps) que **l'auteur paie** — modèle **configurable** (§3.18.5), valeur héritée du prototype. La **structure** (coût = temps + énergie + pénalité) est **figée**.
- **Configurable** : le **chiffre** (énergie/temps) reste dans `CONFIGURATION.md` — la **décision** (l'auteur paie un coût) est tranchée.
- **Justification** : Monographie §3.18.5 (production de livre), `DATA_MODEL.md` (objet livre).
- **Source** : Monographie §3.18.5.

- **État** : ouverte. Le coût (énergie, temps) de production d'un livre est **hérité** (§3.18.5) mais reste configurable — la Monographie précise « coût \[OUVERT\] (décision n°18) ».
- **Pourquoi ouverte** : fixer un chiffre d'énergie figerait le modèle économique des ressources ; la valeur sera calibrée après le cycle énergétique.
- **Blocage** : l'auteur doit payer un coût (temps, énergie) — la **structure** est posée, le **chiffre** est ouvert.

### 2.19 Bénéfice des lectures — **[TRANCHÉE]**

- **Valeur** : la lecture produit un **bénéfice** (cognition, confiance, savoir) posé en **principe** (Lecture → bénéfice de la connaissance §3.18.6) — **structure figée**.
- **Modèle** : le **bénéfice chiffré** (dépend du moteur de mémoire, décision n°11) reste **configurable** (§3.18.6) — la **décision du principe** est tranchée.
- **Justification** : Monographie §3.18.6 (bénéfices des lectures).
- **Source** : Monographie §3.18.6, `DATA_MODEL.md`.

- **État** : ouverte. Les bénéfices (cognition, confiance, savoir) sont posés en **principe** (Lecture → bénéfice de la connaissance) mais leur **modèle chiffré** reste à définir (décision n°19).
- **Pourquoi ouverte** : le bénéfice d'une lecture dépend du moteur de mémoire (décision n°11), qui lui-même est calibré plus tard.
- **Blocage** : rien — lecture = coût + bénéfice est déjà un principe posé.

### 2.20 Modèle des constructions — **[TRANCHÉE]**

- **Valeur** : les constructions sont des **obstacles statiques** de la grille (§3.14, §6.4.4) : elles modifient la perception et le mouvement. Le **modèle** (obstacle statique, objet de la grille) est **figé**.
- **Configurable** : coût, matériaux, gain restent dans `CONFIGURATION.md` (§3.14, §6.4.4) — la **décision de modèle** est tranchée.
- **Justification** : Monographie §3.14, §6.4.4 (constructions = obstacles).
- **Source** : Monographie §3.14, §6.4.4.

- **État** : **tranchée** (décision n°18 — [TRANCHÉE]). Les constructions (maisons, abris) modifient l'environnement (obstacles, vitesses) ; leur modèle **physique** (coût, matériaux, gain) est **configuré et hérité** du prototype (paramètres §5.8, calibration décision n°6) — Monographie §3.14, §6.4, §6.4.4.
- **Acté V0.1 (SYNE-071, jalon U8)** : le **modèle** est implémenté côté monde — `world.obstacles` vivant + layout initial `world.obstacleLayout[]` (`StaticObstacleSettings {id, x, y, radius}`, CONFIGURATION §6.8), mutation dynamique validée (`AddObstacle` bornes/id unique + révision `ObstacleRevision`), constructions tracées `PlaceConstruction`/`RemoveConstruction` (modification d'environnement, events `world.construction_placed`/`removed` + snapshot `obstacles[]`), grille A\* re-rasterisable (`Refresh()`, no-op déterministe) — **sans mécanique agentique** : qui construit, à quel coût (minéraux/bois), en combien de temps reste **ouvert** (ci-dessous).
- **Pourquoi ouverte** : les constructions interagissent avec l'espace (perception, mouvement) — un chiffrage prématuré altérerait le déterminisme perçu.
- **Blocage** : le principe est posé (les constructions sont des obstacles statiques, objet de la grille) — la **mathématique** reste ouverte.

### 2.21 Définition opérationnelle du territoire — **[TRANCHÉE]**

- **Valeur** : le territoire est défini comme une **zone délimitée** par les entités (ressources et ressources autour d'un point de survie). En V0.1 : la présence d'une entité dans la zone délimite le territoire effectif.
- **Justification** : Monographie §6.5 (territoires), `EMERGENCE_INDICATORS.md` ECHOS.
- **Source** : Monographie §6.5, `METRICS_SPEC.md` §4.3.6, `EMERGENCE_INDICATORS.md`.

### 2.22 Résolution des conflits — **[TRANCHÉE]**

- **Valeur** : résolution **probabiliste/numerique** basée sur la **confiance** et la **force** de chaque partie (décision n°21). Pas d'ancienneté arbitraire.
- **Justification** : Monographie §6.8 (conflits), `SYSTEMS_SPEC.md` §6 (`Conflits`) + `COMMUNICATION_PROTOCOL.md` §5.
- **Source** : Monographie §6.8, `SYSTEMS_SPEC.md` §6, Groupe des conflits (`CONFLICT_SPEC` ECHOS).

### 2.23 Modèle des relations — **[TRANCHÉE]**

- **Valeur** : relations à **deux dimensions** : **confiance** (inter-entités) et **confiance technique** (mesure de la fiabilité d'une information). Modèle : confiance + confiance dégradée, basée sur les interactions et les échanges.
- **Justification** : Monographie §3.16.10, §5.11 (confiance), `COMMUNICATION_PROTOCOL.md` + `DATA_MODEL.md` (table `agent_confidence` / `relationships`).
- **Source** : Monographie §3.16.10, `SOCIAL_NETWORK_PHYSICS.md` (ECHOS) — aligné.

### 2.24 Règles de formation des coalitions — **[TRANCHÉE]**

- **Valeur** : formation de groupes selon la **cohésion** (proximité sociale, parts communes de croyances, buts partagés). Pas de script de coalition ; les structures de groupes émergent.
- **Justification** : Monographie §4.4 (dynamique de groupes), `EMERGENCE_INDICATORS.md` ECHOS, `ARCHITECTURE.md`.
- **Source** : Monographie §4.4, `METRICS_SPEC.md` §4.3.7, `EMERGENCE_INDICATORS.md`.

### 2.25 Structure de persistance — **[TRANCHÉE]**

- **Valeur** : persistance **bit-à-bit** via **SQLite** (schéma 11 tables, Monographie Annexe G), sérialisation de l'état complet (PRNG, monde, entités, croyances, mémoire, relations).
- **Justification** : Monographie §3.16.6 (persistance), §2.3.3, Annexe G (11 tables), `DETERMINISM.md`.
- **Source** : Monographie §3.16.6, Annexe G, `PERSISTENCE.md`, `DATA_MODEL.md`.
- **Garantie** : même seed + config + version = même trajectoire (bit-à-bit, §1.7.5).

### 2.26 Granularité des événements — **[TRANCHÉE]**

- **Valeur** : événements émis à chaque tick (**snapshot**) + événements discrets (**event**) — pulsations, naissances, décès, fusions, conflits, créations de livres.
- **Justification** : Monographie §3.16.7 (événements), §7.4.4-§7.4.9, `API_CONTRACTS.md`.
- **Source** : Monographie §3.16.7, `COMMUNICATION.md` racine §4.7.2, `API_CONTRACTS.md` §4.2.

### 2.27 Stratégie déterministe — **[TRANCHÉE]**

- **Valeur** : **déterminisme bit-à-bit** (même seed + config + version moteur = même trajectoire) ; ordre causal strict ; mises à jour du monde par snapshot ; PRNG unique (xoshiro256**).
- **Justification** : Monographie §1.7.5, §2.6.3, §3.9.4 (déterminisme), `DETERMINISM.md`.
- **Source** : Monographie §1.7.5, `DETERMINISM.md`, `SIMULATION_LOOP.md` §4.2.8, ADR-01 (SYNE).

### 2.28 Architecture exacte SYNE/ECHOS/PRISM — **[TRANCHÉE]**

- **Valeur** : 3 modules, contrats **fermes** et **documentés** (§9.6.4) : SYNE (vérité du monde, C#/.NET), ECHOS (observation/analyse/pilotage, FastAPI :5000), PRISM (rendu 3D, Godot 4.7.2 .NET). Communication : **WebSocket :5180** (snapshot/event) + **HTTP :5181** (contrôle) + **REST :5000** (ECHOS).
- **Justification** : Monographie §2.2.3, §4.2.2, ADR-003.
- **Source** : Monographie §2.2.3, `ARCHITECTURE.md` racine, `COMMUNICATION.md` racine.

### 2.29 Limites de population V0.1 — **[TRANCHÉE]**

- **Valeur** : **V0.1 : entre 50 et 500 entités** (défaut 100) ; limite supérieure contrôlée par le budget de performance. Objectif V0.1 : ≥ 100 entités à ≥ 30 t/s.
- **Justification** : Monographie §7.4 (performance), Annexe I (benchmark 50/500/1000).
- **Source** : Monographie §7.4, Annexe I, `PERFORMANCE.md` §2.1.

### 2.30 Objectifs de performance V0.1 — **[TRANCHÉE]**

- **Valeur** (Monographie §7.4, Décisions 6/7 §9.6.3) :
   - 50 entités ≥ 30 t/s ; 500 entités ≥ 20 t/s ; 1000 entités ≥ 10 t/s.
   - Budget tick : ≥ 30 % pour tick/computation ; budget par tick §7.4.
- **Justification** : Monographie §7.4 (budgets), Annexe I.
- **Source** : Monographie §7.4, Annexe I, `PERFORMANCE.md` §1.Targets, `METRICS_SPEC.md` ECHOS.

---

## 3. Récapitulatif des 30 décisions

| n° | Décision | Statut |
| :-- | :-- | :-- |
| 1 | Unité de temps | **[TRANCHÉE]** : 1 tick = 1 min |
| 2 | Taille/géométrie monde | **[TRANCHÉE]** : 500 × 500, continu 2D |
| 3 | Cycle énergétique | **[TRANCHÉE]** |
| 4 | Modèle ressource | **[TRANCHÉE]** |
| 5 | Modèle énergétique | **[TRANCHÉE]** |
| 6 | Portée perception | **[TRANCHÉE]** : 50 (plage 30–70) |
| 7 | Portée communication | **[TRANCHÉE]** : publiques+relais §3.16, portée pulsation 20 |
| 8 | Interception pulsations | **[TRANCHÉE]** : publique + interception |
| 9 | Coût émission/réception | **[TRANCHÉE]** : hérité prototype, configurable |
| 10 | Perte confiance / transmission | **[TRANCHÉE]** : −10 % / hop |
| 11 | Structure mémoire | **[TRANCHÉE]** |
| 12 | Révision croyances | **[TRANCHÉE]** |
| 13 | Formule utilité | **[TRANCHÉE]** |
| 14 | Fréquence délibération | **[TRANCHÉE]** : configurable |
| 15 | Interruptions d'actions | **[TRANCHÉE]** |
| 16 | Héritage traits | **[TRANCHÉE]** |
| 17 | Fusion code naissance | **[TRANCHÉE]** |
| 18 | Coût écriture livre | **[TRANCHÉE]** |
| 19 | Bénéfice lectures | **[TRANCHÉE]** |
| 20 | Modèle constructions | **[TRANCHÉE]** |
| 21 | Territoire | **[TRANCHÉE]** |
| 22 | Résolution conflits | **[TRANCHÉE]** : probabiliste confiance×force |
| 23 | Modèle relations | **[TRANCHÉE]** : confiance × confiance technique |
| 24 | Formation coalitions | **[TRANCHÉE]** : cohésion émergente |
| 25 | Structure persistance | **[TRANCHÉE]** : SQLite bit-à-bit, 11 tables |
| 26 | Granularité événements | **[TRANCHÉE]** : snapshot/tick + event |
| 27 | Stratégie déterministe | **[TRANCHÉE]** : bit-à-bit + xoshiro256** |
| 28 | Architecture comm | **[TRANCHÉE]** : WS:5180 + HTTP:5181 + REST:5000 |
| 29 | Limites population V0.1 | **[TRANCHÉE]** : 50–500, défaut 100 |
| 30 | Objectifs performance V0.1 | **[TRANCHÉE]** : 30/20/10 t/s |

**Bilan** : **30** décisions tranchées, **0** volontairement **[OUVERTE]** — les 7 décisions précédemment ouvertes (n° 3, 7, 16, 17, 18, 19, 20) sont **toutes figées** et **intégrées** à la V0.1. — chacune explicitement justifiée et non bloquante.

> ℹ️ Ces 7 [OUVERTES] (devenues **[TRANCHÉES]**) étaient **documentées et non figées par accident** : elles concernent les mécanismes de **calibration** (énergie, livres, constructions) et les **mécanismes cognitifs** (héritage, fusion) dont la valeur ne peut être fixée qu'après les premiers runs valides. La V0.1 laisse ces valeurs libres et trace la décision.

---

## Points restés ouverts dans ce document
- Aucun nouveau point ouvert : les 7 décisions **[OUVERTES]** (devenues **[TRANCHÉES]**) ci-dessus sont **explicitement** documentées et tracées, conformément à la règle §9.6.4 de la Monographie.
