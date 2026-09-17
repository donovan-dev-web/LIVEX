# CI/CD & Déploiement (Phase 11)

Automatisation de la livraison du projet SSE (Emergent Simulation). Dépôt :
`github.com/donovan-dev-web/SSE-emergent-simulation`, branche par défaut `main`.

## 1. Pipeline CI — `.github/workflows/ci.yml`

Déclenché sur chaque **PR** et sur **push sur `main`**. Trois jobs en parallèle :

| Job | Étapes | Commande clé |
|-----|--------|--------------|
| **dotnet** | restore, build (Release, deux solutions), format, tests + coverage seuillés, upload cobertura | `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test --collect:"XPlat Code Coverage" --settings ci/coverlet.*.runsettings` |
| **web** | `npm ci`, lint, format:check, tests, build, upload `dist/` | `npm run lint`, `npm run format:check`, `npm run test`, `npm run build` |
| **godot** | build assembly C# + import headless (validation projet) | `dotnet build godot-renderer/*.csproj`, `godot --headless --import --quit` |

**Gate de couverture** : `ci/coverlet.sim.runsettings` / `ci/coverlet.an.runsettings`
incluent `Threshold=80`, `ThresholdType=line`, `ThresholdStat=total`. Le job `dotnet`
**échoue** si `Simulation.Core` ou `Analyzer.Core` < 80 % de lignes (validé localement :
90,3 % / 85,0 %). Le SDK .NET est épinglé par `global.json` (10.0.400).

## 2. Lint / format

- **C#** : `.editorconfig` racine (règles de style + nommage) ; `dotnet format`
  appliqué une fois pour normaliser le dépôt, puis vérifié en CI
  (`--verify-no-changes`, échec si écart).
- **TS/JS** : ESLint (flat config `web-ui/eslint.config.js`) + Prettier
  (`web-ui/.prettierrc.json`, `.prettierignore`) ; scripts `lint` / `format` /
  `format:check` dans `web-ui/package.json`.

## 3. Artefacts — Docker

Dockerfiles multi-étages, contexte de build = **racine du dépôt** (`.dockerignore`
exclut `bin`/`obj`/`node_modules`/`dist`/`.godot`/`TestResults`) :

| Service | Dockerfile | Image | Port |
|---------|-----------|-------|------|
| Moteur sim | `simulation-core/Simulation.Console/Dockerfile` | SDK .NET 10 → runtime 10, `serve 5180 --control-port 5181` | 5180/5181 |
| Analyzer  | `analyzer/Analyzer.Service/Dockerfile` | SDK .NET 10 → ASP.NET 10, `ANALYZER_SIM_URL`/`ANALYZER_REST_URL` | 5000 |
| Web UI    | `web-ui/Dockerfile` | node:20 → nginx (serve `dist/`) | 80 |

`compose.yml` orchestre l'empilement complet : `sim` + `analyzer` (dépend de `sim`) +
`web-ui` (dépend de `analyzer`).

```bash
docker compose build
docker compose up -d
docker compose logs -f
```

## 4. Artefact — Godot renderer

Le « build Godot » en CI est **reproductible et validé** : compilation de l'assembly
C# (Release) + import headless du projet (détecte une scène/script C# cassé). La
release empaquette le projet + l'assembly en `.tgz`. Un export exécutable
(distribution) nécessite les **export templates** Godot + un `export_presets.cfg` —
volontairement **hors périmètre V1** pour ne pas risquer de casser l'éditeur du
développeur (cf. `docs/V1/17-RENDERER-3D.md`).

## 5. Release / versioning — `.github/workflows/release.yml`

Déclenché par un tag **SemVer** (`v1.0.0`, `on.push.tags: v*`). Version extraite du
tag (`${GITHUB_REF_NAME#v}`). Étapes :

1. Build + tests + coverage (.NET) et build/lint/test Web UI (re-vérification).
2. **Docker** : images `sim`, `analyzer`, `web-ui` construites et poussées sur
   **GHCR** (`ghcr.io/<owner>/sse-emergent-simulation/<img>`) taguées `version` + `latest`.
3. **Godot** : assembly Release + import headless, projet empaqueté en `.tgz`.
4. **GitHub Release** : notes générées automatiquement + `web-ui/dist/*` attachés.

### Convention de versionnage

- Tags `vM.m.p` (SemVer). Le `schemaVersion` la persistance (Phase 3) reste en
  `1` ; une migration de schéma incrémenterait `schemaVersion` indépendamment de la
  version applicative.

## 6. Commandes locales de validation

```bash
# .NET : format + build + tests + couverture seuillée
dotnet format simulation-core/EmergentSimulation.slnx --verify-no-changes
dotnet format analyzer/Analyzer.slnx --verify-no-changes
dotnet test simulation-core/EmergentSimulation.slnx -c Release --collect:"XPlat Code Coverage" --settings ci/coverlet.sim.runsettings
dotnet test analyzer/Analyzer.slnx -c Release --collect:"XPlat Code Coverage" --settings ci/coverlet.an.runsettings

# Web UI
cd web-ui
npm ci
npm run lint
npm run format:check
npm run test
npm run build
```
