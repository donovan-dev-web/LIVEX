# Migration : abandon de la morphologie physique (SYNE-130)

ADR-010 définit les entités comme des agents logiques. Les consommateurs qui migrent depuis un modèle doté de corps physiques appliquent les règles suivantes :

| Donnée historique | Traitement V0.1 |
|---|---|
| Identifiant et espèce | Conserver comme identité/type logique de l'entité. |
| Position 2D | Conserver comme coordonnées du monde et entrée du déplacement/navigation. |
| Traits, besoins, croyances, mémoire et état de vie | Conserver dans le modèle cognitif correspondant. |
| Forme, taille, masse, membres, squelette, animation physique | Retirer du modèle SYNE ; si nécessaire, garder comme métadonnée de rendu externe. |
| Collisions, volume occupé et contraintes corporelles | Ne pas migrer : aucune collision entre entités n'est simulée. |
| Vitesse ou coût d'action | Mapper uniquement vers les paramètres numériques de déplacement/action existants ; ne pas en déduire une géométrie corporelle. |

Les consommateurs visuels peuvent représenter chaque entité par un marqueur générique (forme standard par type ou espèce). Cette représentation ne doit pas modifier la position, les décisions, le déterminisme ou la persistance SYNE. Les champs inconnus de morphologie ne doivent pas être réinterprétés silencieusement comme propriétés physiques actives.

Cette migration ne supprime pas les caractéristiques logiques déjà persistées ; elle retire uniquement la dépendance à une simulation de forme/collision détaillée.
