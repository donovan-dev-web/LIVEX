# Persistance

## 1. Objectif

Une simulation doit pouvoir être arrêtée puis reprise.

## 2. Données minimales

Sauvegarder :

- simulation time
- seed
- world state
- resources
- agents
- agent state
- memories
- current actions si nécessaire
- configuration
- version du modèle

## 3. Séparation

Le format de sauvegarde doit représenter le monde, pas la scène 3D.

Ne pas sauvegarder :

- GameObjects
- meshes
- Animator state spécifique au renderer
- références Godot/Unity

## 4. Reprise

```text
Save
  -> arrêt
  -> Load
  -> reconstruction du monde
  -> simulation continue
```

## 5. Versioning

Les sauvegardes doivent posséder une version de schéma.

Exemple :

```json
{
  "schemaVersion": 1,
  "simulationVersion": "0.1.0"
}
```
