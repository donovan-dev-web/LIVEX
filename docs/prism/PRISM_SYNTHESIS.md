# PRISM_SYNTHESIS — Synthèse de PRISM V2

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION V2 AVANCÉE — **P0–P9 CLÔTRÉES (P9 LE 15 NOVEMBRE 2026)**, P10 EN COURS (ECHOS ET LAUNCHER LIVRÉS ; INSTALLATEUR OUVERT), P11 (VIEWER) OUVERT ; **G4 FRANCHIE LE 9 NOVEMBRE 2026 (5/5 CHAÎNES PAR LES BOTS, AUDIT VERT)** ; **G5 FRANCHIE LE 14 NOVEMBRE 2026 (R3 REPRODUCTIBLE)** ; G1 OUVERTE NON BLOQUANTE]
**Dernière mise à jour** : 15 novembre 2026
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

**Où en est-on (15 novembre 2026).** Les phases P0–P9 sont clôturées (P9 le 15 novembre 2026), P10 est livré côté ECHOS (ingestion de production, moteur 6, quatre indicateurs, comparaison de runs) et la suite PRISM est verte (**487/487**). Les cinq chaînes sont jouées par des bots (G4) ; la boucle couplée SYNE↔PRISM est reproductible sur 5 000 ticks (R2) et 100 agents × 5 000 ticks (R3, G5). Le coût de l'hôte reste au-dessus des budgets de G1 (431,5 ms/tick à 100 agents, dominés par le pathfinding), sans bloquer l'implémentation. Le viewer (P11) est le chantier ouvert : meshing et client de lecture existent, mais l'atlas, les entités live, le suivi/coupe, l'inspecteur à sept calques, le mode `--connect`/embarqué/rejeu, les interventions journalisées, le test d'indépendance du rendu et le manifeste B8 restent à livrer. Le calibrage et le rapport expérimental (G7) sont reportés après l'implémentation complète.

## Ce qui est incertain. Le budget réel du modèle 2.5D (porte G1) ; l'émergence de la coopération (porte G7, résultat négatif possible et publiable).

**Pour aller plus loin** : [`VISION.md`](VISION.md) · [`ARCHITECTURE.md`](ARCHITECTURE.md) · [`API_CONTRACTS.md`](API_CONTRACTS.md) · [`ROADMAP.md`](ROADMAP.md).
