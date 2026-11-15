# Rapport de porte G3 — contrat et cognition couplée (14 novembre 2026)

**Composant** : PRISM (contrat, hôte, persistance) avec la cognition SYNE
**Porte** : G3 — « Le contrat tient-il avec une vraie cognition ? »
**Runs** : R2 (50 agents × 5 000 ticks, lecture), reprise couplée, write-back (10 agents × 1 500 ticks)
**Statut** : **franchie** — contrat gelé en `1.0.0`, matrice contractuelle verte sur les trois liaisons, R2 reproductible, reprise couplée bit-identique, garde-fou de déterminisme vert
**Matériel / build** : Windows, `Release`, pack minimal `256 × 256`, contenu `793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82`

---

## 1. Ce que la porte demande

| Critère | Preuve | État |
| :-- | :-- | :-- |
| Contrat `1.0.0` gelé | `ProtocolVersion.Current = 1.0.0` depuis le 4 novembre 2026 ([`API_CONTRACTS.md`](API_CONTRACTS.md) §8) | ✅ |
| Tests de contrat sur 2 liaisons (au moins) | `CommandMatrixContractTests` : 21 kinds de commande sur **trois** liaisons (embarquée, HTTP, WebSocket), **23/23 verts** | ✅ dépassé |
| Reprise bit-à-bit | test continu/restauré : ticks, empreinte et entités identiques au milieu d'un déplacement ; reprise couplée `resA-299` → 300 ticks = `44c2ea085d112e05`, égal au run continu de 600 ticks | ✅ |
| Run R2 (SYNE actuel + PRISM) reproductible | 50 agents × 5 000 ticks : `minds = 50/50`, 250 000 observations positionnées, empreinte `0bf833f4bcfc1e5e` reproduite sur **trois hôtes distincts** | ✅ |
| Analyseur : aucun flottant dans `Core` | `WorldArchitectureTests.CoreSourcesPassDeterminismGuard` : `float`, `double`, `Random`, `DateTime.Now` interdits dans `Prism.Core` — **1/1 vert** au 14 novembre 2026 | ✅ |

La porte était « franchie de fait » depuis le 6 novembre ; il manquait ce rapport, pas une mesure. Il consigne aussi deux éléments postérieurs à R2 : le **write-back** (SYNE écrit vers PRISM) et la **reprise couplée**, qui ne faisaient pas partie de la première passe de R2.

## 2. Méthode

Le contrat est le seul canal entre SYNE et PRISM : incarnation, tick lockstep, observations différentielles par agent, commandes typées et flux de lecture. R2 fait délibérer 50 esprits SYNE contre l'hôte réel sur 5 000 ticks ; le monde reste autoritaire côté PRISM, SYNE ne possède ni géométrie ni exécution.

```bash
# hôte
cd prism
LIVEX_SESSION_TOKEN=<token> dotnet run --project Prism.Host -c Release --no-build -- \
  --control-port 8131 --work-dir <dir> --instance-id r2 --log-dir <dir>/logs

# client couplé
cd syne
LIVEX_SESSION_TOKEN=<token> dotnet run --project Simulation.Console -c Release --no-build -- \
  --prism-url http://127.0.0.1:8131 --prism-agents 50 --prism-ticks 5000 --prism-seed 7 \
  --prism-content-hash 793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82 \
  --prism-content-dir <prism>/content/minimal
```

## 3. Résultats

### 3.1 R2 — lecture (6 novembre 2026)

| Mesure | Valeur |
| :-- | :-- |
| Agents / ticks | 50 / 5 000 |
| Esprits incarnés | **50/50** |
| Observations positionnées | **250 000** (50 × 5 000) |
| Empreinte | `0bf833f4bcfc1e5e` sur **trois hôtes distincts** |
| Critère de succès (`minds > 0`) | atteint |

La première tentative (`079b736c91aae957` × 3) était **non concluante** : `minds = 0`, faute de commande `Incarner`. Le correctif (instantané de monde → incarnation) et l'ADR-013 (1 bloc = 1 m = 1 unité SYNE, PRISM autoritaire sur la géométrie) ont produit la passe retenue. Ce faux positif reste consigné : il montre qu'un run « reproductible » peut mesurer un état local sans couplage.

### 3.2 Write-back — SYNE écrit vers PRISM (6 novembre 2026)

R2 ne validait que le sens lecture. Le write-back a été vérifié séparément, 10 agents × 1 500 ticks, graine 7 :

| Profil | Commandes | `action_completed` | `path_not_found` | Déplacements | Désirs `Idle` | Empreinte |
| :-- | --: | --: | --: | --: | --: | :-- |
| référence | 239 | 259 | 6 | 2 386 | 710 / 14 945 (**4,75 %**) | `449942918aec7c4c` × 3 |
| ancien `prism` | 388 | 391 | 22 | 3 832 | **57 %** | `b544dadd2d55f676` × 3 |

Le profil `prism` ralentissait les désirs (cadence 6 TPS, ≈ 5 s/tick) : il a été retiré au profit du profil de référence. `not_adjacent = 0` après correction de la règle d'adjacence (un voisin praticable à n'importe quelle altitude faisait échouer 31 boissons sur 31).

### 3.3 Reprise couplée

Checkpoint SYNE `resA-299` → restauration → 300 ticks : empreinte `44c2ea085d112e05`, **identique au run continu de 600 ticks**. La reprise ne rejoue pas l'incarnation et conserve l'horizon d'empreinte (`startTick + ticksCompleted`), ce qui rend les deux runs comparables.

### 3.4 Contrat

| Liaison | Couverture | Résultat |
| :-- | :-- | :-- |
| Embarquée (`IWorldHost` direct) | 21 kinds de commande + rejets | vert |
| HTTP (contrôle et données) | mêmes cas, enveloppes versionnées | vert |
| WebSocket (lockstep et lecture) | mêmes cas, resynchronisation par snapshot | vert |

Le gel `1.0.0` a été posé après vérification que la campagne dorée réduite de `Prism.Bots.Tests` rend des empreintes **identiques avant et après** l'optimisation du tick : le contrat n'a pas bougé sous couvert de performance.

## 4. Ce que G3 ne couvre pas

| Sujet | Pourquoi | Où il est traité |
| :-- | :-- | :-- |
| **Déterminisme interplateforme** (Windows/Linux) | reporté en fin de projet par décision du propriétaire, pour ne pas ralentir le flux sur une seule plateforme validée | PRISM-079, G2 |
| Débit du transport | G3 valide la sémantique, pas le budget ; le JSON coûte 576 ms à 1 000 agents | G1, PRISM-142/ADR-014 |
| Write-back à 100 agents sur 5 000 ticks | la passe write-back compte 10 agents ; R3 (G5) le couvre à l'échelle | G5, [`G5-R3-PROCEDURE.md`](G5-R3-PROCEDURE.md) |
| Compression du flux | non requise par le contrat, requise par le budget | PRISM-143 |

Ces sujets ne rouvrent pas G3 : ils portent sur le coût et la portabilité, pas sur la tenue du contrat.

## 5. Verdict

**G3 franchie.** Le contrat est gelé, couvert sur les trois liaisons, la cognition SYNE le consomme et l'alimente, R2 est reproductible sur trois hôtes, la reprise couplée redonne l'empreinte du run continu, et aucun flottant ne peut entrer dans `Prism.Core` sans faire échouer le garde-fou d'architecture.

Restent ouverts, hors périmètre de cette porte : le job Windows/Linux (PRISM-079, G2) et les budgets de performance (G1). Le rapport de G3 ne les absorbe pas ; il les nomme.

## 6. Provenance

| Élément | Référence |
| :-- | :-- |
| Contrat | [`API_CONTRACTS.md`](API_CONTRACTS.md) §8, `ProtocolVersion.Current = 1.0.0` |
| Run R2 | [`TESTING.md`](TESTING.md) §4, ligne R2 ; [`ISSUES.md`](ISSUES.md) PRISM-082 |
| Write-back | [`ISSUES.md`](ISSUES.md) PRISM-082 |
| Persistance | [`ISSUES.md`](ISSUES.md) PRISM-076 ; `CoupledCheckpointStoreTests` |
| Déterminisme | `prism/Prism.Architecture.Tests/WorldArchitectureTests.cs`, garde-fou exécuté le 14 novembre 2026 |
