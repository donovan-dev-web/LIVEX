# ADR-001 : Choix de la stack applicative ECHOS (FastAPI + React/Vite web local)

**Composant** : ECHOS
**Statut** : [Accepted]
**Dernière mise à jour** : 23 septembre 2026
**Dépend de** : —
**Source Monographie** : §4.2.1 (architecture cible prototype)

---

## Contexte

La Monographie prévoit une **Application Django** et une interface web React avec un shell Electron. Le prototype analysait néanmoins ses métriques en C#/.NET. La stack V0.1 doit être légère, locale et maintenable dans un monorepo à 3 composants.

## Décision

**V0.1** : ECHOS est **React/TypeScript sur web local servie par FastAPI** (interface) + **FastAPI** (Application/API, PAS Django) + **NumPy/Pandas/SciPy/NetworkX** (analyse) + **ECharts/Plotly** (visualisation) + **SQLite/Parquet** (stockage d'analyse).

L'interface est un site **web local** (`echos-ui`, React + Vite) servie par FastAPI : navigateur ouvert sur `http://127.0.0.1:5000` (ou via PRISM). FastAPI sert le build (dev via `npm run dev`, production en statique). Pour **V0.1** aucun wrapper bureau n'est requis. Le **shell Electron initialement envisagé est conservé**, mais son implémentation est **différée à un horizon ultérieur (post-V0.1)** : il **n'est pas abandonné** (correction décision 23/09/2026, cf. `Mises à jour`).

C'est une **divergence assumée** vs la Monographie, documentée dans `ARCHITECTURE.md`, `../ARCHITECTURE.md` (racine) et ce document.

## Conséquences

### Positives
- Stack légère et **locale**, en phase avec l'interface V0.1 ; aucune dépendance de packaging de bureau.
- NumPy/Pandas/NetworkX = références pour les calculs scientifiques (entropie, Louvain, graphes).
- ECharts/Plotly ≠ dépendance à un framework lourd de rendu serveur.

### Négatives
- Écart avec la documentation prototype (Django **et** shell Electron) → nécessite d'expliquer la divergence partout.
- FastAPI doit maintenir l'équivalent de l'API REST prévue (§4.7, port 5000) **et servir l'interface web**.

### Risques
- Double maintien calculs Python vs spécifications §4.3 (évitée par tests/golden files).

## Alternatives considérées

- **Django (cible Monographie)** : surdimensionné pour une API locale ; écart conservé uniquement comme référence.
- **Prototype C#/.NET analyzer** : diviserait la stack en deux langages pour l'analyse → abandonné au profit d'un seul langage scientifique (Python).
- **Shell Electron (post-V0.1)** : initialement retenu pour le « mono-app desktop », il est **conservé** — le web local React/Vite (dev `echos-ui`, production servie par FastAPI) suffit pour V0.1 ; l'implémentation du shell Electron (packaging natif de bureau, intégration PRISM) est **différée à un horizon ultérieur**, documentée dans `ROADMAP.md`.

## Validation / rejet

- Réouverture si le besoin d'un serveur métier distant apparaît (migration Django possible, documentée dans `ROADMAP.md`).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |
| 23 septembre 2026 | Décision « web local pour V0.1 » : React/Vite **servi par FastAPI** en V0.1 | Décision utilisateur (interface web locale React/Vite pour V0.1) ; aligne ADR sur `echos-ui` réel |
| 23 septembre 2026 | **Correction** : le shell Electron est **conservé**, implémentation **différée à un horizon ultérieur (post-V0.1)** | La lecture initiale « PAS de shell Electron » était une reformulation erronée — ECHOS garde Electron, implémenté plus tard. Docs alignées (ADR, `ARCHITECTURE.md`, `FRONTEND_VISION.md`, `ROADMAP.md`, `ISSUES.md`, `CHANGELOG.md`, docs racine). La Monographie reste un snapshot figé. |