# API_REST.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.7

---

## 1. Positionnement

API REST **locale** d'ECHOS (FastAPI en V0.1 — voir `ARCHITECTURE.md`), port **5000**. Elle sert l'interface ECHOS (intégrée) et expose les données d'analyse. Elle est distincte du contract de contrôle de SYNE (HTTP :5181) et de transport de SYNE (WS :5180).

## 2. Les endpoints principaux

| Méthode | Endpoint | Description |
| :-- | :-- | :-- |
| GET | `/health` | Vérification de santé du service |
| GET | `/api/runs` | Liste des runs enregistrés |
| GET | `/api/runs/{id}` | Métriques complètes du run |
| GET | `/api/runs/{id}/metrics` | Dernières métriques (JSON) |
| GET | `/api/runs/{id}/export` | Export des métriques (CSV/JSON) |
| GET | `/api/compare?a={run1}&b={run2}` | Comparaison de deux runs |
| GET | `/api/beliefs/{agentId}` | Croyances de l'entité au tick courant |
| GET | `/api/relationships/{agentId}` | Réseau de confiance de l'entité |
| GET | `/api/groups` | Liste des groupes actifs |
| GET | `/api/emergent-phenomena` | Phénomènes émergents détectés |
| GET | `/api/communication-heatmap` | Heatmap des communications entre entités |

(Source : Monographie §4.7.1)

## 3. La diffusion temps réel

- Prototype : ECHOS diffusait les métriques en temps réel via WebSocket (`ws://localhost:5180/metrics`) vers l'interface.
- **V0.1** : l'interface étant **intégrée à ECHOS**, cette diffusion alimente directement les vues d'analyse (consommation temps réel des `snapshot`/`event` de SYNE + métriques calculées en ligne).

## 4. L'optimisation

- **Agrégation incrémentale** : les métriques sont calculées à chaque snapshot au moment de l'ingestion (aucun recalcul complet).
- **Cache de séries** : les métriques par run sont mises en cache et invalidées uniquement sur ajout/mort d'entité.
- **Parallélisation** : les calculs lourds (co-localisation O(n²), plus proche ressource) sont parallélisés.
- **Sous-échantillonnage** : `--sample-every=N` pour ne garder que 1 snapshot sur N ; `?every=N` pour retourner des séries sous-échantillonnées.

---

## Points restés ouverts dans ce document
- Le schéma JSON exact des réponses (types, pagination des séries) sera stabilisé à l'implémentation ; la liste des endpoints ci-dessus est la référence contractuelle.
- Compatibilité de versionnage des réponses à aligner sur `VERSIONING.md` (évolutions additives = MINOR).