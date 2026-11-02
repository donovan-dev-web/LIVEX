# PRISM_SYNTHESIS — Synthèse de PRISM V2

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`README.md`](README.md)

---

## En une page

**Quoi.** PRISM V2 est le composant de LIVEX qui porte le **monde** : un monde voxel borné (256 × 256 × 64, chunks 16³), déterministe, décrit par des **données** (matériaux, objets, recettes, gabarits). Les agents (SYNE) émettent des intentions ; PRISM les exécute sur plusieurs ticks et renvoie observations et événements.

**Pourquoi.** Enrichir ce que les agents peuvent faire (extraction, outils, agriculture, stockage, construction), créer des raisons structurelles de coopérer, alléger SYNE du monde, et donner à LIVEX une identité visuelle.

**Comment.** .NET : noyau headless sans dépendance graphique, hôte avec contrôle HTTP et pas de verrouillage par tick, visualiseur Silk.NET OpenGL facultatif. Arithmétique entière et hasard à compteur pour un déterminisme bit-à-bit, y compris entre plateformes.

| Chiffre | Valeur (hypothèse de départ) |
| :-- | :-- |
| Blocs | 4 194 304 (≈ 12,6 Mo) |
| Chunks | 1 024 de 4 096 blocs |
| Agents | 100 (référence), 1 000 (charge) |
| Cibles headless | ≥ 20 ticks/s à 100 agents, ≥ 5 à 1 000 |
| Chaînes de production | 5 (outils, agriculture, eau, transformation, stockage) |
| Mécanismes de coopération testés | 7 (ablations A1…A8) |
| Phases / portes | 14 / 9 |

**Ce qui est incertain.** Le budget de performance 3D (porte G1) ; l'émergence de la coopération (porte G7, résultat négatif possible et publiable).

**Pour aller plus loin** : [`VISION.md`](VISION.md) · [`ARCHITECTURE.md`](ARCHITECTURE.md) · [`API_CONTRACTS.md`](API_CONTRACTS.md) · [`ROADMAP.md`](ROADMAP.md).
