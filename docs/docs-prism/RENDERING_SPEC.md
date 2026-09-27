# RENDERING_SPEC — Objectifs de rendu PRISM

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`SCENE_SPEC.md`](SCENE_SPEC.md)

---

## 1. Statut

Les points ci-dessous sont des objectifs visuels issus des spécifications
historiques PRISM. Ils ne décrivent pas des fonctionnalités déjà fournies par
`PrismLdk` et ne prescrivent pas de classes ou d'assets Unreal. Leur
implémentation relève du projet Unreal PRISM.

## 2. Entités et ressources

- Représenter les agents d'une manière lisible et cohérente avec leur état
  reçu : position, action ou indicateurs de besoin disponibles dans le
  snapshot.
- Interpoler visuellement entre états si nécessaire, sans altérer ni retarder
  l'état autoritaire SYNE.
- Distinguer les ressources par type et présenter leur quantité avec
  discernement. Les snapshots donnent des stocks globaux par type ; ne pas
  les traiter comme des stocks locaux attachés à des objets du décor.
- Permettre l'identification et l'inspection d'un agent via son identifiant
  stable SYNE.

Les valeurs exactes, champs optionnels et extensions du snapshot sont définis
dans [`../docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md).

## 3. Principes de lisibilité

- Les codes couleur (état, groupe, sélection ou type d'événement) doivent
  rester distinguables et accompagnés d'un indice accessible lorsque la
  couleur seule est ambiguë.
- L'animation et l'interpolation sont des projections de présentation ; elles
  ne constituent pas une nouvelle trajectoire simulée.
- La topologie, les obstacles et la taille du monde reçus de SYNE guident la
  représentation ; PRISM reste responsable du rendu, du culling et
  des performances.

## 4. Limites de données à respecter

- SYNE fournit des coordonnées logiques 2D ; l'altitude et la projection 3D
  sont des choix de présentation.
- Les ressources initiales de `WorldDescription` sont des marqueurs de
  placement ; les quantités des snapshots sont des stocks globaux.
- Les détails de terrain, hauteur ou navigation ne doivent pas être inventés
  comme s'ils étaient simulés si le contrat ne les fournit pas.
- `OnSnapshot` donne l'état courant complet. Les événements/deltas ne doivent
  pas faire appliquer deux fois les mêmes mutations.
