# TESTING — Plan de test et de calibrage PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`PERFORMANCE.md`](PERFORMANCE.md), [`DETERMINISM.md`](DETERMINISM.md), [`SYSTEMS_SPEC.md`](SYSTEMS_SPEC.md), `../syne/TESTING.md`
**Source Monographie** : Annexe J.2

---

## 1. Objectifs

1. Prouver que le monde est **correct** (règles, conservation).
2. Prouver qu'il est **reproductible** (déterminisme, reprise).
3. Prouver qu'il est **viable** (agents peuvent y vivre) puis **utile** (la coopération y est mesurable).
4. Éviter la confusion entre « la simulation tourne » et « le monde sert l'hypothèse ».

## 2. Pyramide

| Niveau | Contenu | Outils |
| :-- | :-- | :-- |
| Unitaire | Indices, chunks, hachage, bruit, recettes, durabilité, péremption | xUnit |
| Par propriétés | Conservation, absence de duplication, idempotence, ordre | FsCheck (ou équivalent) |
| Contenu | Validation de pack : schéma, références, graphe de recettes, amorçage à mains nues | Tests du validateur |
| Génération | Mondes dorés, contrôles de jouabilité | Empreintes figées |
| Contrat | Mêmes cas sur liaison réseau et embarquée ; versions inconnues ; échecs motivés | `Prism.Contracts.Tests` |
| Intégration | Host + bots, runs courts | `Prism.Host.Tests` |
| Bots | Runner limité au contrat, empreintes de run, bout-en-bout sur le pack minimal | `Prism.Bots.Tests` |
| Rendu | Faces visibles par région, déterminisme du maillage, overlays de hauteur | `Prism.Viewer.Tests` |
| Bout en bout | SYNE + PRISM + ECHOS via Launcher | `Launcher.Tests.EndToEnd` |
| Performance | B1…B8 | `Prism.Benchmarks` |
| Rendu | Meshing, captures | CI optionnelle |

## 3. Tests de non-régression clés

| Test | Critère |
| :-- | :-- |
| **Monde doré** | Empreinte du monde généré inchangée pour `(seed, worldGenVersion, contentHash)` de référence |
| **Run doré** | Empreintes de run (bots, 5 000 ticks, 3 seeds) inchangées |
| **Conservation** | `MatterLedger` équilibré à chaque intervalle pour tous les runs de test |
| **Reprise** | Run restauré = run continu (empreintes) |
| **Parallélisme** | 1, 2, 8 threads → même résultat |
| **Plateformes** | Windows = Linux |
| **Ablation sans effet de bord** | Un mécanisme désactivé ne modifie pas ce qui n'en dépend pas |
| **Contenu minimal** | Les 5 chaînes sont jouables par un bot scripté de bout en bout |
| **Couverture** | ≥ 80 % sur `Prism.Core` et `Prism.Content` (même seuil que le projet) |

## 4. Runs de référence

| Run | Agents | Ticks | Rôle |
| :-- | :-- | :-- | :-- |
| R0 | bots (marche aléatoire) | 1 000 | Fumée |
| R1 | bots planificateurs, 50 | 5 000 | Validation des chaînes |
| R2 | agents SYNE (adaptateur minimal), 50 | 5 000 | Validation du contrat avec la cognition actuelle (G3) — **concluante le 6 novembre 2026** (deuxième passe, après incarnation et ADR-013) : `minds = 50/50`, 250 000 observations positionnées, empreinte `0bf833f4bcfc1e5e` reproduite sur **trois hôtes distincts** ; reprise couplée (checkpoint `resA-299` → restauration → 300 ticks) = `44c2ea085d112e05`, identique au run continu de 600 ticks. Critère de succès : `minds > 0` en fin de run. La première passe (`079b736c91aae957` × 3, `minds = 0`) mesurait un état SYNE local faute de commande `Incarner` ; elle reste consignée. **Write-back branché (troisième passe, 6 novembre 2026)** : 10 agents × 1 500 ticks, seed 7, pack minimal — 388 commandes émises, 391 `action_completed`, 22 `path_not_found`, **3 832 déplacements observés**, 559 gisements exposés (bois), empreinte `b544dadd2d55f676` reproduite 3/3 sur le même hôte. Critère de succès : `minds > 0` **et** déplacements > 0. Limites : les désirs sous le profil `prism` restent majoritairement `Idle` (57 %) ; la mer du pack minimal est enterrée (niveau 26 sous un plateau à ~56), donc aucun gisement buvable n'existe et `Consommer` n'est couvert que par les tests de contrat ; coût ≈ 290 ms/tick à 50 agents, dont ≈ 60 ms fixes côté hôte PRISM. |
| R3 | agents SYNE refondus, 100 | 5 000 | Run de référence V2 (G5) |
| R4 | agents SYNE refondus, 1 000 | 2 000 | Charge (G5/P12) |
| R5 | ablations | 10 000 | Effet des mécanismes (G7) |

> **Durée.** Les runs de référence actuels du dépôt sont de **2 500 ticks** (ADR-016 de SYNE), insuffisants pour un cycle agricole complet (plusieurs centaines de ticks par culture, ≈ 1 920 ticks par saison). Les runs de validation V2 durent **au moins 5 000 ticks**. (Le brouillon évoquait 400 ticks : valeur antérieure aux campagnes d'octobre.)

## 5. Critères de viabilité (hypothèses à confirmer)

Évalués sur 3 graines (`12345`, `424242`, `999`), profil `reference`, 100 agents, 5 000 ticks.

| # | Critère | Seuil de départ |
| :-- | :-- | :-- |
| V1 | Pas d'extinction | 0 extinction sur 3 graines |
| V2 | Survie | ≥ 70 % des agents vivants à 5 000 ticks |
| V3 | Les chaînes sont jouées | ≥ 80 % des agents ont fabriqué un outil de palier 1 ; ≥ 1 outil de palier 2 par run |
| V4 | Agriculture effective | ≥ 1 récolte complète par saison ; ≥ 50 % de la nourriture consommée issue de production |
| V5 | Pas de duplication | Audit de conservation vert |
| V6 | Pas de blocage | < 5 % des agents sans action utile pendant 200 ticks consécutifs |
| V7 | Exploration | Couverture de ≥ 60 % des chunks par la mémoire spatiale collective |

## 6. Expériences d'ablation (test de l'hypothèse)

Chaque expérience compare le profil `reference` à un profil où **un mécanisme** est désactivé (`CONFIGURATION.md` §3), sur ≥ 5 graines, 10 000 ticks.

| Ablation | Mécanisme retiré | Observable attendu s'il compte |
| :-- | :-- | :-- |
| A1 | Structures partagées / coûts fixes | Moins de structures collectives, plus d'autarcie |
| A2 | Maîtrise (division du travail) | Moins de spécialisation et d'échanges |
| A3 | Péremption | Moins de recours aux greniers, moins de cuisson |
| A4 | Limite de transport | Moins de logistique, moins de dépôts partagés |
| A5 | Usure des outils | Baisse de la demande et des échanges d'outils |
| A6 | Partage d'information (messages locaux) | Découverte plus lente des ressources |
| A7 | Savoir riche (compare à simple) | Plus de formation de groupes pour la transmission |
| A8 | Usure des structures | Pas de dilemme d'entretien collectif |

**Lecture.** ECHOS compare les distributions (taille des groupes, indice de coopération, inégalité des stocks, spécialisation, usage des infrastructures) entre profils. Un effet doit dépasser la variabilité inter-graines (critère statistique défini dans `ECHOS/EXPERIMENT_COMPARISON.md`). **Un résultat nul ou négatif est un résultat** : il est publié avec ses limites.

## 7. Calibrage

Procédure itérative, comme les ADR-014/015/016 de SYNE :

1. Fixer un paramètre de `CONTENT_SPEC.md` §7 ; 2. lancer R1 sur 3 graines ; 3. lire V1…V7 ; 4. consigner l'itération dans un **ADR de calibrage** (`ADR-0xx-calibration-…`) ; 5. figer les empreintes dorées si un paramètre change. Ordre conseillé : durée des extractions → capacité de transport → croissance et eau → péremption → coûts de structures → maîtrise → seuils d'usure.

## 8. Mock et bots

Le mock Node.js de SYNE ne sert plus. `Prism.Bots` fournit quatre bots : *marche aléatoire* (fumée), *glouton* (bouclage simple besoin → source), *planificateur* (chaînage arrière sur recettes, sert aussi de **référence** pour le futur planificateur de SYNE) et *inactif* (`idle`, mesure du coût du tick sans commande). Les bots ne remplacent pas la validation contre le SYNE réel.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 3 novembre 2026 | Matrice de tests complétée (`Prism.Bots.Tests`) et quatre bots documentés | PRISM-077 |
| 1er novembre 2026 | Création | Refonte V2 |
