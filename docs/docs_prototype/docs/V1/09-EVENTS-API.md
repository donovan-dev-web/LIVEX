# Événements et API

## 1. Principe

L'état courant et les événements sont deux concepts distincts.

### State

Décrit la situation actuelle.

### Event

Décrit quelque chose qui s'est produit.

## 2. Événements V1

```text
SimulationStarted
SimulationPaused
SimulationResumed
TickCompleted

AgentCreated
AgentMoved
AgentDied

NeedChanged

PerceptionUpdated

DecisionMade

ActionStarted
ActionCompleted
ActionCancelled
ActionFailed

ResourceCreated
ResourceChanged
ResourceDepleted
```

### 2.1 Payload de `DecisionMade`

L'événement `DecisionMade` porte le `DecisionRecord` défini en `docs/V1/03-V1-SPECIFICATION.md` §12.6 :

```text
AgentId, Tick, CandidateActions[{action, score}], SelectedAction
```

## 3. Exemple

```json
{
  "type": "ActionStarted",
  "tick": 1250,
  "agentId": "agent-42",
  "action": "MoveTo",
  "targetId": "food-12"
}
```

## 4. État agent exposable

```json
{
  "id": "agent-42",
  "position": { "x": 12.4, "y": 21.8 },
  "health": 87,
  "hunger": 72,
  "thirst": 30,
  "energy": 65,
  "currentAction": {
    "type": "MoveTo",
    "targetId": "food-12"
  }
}
```

## 5. API future

L'API pourra fournir :

```text
GetSimulationState
GetAgent
GetWorld
GetEvents
SubscribeEvents

Pause
Resume
Step
SetSimulationSpeed

Save
Load
```

## 6. Principe

Le renderer ne doit jamais envoyer :

```text
"Agent 42, mange."
```

sauf commande explicite de contrôle/debug.

La décision normale vient du Simulation Core.
