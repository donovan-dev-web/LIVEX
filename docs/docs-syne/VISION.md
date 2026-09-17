# VISION.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../VISION.md` (racine), `../GLOSSARY.md`
**Source Monographie** : Partie 3 (SYNE : Le Moteur de Simulation), §2.3.1

---

## 1. Rôle de SYNE

SYNE (**Systems & Emergent Network Engine**) est le **moteur de simulation** : il possède la **vérité du monde**. Il calcule les états, les interactions et les décisions, indépendamment de toute représentation graphique.

Il fonctionne en **mode headless** (sans interface), produit des événements observables et sauvegarde/restaure son état avec une garantie de **déterminisme bit-à-bit**.

## 2. Principes fondateurs

1. **Le moteur ne dicte pas aux entités ce qu'elles doivent faire** — il fournit des structures (état, besoins, perception, mémoire, croyances, capacités, système de décision, actions). (Monographie §3.7.1)
2. **Indépendance du rendu** — aucune dépendance à un moteur graphique ; PRISM/ECHOS sont de simples consommateurs. (Monographie §1.4.6)
3. **Déterminisme** — reproductibilité exacte à seed + config + version moteur identiques. (Monographie §3.6.2)
4. **Observabilité** — chaque décision produit une trace (DecisionRecord) ; chaque événement est horodaté et attribué. (Monographie §3.14.12)

## 3. Ce que SYNE n'est pas

- Ce n'est **pas** un moteur physique réaliste (entités logiques, sans collisions physiques — ADR-010).
- Ce n'est **pas** une IA d'apprentissage : la cognition est explicite, déterministe et inspectable.
- Ce n'est **pas** un moteur de rendu : le monde simulé est un plan 2D logique.

## 4. Ambition V0.1

SYNE doit être un **instrument scientifique validable** :
- capacité de 50 → 1000 entités avec un budget de tick maîtrisé ;
- architecture BDI (boucle cognitive en 15 étapes) ;
- communication inter-entités par pulsations lumineuses ;
- persistance (JSON → SQLite) et reprise exacte ;
- anti-triche : son observabilité est **partielle** (rayon de perception) ;
- ensembles de tests massifs (couverture ≥ 80 %, jalon 160+ tests).

## 5. Références

- Architecture : `ARCHITECTURE.md`
- Modèle de données : `DATA_MODEL.md`
- Boucle de simulation : `SIMULATION_LOOP.md`
- Cognition : `COGNITIVE_ARCHITECTURE.md`
- Systèmes : `SYSTEMS_SPEC.md`

---

## Points restés ouverts dans ce document
- Aucun — vision du composant stabilisée ; les détails opérationnels jusqu'à `[OUVERT]` sont déportés vers les docs techniques du composant.