# PACKAGING — Publication de PRISM V2

**Composant** : PRISM
**Statut** : [DISTRIBUTION V2 IMPLÉMENTÉE — HÔTE, VIEWER ET PACK PUBLIÉS ; RECETTE MACHINE PROPRE OUVERTE]
**Dernière mise à jour** : 15 novembre 2026
**Dépend de** : `../launcher/PACKAGE_FORMAT.md`, `../installer/COMPONENTS.md`, `../../VERSIONING.md`
**Source Monographie** : —

---

## 1. Entrées de release

| Entrée | Contenu | Cibles |
| :-- | :-- | :-- |
| `prism-host` | `Prism.Host` publié **autonome** (self-contained), pack de contenu par défaut, `component.json` | Windows x64, Linux x86-64 |
| `prism-viewer` | `Prism.Viewer` publié autonome ; dépendances natives GLFW | Windows x64, Linux x86-64 |
| `prism-content` | Pack de contenu par défaut seul (mise à jour indépendante) | Toutes |

Les assets graphiques du viewer (atlas, palettes, police, captures) sont spécifiés dans [`ASSETS.md`](ASSETS.md). `publish_prism_v2.py` copie `prism/assets/` en `prism-viewer/bin/assets/` (à côté de l'exécutable) lorsqu'il existe ; ils ne forment pas une entrée séparée et le binaire reste fonctionnel sans eux.

Le tag est `prism-v2.0.0` (`VERSIONING.md` §4) ; il héberge les entrées consommées par la CI. Les paquets Unreal (`prism-v1.x`) restent disponibles jusqu'à la release `2.0.0`.

## 2. Manifestes Launcher (schéma v1)

`component.json` de l'hôte (extrait) :

```json
{
  "schema": 1,
  "id": "prism-host",
  "name": "PRISM World Host",
  "type": "world",
  "version": "2.0.0-preview.1",
  "runtime": "native",
  "executable": { "windows": "bin/prism-host.exe", "linux": "bin/prism-host" },
  "workingDirectory": ".",
  "endpoints": {
    "control": { "transport": "http" },
    "readStream": {
      "transport": "websocket",
      "launchArgument": "--read-port",
      "path": "/data/read"
    }
  },
  "capabilities": ["headless", "snapshotStream"],
  "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
  "timeouts": { "startupMs": 30000, "shutdownMs": 5000 },
  "protocolVersion": 1,
  "contributesTo": [],
  "requires": []
}
```

Le Launcher résout l'endpoint `readStream` par son `path` (`/data/read`) et transmet `--read-port` ; `prism/scripts/publish.sh` produit les binaires autonomes du host et du viewer (`win-x64`/`linux-x64`).

Le visualiseur a désormais son manifeste distinct, `prism/viewer.component.json` (PRISM-118) :

```json
{
  "schema": 1,
  "id": "prism-viewer",
  "name": "PRISM Viewer",
  "type": "viewer",
  "version": "2.0.0-preview.1",
  "runtime": "native",
  "executable": { "windows": "bin/Prism.Viewer.exe", "linux": "bin/Prism.Viewer" },
  "workingDirectory": ".",
  "endpoints": {
    "control": { "transport": "http", "launchArgument": "--control-port" },
    "readStream": { "transport": "websocket", "launchArgument": "--connect", "path": "/data/read" }
  },
  "capabilities": ["renderCadence", "readOnlyClient", "replay"],
  "health": { "probe": "process", "intervalMs": 1000 },
  "timeouts": { "startupMs": 15000, "shutdownMs": 5000 },
  "protocolVersion": 1,
  "contributesTo": ["immersion"],
  "requires": ["prism-host"],
  "consumes": {
    "readStream": { "from": "prism-host", "launchArgument": "--connect" },
    "control": { "from": "prism-host", "launchArgument": "--control" }
  }
}
```

`scripts/release/publish_prism_v2.py` le copie en `prism-viewer/component.json` à côté du binaire et vérifie sa version contre `installer/build/components.json`. Le Launcher peut ainsi résoudre l'URL du producteur et transmettre `--connect`.

Le viewer expose en plus un endpoint `control` HTTP (`GET /health/ready`, `GET /info`, `POST /control/play|pause|stop|quit|shutdown|intervention`). C'est le **plan de contrôle** utilisé par le Launcher : les commandes de session sont relayées à `prism-host`, qui reste l'autorité du monde. La capacité `readOnlyClient` reste exacte : le viewer ne modifie jamais l'état du monde et ne décide d'aucune commande de simulation ; il ne fait que transmettre les ordres de l'utilisateur. Les exigences de PRISM du Launcher (`INTEGRATION_CONTRACT.md` §11) sont **réécrites** : *autorité du monde*, *flux de lecture*, *indépendance du rendu*, *plan de contrôle*.

## 3. Intégration au format de paquet `.livexp`

Un paquet d'expérience enregistre, en plus de la configuration SYNE : la configuration PRISM, le **`contentHash`** et la version du pack, les empreintes de run. Un résultat n'est comparable que si ces éléments sont identiques (sauf expérience déclarée).

## 4. Installateur

Les composants PRISM remplacent les paquets Unreal ; leur taille est **sensiblement plus petite**. Options d'installation : *hôte seul* (par défaut), *visualiseur* (facultatif, dépendances GL), *packs de contenu*. ADR-010 de l'installateur (production d'Unreal hors CI) devient caduc pour PRISM.

## 5. CI

| Job | Contenu |
| :-- | :-- |
| `prism-build` | Build + analyseur d'architecture (pas de GL dans `Core`, pas de flottant) |
| `prism-test` | Tests unitaires, propriétés, contenu, contrat, intégration |
| `prism-determinism` | Rejeu + parallélisme + Windows/Linux |
| `prism-bench` | B1…B6 ; échec si dérive > seuil |
| `prism-render` (optionnel) | Rendu logiciel, captures |
| `prism-publish` | Publication autonome, notices tierces, signature (si adoptée) |

## 6. Notices tierces

Toute dépendance (Silk.NET, bibliothèque d'interface, SQLite, outils de test) est ajoutée à `THIRD-PARTY-NOTICES.md` avec **notices transitives** (dette déjà identifiée dans `docs/governance/ROADMAP.md` §7), vérifiées avant toute publication binaire.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 15 novembre 2026 | Manifeste du viewer (`prism/viewer.component.json`) publié avec le binaire et consommateur explicite de `prism-host` | PRISM-118 |
| 3 novembre 2026 | Exemple `component.json` aligné sur le manifeste réel (`prism-host`, endpoint `readStream.path`) et `publish.sh` documenté | PRISM-080 |
| 1er novembre 2026 | Création | Refonte V2 |
