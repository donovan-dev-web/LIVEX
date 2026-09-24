# SYSTEMS_SPEC.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `DATA_MODEL.md`, `COGNITIVE_ARCHITECTURE.md`, `SIMULATION_LOOP.md`
**Source Monographie** : Partie 3 (SYNE), Partie 6 (concepts détaillés)

---

## 1. Objectif

Spécifications fonctionnelles des **sous-systèmes** du moteur SYNE. Chaque section référence la partie de la Monographie à implémenter. Les entités logiques, ressources, groupes, conflits, héritage/fusion, livres sont traités ici à niveau système.

## 2. Sous-systèmes techniques

| Sous-système | Spécification | Source Monographie |
| :-- | :-- | :-- |
| **Spatial (grille)** | Grille uniforme ; `cellSize = sqrt(worldArea / (agentCount / 7))` (V0.1 : paramètre direct, défaut `largeur/10`) ; 9 cellules voisines (fenêtre 3×3) ; mises à jour incrémentales | §3.9.6 |
| **Perception** | Grille + filtre distance/rayon 50 ; perception étagée (`id % 4`) ; **ligne de vue** obstacle (ADR-013) | §3.9 |
| **Navigation** | Pathfinding 2D autour des obstacles, cache de chemins ; indépendant des moteurs graphiques ; appui sur Navigation2D = [HÉRITÉ] (V0.1 : SYNE calcule lui-même) | §3.19 |
| **Scheduler** | Exécution multi-fréquences + LOD décisionnel | §3.4 |
| **Événements** | Ring buffer borné (500 000 événements) ; émission `ExternalEvent` | §7.4.5 |

## 3. Monde & Environnement

- Espace 2D logique, dimensions configurables (défaut 500×500).
- Clamping des positions ; monde non-toroidal.
- Obstacles statiques (cercle en V0.1, rectangle reporté) : blocage mouvement **et** ligne de vue (perception masquée, ADR-013) — depuis le jalon SYNE ph1.
- Ressources : FoodSource / WaterSource (V1 : taux de régénération nul, eau infinie ; V2 : régénération + dégradation).
- Saisons, événements du monde, obstacles : flags de configuration (`world.seasons`, `world.events`, `world.obstacles` — prototype faux par défaut, Annexe H).

## 4. Ressources (Monographie §3.18, §6.9)

- **Contrainte d'absence** : la finitude des ressources est un moteur de comportement (compétition, stockage, déplacement).
- V2 : agents en sources (`sources d'eau et de nourriture`), dégradation, puissance de régénération.
- Représentation : entité spatiale (Quantity, Capacity, RegenerationRate, Infinite).
- **V0.1 (SYNE-042, jalon SYNE ph4 puis SYNE-070, jalon U8)** : **réserves globales** partagées
  (`ResourceStocks` : Food 100 / Water 1000 / Wood 50 / **Mineral 0**, régénération décision n°4),
  initialisées depuis `resources.*`, consommées par les actions terminales Eat/Drink et émissées
  dans le snapshot (`resources`, DATA_MODEL §8.1) ; **cycle de vie (SYNE-070)** : régénération
  `+ regenerationRate` par tick et dégradation périodique (`− regenerationRate × degradationTick`
  à chaque période, clamp ≥ 0) appliquées en fin de tick, 0 tirage PRNG ; les **sources spatiales**
  → **V0.1 (SYNE-071, jalon U8)** : les constructions sont implémentées côté **monde** —
  disques statiques `Obstacle {Id, Position, Radius}` (layout `world.obstacleLayout[]`,
  CONFIGURATION §6.8), mutation dynamique validée (`AddObstacle` bornes/id unique + révision
  `ObstacleRevision`), constructions tracées `PlaceConstruction`/`RemoveConstruction`
  (modification d'environnement), grille A\* re-rasterisable (`Refresh()`, no-op déterministe,
  SYNE-071) ; la **mécanique agentique** (qui construit, coût en bois/minéraux, durée) reste
  ouverte (décision n°20). Les **sources spatiales** de ressources restent au jalon Saisons
  (SYNE-072).

## 5. Groupes (Monographie §3.17, §6.7)

- Formation émergente par **cohésion** (proximité sociale/représentation partagée).
- Leader désigné par dynamique (attributs, confiance, contribution).
- Décisions collectives; rôles; dissolution.
- Récupération dans le SQLite : `groups`, `group_memberships`.

## 6. Conflits (Monographie §6.8)

- Combat = action coûteuse en énergie (ADR-009).
- Résolution probabiliste/numerique basée sur Strength et contexte (détails chiffrés à confirmer).
- Un conflit produit des événements typés (observables).

## 7. Communication (vu système — détail dans `COMMUNICATION_PROTOCOL.md`)

- **Locale** (rayon limité) et **non confidentielle** (ADR-008). Pas de canaux privés.
- Monte en 7 types de messages (Information, Request, Response, Announcement, Warning, Trading, Acknowledgement).
- Coûts d'envoi/réception, bande passante (maxSendsPerTick=5, maxReceivesPerTick=3), incompréhension (5%), dégradation de confiance par saut (10%/hop).

## 8. Héritage & Fusion (Monographie §6.6)

- **Naissance** : fusion consentie (décision n°17).
- **Héritage** : transmission de traits/connaissances (via **fusion consentie** §6.6.3, mécanismes fins configurables — décision n°16 **[TRANCHÉE]**).
- **Mort** : dissolution complète (V0.1) — seul un événement de trace subsiste.

## 9. Livres / Persistance de connaissance (Monographie §3.18 (livres))

- **Auteur** : rédige un livre matérialisant des connaissances à un instant T — coût (énergie, temps) configurable (décision n°18 — [TRANCHÉE]).
- **Lecteur consultant** : toute entité ayant accès peut consulter — coût/bénéfice posé (décision n°19 — [TRANCHÉE]).
- Modèle de stockage spatial (lieu) à préciser.

## 10. Observabilité interne (Monographie §3.15)

- Événements typés : perception, décision, action, communication, naissance/mort, groupe, ressource, conflit.
- `ExternalEvent` émis via WebSocket ; DecisionRecord mémorisé.
- Exigence anti-triche : l'entité ne voit jamais le monde complet (observabilité partielle).

---

## Points restés ouverts dans ce document
- Détails chiffrés de résolution des conflits (probabilités, dégâts) — à documentation quand elle sera tranchée.
- Modèle d'héritage (fusion consentie) : logique cognitive exacte à détailler.
- Coûts des livres (décisions n°18/19) : modèles économiques — **[tranchés] au niveau hérité/prototype** (coûts héritables, budgets par fusion §6.6.3).
- Mécanique V2 du monde (saisons, événements globaux) : non activée en V0.1.