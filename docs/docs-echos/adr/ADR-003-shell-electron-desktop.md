# ADR-003 : Shell Electron et empaquetage de bureau (.exe / .deb)

**Composant** : ECHOS
**Statut** : [Accepted]
**Dernière mise à jour** : 29 septembre 2026
**Dépend de** : `ADR-001-stack-applicative.md`
**Source Monographie** : §4.2.1 (application de bureau), Annexe K (feuille de route V2)

---

## Contexte

La V0.1 d'ECHOS est un **web local** : l'interface `echos-ui` (React + Vite) est
pilotée par l'utilisateur via un navigateur, et le backend FastAPI est lancé à
la main. L'`ADR-001` a **conservé** le shell Electron en **différant** son
implémentation (correction du 23/09/2026). Ce palier est franchi ici : ECHOS doit
devenir un **exécutable distribuable** (`.exe` Windows, `.deb` Linux), sans que
l'utilisateur installe Python ni lance un serveur lui-même.

La contrainte structurante est le **backend Python** (analyse, ingestion, API) :
contrairement à une application Electron classique, le cœur n'est pas du
JavaScript. Il doit voyager avec l'application.

## Décision

Le shell de bureau vit dans `echos/echos-desktop/` et repose sur quatre choix :

1. **Le backend Python est un enfant du processus Electron.** `electron/main.js`
   choisit un **port TCP libre**, lance le backend sur `127.0.0.1`, attend que
   `GET /health` réponde 200, puis ouvre la fenêtre. Le backend est terminé à la
   fermeture de l'application (SIGTERM, puis SIGKILL après 3 s).

2. **L'interface est servie par FastAPI, pas par Electron.** `echos.server`
   monte le build `echos-ui/dist` via un `StaticFiles` à repli *SPA* (routeur
   React) à la racine, **après** les routes `/api/*`. Electron charge
   `http://127.0.0.1:<port>/`.
   - Conséquence directe : **même origine** → l'interface appelle `/api/*` en
     relatif (`API_BASE`/`CONTROL_BASE` valent `''` par défaut) et **aucun CORS
     n'est requis**. C'est le modèle « production : build statique servi par
     FastAPI » déjà annoncé par l'`ADR-001`, désormais implémenté.

3. **Le backend est empaqueté par PyInstaller en mode *onedir*.**
   - *onedir* plutôt que *onefile* : pas d'extraction en dossier temporaire au
     lancement (démarrage à froid, antivirus Windows).
   - `pyarrow` est collecté intégralement (`collect_all`) car importé
     dynamiquement par le stockage Parquet.

4. **L'assemblage final est fait par electron-builder** : `.deb` (Linux) et
   NSIS `.exe` (Windows), en embarquant `resources/backend/` (binaire PyInstaller)
   et `resources/ui/` (build de l'interface) via `extraResources`.
   - PyInstaller **ne cross-compile pas** : la CI construit chaque OS sur son
     runner (matrice `ubuntu-latest` / `windows-latest`), sur tag `echos-v*`.

La fenêtre est créée avec `contextIsolation: true`, `sandbox: true`,
`nodeIntegration: false` et un preload minimal : l'interface n'a besoin ni de
Node ni des API Electron, tout passe par HTTP.

## Conséquences

### Positives
- Un seul artefact par plateforme ; aucune installation de Python côté utilisateur.
- Origine unique : pas de configuration d'URL, pas de CORS.
- Le même backend sert le mode navigateur (dev, PRISM) et le mode bureau : une
  seule base de code, un seul contrat.
- `gh-pages`/CI et le modèle « web local » restent valides sans régression.

### Négatives
- Taille de paquet importante (runtime Python + `pyarrow` + Electron).
- Le binaire backend est **spécifique à la plateforme** et à l'architecture.
- Démarrage à froid supérieur à un serveur déjà lancé (latence masquée par
  l'attente de `/health` avant l'ouverture de la fenêtre).

### Risques
- **Conflit de port** : neutralisé par la recherche d'un port libre.
- **Backend qui meurt** : la mort inattendue de l'enfant ferme l'application avec
  un message d'erreur.
- **Antivirus Windows** : atténué par *onedir* (pas d'auto-extraction).
- **Empreinte** : `pyarrow` complet alourdit le binaire ; à réévaluer si la
  taille devient un problème (instantané Parquet optionnel en V0.1).

## Validation / rejet

- Réouverture si l'on opte pour un runtime Python embarqué partagé
  (`python-build-standalone`) plutôt que PyInstaller, ou si PRISM devient le
  seul hôte de l'interface (le shell Electron serait alors redondant).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 29 septembre 2026 | Création — implémentation du shell Electron (p.10 ROADMAP) | ECHOS doit être distribuable en `.exe`/`.deb` |
