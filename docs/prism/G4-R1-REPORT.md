# Rapport de porte G4 — run R1 (9 novembre 2026)

**Composant** : PRISM
**Porte** : G4 — « Les chaînes existent-elles vraiment ? »
**Run** : R1 — bots planificateurs, cinq chaînes de production, audit de conservation
**Statut** : **franchie** — 5/5 chaînes, audit vert, campagne reproductible
**Matériel / build** : Windows, `Debug`, pack minimal `256 × 256`, contenu `793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82`

---

## 1. Ce que la porte demande

| Critère | État au 9 novembre 2026 |
| :-- | :-- |
| Les cinq chaînes jouées **par des bots** | ✅ 5/5 (outils, agriculture, eau, cuisson, stockage) |
| Audit de conservation vert | ✅ vert (0 violation) |
| Graphe amorçable à mains nues | ✅ (PRISM-022) |
| Campagne reproductible | ✅ deux exécutions identiques (`abab272bd13e6ba3`) |

R1 mesure la capacité d'un bot à jouer les chaînes **seul, par le contrat**, sans plan écrit à la main ni accès au monde. Le détail fonctionnel des chaînes est couvert par des scénarios d'acceptation au niveau système.

## 2. Méthode

`R1Campaign` instancie cinq `PlannerBot`, un par chaîne :

| Agent | Chaîne | Objectif | Plan |
| :-- | :-- | :-- | :-- |
| 1 | tools | `iron_pickaxe` × 1 | 20 étapes |
| 2 | agriculture | `wheat` × 3 | 12 étapes |
| 3 | water | `wooden_bucket` × 1 | 3 étapes |
| 4 | cooking | `flatbread` × 1 | 17 étapes |
| 5 | storage | `chest` × 1 | 3 étapes |

Chaque agent est placé sur une case praticable voisine d'une ressource, choisie par chaîne (pierre exposée + arbre pour outils ; eau + pierre + arbre pour les fermes). Les bots ne parlent au monde que par `IWorldHost` ; à la fin du run, `PrismHostRuntime.AuditConservation()` compare le ledger à ce que les agents, les conteneurs et le sol détiennent.

Reproduction :

```bash
cd prism
dotnet run --project Prism.Bots -c Debug -- --r1 --agents 5 --ticks 2500 --seeds 1
```

Durée mesurée : ~5 min 15 s en `Debug` sur la machine de référence (traces désactivées ; `PRISM_DEBUG_BOT=1` / `PRISM_DEBUG_MACRO=1` les rallument pour un diagnostic).

## 3. Résultats (graine 1, 5 agents, 2 500 ticks)

| Mesure | Valeur |
| :-- | :-- |
| Empreinte de run | `abab272bd13e6ba3` |
| Commandes envoyées / acceptées | 156 / 197 (les étapes différées comptent une acceptation de plus) |
| Refus | `action_already_active` 93, `support_required` 2 |
| `cropRipeEvents` / `cropWitheredEvents` | 4 / 0 |
| Audit de conservation | **vert** (0 violation) |
| Chaînes complétées | **5/5** |

| Chaîne | Étapes du plan | Étapes refusées | Achevée |
| :-- | :-- | :-- | :-- |
| tools | 20 | — | ✅ |
| agriculture | 12 | — | ✅ (4 cultures mûres) |
| water | 3 | — | ✅ |
| cooking | 17 | — | ✅ |
| storage | 3 | — | ✅ |

Les seuls refus restants sont bénins : `action_already_active` (le bot sonde une macro encore en cours, il attend son événement de fin) et `support_required` (une sous-étape refuse de miner le bloc sous les pieds ; le macro choisit la source suivante au lieu de mourir).

Campagne réduite épinglée (5 agents × 200 ticks, graine 1) : empreinte `70455f3b3b4068e6`, audit vert, deux exécutions identiques — c'est le test permanent `R1CampaignTests`.

## 4. Correctifs de la première passe (2/5 → 4/5)

| Correctif | Effet |
| :-- | :-- |
| **Plant exigeait la perception de la cellule d'air** (`WorldActionCommandHandler.Plant` accepte aussi le bloc de support) | `cropRipeEvents` passe de 0 à 4 : l'agriculture mûrit |
| **Chemin différé** (`MacroActionTickPhase.CompleteDeferredStep`) : un `Mine` refusé `support_required` ne tue plus la macro | la bûche sous les pieds ne fait plus perdre la graine |
| **Churn `action_already_active`** (PlannerBot attend l'événement de fin de macro) | refus de 732 → 108, commandes de 5 539 → 186 |
| **Réordonnancement de l'expansion** (station d'abord, entrées périssables en dernier) | le blé n'expire plus avant la cuisson : `flatbread` produit |

## 5. Correctifs de la seconde passe (4/5 → 5/5)

Le dernier blocage résiduel était la chaîne **outils** : `source_not_found` sur `iron_ore_chunk × 3`, puis `path_not_found` vers l'établi.

| Cause racine | Diagnostic | Correctif |
| :-- | :-- | :-- |
| **Le graphe de déplacement ignore les trous minés** | l'agent mine la pierre de son four autour de lui ; le bloc de surface retiré rend la colonne « non praticable » pour toujours, l'agent reste sur un pilier 1×1 et **tous** ses chemins échouent (`expanded=1`) | `MovementGraph.CanStandAt` descend dans le trou : la cellule minée est praticable un niveau plus bas (jusqu'au premier bloc solide) |
| **Pose de station au niveau des pieds** | dans un trou d'un niveau, les quatre voisins à ce niveau sont la pierre non minée → `cell_occupied`, le four n'est jamais posé (`station_absent`) | `PlannerBot.FindPlacementCell` pose la station sur le **sol perçu** de la colonne voisine (air au-dessus du bloc solide le plus haut), à un niveau des pieds |
| **Sources de minerai comptées sans atteignabilité** | le site outils voyait 342 faces de fer derrière un ravin ; le macro émettait un `Move` par tick vers une face inatteignable puis épuisait la trame | `R1Campaign` calcule la composante praticable du spawn (BFS cardinal, +1/−3, eau et solides bloqués) et ne compte que le minerai dont la cellule d'approche y est |
| **Trace de diagnostic manquante** | un refus ne disait ni ce que le bot possédait ni où était la station | `PlannerBot.DescribeState` (possédé / station) et `[macro-walk-fail]` (cible, cellule d'approche, position) sous `PRISM_DEBUG_BOT=1` / `PRISM_DEBUG_MACRO=1` |

## 6. Journal des empreintes (graine 1, 5 × 2 500)

| Empreinte | État | Origine |
| :-- | :-- | :-- |
| `cc2d6b4630e7de77` | 2/5 | état d'entrée de la passe du 9 novembre (outils bloqués sur le fer, cf. §9) |
| `f2b01ee59a4e82db` | 3/5 | correctif de plantation (perception du support) |
| `994c46443504033e` | 4/5 | pose de station + réordonnancement des périssables (avec churn) |
| `5c765a0e45c035a1` | 4/5 | attente de fin de macro (churn supprimé) |
| `abab272bd13e6ba3` | **5/5** | trous minés praticables + pose sur le sol + sources atteignables |

## 7. Ce qui reste ouvert

1. **`action_already_active` (93)** : bénin mais coûteux — le bot interroge une macro encore active ; une souscription explicite à l'événement de fin supprimerait les 93 refus.
2. **`support_required` (2)** : la sous-étape est refusée puis le macro repart ; une sonde évitant d'emblée le bloc sous les pieds économiserait deux ticks.
3. **Coût du run** : 5 min 15 s pour 5 × 2 500 en `Debug` ; le gate de release (3 graines × 5 000 ticks, forme 50 agents de `TESTING.md` §4) reste manuel.
4. **Sélection de site** : `ReachableStands` parcourt la composante entière du spawn pour chaque candidat ; acceptable à 256 × 256, à mémoïser si la carte grandit.

## 8. Verdict

**G4 franchie : 5/5 chaînes, audit vert (0 violation), campagne reproductible.** Les cinq chaînes sont jouées par des `PlannerBot` sans plan écrit à la main, dans la fenêtre de 2 500 ticks, et deux exécutions rendent exactement la même empreinte. Les suites PRISM sont vertes (423/423 : Core 193, Content 51, Host 72, Bots 22, Viewer 62, Contracts 15, Architecture 8) et les empreintes golden ont été re-épinglées avec leur raison dans `docs/prism/ISSUES.md` (PRISM-102).

## 9. Historique des passes antérieures (7–8 novembre 2026)

Ce rapport a été réécrit le 9 novembre : les sections détaillées des passes précédentes y sont condensées ci-dessous. Le détail de chaque changement reste dans les journaux de [`ROADMAP.md`](ROADMAP.md) §« Mises à jour » et [`ISSUES.md`](ISSUES.md) (PRISM-077, PRISM-102).

| Date | Passe | Mesure R1 (graine 1) | Correctif principal |
| :-- | :-- | :-- | :-- |
| 7 novembre | Harnais R1 | 5 × 1 500 ticks, `10de591b086cb5a9`, audit vert, **0/5** ; campagne réduite 5 × 200 épinglée (`2608687a37a7c72d`) | `prism-bots --r1`, `AuditConservation()`, `R1CampaignTests` : la porte devient mesurable par des bots, plus seulement par des scénarios hôte |
| 7 novembre | P8 (contenu) | **2/5** (eau, stockage), audit vert | Portée de bloc élargie (3×3, pieds et un bloc dessous) ; le macro passe à la source suivante quand la sienne disparaît |
| 8 novembre | A1 (génération/contenu) | `b0562544e270a726`, **1/5** (eau), audit vert | La côte est rapprochée de l'herbe (`ShoreBand`, `worldGenVersion` 2), l'agriculture côtière devient possible (`ShorelineAgricultureAcceptanceTests`) |
| 8 novembre | A2 (macro-actions) | `88ab0dcbaaab3e36`, **2/5** (eau, stockage), audit vert | Portée de bloc = cube 3×3×3 autour du corps et **une seule règle** de portée entre macro et actions : les deux logs d'un arbre sont récoltables |
| 8 novembre | PRISM-077 (perception) | run outils 1 × 2 500 : 214 commandes / 58 acceptées, `source_not_found` 1 ; R1 réduit `bf31047f1f0385f4` | La perception publie enfin les minerais (0 → 320 entrées `iron_ore` au spawn outils) et six capacités du planificateur arrivent (attente de fin de trame, tags par objets portés, mémoire de station, case de pose avec support, reprise de culture, prospection) |
| 8 novembre | Passe A2/R1 | `cc2d6b4630e7de77`, **2/5** | Point d'entrée de la passe du 9 novembre : chaîne outils bloquée sur le fer malgré le minerai désormais perçu |
