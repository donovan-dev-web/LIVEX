# PACKAGING — Publication de PRISM V2

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : `../launcher/PACKAGE_FORMAT.md`, `../installer/COMPONENTS.md`, `../../VERSIONING.md`
**Source Monographie** : —

---

## 1. Entrées de release

| Entrée | Contenu | Cibles |
| :-- | :-- | :-- |
| `prism-host` | `Prism.Host` publié **autonome** (self-contained), pack de contenu par défaut, `component.json` | Windows x64, Linux x86-64 |
| `prism-viewer` | `Prism.Viewer` publié autonome ; dépendances natives GLFW | Windows x64, Linux x86-64 |
| `prism-content` | Pack de contenu par défaut seul (mise à jour indépendante) | Toutes |

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

Le visualiseur aura son manifeste distinct (`type: "viewer"`, `contributesTo: ["immersion"]`, capacité `renderCadence`), à produire avec PRISM-118. Les exigences de PRISM du Launcher (`INTEGRATION_CONTRACT.md` §11) sont **réécrites** : *autorité du monde*, *flux de lecture*, *indépendance du rendu*.

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
| 3 novembre 2026 | Exemple `component.json` aligné sur le manifeste réel (`prism-host`, endpoint `readStream.path`) et `publish.sh` documenté | PRISM-080 |
| 1er novembre 2026 | Création | Refonte V2 |
