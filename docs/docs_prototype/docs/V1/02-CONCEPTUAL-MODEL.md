# Modèle conceptuel

## 1. Entités

La V1 distingue trois catégories :

```text
World
├── Agents
├── Resources
└── Environment
```

### Agent

Entité autonome capable de percevoir, décider et agir.

### Resource

Entité exploitable ou consommable.

### Environment

Éléments passifs du monde : terrain, obstacles, zones, etc.

## 2. Agent

```text
Agent
├── Identity
├── State
├── Needs
├── Perception
├── Memory
├── Capabilities
├── Decision
└── CurrentAction
```

## 3. État

V1 :

- Health
- Energy
- Hunger
- Thirst
- Position

Traits initiaux possibles :

- Aggression
- Sociability

Les traits influencent les décisions ; ils ne doivent pas directement déclencher une action.

## 4. Besoins

V1 :

- Food
- Water
- Rest
- Safety

Les besoins représentent une pression comportementale.

## 5. Perception

La perception est locale.

Un agent ne connaît pas automatiquement tout le monde.

V1 :

- rayon de perception
- entités détectables
- distance
- direction
- type
- informations publiques disponibles

## 6. Mémoire

V1 :

- entités connues
- ressources connues
- positions connues
- événements simples

La mémoire doit pouvoir diverger entre agents.

## 7. Capacités

Une capacité définit ce qu'un agent peut faire.

Exemples :

- Move
- Eat
- Drink
- Rest
- Explore
- Gather
- Talk
- Attack
- Flee

Une capacité ne constitue pas une décision.

## 8. Décision

La décision transforme :

```text
State + Needs + Perception + Memory + Capabilities
```

en action choisie.

## 9. Action

Une action représente une intention exécutable.

Exemple :

```text
MoveTo(Food_12)
Eat(Food_12)
Drink(Water_3)
Rest()
```

## 10. World

Le monde contient :

- temps
- agents
- ressources
- environnement
- paramètres globaux

## 11. Boucle causale

```text
World
  -> Perception
  -> Internal State
  -> Needs
  -> Decision
  -> Action
  -> World Change
  -> Perception
```
