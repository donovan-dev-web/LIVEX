# LIVEX — rapport de performance et de tests (octobre 2026)

**Composant** : LIVEX (général) · **Statut** : [EN VALIDATION] · **Date des mesures** : 19 octobre 2026
**Périmètre** : comptage des tests, benchmark par composant (SYNE, ECHOS, `syne-mock`), benchmark par scénario (SYNE+ECHOS, LAUNCHER+SYNE, LAUNCHER+SYNE+ECHOS), capacité maximale observée, coût à la cadence PRISM et recommandations d'exécution.

> Ce rapport est un **relevé de mesures locales**. Il ne certifie ni une conclusion scientifique, ni une compatibilité inter-composants générale, ni une release. Les chiffres sont ceux d'une machine de bureau unique, décrits en §1 ; ils ne sont pas des engagements de performance.

## Sommaire

1. [Environnement et méthode](#1-environnement-et-méthode)
2. [Comptage des tests](#2-comptage-des-tests)
3. [Benchmark par composant](#3-benchmark-par-composant)
4. [Benchmark par scénario](#4-benchmark-par-scénario)
5. [Capacité maximale observée](#5-capacité-maximale-observée)
6. [Coût à la cadence PRISM](#6-coût-à-la-cadence-prism)
7. [Recommandations](#7-recommandations)
8. [Limites](#8-limites)

## 1. Environnement et méthode

### Machine de mesure

| Élément | Valeur |
| :-- | :-- |
| CPU | Intel Core i7-8750H (6 cœurs / 12 fils, 2,20 GHz de base) |
| RAM | 15,8 Go |
| OS | Windows 11 (10.0.26200) |
| .NET | 10.0.402 |
| Node.js | v25.9.0 |
| Python | 3.12.10 (`echos/.venv`) |
| Plan d'alimentation | « Utilisation normale » (pas de mode performance) |

Les mesures .NET utilisent le binaire **publié** `syne/Simulation.Console/bin/Release/net10.0/win-x64/publish/Simulation.Console.exe`.

### Outil de mesure

Le harnais est [`scripts/benchmark-windows.py`](../../scripts/benchmark-windows.py) : il complète [`scripts/benchmark.py`](../../scripts/benchmark.py) (Unix-only, qui repose sur `os.wait4`/`os.killpg`, absents sous Windows). Le pic mémoire est le **pic de working set** (`psutil.memory_info().peak_wset`, équivalent Windows de `ru_maxrss`), sommé sur l'arbre de processus suivi ; le débit est un temps mural (temps de calcul SYNE compris, pacing compris).

**Séparer calcul et cadence.** Un run `--headless` sans `ticksPerSecond` forcé mesure la **capacité de calcul** de SYNE. Un run avec `ticksPerSecond` bas mesure la **cadence tenue**. Le rapport distingue toujours les deux.

### Reproductibilité

```powershell
# Composants (SYNE 300 ticks jusqu'à 1000 entités, ECHOS, syne-mock)
echos\.venv\Scripts\python.exe scripts\benchmark-windows.py --only syne --only echos --only mock --timeout 600

# Scénarios de pile complète (SYNE --serve + API ECHOS + ingestion WebSocket)
echos\.venv\Scripts\python.exe scripts\benchmark-windows.py --only syne+echos

# Branche LAUNCHER sur composants réels
cd launcher
$env:LIVEX_SYNE_PUBLISHED_ROOT="D:\LIVEX\syne"
dotnet test Launcher.Tests.EndToEnd -c Release
```

## 2. Comptage des tests

### Nombre de cas et résultat

Le relevé du 19/10/2026 compte **1 372 cas de test distincts** dans les suites exécutées : **1 356 réussis, 16 ignorés, 0 échec**.

| Composant | Type de suite | Cas | Résultat consigné le 19/10 |
| :-- | :-- | --: | :-- |
| SYNE | Unitaire (Core) | 604 | 604 réussis, 0 échec |
| SYNE | Intégration (Console) | 91 | 91 réussis, 0 échec |
| ECHOS | pytest (API, stockage, analyse, ingestion) | 397 | 383 réussis, 14 ignorés, 0 échec |
| `syne-mock` | Node (unitaire / serveur / contrat) | 53 | 51 réussis, 2 ignorés, 0 échec |
| Launcher | Unitaire | 174 | 174 réussis, 0 échec |
| Launcher | Intégration | 20 | 20 réussis, 0 échec |
| Launcher | End-to-end réel (SYNE publié) | 33 | 33 réussis, 0 échec |
| PRISM / PRISM-LDK | Automation Specs Unreal | (69 documentés) | non exécuté ici — hors total |

> ECHOS passe de 382 à **383** tests verts : un test de régression a été ajouté avec le correctif d'ingestion décrit en §8. La couverture est de **91,32 %** (seuil configuré : 80 %).

### Ventilation par niveau

| Niveau | Suites incluses | Cas | Statut |
| :-- | :-- | --: | :-- |
| Unitaire (suite dédiée) | SYNE Core + Launcher Unit | **778** (604 + 174) | Fiable (projets dédiés). |
| Intégration (suite dédiée) | SYNE Console + Launcher Integration | **111** (91 + 20) | Fiable (projets dédiés). |
| End-to-end | Launcher E2E réel | **33** | 33/33 avec SYNE publié. |
| Types mêlés dans leur runner | ECHOS + `syne-mock` | **450** (397 + 53) | pytest et Node.js mêlent unitaire/API/stockage/intégration ; pas de ventilation honnête par cas. 16 skips inclus. |
| Unreal Automation | PRISM / PRISM-LDK | 69 documentés | Non relancé ; exclu du total. |

**Total exécuté : 1 372 cas = 1 356 réussis + 16 ignorés + 0 échec.** Les 69 specs Unreal sont exclues : elles n'ont pas été relancées ici.

### Changement notable depuis le relevé du 18/10

Les deux tests J3 (LAUNCHER sur SYNE publié + ECHOS réel) passent désormais : **Launcher EndToEnd est à 33/33** (31/33 le 18/10). Le signal alors manquant (« analyse individuelle collectée ») provenait du bug d'ingestion ECHOS corrigé en §8, qui faisait échouer la persistance d'un `tick_bundle` à utilité nulle.

## 3. Benchmark par composant

### 3.1 SYNE (moteur seul, `--headless`)

Run de **300 ticks**, deux graines ; débit = capacité de calcul (aucun pacing).

| Entités | Débit graine 12345 | Débit graine 42 | Pic RSS | Temps/tick |
| --: | --: | --: | --: | --: |
| 50 | 326,8 t/s | 208,0 t/s | 52–55 Mo | ~3–5 ms |
| 100 | 129,4 t/s | — | 74 Mo | ~7,7 ms |
| 500 | 8,1 t/s | 10,0 t/s | 270 Mo | ~100–123 ms |
| 1000 | 2,5 t/s | 2,9 t/s | 655–670 Mo | ~345–400 ms |

**Mesures complémentaires (horizon)** : 100 agents sur **1000 ticks** → **67,4 t/s** (RSS 98 Mo) ; 200 agents sur 1000 ticks → **20,8 t/s** (RSS 172 Mo).

**Le débit dépend fortement de l'horizon, pas seulement de la population.** À 500 entités, le même moteur passe de **45,6–46,5 t/s sur 100 ticks** à **8,1–10,0 t/s sur 300 ticks** : la population reste stable (500), mais la mémoire/croyances s'accumulent — la somme des `memoryCount` monte de ~2 900 (tick 100) à ~494 000 (tick 300), ce qui alourdit chaque tick suivant. La mesure « à horizont court » ne doit donc pas être présentée comme le débit du moteur sur un run long.

Point de comparaison documentaire (`docs/syne/PERFORMANCE.md` §9.2, V5, Ubuntu 08/10) : 50 → 1354–2257 t/s, 500 → 42,8–53,9 t/s, 1000 → 5,9–6,5 t/s. Nos mesures Windows sur **100 ticks** à 500 entités (**45,6–46,5 t/s**) recoupent la référence ; l'écart apparent à 300 ticks est l'effet d'accumulation ci-dessus, pas une divergence de mesure.

### 3.2 ECHOS (ingestion batch d'un run archivé + analyse)

Ingestion d'un run SYNE archivé via `POST /ingest/run`, puis `POST /analysis/run`. Processus API unique (uvicorn, sans worker).

| Run ingéré | Ingestion | Débit d'ingestion | Analyse | Pic RSS |
| :-- | --: | --: | --: | --: |
| 50 entités × 100 ticks | 3,24 s | 30,8 t/s | 0,53 s | 145 Mo |
| 100 entités × 100 ticks | 6,39 s | 15,6 t/s | 1,04 s | 209 Mo |

L'analyse (`/analysis/run`) coûte 0,5–1,0 s pour 100 ticks et croît avec l'horizon (≈ 2,7 s pour 200 ticks en scénario live, §4.1). L'ingestion est **mono-thread** : c'est le goulot de la pile (voir §5).

### 3.3 `syne-mock` (émission de ticks)

Profil `prism` (100 agents, 6 TPS) ; `ticksPerSecond` poussé hors d'atteinte pour mesurer la capacité d'émission.

| Agents | Ticks | Temps | Débit | Pic RSS |
| --: | --: | --: | --: | --: |
| 100 | 200 | 0,64 s | 312,8 t/s | 72 Mo |

Le mock émet ~50× la cadence PRISM : il n'est jamais le facteur limitant d'un banc de développement.

## 4. Benchmark par scénario

### 4.1 SYNE + ECHOS (pile live : `--serve` + API + ingestion WebSocket)

- **Soutenu** : `ticksPerSecond = 10`, 100 ticks. Mesure la tenue de cadence quand la pile doit suivre.
- **Capacité** : `ticksPerSecond` hors d'atteinte, 200 ticks. Mesure le débit réellement tenu.

| Mode | Agents | Ticks | Run SYNE | Temps pipeline | Débit pipeline | Analyse | Pic RSS total |
| :-- | --: | --: | --: | --: | --: | --: | --: |
| Soutenu | 50 | 100 | 11,88 s | 11,89 s | 8,4 t/s | 0,59 s | 300 Mo |
| Soutenu | 100 | 100 | 12,52 s | 12,52 s | 8,0 t/s | 1,24 s | 377 Mo |
| Capacité | 100 | 200 | 6,18 s | 15,30 s | 13,1 t/s | 2,72 s | 599 Mo |

Lecture :

- En mode **soutenu**, `pipeline ≈ run SYNE` : à 10 t/s, ECHOS **suit sans retard**. Le débit de 8,0–8,4 t/s reflète la consigne (10 t/s) moins les phases de démarrage — la pile n'est pas limitée par ECHOS.
- En mode **capacité**, le run SYNE termine en 6,18 s (**32,4 t/s**, calcul seul) mais le **pipeline** prend 15,30 s (**13,1 t/s**). L'ingestion ECHOS reste donc **en retard de ~9 s** sur les 200 ticks : **ECHOS est le facteur limitant de la pile**, pas SYNE.
- Répartition mémoire (capacité) : API 378 Mo, ingestion 122 Mo, SYNE 98 Mo. La mémoire est dominée par le service API/analyse.

### 4.2 LAUNCHER + SYNE (campagne sur SYNE publié)

Suite `Launcher.Tests.EndToEnd`, classe `PublishedSyneCampaignEndToEndTests` : le Launcher lance le SYNE publié, collecte les données et scelle le paquet `.livexp`.

| Cas | Charge réelle | Durée observée (TRX) | Débit end-to-end |
| :-- | :-- | --: | --: |
| `Launcher_lance_SYNE_publie_collecte_les_donnees_et_scelle_le_paquet` | campagne courte + scellement `.livexp` | 1,1 s | — |
| `La_progression_du_run_vient_du_tic_rapporte_par_le_moteur` | **1 run, 1000 ticks, 200 agents**, profil `reference` complet (10 t/s visés) | **223,4 s** (suite) / **268,7 s** (isolé) | **~3,7–4,5 t/s** |

Le second cas est un run réel complet. À 10 t/s visés, 1000 ticks devraient tenir en ≥ 100 s de pacing ; les 224–269 s observés montrent que **le moteur devient limitant avant la consigne** sur cet horizon. Le run isolé (268,7 s) n'est pas plus rapide que le run en suite parallèle (223,4 s) : la durée n'est donc **pas** un artefact de contention.

Écart notable : le même volume en `--headless` avec une **configuration minimale** (200 agents, 1000 ticks) donne **20,8 t/s** (§3.1), soit ~5× plus vite. L'écart tient probablement au **profil `reference` complet** (communication, obstacles, oracles) et à la chaîne supervisée du Launcher, non au mode d'exécution en soi ; la cause exacte n'est pas isolée ici et mériterait une mesure dédiée.

### 4.3 LAUNCHER + SYNE + ECHOS (parcours J3 réel)

Classe `J3RealComponentsEndToEndTests` : campagne 3 runs contre le SYNE publié **et** ECHOS réellement lancé par son `echos-launcher`, interruption, reprise, analyse réelle et paquet scellé.

| Cas | Charge réelle | Durée observée (TRX) |
| :-- | :-- | --: |
| `J3_SYNE_publie_et_ECHOS_reel_interruption_reprise_analyse_et_rapport` | 50 ticks, 10 agents, profil `Analyse`, 3 runs, interruption après le run 1 | 7,7 s |
| `J3_echec_ECHOS_ne_supprime_rien_et_laisse_la_campagne_se_terminer` | idem avec ECHOS en échec simulé | 9,1 s |

À 10 agents / 50 ticks, une campagne J3 complète (démarrage SYNE publié + ECHOS réel par son `echos-launcher`, campagne, interruption/reprise, analyse ECHOS, scellement) tient **sous les 10 s**. C'est donc l'orchestration, non le calcul, qui domine à cette échelle ; le coût croît avec l'horizon (cf. §4.2).

> Les durées de test incluent la préparation de l'environnement, le démarrage des processus réels, la campagne et les assertions — ce sont des temps de bout en bout, pas des temps de tick. Elles servent de borne haute de latence d'orchestration, pas de débit moteur.

### 4.4 PRISM (non mesuré)

PRISM / PRISM-LDK est en version de travail ; l'éditeur Unreal n'est pas installé sur cette machine (`UnrealEditor` absent du `PATH`). Aucun débit de rendu n'est rapporté. Le coût PRISM est donc exprimé en §6 comme **cadence cible**, pas comme mesure.

## 5. Capacité maximale observée

| Composant / scénario | Capacité observée | Condition |
| :-- | :-- | :-- |
| SYNE (50 entités) | ~327 t/s | 300 ticks, calcul seul |
| SYNE (100 entités) | ~129 t/s sur 300 ticks ; ~67 t/s sur 1000 ticks | Dépend de l'horizon |
| SYNE (200 entités) | ~21 t/s | 1000 ticks, calcul seul |
| SYNE (500 entités) | ~10 t/s sur 300 ticks ; ~46 t/s sur 100 ticks | Dépend de l'horizon |
| SYNE (1000 entités) | ~2,9 t/s | 300 ticks, calcul seul |
| ECHOS ingestion | ~31 t/s (50 ent.) ; ~16 t/s (100 ent.) | Batch mono-thread |
| `syne-mock` | ~313 t/s | 100 agents |
| Pile SYNE+ECHOS | **~13,1 t/s** | 100 agents, live, analyse à chaque tick |

**Conclusion de capacité** : pour la pile complète, **ECHOS borne à ~13 t/s à 100 agents** (ingestion mono-thread + analyse `analysisEvery=1`). SYNE seul a encore de la marge à cette population. Augmenter la cadence de la pile suppose d'abord d'alléger l'ingestion (analyse moins fréquente, découplage batch), pas d'accélérer le moteur.

## 6. Coût à la cadence PRISM

**Cadence PRISM de référence** : `configs/simulation/prism.json` — **100 agents, 6 ticks/s** (166,7 ms/tick), 5 secondes simulées par tick, monde 2240×2240.

| Ressource | Besoin à 6 t/s (100 agents) | Capacité mesurée | Utilisation | Marge |
| :-- | --: | --: | --: | --: |
| Calcul SYNE | 6 t/s | 67,4 t/s (100 agents, 1000 ticks) | ~9 % | ×11 |
| Ingestion ECHOS | 6 t/s | ~15,6 t/s | ~38 % | ×2,6 |
| Pile SYNE+ECHOS | 6 t/s | ~13,1 t/s | ~46 % | ×2,2 |
| Émission `syne-mock` | 6 t/s | ~313 t/s | ~2 % | ×52 |

Autrement dit, à la cadence PRISM **le moteur ne tourne pas à sa fréquence maximale** : SYNE consomme une petite fraction de sa capacité de calcul, et la pile laisse ~2× de marge. Le coût dominant n'est donc ni le CPU SYNE ni le rendu : c'est le **maintien de l'ingestion et de la base d'analyse** dans la durée (la base SQLite et le pic mémoire croissent avec l'horizon, cf. §3.1).

Coût mémoire indicatif à cette cadence : SYNE ~74–98 Mo (100 agents, horizon 300–1000 ticks), service ECHOS ~145–210 Mo (base comprise), soit **~250–300 Mo** pour la pile observée en §4.1 pour 100 agents.

## 7. Recommandations

### Configuration minimale

| Poste | Valeur |
| :-- | :-- |
| CPU | 4 cœurs |
| RAM | 8 Go |
| Disque | SSD (la base d'analyse ECHOS grandit avec l'horizon) |
| Charge | SYNE headless 100 agents @ 6 t/s + ECHOS, **sans** rendu Unreal ; `analysisEvery` ≥ 5 |

Tient la cadence PRISM avec ~2× de marge sur la pile, à condition de **découpler l'analyse** (ne pas recalculer à chaque tick) et de surveiller la taille de la base sur les longs horizons.

### Configuration recommandée

| Poste | Valeur |
| :-- | :-- |
| CPU | 6–8 cœurs (le scénario complet fait tourner SYNE + API ECHOS + ingestion en parallèle) |
| RAM | 16 Go |
| GPU | Carte dédiée pour PRISM / Unreal (rendu 2240×2240) |
| Disque | NVMe, base d'analyse et archives séparées |
| Charge | Pile complète SYNE + ECHOS + LAUNCHER + PRISM @ 6 t/s, 100–500 agents selon l'objectif |

Recommandations opérationnelles :

1. **Ne pas confondre cadence et capacité.** Pour un run long, dimensionner sur le débit **à l'horizon visé** : un débit mesuré sur 100 ticks surestime d'un facteur ~5 à 500 entités.
2. **Découpler l'analyse** : `analysisEvery` > 1 ou ingestion batch d'archive. L'analyse à chaque tick est le principal coût ECHOS.
3. **Piloter la cadence par la droite de la chaîne** : à 100 agents, ECHOS borne la pile à ~13 t/s ; au-delà, c'est l'ingestion qu'il faut alléger en premier.
4. **Bornes de test** : conserver les planchers anti-régression de `docs/syne/PERFORMANCE.md` ; ne pas les relever sur la base de mesures à horizon court.
5. **Surveiller le pic à long horizon** : SYNE approche 670 Mo à 1000 entités / 300 ticks ; ECHOS dépasse 200 Mo à 100 ticks. Un run de production long doit être dimensionné sur la croissance mémoire, pas sur le premier tick.

## 8. Limites

- **Machine unique, non dédiée.** Mesures sur un portable 6 cœurs avec plan d'alimentation standard ; d'autres charges (antivirus, indexation) peuvent dégrader les chiffres. Une exécution sous contention a déjà produit un 32/33 E2E ponctuel (voir `realignment/LAUNCHER-ACCEPTANCE-2026-10.md`).
- **Horizon.** Le débit SYNE dépend de l'horizon (accumulation mémoire/croyances) ; les tableaux donnent l'horizon exact.
- **Correctif ECHOS non publié.** Le passage à 383 tests ECHOS et 33/33 E2E repose sur un correctif d'ingestion `append_tick_bundle` (utilité nulle + `held_utility`/`decision_state`/`decision_tick`) présent dans le checkout, **avec son test de régression**, mais non encore commité au moment de ce relevé.
- **PRISM non mesuré.** Aucun test Unreal ni rendu n'a été exécuté ; les 69 specs restent un résultat documentaire antérieur.
- **Aucune mesure de latence/réseau distribué** : tout est local (127.0.0.1). Une topologie répartie ajouterait la latence réseau, non mesurée ici.

## 9. Sources

- Harnais : [`scripts/benchmark-windows.py`](../../scripts/benchmark-windows.py) — sortie JSON brute par `--out` (par défaut `echos/data/benchmarks/windows-bench.json`).
- Référence moteur : [`docs/syne/PERFORMANCE.md`](../syne/PERFORMANCE.md) §9.2.
- État général : [`LIVEX-STATUS.md`](LIVEX-STATUS.md).
- Acceptations de réalignement : [`realignment/ECHOS-ACCEPTANCE-2026-10.md`](realignment/ECHOS-ACCEPTANCE-2026-10.md), [`realignment/LAUNCHER-ACCEPTANCE-2026-10.md`](realignment/LAUNCHER-ACCEPTANCE-2026-10.md).
