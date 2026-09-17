# ADR-001 : Choix de la stack applicative ECHOS (FastAPI + Electron/React)

**Composant** : ECHOS
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : §4.2.1 (architecture cible prototype)

---

## Contexte

La Monographie prévoit une **Application Django** et une interface web React avec un shell Electron **abandonné**. Le prototype analysait néanmoins ses métriques en C#/.NET. La stack V0.1 doit être légère, locale et maintenable dans un monorepo à 3 composants.

## Décision

**V0.1** : ECHOS est **Electron + React/TypeScript** (interface) + **FastAPI** (Application/API, PAS Django) + **NumPy/Pandas/SciPy/NetworkX** (analyse) + **ECharts/Plotly** (visualisation) + **SQLite/Parquet** (stockage d'analyse).

C'est une **divergence assumée** vs la Monographie, documentée dans `ARCHITECTURE.md`, `../ARCHITECTURE.md` (racine) et ce document.

## Conséquences

### Positives
- Stack légère et locale, en phase avec le mono-app desktop.
- NumPy/Pandas/NetworkX = références pour les calculs scientifiques (entropie, Louvain, graphes).
- ECharts/Plotly ≠ dépendance à un framework lourd de rendu serveur.

### Négatives
- Écart avec la documentation prototype (Django) → nécessite d'expliquer la divergence partout.
- FastAPI doit maintenir l'équivalent de l'API REST prévue (§4.7, port 5000).

### Risques
- Double maintien calculs Python vs spécifications §4.3 (évitée par tests/golden files).

## Alternatives considérées

- **Django (cible Monographie)** : surdimensionné pour une API locale ; écart conservé uniquement comme référence.
- **Prototype C#/.NET analyzer** : diviserait la stack en deux langages pour l'analyse → abandonné au profit d'un seul langage scientifique (Python).

## Validation / rejet

- Réouverture si le besoin d'un serveur métier distant apparaît (migration Django possible, documentée dans `ROADMAP.md`).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |