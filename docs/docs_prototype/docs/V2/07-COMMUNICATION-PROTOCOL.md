# Protocole de communication V2

## 1. Objectifs

Le système de communication doit permettre :

- **Échange local d'informations** (agents dans le rayon de perception)
- **Propagation des croyances** (l'agent A dit à B, B croit selon la confiance)
- **Requêtes de coopération** (négocier des actions, échanges, formation de groupes)
- **Rumeurs et désinformation** (l'information se dégrade ou devient fausse avec la distance/temps)

---

## 2. Modèle de message

### 2.1 Structure de base

```csharp
public class Message
{
    // Identity
    public string MessageId { get; set; }              // Unique UUID
    
    // Participants
    public string SenderId { get; set; }               // Who sends
    public string ReceiverId { get; set; }             // Who receives (can be "*" for broadcast)
    
    // Content & Type
    public MessageType Type { get; set; }              // Information, Request, Response, Announcement, Warning, Trading
    public string ContentType { get; set; }            // "position", "resource", "request_help", "trade_offer"
    public Dictionary<string, object> Payload { get; set; }  // Structured data
    
    // Metadata
    public ulong SendTick { get; set; }                // When sent
    public ulong DeliveryTick { get; set; }            // When delivered (may differ)
    public float ConfidenceLevel { get; set; }         // Sender's confidence in content (0–1)
    public string Source { get; set; }                 // "direct_observation", "memory", "hearsay"
    
    // Cost & logistics
    public float EnergyCost { get; set; }              // Cost to send
    public float ProcessingCost { get; set; }          // Cost to recipient to process
}

public enum MessageType
{
    Information,      // "I see food at (100, 50)"
    Request,          // "Can you help me hunt ?"
    Response,         // "Yes" / "No" / "I'm tired"
    Announcement,     // "Group forming, seeking members"
    Warning,          // "Danger ahead !"
    Trading,          // "Offer X for Y"
    Acknowledgement    // "Message received"
}
```

### 2.2 Exemples de Payload

**Information : Localisation**
```json
{
  "messageType": "Information",
  "contentType": "position",
  "payload": {
    "entityType": "Food",
    "entityId": "food-12",
    "position": { "x": 100, "y": 50 },
    "quantity": 30,
    "observationConfidence": 0.95
  }
}
```

**Requête : Aide**
```json
{
  "messageType": "Request",
  "contentType": "request_help",
  "payload": {
    "requestType": "Hunt",
    "targetId": "deer-5",
    "targetPosition": { "x": 150, "y": 75 },
    "expectedUtility": 60,
    "groupFormingId": "group-42"
  }
}
```

**Annonce : Groupe**
```json
{
  "messageType": "Announcement",
  "contentType": "group_formation",
  "payload": {
    "groupId": "group-42",
    "objective": "Hunting",
    "leaderId": "agent-1",
    "rolesNeeded": ["Scout", "Gatherer"],
    "expectedDuration": 20
  }
}
```

**Échange : Offre**
```json
{
  "messageType": "Trading",
  "contentType": "trade_offer",
  "payload": {
    "offerId": "trade-100",
    "give": { "resourceType": "Food", "quantity": 5 },
    "receive": { "resourceType": "Water", "quantity": 3 },
    "validUntilTick": 120
  }
}
```

---

## 3. Diffusion et réception

### 3.1 Mécanisme d'envoi

```csharp
public class CommunicationSystem
{
    private float communicationRadius = 20;  // Configurable
    private Queue<Message> messageQueue = new();
    
    public void SendMessage(Message msg)
    {
        // Cost to sender
        Agent sender = world.GetAgent(msg.SenderId);
        sender.energy -= msg.EnergyCost;
        
        // Queue for processing
        messageQueue.Enqueue(msg);
    }
    
    public void ProcessMessages()
    {
        while (messageQueue.TryDequeue(out var msg))
        {
            BroadcastMessage(msg);
        }
    }
    
    private void BroadcastMessage(Message msg)
    {
        Agent sender = world.GetAgent(msg.SenderId);
        
        // Broadcast to all nearby agents
        foreach (var agent in world.agents)
        {
            float distance = Vector2.Distance(sender.position, agent.position);
            
            if (distance <= communicationRadius && agent.id != sender.id)
            {
                // Recipient can receive
                agent.receiveMessage(msg);
                
                // Cost to recipient
                agent.energy -= msg.ProcessingCost;
            }
        }
    }
}
```

### 3.2 Réception et interprétation

```csharp
public class Agent
{
    public void ReceiveMessage(Message msg)
    {
        // Evaluate sender credibility
        Relationship relationship = relationships.GetOrCreate(msg.SenderId);
        float senderTrust = relationship.TrustLevel;
        
        // Message confidence adjusted by trust
        float adjustedConfidence = msg.ConfidenceLevel * senderTrust;
        
        // Process based on type
        switch (msg.Type)
        {
            case MessageType.Information:
                HandleInformation(msg, adjustedConfidence);
                break;
            case MessageType.Request:
                HandleRequest(msg);
                break;
            case MessageType.Announcement:
                HandleAnnouncement(msg);
                break;
            case MessageType.Trading:
                HandleTradeOffer(msg);
                break;
        }
    }
    
    private void HandleInformation(Message msg, float confidence)
    {
        // Extract fact from payload
        string fact = msg.Payload["description"].ToString();
        Vector2 position = ((Dictionary<string, object>)msg.Payload["position"]).ToVector2();
        
        // Create or update belief
        Belief belief = new Belief
        {
            Fact = fact,
            Position = position,
            Confidence = confidence,
            Source = BeliefSource.Communication,
            FromAgent = msg.SenderId,
            Timestamp = msg.DeliveryTick,
            ExpiryTick = msg.DeliveryTick + 100  // Belief good for 100 ticks
        };
        
        beliefs.UpdateBelief(belief);
        
        // May trigger new goal
        if (confidence > 0.6 && NeedsMatch(fact))
        {
            GenerateGoalFromInformation(fact, position);
        }
    }
    
    private void HandleRequest(Message msg)
    {
        // Evaluate cooperation possibility
        float cooperationUtility = EvaluateCooperationUtility(msg);
        
        if (cooperationUtility > threshold)
        {
            // Accept: send Response
            SendMessage(new Message
            {
                ReceiverId = msg.SenderId,
                Type = MessageType.Response,
                Payload = new() { { "answer", "accept" } }
            });
            
            // Add group goal
            GenerateGoalForRequest(msg);
        }
        else
        {
            // Decline: send Response
            SendMessage(new Message
            {
                ReceiverId = msg.SenderId,
                Type = MessageType.Response,
                Payload = new() { { "answer", "decline" }, { "reason", "tired" } }
            });
        }
    }
    
    private void HandleAnnouncement(Message msg)
    {
        // Consider joining group
        string groupId = msg.Payload["groupId"].ToString();
        string objective = msg.Payload["objective"].ToString();
        
        float interestInGroup = EvaluateGroupUtility(objective);
        
        if (interestInGroup > 0.5)
        {
            // Send join request
            SendMessage(new Message
            {
                ReceiverId = msg.SenderId,
                Type = MessageType.Request,
                ContentType = "join_group",
                Payload = new() { { "groupId", groupId } }
            });
        }
    }
    
    private void HandleTradeOffer(Message msg)
    {
        // Evaluate trade utility
        var give = msg.Payload["give"] as Dictionary<string, object>;
        var receive = msg.Payload["receive"] as Dictionary<string, object>;
        
        float tradeUtility = EvaluateTradeUtility(give, receive);
        
        if (tradeUtility > 0)
        {
            AcceptTrade(msg);
        }
        else
        {
            SendDeclineResponse(msg);
        }
    }
}
```

---

## 4. Dégradation de l'information

### 4.1 Propagation des rumeurs

L'information se propage mais se dégrade :

```
Tick 100 : Agent A sees food at (100, 50)
           A's belief: confidence 1.0
           
Tick 101 : A tells B
           B's belief: confidence 0.9 (10% uncertainty)
           
Tick 102 : B tells C
           C's belief: confidence 0.8 (20% uncertainty)
           
Tick 103 : C tells D
           D's belief: confidence 0.7 (30% uncertainty)
           
Tick 104 : D tells E
           E's belief: confidence 0.6 (40% uncertainty)
```

**Implémentation** :

```csharp
// When relaying information
Message relayMsg = new Message
{
    SenderId = agent.id,
    ReceiverId = targetAgent.id,
    Type = MessageType.Information,
    ConfidenceLevel = originalMsg.ConfidenceLevel * 0.9,  // 10% degrade
    Source = "hearsay"  // Mark as indirect
};
```

### 4.2 Propagation de fausses croyances

```csharp
// If agent B misunderstands message:
float misunderstandingChance = 0.05;

if (Random.Chance(misunderstandingChance))
{
    // Distort information
    belief.Position += new Vector2(Random.Range(-5, 5), Random.Range(-5, 5));
    belief.Confidence *= 0.7;  // Less confident in corrupted info
    belief.Source = "misunderstood_communication";
}
```

---

## 5. Confiance et crédibilité

### 5.1 Évolution dynamique de la confiance

```csharp
public class TrustEvaluator
{
    public void UpdateTrust(Agent observer, Agent subject, Message msg, bool proven)
    {
        Relationship relationship = observer.GetRelationship(subject);
        
        if (proven)
        {
            // Information was accurate
            relationship.TrustLevel += 0.1;
        }
        else
        {
            // Information was false or misleading
            relationship.TrustLevel -= 0.15;  // Stronger penalty for lies
        }
        
        relationship.TrustLevel = MathF.Clamp(relationship.TrustLevel, 0, 1);
        relationship.LastInteractionTick = currentTick;
    }
}
```

### 5.2 Matrice de crédibilité

L'agent maintient une matrice des niveaux de confiance :

```
Agent A trusts:
  Agent B : 0.9  (always reliable)
  Agent C : 0.4  (sometimes wrong)
  Agent D : 0.1  (often lies)
  Agent E : 0.0  (never trust)
```

Lors de la réception d'informations, utiliser la confiance de l'émetteur comme multiplicateur.

---

## 6. Logistique de communication

### 6.1 File de messages et traitement

```csharp
public class CommunicationQueue
{
    private Queue<Message> incomingMessages = new();
    private Queue<Message> outgoingMessages = new();
    
    public void EnqueueIncoming(Message msg) => incomingMessages.Enqueue(msg);
    public void EnqueueOutgoing(Message msg) => outgoingMessages.Enqueue(msg);
    
    public void ProcessIncoming(Agent agent)
    {
        // Process up to N messages per tick (bounded processing)
        int processLimit = 3;
        
        for (int i = 0; i < processLimit && incomingMessages.TryDequeue(out var msg); i++)
        {
            agent.ReceiveMessage(msg);
        }
    }
}
```

### 6.2 Coûts énergétiques

```csharp
public class CommunicationCosts
{
    public static float GetSendCost(Message msg)
    {
        // Base cost + content size
        float baseCost = 0.5;
        float sizeMultiplier = msg.Payload.Count * 0.1;
        
        // Broadcast more expensive than direct
        float broadcastMultiplier = msg.ReceiverId == "*" ? 1.5f : 1.0f;
        
        return baseCost + sizeMultiplier * broadcastMultiplier;
    }
    
    public static float GetReceiptCost(Message msg)
    {
        // Cost to listen and process
        return 0.2 + msg.Payload.Count * 0.05;
    }
}
```

### 6.3 Accusé de réception de message

```csharp
// If important, sender may request acknowledgement
Message msg = new Message
{
    Type = MessageType.Information,
    Payload = new() { { "requiresAck", true } }
};

// Recipient sends back:
Message ackMsg = new Message
{
    Type = MessageType.Acknowledgement,
    ReceiverId = msg.SenderId,
    Payload = new() { { "ackOf", msg.MessageId } }
};
```

---

## 7. Contraintes du protocole

### 7.1 Limites de bande passante

```csharp
// Agent can send max N messages per tick
public class CommunicationBandwidth
{
    private const int MaxMessagesPerTickPerAgent = 5;
    
    public bool CanSend(Agent agent)
    {
        int sentThisTick = agent.messagesSentThisTick.Count;
        return sentThisTick < MaxMessagesPerTickPerAgent;
    }
}
```

### 7.2 Limitations de rayon

```csharp
// Communication only works within radius
private const float CommunicationRadius = 20;  // World units

bool CanCommunicate(Agent sender, Agent receiver)
{
    return Vector2.Distance(sender.position, receiver.position) <= CommunicationRadius;
}
```

### 7.3 Latence

Les messages ne sont pas instantanés (optionnel) :

```csharp
// Message takes 1 tick to traverse 10 units
int deliveryLatency = (int)MathF.Ceil(distance / 10);
msg.DeliveryTick = sendTick + deliveryLatency;
```

---

## 8. Référence des types de messages

| Type | But | Exemple de contenu |
|------|---------|-----------------|
| **Information** | Partager une observation | "Food at (100, 50)" |
| **Request** | Demander aide/collaboration | "Help me hunt ?" |
| **Response** | Répondre à une requête | "Yes" / "No" |
| **Announcement** | Diffuser une intention de formation | "Group forming !" |
| **Warning** | Alerter d'un danger | "Danger zone ahead" |
| **Trading** | Proposer un échange | "5 food for 3 water" |
| **Acknowledgement** | Confirmer la réception | Message ID ack |

---

## 9. Exemple de flux d'interaction

```
Tick 100:
  Agent A perceives food at (100, 50)
  A's belief: confidence 1.0
  
Tick 101:
  A sends message to B:
    "Food at (100, 50)"
    confidence 1.0
    
  B receives message
  B evaluates:
    - A's trust: 0.8
    - Message confidence: 0.8 * 1.0 = 0.8
    
  B updates belief:
    "Food at (100, 50)"
    confidence 0.8
    
Tick 102:
  B sends message to C:
    "Food at (100, 50)"
    confidence 0.72  (0.8 * 0.9 degradation)
    
  C receives message
  C trusts B: 0.6
  C updates belief:
    "Food at (100, 50)"
    confidence 0.43  (0.72 * 0.6)
    
Tick 103:
  Meanwhile, Agent A eats the food
  Food now at (100, 50) : quantity 0
  
Tick 105:
  C moves to (100, 50) based on belief
  C finds no food
  C updates belief:
    "Food at (100, 50)" confidence 0.0 (proven false)
    
  C sends message to B:
    "No food found at (100, 50)"
    
  B updates beliefs
  B's trust in A decreases (provided false info)
  
Result:
  - A: unaware of outcome
  - B: learns from mistake, trust in A decreases
  - C: learned world is dynamic, information becomes stale
  
  Emergent behavior: Agents learn to verify old information
```

---

## 10. Configuration (config.json)

```json
{
  "communication": {
    "enabled": true,
    "radiusMeters": 20,
    "messageTypesAllowed": ["Information", "Request", "Response", "Announcement", "Warning", "Trading"],
    "sendCostBase": 0.5,
    "receiptCostBase": 0.2,
    "degradationPerHop": 0.1,
    "maxMessagesPerTickPerAgent": 5,
    "messageRetentionTicks": 100,
    "trustDecay": 0.001
  }
}
```
