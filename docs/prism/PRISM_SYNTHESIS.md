# PRISM_SYNTHESIS — Synthèse de PRISM V2

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION V2 EN COURS — P2 ; G1 EN ATTENTE]
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : [`README.md`](README.md)

---

## En une page

**Quoi.** PRISM V2 est le composant de LIVEX qui porte le **monde** : un monde borné 2.5D (256 × 256 colonnes, régions horizontales 16², altitude de surface et strates déterministes), décrit par des **données** (matériaux, objets, recettes, gabarits). Les agents (SYNE) émettent des intentions ; PRISM les exécute sur plusieurs ticks et renvoie observations et événements.

**Pourquoi.** Enrichir ce que les agents peuvent faire (extraction, outils, agriculture, stockage, construction), créer des raisons structurelles de coopérer, alléger SYNE du monde, et donner à LIVEX une identité visuelle.

**Comment.** .NET : noyau headless sans dépendance graphique, hôte avec contrôle HTTP et pas de verrouillage par tick, visualiseur Silk.NET OpenGL facultatif. Arithmétique entière et hasard à compteur pour un déterminisme bit-à-bit, y compris entre plateformes.

| Chiffre | Valeur (hypothèse de départ) |
| :-- | :-- |
| Colonnes | 65 536 (≈ 384 Kio de métadonnées de surface denses) |
| Régions | 256 de 16 × 16 colonnes ; modifications en couches clairsemées |
| Agents | 100 (référence), 1 000 (charge) |
| Cibles headless | ≥ 20 ticks/s à 100 agents, baseline indicative 4 ticks/s à 1 000 (G1 : ≥ 3) |
| Chaînes de production | 5 (outils, agriculture, eau, transformation, stockage) |
| Mécanismes de coopération testés | 7 (ablations A1…A8) |
| Phases / portes | 14 / 9 |

**Ce qui est incertain.** Le budget réel du modèle 2.5D (porte G1) ; l'émergence de la coopération (porte G7, résultat négatif possible et publiable).

**Pour aller plus loin** : [`VISION.md`](VISION.md) · [`ARCHITECTURE.md`](ARCHITECTURE.md) · [`API_CONTRACTS.md`](API_CONTRACTS.md) · [`ROADMAP.md`](ROADMAP.md).
