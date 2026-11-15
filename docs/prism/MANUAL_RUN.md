# Lancer PRISM à la main

**Objet** : commandes complètes, copiables, pour voir le viewer et le relier à un hôte, sans passer par le Launcher.
**Date de vérification** : 15 novembre 2026 (Windows, bash Git for Windows).
**Prérequis** : SDK .NET 10 (`global.json`), dépôt à jour, session graphique avec OpenGL 3.3 pour toute commande qui ouvre une fenêtre.

Tous les chemins partent de la racine du dépôt. Le viewer charge automatiquement `prism/assets/` (atlas, palettes, police) ; `--assets <dir>` impose un autre dossier.

---

## 1. Les quatre recettes

| Recette | Ce qu'elle montre | Hôte requis |
| :-- | :-- | :-- |
| Aperçu statique | Monde généré, agents de démonstration | non |
| Embarqué | Hôte PRISM hébergé dans le viewer, ticks réels | non (interne) |
| Client `--connect` | Viewer branché sur un hôte séparé ; le monde n'avance qu'avec `POST /data/tick` | oui |
| SYNE + PRISM couplés | Cognition SYNE pilotant le monde PRISM | oui |

---

## 2. Aperçu statique (le plus rapide)

```bash
cd prism
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --seed 12345 \
  --size 96
```

`--no-demo-agents` masque les agents d'aperçu. `--assets ../tmp/mon-pack` force un autre dossier d'assets. `F12` écrit une capture PNG, ou `--screenshot <fichier> --screenshot-frames 25` en ligne de commande.

## 3. Mode embarqué

```bash
cd prism
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --embedded \
  --content content/minimal \
  --seed 12345 \
  --ticks-per-second 20
```

Le viewer compile le pack, prépare le monde, démarre l'horloge et consomme son propre flux. Le HUD affiche `MODE: EMBEDDED HOST`. C'est le mode à utiliser pour une démonstration sans orchestrateur.

## 4. Hôte séparé + viewer client

### 4.1 Démarrer l'hôte (terminal 1)

```bash
cd prism
LIVEX_SESSION_TOKEN=dev dotnet run --project Prism.Host -- \
  --control-port 8131 \
  --read-port 8132 \
  --work-dir ../tmp/prism-manual/work \
  --log-dir ../tmp/prism-manual/logs \
  --instance-id manual \
  --correlation-id manual \
  --headless
```

`--read-port` doit différer de `--control-port`. `--headless` exige les quatre options `--work-dir`, `--log-dir`, `--instance-id` et `--correlation-id`.

### 4.2 Préparer puis démarrer le monde (terminal 2)

L'hôte démarre en `idle` : il ne génère rien tant qu'on ne lui donne pas un pack, une graine et le `contentHash` de ce pack. Le hash du pack `minimal` livré est stable ; il change dès qu'un fichier du pack change.

```bash
# Hash du pack (le programme l'imprime et sort) :
cd prism && dotnet run --project Prism.Host -- --content-hash content/minimal
# → 793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82
```

```bash
curl -s http://127.0.0.1:8131/health/ready   # 503 {"error":"not_ready"} tant que rien n'est préparé

curl -s -X POST -H "Content-Type: application/json" \
  -H "X-LIVEX-Session-Token: dev" \
  -d '{"seed":12345,
       "contentHash":"793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82",
       "contentDirectory":"'$PWD'/content/minimal"}' \
  http://127.0.0.1:8131/control/prepare

curl -s -X POST -H "Content-Type: application/json" \
  -H "X-LIVEX-Session-Token: dev" -d '{}' \
  http://127.0.0.1:8131/control/start
```

`content_hash_mismatch` signifie que le hash ne correspond pas au dossier : relancer `--content-hash` sur le bon dossier, ne pas improviser.

### 4.3 Ouvrir le viewer client (terminal 3)

```bash
cd prism
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --connect ws://127.0.0.1:8132/data/read
```

Options utiles : `--record ../tmp/flux.jsonl` enregistre le flux consommé (rejeu avec `--replay`), `--screenshot <fichier>` capture après `--screenshot-frames` images, `--control-port <port>` ouvre le plan de contrôle HTTP décrit ci-dessous.

**Sans `--autostart` ni pilote, l'hôte ne s'anime pas.** `/control/start` passe l'état à `running`, mais le monde n'avance qu'à chaque `POST /data/tick` (ou à la cadence de `--ticks-per-second`). Le viewer ne modifie jamais le monde : il relaie seulement `play|pause|stop|quit` à l'hôte. Sans pilote, le HUD reste à `TICK: 0` et le monde est figé.

Le viewer ouvre aussi un endpoint de contrôle local (`--control-port`) : `GET /health/ready`, `GET /info`, `POST /control/play`, `/control/pause`, `/control/stop`, `/control/quit`. Chaque commande est transmise à l'hôte (`--control`), qui reste l'autorité du monde. Pour animer à la main :

```bash
for tick in $(seq 0 199); do
  curl -s -o /dev/null -X POST -H "Content-Type: application/json" \
    -H "X-LIVEX-Session-Token: dev" \
    -d "{\"protocolVersion\":\"1.0.0\",\"tick\":$tick,\"commands\":[]}" \
    http://127.0.0.1:8131/data/tick
  sleep 0.05
done
```

Le numéro de tick doit être exactement le suivant (`0`, puis `1`, …) ; un saut est refusé. Pour une démonstration sans pilote, préférer le mode embarqué (§3).

### 4.4 Hôte autonome (voir le run sans pilote)

Pour une démonstration où personne n'envoie `/data/tick`, l'hôte prépare et anime lui-même le monde :

```bash
cd prism
LIVEX_SESSION_TOKEN=dev dotnet run --project Prism.Host -- \
  --control-port 8131 --read-port 8132 \
  --work-dir ../tmp/prism-manual/work --log-dir ../tmp/prism-manual/logs \
  --instance-id manual --correlation-id manual --headless \
  --autostart --ticks-per-second 20
```

`--autostart` compile le pack (par défaut `content/minimal`, ou `--content <dir>`), le prépare avec `--seed` et démarre le monde. L'horloge demande ensuite un tick séquentiel à chaque battement ; un client `--connect` voit le monde et les agents avancer sans jamais écrire. En mode couplé SYNE, laisser `--ticks-per-second` à `0` (défaut) : c'est SYNE qui pilote.

### 4.5 Arrêter proprement

```bash
curl -s -X POST -H "Content-Type: application/json" \
  -H "X-LIVEX-Session-Token: dev" -d '{}' \
  http://127.0.0.1:8131/control/shutdown
```

Un `Ctrl+C` dans le terminal de l'hôte reste possible : les checkpoints sont écrits dans `--work-dir` (SQLite).

## 5. SYNE + PRISM couplés

La procédure complète (hôte PRISM + client SYNE avec `--prism-url`, `--prism-agents`, `--prism-ticks`, `--prism-content-hash`) est décrite dans [`G5-R3-PROCEDURE.md`](G5-R3-PROCEDURE.md) §« Terminal 1 / Terminal 2 ». Elle sert aux runs de référence R2/R3, pas à l'affichage : aucun viewer n'y est branché.

Pour regarder un run couplé, ajouter un viewer sur le flux de l'hôte :

```bash
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --connect ws://127.0.0.1:8131/data/read
```

(adapter le port à celui de l'hôte lancé pour le run couplé).

## 6. Lancement depuis le Launcher

L'écran **PRISM** propose deux modes, figés tant qu'une session tourne :

| Mode | Ce qui démarre | Démarrage du run |
| :-- | :-- | :-- |
| **Hôte** | `prism-host` seul, monde préparé, horloge locale 20 ticks/s | immédiat |
| **Viewer** | `prism-host` préparé → `syne` en attente → `prism-viewer` | **Play (écran PRISM)** |

Prérequis : publier PRISM et construire le Launcher.

```bash
python scripts/release/publish_prism_v2.py --rid win-x64
cd launcher && dotnet build Launcher.sln
LIVEX_HOME="../dist/staging/win-x64" \
  ./Launcher.App/bin/Debug/net10.0/livex-launcher.exe --check
# [OK  ] composants : 5 composant(s) détecté(s), 4 valide(s)
```

Le staging publie SYNE à plat et `build-release` réécrit les chemins d'exécutable du manifeste (`scripts/release/flatten_staging_manifest.py`) : `--check` doit rendre **5 composant(s) détecté(s), 5 valide(s)** et `moteur : SYNE … présent et exécutable`. Un `moteur absent` signale un staging incomplet (binaire non publié), pas un problème du viewer.

### 6.1 Mode viewer (pilotage par le viewer)

1. `prism-host` démarre avec `--prepare` : le monde est prêt (`state: ready`, tick `-1`), mais **aucun tick** n'est exécuté ;
2. le Launcher attend `/health/ready` de l'hôte, puis lance `syne` avec `--prism-url`, `--prism-agents 12`, `--prism-ticks 1000000`, `--prism-content-dir` et `--prism-standby` : SYNE construit sa cognition et n'ouvre sa readiness qu'à ce moment ;
3. le Launcher attend la readiness de SYNE, puis lance `prism-viewer` en dernier : sa **fenêtre** s'ouvre directement (le Launcher n'ouvre pas de console pour le viewer).

La pile est alors prête et immobile. Les boutons **Play**, **Pause**, **Stop** et **Quitter le viewer**, affichés dans l'écran PRISM, envoient respectivement `/control/play`, `/control/pause`, `/control/stop` et `/control/quit` au viewer, qui les relaie à l'hôte PRISM :

- **Play** démarre le monde (`ready → running`) ; SYNE, en attente, commence ses ticks ; en pause, Play reprend (`resume`) ;
- **Pause** fige les ticks — SYNE attend la reprise au lieu d'abandonner le run ;
- **Stop** termine le run (`stopped`) et laisse le viewer ouvert ;
- **Quitter** ferme la fenêtre du viewer ; **Arrêter** (écran PRISM) arrête ensuite viewer, SYNE puis hôte.

Chaîne gatée vérifiée le 15 novembre 2026 avec les binaires publiés : hôte `ready` tick `-1`, SYNE `ready` sans tick, viewer `ready`, puis **Play** → `running` tick 338, **Pause** → 343 stable, **Play** (reprise) → 499, **Stop** → `stopped` 503, **Quit** → HTTP 202. Limite connue : après Stop, le prochain pas de SYNE est refusé (`409 invalid_state`) et son run se termine ; pour relancer, recharger le mode viewer.

## 7. Publication et assets

```bash
python scripts/release/publish_prism_v2.py --rid win-x64
```

Produit `dist/staging/win-x64/prism-host`, `prism-viewer` et `prism-content`. Les assets sont copiés dans `prism-viewer/bin/assets/` (à côté de l'exécutable, cf. `prism/viewer.component.json`) ; absents, le viewer génère tout par programme. Le pack `prism-content` n'a pas de `component.json` : c'est une donnée, pas un composant pilotable.

## 8. Ports et nettoyage

| Usage | Port par défaut |
| :-- | :-- |
| Contrôle PRISM | `--control-port` (exemple 8131) |
| Flux de lecture PRISM | `--read-port` (exemple 8132) |
| Contrôle SYNE | 5181 |
| Observabilité SYNE | 5180 |
| API ECHOS | 5000 |

Les ports 8131/8132 évitent les ports SYNE. Vérifier qu'aucun `prism-host` ne traîne avant une build Release (binaires verrouillés) :

```bash
tasklist //FI "IMAGENAME eq prism-host.exe"
taskkill //F //IM prism-host.exe
```
