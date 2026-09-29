# Rapport de review / refactor — SYNE

Jalon : review/refactor complet du moteur SYNE.
Version moteur : `0.12.0` (bump MINOR appliqué, cf. [DETERMINISM.md §7](DETERMINISM.md)).
Version de schéma de snapshot : `4`.

- **Build** : `~/.dotnet/dotnet build Syne.sln` — 0 erreur, 0 avertissement.
- **Tests** : `~/.dotnet/dotnet test Syne.sln --configuration Release` — **553 verts, 0 échec**
  (`Simulation.Core.Tests` 494, `Simulation.Console.Tests` 59).
- **Intégration transverse** : `LIVEX_SYNE_E2E=1 pytest echos/tests/test_syne_echos_integration.py`
  — 5 exécutions consécutives vertes (le job CI `syne-echos-u8-integration`).
- **Build** : `dotnet build --configuration Release --warnaserror` — 0 avertissement
  (la CI traite les avertissements comme des erreurs).

---

## 1. Principe directeur

Chaque correction de comportement est accompagnée d'un test qui **échoue si on
rétablit le bug**. Cette vérification a été faite par mutation : le défaut est
réintroduit temporairement dans le code, le test doit devenir rouge, puis le
correctif est restauré. Les sections ci-dessous indiquent explicitement les
corrections **défensives** (aucun scénario de reproduction déterministe trouvé),
afin de ne pas sur-vendre la couverture.

---

## 2. Déterminisme et fluctuations numériques

### 2.1 Centralisation des primitives de hachage

`SplitMix64.Gamma/Avalanche` et l'implémentation FNV-1a étaient dupliqués dans
plusieurs fichiers, avec des constantes recopiées. Une divergence future aurait
produit deux hachages différents pour la même valeur.

- `Simulation.Core/Prng/SplitMix64.cs` : `Gamma`, `Avalanche`, `Finalize` exposés,
  `Next()` désormais bâti sur `Avalanche`.
- `Simulation.Core/Prng/Fnv1a64.cs` : **nouveau** — constantes, `Append`, `HashUtf8`.
- Sites dédupliqués : `PriorityConflictResolver`, `Inheritance`,
  `CommunicationSystem`, `ActionExecutor`, `Perception`,
  `SimulationSnapshotCodec`.

**Neutralité vérifiée** : la suite dorée passe sans réépinglage supplémentaire.

### 2.2 Relais et rejeu — cause unique des écarts de trajectoire

La divergence observée sur le scénario doré a été isolée : la neutralisation de
`CommunicationState.HasRelayed` seule suffisait à restaurer les checksums
historiques. Le garde de rejeu existe déjà dans `CommunicationSystem` mais
n'était jamais appliqué.

- `CommunicationSystem.CanRelay` applique désormais réellement `HasRelayed`
  (le destinataire d'un message ne le relaie pas).
- `CognitionPipeline.EnqueueSharePulse` transmet `MaxSendsPerTick`.
- File de messages bornée, politique « plus recent gagne ».

### 2.3 Réépinglage (DETERMINISM.md §7)

| Artefact | Avant | Après |
| --- | --- | --- |
| Checksum percepts (journal) | `0x27fad50065d8c4a4` | `0xdb57f58566418f5d` |
| Checksum état Ph10 | `0x072a488aa18c05eb` | `0x88bbc67950002bab` |
| `ObservabilityContract.EngineVersion` | `0.11.0` | `0.12.0` |
| `SimulationSnapshotCodec.SchemaVersion` | `3` | `4` |

Le schéma `4` ajoute `territories[]` (chaque territoire portant ses `members`).
L'addition est rétrocompatible à la lecture : un snapshot `v3` se restaure avec
des listes vides.

### 2.4 Piège de déterminisme : cellule spatiale

`SpatialGrid.QueryCircle` renvoie les voisins dans l'ordre de parcours des
cellules. Cet ordre décide de l'ordre de considération des perceptions, donc de
la trajectoire. Or la cellule était dimensionnée par `agents.perception.radius` :
**régler la portée de la perception modifiait silencieusement l'évolution du
monde**.

- `PerceptionSettings.SpatialCellSize` : **nouveau** paramètre explicite, défaut
  `50` — égal au rayon par défaut, donc trajectoire de référence inchangée
  (vérifié : aucun réépinglage supplémentaire nécessaire).
- `SimulationFactory` et `Program` n'utilisent plus le rayon de perception.
- Documenté : la taille de cellule est un paramètre de **performance**, à figer
  par graine, pas un réglage de perception.

### 2.5 LCG de la description du monde

`WorldDescriptionBuilder` place les ressources via un LCG local
(`1664525 / 1013904223`). Ce comportement est **conservé à l'identique** (il
conditionne l'accord description ↔ monde affiché) mais désormais documenté :
ce générateur est volontairement distinct du PRNG de simulation, car
l'observabilité ne doit consommer aucun tirage (DETERMINISM.md §3).

---

## 3. Corrections de logique

| Domaine | Défaut | Correction |
| --- | --- | --- |
| `SpatialGrid` | Comptages et bornes X/Y confondus | Cellules calculées par axe |
| `BirthSystem.Step` | `childId` non unique | Compteur incrémental monotone |
| `ResourceStocks.TryConsume` | Consommation partielle face à un stock insuffisant | Tout-ou-rien, valeurs non finies/négatives rejetées |
| `ActionExecutor.Execute` | Bénéfices appliqués avant l'échec d'une réserve | Réserve prélevée en premier |
| `CommunicationState.Enqueue` | File non bornée | Plafond + « plus recent gagne » |
| `MindState.RestoreLastDecision(null)` | Ancienne décision conservée | Décision effacée |
| `GroupSystem` | Leader non nullable (id `0` impossible) ; cohésion figée | Leader nullable ; cohésion recalculée à chaque revue d'un groupe survivant |
| Territoires | Non persistés : une restauration perdait les zones et réassignait « Communes » | Zones + appartenance persistées, sans faux événements |

---

## 4. Persistance et configurations

### 4.1 Fusion sparse destructive

Le corps `config` était désérialisé en `SimulationOptions` **avant** fusion.
L'objet obtenu étant complet, chaque clé absente prenait la valeur par défaut du
type : un client ne pouvait surcharger un champ sans détruire silencieusement
tous les autres réglages.

- `ConfigLoader.MergeJson(SimulationOptions, string/JsonElement)` : fusion JSON
  récursive ; les objets sont fusionnés, les tableaux remplacés en bloc.
- `ConfigLoader.ToJson`, `SimulationProfiles.ReferenceJson()`.
- `SimulationFactory.ResolveOptions(..., string? configJson, ...)`,
  `SimulationController`, `ControlServer` transportent désormais le **JSON brut**
  jusqu'à la fusion. `ConfigLoader.Merge(SimulationOptions, SimulationOptions)`
  a été supprimé.
- Un JSON malformé est transformé en `InvalidDataException` explicite.
- `config` non-objet → `400 invalid_config`.

### 4.2 Validation : `NaN` silencieusement accepté

Les contrôles de plage s'écrivent `value is < 0.0 or > 1.0`. En IEEE-754,
**toute comparaison avec `NaN` est fausse** : `NaN is < 0.0 or > 1.0` vaut
`false`, donc un `NaN` franchissait la validation puis se propageait dans les
distances, l'énergie et la Barclay, corrompant le monde sans lever d'erreur.

- Balayage récursif par réflexion de tout le graphe d'options
  (`FindNonFiniteNumbers`), avec des chemins en **camelCase** comme le
  contrat de configuration, avec garde anti-cycle et profondeur bornée.
- Garantie couvrant aussi les champs ajoutés ultérieurement.
- `simulation.worldCellSize` et `agents.perception.spatialCellSize` désormais
  validés (absents de la validation initiale).

### 4.3 Cohérence des collecteurs

`TickBudgetCollector.PhaseCount` valait **8 pour une énumération de 7 phases** :
un tableau surnuméraire invisible. `PhaseCount` est maintenant dérivé de
l'énumération.

---

## 5. Concurrence et cycle de vie

### 5.1 `SimulationController` — boucle de tick en « feu et forget »

La tâche de boucle était `_ = Task.Run(...)`, jamais conservée. Conséquences :

1. un second `StartAsync` superimposait une **deuxième boucle sur le même monde** ;
2. `Stop` n'annulait que la **dernière** `CancellationTokenSource`, laissant
   l'ancienne boucle active (le `using (runCts)` de la première n'était jamais
   atteint) ;
3. l'ancienne boucle pouvait **repasser l'état à `Finished`** par-dessus un run
   plus récent (course sur `_state` partagé) ;
4. `DisposeAsync` simulait l'attente avec `Task.Delay(20)`.

Corrections : tâche conservée (`_runTask`), **génération de run** vérifiée avant
toute écriture d'état partagé, second `StartAsync` refusé sur run actif,
`StopAsync`/`ResetAsync`/`DisposeAsync` attendent réellement la fin de boucle
(bornée à 5 s), `DisposeAsync` idempotent.

`ControlServer` répond désormais `200` sur `stop` **après** l'arrêt effectif.

### 5.2 `ControlServer` — corps de requête

- **Fuite par requête** : `JsonDocument.Parse(text).RootElement` renvoyé sans
  `Clone()` ni `Dispose()` — propriété invalide à la fin de vie du document.
  Corrigé par `RootElement.Clone()`.
- **Absence de limite de taille** : un `Content-Length` (ou un transfert par
  blocs) arbitrairement grand était lu intégralement en mémoire. Corrigé par un
  plafond de 1 Mio avec lecture **bornée** (`413 payload_too_large`).
- **JSON malformé → 500** au lieu de `400` : corrigé (`RequestException`).
- **Type de configuration incompatible → 500** : corrigé en `400 invalid_config`,
  avec un message nettoyé de toute fuite de type interne ou de numéro de ligne.

### 5.3 Observabilité — l'observabilité ne doit jamais bloquer le simulateur

- `ObservabilityServer` : sémaphore d'écriture **par client** (un `WebSocket`
  n'accepte qu'un `SendAsync` à la fois ; deux diffusions concurrentes levaient
  `InvalidOperationException`, non rattrapée), envois **bornés dans le temps**,
  retrait des clients dans le `finally` du drain (auparavant un client déconnecté
  restait au dictionnaire et son socket n'était jamais fermé), fermetures
  réellement attendues, `Start`/`DisposeAsync` idempotents, `ClientCount` purgé.
- `ObservabilityTickEmitter` : l'émetteur **ne fait plus confiance au sink** pour
  être borné (`SafeBroadcastAsync` + plafond de 5 s, aucune exception propagée) ;
  travail de capture **sauté quand aucun client n'est connecté** via
  `IObservabilityDemand` ; **tampons du tick toujours vidés** (la couche
  d'observabilité en est le seul consommateur — sans cela ils croissaient sans
  borne et rejouaient des événements obsolètes).
- `GroupOf` ne lève plus quand un groupe s'est dissous dans le même tick qu'il
  s'est formé. **Correctif défensif** : un balayage de 30 graines × 300 ticks
  (69 formations) n'a produit aucun cas de disparition intra-tick, la condition
  n'est donc pas atteignable avec la dynamique de groupes actuelle.

---

## 6. Navigation et interface CLI

### 6.1 A* — coupe de coin

Une diagonale était autorisée même lorsque **les deux** cellules orthogonales
intermédiaires étaient bloquées : le pas rase le point d'appui entre deux
cellules solides et l'entité traverse un angle d'obstacle. Règle stricte
appliquée : une diagonale n'est franchissable que si les deux orthogonales sont
libres. Sans effet sur la trajectoire dorée (monde de référence sans obstacle).

### 6.2 `CliArgs` / `Program.Main`

- `int.Parse`/`ulong.Parse` en culture courante : `FormatException` et
  `OverflowException` **n'étaient pas filtrées** par `Main` — `--seed -1` ou
  `--max-ticks abc` produisaient une **trace d'appels** au lieu d'un message.
  Analyse désormais explicite en culture invariante, erreurs converties en
  `ArgumentException` (filtrée, code de sortie 2), et `FormatException`/
  `OverflowException`/`DirectoryNotFoundException` ajoutées au filtre de `Main`.
- `--world-size 500 --max-ticks 10` : la hauteur manquante consommait le flag
  suivant (`int.Parse("--max-ticks")`). Les deux valeurs sont désormais lues d'un
  bloc et un jeton ressemblant à un drapeau est signalé comme « deux valeurs
  manquantes ».
- `--help` / `-h` : absents, ils levaient « flag inconnu ». Ajoutés avec un
  texte d'usage complet (code de sortie 0).
- Ports : `--serve-port 99999` échouait bien plus tard avec un message opaque,
  `--serve-port 0` faisait écouter sur un port **aléatoire en silence**. Plage
  `[1, 65535]` validée à l'analyse.
- `--serve-port` sans `--serve` était **silencieusement ignoré** : désormais
  refusé explicitement.

> **Piège du même type, découvert par le test d'intégration et non par la suite
> unitaire.** Une première version de cette correction appliquait la même règle à
> `--observe-port` : « `--observe-port` sans `--observe` est refusé ». C'est
> faux. En mode `--serve`, `Program` démarre l'observability **sur
> `--observe-port`**, pour que les clients d'un run contrôlé reçoivent le même
> flux qu'en mode `--observe`. Or le contrat d'intégration SYNE → ECHOS lance
> exactement `--serve --serve-port P --observe-port Q`, et le job CI
> `syne-echos-u8-integration` l'exécute : SYNE refusait ses arguments et
> n'ouvrait aucun port (« Connection refused »), ce qui fait échouer la CI sur
> un défaut de *validation* et non de moteur. Un test qui passe au vert ne
> prouve pas qu'un contrat transverse est préservé ; ici seul le test E2E réel
> l'a révélé. La règle correcte : un port d'écoute est refusé s'il n'est
> consommé par **aucun** mode actif, et le port d'observation est consommé par
> deux modes.

> **Précision d'honnêteté** : la culture invariante est une garantie de
> contrat, pas une correction aujourd'hui discriminante. Pour des drapeaux
> entiers, `NumberStyles.Integer` refuse déjà les séparateurs de milliers quelle
> que soit la culture ; les tests correspondants vérifient la **stabilité** sous
> plusieurs cultures, pas une différence observable.

---

## 7. Couverture de tests ajoutée

**132 tests** ajoutés durant ce jalon (466 → 598). La suite SYNE compte
**553 tests** (494 Core + 59 Console).

| Fichier | Tests | Détecteur de bug vérifié par mutation |
| --- | --- | --- |
| `HashPrimitiveTests` | 11 | vecteurs de référence |
| `ActionExecutorReserveAtomicityTests` | 6 | oui |
| `CommunicationAntiRelayTests` | 7 | oui (via trajectoire dorée) |
| `SpatialGridRectangularTests` | 8 | oui |
| `BirthSystemChildIdTests` | 4 | oui |
| `TerritoryPersistenceTests` | 5 | oui |
| `GroupSystemLeaderTests` | 5 | oui |
| `MindStateRestoreDecisionTests` | 4 | oui |
| `ConfigLoaderMergeJsonTests` | 10 | oui |
| `ControlServerSparseConfigTests` | 5 | oui |
| `SimulationControllerRunLifecycleTests` | 7 | oui |
| `ControlServerHardeningTests` | 8 | oui (`Clone()` : 5 tests ; limite : 1 test) |
| `ObservabilityHardeningTests` | 11 | oui (diffusion bornée, saut sans abonné, vidage) |
| `SimulationOptionsValidatorNonFiniteTests` | 14 | oui (balayage désactivé : 7 échecs) |
| `SpatialCellSizeDecouplingTests` | 6 | oui |
| `AStarNoCornerCuttingTests` | 6 | oui (coupe de coin, `PhaseCount`) |
| `CliArgsTests` | 29 | oui (`--help`, ports, `--world-size`, `--observe-port` + `--serve`) |

### Pièges de faux positifs rencontrés et corrigés

Trois tests ont d'abord « détecté » un bug **pour une autre raison**. Ils ont été
réécrits pour exiger le motif exact :

1. `SecondStart_OnAnActiveRun_IsRejected` passait car le second `StartAsync`
   levait `InvalidOperationException` au titre de la règle *Prepare/ready*, pas de
   la détection de run actif. Le test utilise désormais le chemin « monde déjà
   construit » et vérifie le message.
2. `NoSubscribers_SkipsSnapshotButStillDrainsTickBuffers` passait alors que les
   tampons étaient vides : aucun scénario ne les alimentait. Le test place
   maintenant réellement une construction.
3. `WorldSize_WithMissingHeight_DoesNotSwallowTheNextFlag` ne vérifiait que le
   nom du flag, présent dans le message d'erreur pour une autre cause.

Deux autres tests se sont révélés **non discriminants** et ont été reclassés
explicitement : le test de culture invariante (§6.2) et le correctif de
`GroupOf` (§5.3).

---

## 8. Points laissés ouverts

- **Granularité des trames d'observabilité** : `API_CONTRACTS.md §2` impose une
  trame par événement, donc le coût par tick reste linéaire en le nombre
  d'événements chez un client connecté. Corriger cela demanderait de modifier le
  contrat de diffusion, hors périmètre de ce jalon. L'optimisation appliquée
  porte sur le cas « aucun client ».
- **Ordre de `QueryCircle`** : le rendre indépendant de la géométrie de la grille
  (tri par identifiant) changerait la trajectoire et imposerait un nouveau
  réépinglage. Le découplage de §2.4 supprime le déclencheur caché ; l'ordre
  reste documenté comme dépendant de la cellule, et un test le verrouille.
- **Documentation** : la documentation existante n'a pas été considérée comme
  une vérité de référence. La refonte documentaire reste à faire ; les fichiers
  JavaScript de `docfx` sous `docs/pages-dist/docs/styles/` sont hors périmètre
  et n'ont pas été touchés.
