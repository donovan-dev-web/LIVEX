# UX_INTERACTION — Intentions d'interaction

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`VISUALIZATION_SPEC.md`](VISUALIZATION_SPEC.md), [`TRANSPORT_API.md`](TRANSPORT_API.md)

---

## 1. Périmètre

`PrismLdk` fournit des fonctions et événements Blueprint, pas une caméra, un
HUD, des widgets ou des interactions préfabriquées. Les choix UX de ce
document sont des propositions pour le projet Unreal PRISM qui intègre le
plugin.

## 2. Interactions de monde possibles

PRISM peut proposer des contrôles adaptés à son expérience :

- navigation et recentrage de la vue ;
- sélection d'un agent et accès à ses données de snapshot ;
- suivi d'un agent sans modifier sa trajectoire simulée ;
- affichage d'indicateurs de tick, de population et d'état de connexion ;
- consultation du statut SYNE et des événements récents.

Les commandes de simulation doivent être initiées explicitement par
l'utilisateur ou le Blueprint du projet. Afficher l'état renvoyé par SYNE ;
ne pas supposer qu'une requête asynchrone a réussi avant `OnControlResult`.

## 3. Contrôles de simulation

Le projet peut fournir des contrôles de connexion et de run qui appellent les
fonctions Blueprint du plugin. Pour une préparation explicite, respecter
l'ordre `Prepare` → recevoir `OnWorldInitialized` → préparer l'expérience
visuelle → `Ready` → attendre le résultat → `Start`. Les boutons pause, reprise,
arrêt, reset et statut doivent refléter l'état réel rapporté par SYNE.

Voir [`TRANSPORT_API.md`](TRANSPORT_API.md) pour les fonctions, routes et
résultats ; la mécanique d'intégration Blueprint est dans
[`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md).

## 4. Interface d'analyse

Une interface analytique peut compléter le monde visuel dans PRISM
ou un outil LIVEX distinct comme ECHOS selon l'architecture produit retenue.
Les métriques, graphes et inspections doivent identifier leur source et suivre
les contrats disponibles ; ce dossier ne fixe pas de technologie front-end ni
ne suppose que l'interface ECHOS est embarquée dans PrismLdk.
