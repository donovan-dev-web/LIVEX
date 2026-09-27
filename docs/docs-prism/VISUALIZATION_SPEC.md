# VISUALIZATION_SPEC — Vues de données

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`RENDERING_SPEC.md`](RENDERING_SPEC.md), [`TRANSPORT_API.md`](TRANSPORT_API.md)

---

## 1. Statut et périmètre

Ce document décrit des idées de visualisation pour l'expérience PRISM. Elles
ne sont pas des capacités intégrées à `PrismLdk` : le plugin expose les champs
et événements disponibles, et le projet Unreal PRISM décide quelles vues
construire. Les données réellement disponibles sont déterminées par le
contrat SYNE et sa version.

## 2. Inspection des entités

Une vue d'inspection peut afficher les champs présents dans le snapshot pour
l'agent sélectionné, par exemple ses besoins, intention/action, croyances,
objectifs, traits et relations de confiance. Les champs absents ou non
exposés ne doivent pas être complétés par des suppositions. Associer la vue à
l'identifiant SYNE de l'agent et l'actualiser depuis l'état courant du snapshot.

Une projection spatiale des croyances ou des besoins peut être envisagée si
les données requises sont effectivement disponibles et si la vue indique
qu'elle représente une projection/agrégation.

## 3. Relations et groupes

PRISM peut présenter les relations de confiance et les groupes
renvoyés par SYNE, au moyen de graphes, couleurs, panneaux ou autres vues.
Toute agrégation ou mise en page (par exemple un graphe de forces) est une
convention de présentation, pas un calcul décisionnel du plugin.

Pour garantir une lecture stable, dériver les couleurs ou symboles de manière
déterministe dans PRISM et ne pas leur donner une signification
différente de celle annoncée à l'utilisateur. Afficher les inconnues ou
valeurs non reconnues sans perdre leur valeur texte d'origine si le contrat
est étendu.

## 4. Communication et événements

Les événements de communication, décision, action et monde peuvent alimenter
un journal, une chronologie ou des effets transitoires. Ces effets sont des
indices visuels ; ils ne remplacent pas l'état complet des snapshots. Un
événement peut arriver en plus d'une mutation agrégée dans le snapshot : éviter
les doubles mises à jour.

Consulter [`../docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md)
pour les types d'événements et champs effectivement définis.
