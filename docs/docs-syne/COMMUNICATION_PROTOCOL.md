# COMMUNICATION_PROTOCOL.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `DATA_MODEL.md`, `COGNITIVE_ARCHITECTURE.md`
**Source Monographie** : §3.16 (communication inter-entités), ADR-008 (communication non confidentielle), Annexe H (config communication)

---

> ⚠ Ce document traite de la **communication entre entités dans le monde simulé** (pulsations lumineuses). Pour le transport **entre composants** (SYNE↔ECHOS↔PRISM), voir `../COMMUNICATION.md` et `API_CONTRACTS.md`.

## 1. Le principe : pulsations lumineuses

La communication est un **signal lumineux public**, local et dégradable :

- **Publicité** : visible de quiconque le perçoit (ADR-008 — non confidentiel). Pas de canaux privés.
- **Portée** : locale — le signal est nul au-delà du rayon effectif `transmissionRange` (**défaut 20 u.** — portée héritée du prototype, décision n°7 **[TRANCHÉE]** ; distincte de la portée de perception, 50 u.).
- **Limite de ligne de vue** : la portée est délimitée par la **distance** et la **ligne de vue** (`LineOfSight.IsClear` — pas de transmission à travers les obstacles).
- **Interception** : toute entité dans la portée (ligne de vue) reçoit le message **quelle que soit sa cible** — **[TRANCHÉE et livrée]** (décision n°8, SYNE-051).

(Monographie §3.16.1)

## 2. Les 7 types de messages

| Type | Usage | Exemple |
| :-- | :-- | :-- |
| **Information** | Partager une observation | « Nourriture à (85, 42) » |
| **Request** | Demander de l'aide / collaboration | « Peux-tu m'aider à chasser ? » |
| **Response** | Répondre à une requête | « Oui » / « Non » |
| **Announcement** | Diffuser une intention de formation | « Formation de groupe ! » |
| **Warning** | Alerter d'un danger | « Danger devant ! » |
| **Trading** | Proposer un échange | « 5 nourriture pour 3 eau » |
| **Acknowledgement** | Confirmer la réception | ack vers un ID de message |

Le moteur implémente le type **Information** en V0.1 (les autres types arrivent avec l'analyse/les besoins sociaux, jalon ph6+) ; l'enum `MessageType` liste les 7 pour le contrat de messages.

(Monographie §3.16 ; prototype V2 §07-COMMUNICATION-PROTOCOL)

## 3. Modèle de message

```text
MessageId (ulong, hash déterministe SplitMix64) | SenderId (émetteur d'origine, préservé aux relais) | TargetId (optionnel, ignore le broadcast) | Type | Payload | Confidence | Hops | Tick
```

- **MessageId** : `ulong` dérivé par hash stable (émetteur, tick, séquence) — **déterministe, aucun PRNG global** (DETERMINISM.md §3) : deux runs au même seed produisent les mêmes identifiants.
- **Confidence** : quantité portée par le message ; ajustée à la réception par la **confiance du récepteur envers l'émetteur** (`Relationships.TrustWith` — inconnue = 0.0).
- **Hops** : nombre de relais (**0** à l'émission). Le **SenderId d'origine est préservé** à chaque relais — seuls `hops+1` et la confiance changent.
- **Dégradation par hop** : **perte ~10 %/hop** (`confidence *= 0.9^hops`, `hopConfidenceDecay`, décision n°10).

## 4. Diffusion et relais

- **Diffusion** : le signal est reçu par toutes les entités dans le rayon (broadcast local), **quel que soit TargetId** (interception incluse). Dégradation = confiance amoindrie + incompréhension.
- **Relais** (SYNE-053) : un message **compris** (hors incompréhension) est relayé par le récepteur, **une seule fois par entité** (`RelaySeen`, anti-boucle), tant que `hops < maxHops` (défaut 2). Le relais partage le cap `maxSendsPerTick` et applique `confidence × hopConfidenceDecay` + `hops++`.
- **Incompréhension** : taux configurable (défaut 5 %) — tirage déterministe `SplitMix64(receiverId, messageId)` (0 PRNG global).
- **Ordre** : la communication s'exécute en **une passe par tick** (`CommunicationSystem.Step`, `PERFORMANCE.md` — `batchCommunication`), dans un ordre causal strict : **diffusion** (identifiants croissants) puis **relais** — la chaîne est bornée à `maxHops` sauts par tick.

## 5. Contraintes de bande passante et latence (Annexe H)

| Paramètre | Défaut | Note |
| :-- | :-- | :-- |
| `communication.transmissionRange` | 20 | Portée effective une pulsation (décision n°7) |
| `communication.relayEnabled` | true | Active le relais au-delà du rayon |
| `communication.maxHops` | 2 | Nombre maximal de sauts d'un message |
| `communication.maxSendsPerTick` | 5 | Max d'envois (y compris relais) par entité et par tick |
| `communication.maxReceivesPerTick` | 3 | Max de réceptions traitées par tick |
| `communication.incomprehensionRate` | 0.05 | Probabilité d'incompréhension |
| `communication.trustDecay` | 0.9 | Décroissance de confiance par tick sans interaction (`Relationships`) |
| `communication.hopConfidenceDecay` | 0.9 | Dégradation de confiance par hop (décision n°10) |
| `communication.sendEnergyCost` | 0.5 | Coût d'émission d'une pulsation |
| `communication.sendEnergyPayloadFactor` | 0.1 | Coût d'émission par caractère de payload |
| `communication.receiveEnergyCost` | 0.2 | Coût de réception d'une pulsation |
| `communication.receiveEnergyPayloadFactor` | 0.05 | Coût de réception par caractère de payload |

**Latence** : non instantanée (optionnel) — ex. 1 tick pour 10 unités. La **vitesse de signal** (temporisation de la ligne de vue) reste un **paramètre configurable** (§3.16.6) **[ouvert]**.

## 6. Coûts (décision n°9 — [TRANCHÉE et livrée])

Les coûts de production d'une pulsation (énergie, temps) sont **hérités du prototype** et **configurables** (SYNE-052). Valeurs par défaut (décision n°9) :

- coût d'envoi : énergie = **0.5 + `payloadLength` × 0.1** ;
- coût de réception : énergie = **0.2 + `payloadLength` × 0.05**.

Les coûts sont appliqués via `BodyNeeds.ExertEnergy` (émetteur à l'émission, récepteur avant traitement). Une réception **écartée par le cap** `maxReceivesPerTick` ne coûte rien.

## 7. Mécanique de réception (intégration cognitive)

1. Les messages entrants sont mis en file (`CommunicationState`), bornée par `maxReceivesPerTick`.
2. À la réception, la **confiance du message** est ajustée par la **confiance du récepteur envers l'émetteur** (`TrustWith`).
3. L'interaction est marquée (`Relationships.Interact`) : première rencontre → confiance initiale, relation connue → renforcement (véracité), décroissance `× trustDecay` par tick sans interaction.
4. **V0.1** : la file entrante est consommée comme **trace d'activité du tick** (relais, observabilité) — la **révision des croyances par messages** (alignement / conflit / sources multiples) arrive avec l'analyse/les besoins sociaux (jalon ph6+).

(Monographie §3.16, prototype V2 §07)

## 8. Suivi et observabilité

- Activité exposée par `MessageSent`/`MessageReceived` (id, émetteur d'origine, cible, type, payload, hops, confiance effective) — alimente l'observabilité et les traces pour ECHOS (heatmap de communication, `NetworkCentrality`...).
- Événements typés `message_sent` / `message_received` (`ExternalEvent`, `ObservabilityContract` 0.4.0) diffusés sur WebSocket (SYNE-054) — ingestion ECHOS générique.

---

## Points restés ouverts dans ce document
- **Latence de signal** (durée de traversée) : valeur à choisir — paramètre configurable (optionnel).
- Coûts/bénéfices des livres (décisions n°18/19) et modèles d'héritage — hors protocole mais liés à la transmission de connaissance.
- Révision des croyances par messages (intégration cognitive complète) — V0.1 consomme la file comme trace d'activité ; production de messages Request/Warning/Trading avec les besoins sociaux (jalon ph6+).