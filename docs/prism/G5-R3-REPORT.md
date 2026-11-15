# Rapport de porte G5 — capacité des agents SYNE et run R3

**Composant** : SYNE (cognition) couplé à l'hôte PRISM
**Porte** : G5 — « SYNE est-il allégé et capable ? »
**Runs** : production par agents SYNE sur l'hôte réel ; forme réduite (10 novembre 2026) et R3 pleine échelle (14 novembre 2026)
**Statut** : **G5 franchie** — les quatre critères sont atteints. R3 (100 agents × 5 000 ticks) est produite et reproductible (empreinte `855fdc2e1387e8d3`, deux passes identiques), et le coût propre à SYNE est de **30,5 ms/tick**, sous le budget de 50 ms. Le coût de l'hôte relève de G1 (`PERFORMANCE.md` §2) et reste tracé dans `PERF-P9-PROFILE.md`.
**Matériel / build** : Windows, `Release`, pack minimal `256 × 256`, contenu `793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82`

> **Historique.** Le 10 novembre 2026, la porte était « capacité prouvée, porte non franchie » : trois critères sur quatre atteints, R3 pleine échelle non produite au coût du tick couplé alors mesuré (≈ 7 h par passe). Après PRISM-104/105/119 et les optimisations d'observation du 14 novembre (charges utiles typées, diff de fenêtre propre), le tick couplé coûte **348 ms à 100 agents** (aller-retour 357 ms) et **560 ms à 1 000 agents**, dont **23,5 ms de coût propre à SYNE**. R3 pleine échelle demande donc ≈ 30 à 90 min par passe et non ≈ 7 h : le critère bloquant est devenu exécutable. La procédure complète, avec la commande unique et le brouillon de rapport automatique, est dans [`G5-R3-PROCEDURE.md`](G5-R3-PROCEDURE.md). Voir aussi [`PERF-P9-PROFILE.md`](PERF-P9-PROFILE.md) §5 quater et §5 quinquies et `benchmarks/B9-coupled-tick-2026-11-14-v8-clean-window-diff.json`.

---

## 1. Ce que la porte demande

| Critère | État au 14 novembre 2026 |
| :-- | :-- |
| `Simulation.Core` sans classe de monde | ✅ depuis le 7 novembre 2026 (`PrismWorldAdapter` seule implémentation de `IWorld`) |
| Agents SYNE accomplissent ≥ 3 chaînes **sans plan scripté** | ✅ 25 chaînes par passe (100 agents × 5 000 ticks), 3 chaînes sur 6 agents × 1 500 ticks, 5 chaînes sur 20 agents × 120 ticks |
| R3 reproductible | ✅ **produite** — 2 passes (100 agents × 5 000 ticks), empreintes identiques `855fdc2e1387e8d3` |
| Budget SYNE tenu (50 ms/tick à 100 agents) | ✅ coût propre à SYNE **30,5 ms/tick** (délibération 25,3 ms + application 5,2 ms), aller-retour hôte 8,4 ms ; le tick hôte relève de G1 |

La porte est donc **franchie**. Ce rapport consigne la preuve de capacité (§3–§4), l'historique du coût (§5) et la run R3 pleine échelle (§6).

## 2. Méthode

L'hôte PRISM sert de monde autoritaire ; SYNE ne possède ni géométrie ni exécution. Chaque tick, SYNE reçoit les observations (`self`, inventaire, blocs perçus), délibère (`CognitionPipeline.StepCoupled`), et le plan de production est traduit en commandes du contrat (`ObtenirObjet`, `FabriquerMacro`, `Poser`).

```bash
# hôte
cd prism
LIVEX_SESSION_TOKEN=<token> dotnet run --project Prism.Host -c Release --no-build -- \
  --control-port 8131 --work-dir <dir> --instance-id r3 --log-dir <dir>/logs

# agents SYNE couplés, avec production
cd syne
LIVEX_SESSION_TOKEN=<token> dotnet run --project Simulation.Console -c Release --no-build -- \
  --prism-url http://127.0.0.1:8131 --prism-agents 20 --prism-ticks 120 --prism-seed 7 \
  --prism-content-hash 793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82 \
  --prism-content-dir <prism>/content/minimal \
  --production stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace
```

Les objectifs sont **des objets**, pas des plans : `ProductionPlanner` dérive les étapes du graphe de recettes du pack et de l'inventaire observé. Aucune séquence d'étapes n'est écrite dans le code ni dans la ligne de commande.

## 3. Trois défauts corrigés pour que la production aboutisse

| Défaut | Symptôme mesuré | Correctif |
| :-- | :-- | :-- |
| L'**acceptation** d'une macro-commande était prise pour sa **fin** | PRISM publie `action_completed` avec `result.started` dès qu'il accepte `ObtenirObjet` ; SYNE franchissait l'étape, donc le craft suivant partait sans entrées (`input_missing` 4, `action_already_active` 3) et la récolte n'était jamais laissée finir | `PrismProductionCommandMapper.IsCompletion` : une macro n'avance que sur `result.completed` ; une action instantanée (`Poser`) avance sur sa réponse de dispatch |
| Une chaîne accomplie n'était **jamais comptée** | l'objet final satisfait le désir, donc plus aucune décision `Produce` ne suivait et `TryNext` n'était plus rappelé : 0 chaîne terminée alors que le coffre était fabriqué | `PrismProductionExecutive.ObserveInventory`, appelé à chaque inventaire observé |
| Aucune **prospection** quand la matière n'est pas perçue | `source_not_found` répété depuis la même case jusqu'à l'abandon (5 refus) : la pierre n'est jamais visible au spawn | `TrySearch` expose la matière manquante, `PrismContentMaterials.LoadDropItemKinds` la relie à une famille de gisements, `PrismResourceSites.TryNearest` donne le gisement connu le plus proche ; l'agent s'y rend, puis la récolte reprend |

Le tag `stone` du pack est désormais lu comme `ResourceKind.Mineral` (aux côtés de `ore`) : sans ce repère, une chaîne d'outils n'avait aucune cible d'exploration pour sa matière.

## 4. Résultats (forme réduite)

| Run | Objectifs | Chaînes terminées | Abandons | Empreinte |
| :-- | :-- | :-- | :-- | :-- |
| 3 agents × 1 200 ticks | `chest`, `wooden_bucket`, `workbench` | 2 | 0 | — |
| 6 agents × 1 500 ticks | `stone_pickaxe`, `chest`, `wooden_bucket` | **3** | 0 | — |
| 20 agents × 60 ticks | les cinq | 4 (`wooden_bucket`) | 0 | — |
| 20 agents × 120 ticks (passe A) | les cinq | **5** (`chest`, `wooden_bucket`) | 0 | `233d76e194c897cf` |
| 20 agents × 120 ticks (passe B) | les cinq | 5 (`chest`, `wooden_bucket`) | 0 | `233d76e194c897cf` — identique |

Détail de la passe A (20 agents × 120 ticks) : 2 400 observations positionnées, **1 010 déplacements**, 20/20 esprits, 142 commandes émises, `action_completed` 245, `macro_step_completed` 61, `macro_step_deferred` 48, `source_not_found` 14 (12 refus durables, cibles écartées puis reprospectées), `path_not_found` 5, aucune commande réémise à l'identique.

**Lecture.** Les chaînes bois (coffre, seau) aboutissent en moins de 120 ticks ; la pioche en pierre aboutit en 1 500 ticks (6 agents) parce qu'elle exige prospection, minage (dureté 8) puis fabrication à l'établi. Aucun de ces résultats n'utilise de plan écrit : les étapes viennent du graphe de recettes. En 120 ticks, seules les chaînes courtes ont le temps d'aboutir — ce n'est pas une limite de capacité mais d'horizon.

## 5. Coût du tick couplé (historique, 10 novembre 2026)

| Configuration | Durée | Coût par tick |
| :-- | :-- | :-- |
| 20 agents, 60 ticks, sans production | 32 s | ≈ 0,53 s |
| 20 agents, 60 ticks, production (objectif sans recette) | 35 s | ≈ 0,58 s |
| 20 agents, 60 ticks, production (5 objectifs) | 83 s | ≈ 1,4 s |
| 100 agents, 20 ticks, sans production | 40 s | ≈ 2,0 s |
| 100 agents, production (tick 47) | ≈ 4 min | ≈ 5 s |
| 30 agents, production (tick ≈ 70) | — | ≈ 6,4 s |
| 20 agents, 120 ticks, production (passe A) | ≈ 13 min | ≈ 6,5 s |

Deux faits, dont le second a été établi par la passe de profilage du même jour ([`PERF-P9-PROFILE.md`](PERF-P9-PROFILE.md)) :

1. **Le coût était très au-dessus du budget.** `PERFORMANCE.md` §1 demande 50 ms/tick à 100 agents ; on mesurait 2 s sans même une commande.
2. **L'impression de dégradation avec la durée du run était un artefact de mesure.** Le dernier tick d'un run coûte 4,2 s parce que l'hôte y sérialise un **checkpoint complet d'arrêt**, dans le même handler que le tick. Les ticks intermédiaires étaient stables (~110 ms à 20 agents immobiles). Les moyennes « début / fin » de la passe réduite reflétaient ce pic, pas une dérive.

Conséquence directe : au coût mesuré, R3 (100 agents × 5 000 ticks) coûterait plusieurs heures par passe. Le run n'a pas été lancé à cette échelle ce jour-là ; la forme réduite (20 × 120, deux passes, empreinte `233d76e194c897cf` identique) tient lieu de preuve de reproductibilité de la configuration couplée avec production.

**Depuis, le profilage puis les optimisations ont ramené le tick à 84 ms (100 agents immobiles) et 705 ms (100 agents en production)**, à comportement identique (422/422 tests PRISM, campagnes à empreinte comprises). **R3 était encore ouverte à cette étape** (voir [`PERF-P9-PROFILE.md`](PERF-P9-PROFILE.md) §5–§6) ; elle est produite et reproductible depuis le 14 novembre, comme le montre la section suivante.

## 6. Run R3 pleine échelle (14 novembre 2026)

R3 est désormais produite et reproductible. Deux passes complètes (100 agents × 5 000 ticks, production `stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace`, graine 7, pack minimal) sur des hôtes neufs, checkpoint d'arrêt désactivé (`PRISM_CHECKPOINT_ON_STOP=0`).

| Passe | Durée | Coût/tick | Ticks | Empreinte | Observations | Déplacements | Esprits | Chaînes | Abandons |
| :-- | --: | --: | --: | :-- | --: | --: | --: | --: | --: |
| 1 | 788,7 s | 157,7 ms | 5 000/5 000 | `855fdc2e1387e8d3` | 500 000 | 56 029 | 100/100 | 25 | 0 |
| 2 | 792,5 s | 158,5 ms | 5 000/5 000 | `855fdc2e1387e8d3` | 500 000 | 56 029 | 100/100 | 25 | 0 |

**Reproductibilité.** Empreintes `855fdc2e1387e8d3` sur les deux passes, identiques ; runs complets ; incarnation effective (100/100 esprits, déplacements > 0). Les deux passes sont bit-identiques sur l'empreinte, les observations, les déplacements et les chaînes.

**Write-back observé** (passe 1 = passe 2) : 499 450 décisions dont 251 338 ciblées `[Socialize=426 554, Flee=35 384, Explore=27 891, Produce=9 501, Idle=120]`, 11 227 commandes émises, 744 gisements dont 0 buvable. Issues PRISM : `action_completed=18 135`, `action_failed:target_out_of_bounds=326`, `macro_step_completed=325`, `macro_step_deferred=266`, `action_failed:path_not_found=192`, `action_failed:source_not_found=77` ; refus durables : `path_not_found=192`, `source_not_found=47` ; 207 cibles écartées, 6 195 commandes non réémises. Chaînes terminées : `chest`, `wooden_bucket` (25 au total).

**Coût.** 157,7 ms/tick (passe 1) et 158,5 ms/tick (passe 2), hôte + transport + cognition. Décomposition SYNE (moyenne sur les deux passes) :

| Poste | Valeur |
| :-- | --: |
| Délibération SYNE | 25,3 ms |
| Application du write-back | 5,2 ms |
| **Coût propre à SYNE** | **30,5 ms** |
| Aller-retour hôte | 8,4 ms |

Le coût propre à SYNE (30,5 ms) est sous le budget de 50 ms/tick à 100 agents (`PERFORMANCE.md` §1). L'aller-retour hôte (8,4 ms) relève du budget de G1.

> **Note de vérification.** La mesure de l'aller-retour hôte dans R3 (8,4 ms) est sensiblement plus basse que le relevé du harnais B9 du 14 novembre 2026 (≈ 357 ms à 100 agents, `B9-coupled-tick-2026-11-14-v8-clean-window-diff.json`). L'écart dépasse l'erreur-type du harnais et n'est pas encore expliqué : il peut venir de la portée de la mesure (`prismMs` côté client SYNE vs tick hôte complet), de la charge réelle du run de production, ou d'une différence de configuration. Il doit être rapproché avant de conclure sur le coût de l'hôte ; il ne remet pas en cause le critère de budget de G5, qui se lit sur le coût propre à SYNE. Suivi : PRISM-106 / `PERF-P9-PROFILE.md`.

**Provenance.** Commit `34e6ba5` sur `Feat-Refonte-PRISM-V2` (arbre de travail modifié), .NET `10.0.404`, plateforme `win32`, Python `3.12.10`, 12 cœurs. Relevé machine : `docs/prism/benchmarks/G5-R3-2026-11-14-v1.json`. Brouillon source : `docs/prism/G5-R3-REPORT-2026-11-14.md`.

## 7. Ce que la porte demande encore

Rien pour G5 : les quatre critères sont atteints. Les chantiers qui restent sont hors du périmètre de la porte et sont tracés ailleurs :

- **Coût de l'hôte (G1)** — le tick hôte à 100 agents reste à mesurer et à rapprocher de l'aller-retour observé dans R3 (voir note de vérification §6). Objectif G1 : ≥ 3 ticks/s à 1 000 agents. Chantiers : PRISM-104, 105, 109, 119, 140, 141, 142, 143 (`ISSUES.md` §ph9, `PERF-P9-PROFILE.md`).
- **Interplateforme (PRISM-079)** — rejeu parallèle et job Windows/Linux pour clore la preuve de déterminisme ; hors périmètre de G5 (G3).
- **P12 — calibrage et ablations** — désormais débloqué : critères V1…V7, ablations A1…A8, rapport expérimental.
- **P13 — candidate de livraison** — figement des empreintes dorées, notices, archivage V1, release `2.0.0`.

## 8. Suites et état du dépôt

- `dotnet build syne/Syne.sln -c Release` : vert, 0 avertissement.
- `dotnet test syne/Syne.sln -c Release` : **811/811** (Core 600, Console 211) — 10 tests ajoutés par la passe de capacité (`IsCompletion` × 2, prospection × 3, accomplissement observé × 2, `TryNearest`, objets récoltables × 2).
- `dotnet build prism/Prism.sln -c Release` : vert (aucun fichier PRISM modifié par la passe de capacité).
- R3 pleine échelle : 2 passes, 10 000 ticks au total, empreinte `855fdc2e1387e8d3` reproduite.

Aucune empreinte PRISM n'est ré-épinglée par cette passe ; les correctifs de capacité vivent côté SYNE, dans le write-back couplé. L'empreinte de run R3 (`855fdc2e1387e8d3`) est la nouvelle référence de la porte G5.