# TESTING.md

**Composant** : PRISM
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `TRANSPORT_API.md`
**Source Monographie** : Partie 7.6 (CI), §5 (PRISM) — stratégie à créer

---

## 1. Objectif

Garantir que PRISM **reflète fidèlement** l'état de SYNE, se connecte/reconnecte correctement, relaie les commandes, et que les visualisations sont correctes (mapping 2D→3D, interpolations, code couleur).

## 2. Stratégie de test

| Niveau | Contenu |
| :-- | :-- |
| **Tests unitaires C# (Godot .NET)** | Mapping 2D→3D (x→x, y→z), interpolation entre snapshots, conversion `quantité/capacité` pour tailles, calcul des couleurs (santé, groupe, pulsations), tri des croyances du BeliefViewer. |
| **Tests d'intégration transport** | Connexion WebSocket :5180, réception `snapshot`/`event`, **reconnexion automatique 1,5 s** (déconnexion simulée), relais HTTP :5181 (`start/pause/resume/reset`), parsing d'un fixture de `WorldSnapshot`. |
| **Tests visuels / manuels** | Rendu des capsules, couleurs, heatmaps, graphe social — checklist manuelle avec captures de référence (Godot Headless → rendu offscreen si possible). |
| **Non-régression scénique** | Scénarios de démo : 50 entités, 2000 ticks ; 1000 entités — vérifier FPS et culling. |

## 3. Outillage

- **Godot édition .NET** : tests via le runner de tests .NET / `dotnet test` sur le code de logique (hors boucle de rendu).
- **Simulation headless** : SYNE lancé en `--headless` comme partenaire de test d'intégration (fixtures de runs réels).
- CI : job PRISM dans `ci.yml` racine (build + tests logique + checklist visuelle manuelle hors CI).

## 4. Critères

- Mapping 2D→3D : entité dans monde 500×500 → coordonnées Godot bornées et cohérentes.
- Reconnexion : après coupure, le client se reconnecte en ≤ 1,5 s et resynchronise le dernier snapshot.
- Relais : chaque commande HUD produit l'appel HTTP :5181 correspondant (vérifié par mock).

---

## Points restés ouverts dans ce document
- Le rendu offscreen/headless de Godot comme test automatisé est à évaluer à l'implémentation (sinon tests visuels manuels).
- Checklist visuelle de référence à créer lors de la première implémentation V0.1.