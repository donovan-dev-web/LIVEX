# COMMUNICATION_PROTOCOL.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `DATA_MODEL.md`, `COGNITIVE_ARCHITECTURE.md`
**Source Monographie** : §3.16 (communication inter-entités), ADR-008 (communication non confidentielle), Annexe H (config communication)

---

> ⚠ Ce document traite de la **communication entre entités dans le monde simulé** (pulsations lumineuses). Pour le transport **entre composants** (SYNE↔ECHOS↔PRISM), voir `../COMMUNICATION.md` et `API_CONTRACTS.md`.

## 1. Le principe : pulsations lumineuses

La communication est un **signal lumineux public**, local et dégradable :

- **Publicité** : visible de quiconque le perçoit (ADR-008 — non confidentiel). Pas de canaux privés.
- **Portée** : locale — le signal est nul au-delà du rayon effectif.
- **Limite de ligne de vue** : la portée est délimitée par la **distance** et la **ligne de vue** (pas de transmission à travers les obstacles).
- **Interception** (entité interprétant un signal non destiné) : **possible** mais reste **[OUVERTE]** (décision n°8) — à ne pas transformer en règle implicite avant décision.

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

(Monographie §3.16 ; prototype V2 §07-COMMUNICATION-PROTOCOL)

## 3. Modèle de message

```text
MessageId (UUID) | SenderId | Type | Payload | Confidence | Hops | Timestamp(tick)
```

- **Confidence** : quantité portée par le message ; ajustée par la confiance récepteur→émetteur à la réception.
- **Hops** : nombre de relais. Dégradation de confiance par hop : **perte ~10 %/hop** (`confidence *= 0.9^hops`).

## 4. Diffusion et dégradation

- **Diffusion** : le signal est reçu par toutes les entités dans le rayon (broadcast local). Dégradation = confiance amoindrie + incompréhension.
- **Relais** : la propagation d'un message au-delà du rayon est possible (ex. V2/prototype) via transmission entité-à-entité, avec dégradation à chaque saut (`hops++`).
- **Incompréhension** : taux configurable (défaut 5 %) — le message peut être mal interprété.

## 5. Contraintes de bande passante et latence (Annexe H)

| Paramètre | Défaut | Note |
| :-- | :-- | :-- |
| `communication.maxSendsPerTick` | 5 | Max d'envois par entité et par tick |
| `communication.maxReceivesPerTick` | 3 | Max de réceptions traitées par tick |
| `communication.incomprehensionRate` | 0.05 | Probabilité d'incompréhension |
| `communication.trustDecay` | 0.9 | Décroissance de confiance |

**Latence** : dans le prototype, les messages pouvaient être non instantanés (optionnel) — ex. 1 tick pour traverser 10 unités. Reste **[OUVERT]** côté décision vitesse de signal en V0.1.

## 6. Coûts (décision n°9 — [OUVERT])

Les coûts de production d'une pulsation (énergie, temps) restent **ouverts** (décision n°9). Référence prototype (à réévaluer) :

- coût d'envoi d'un message : énergie (valeur prototype) ;
- coût de réception : énergie (valeur prototype).

> La V0.1 ne fige pas les chiffres (Monographie §3.16.9) : les valeurs figurent comme paramètres configurables et seront calibrées.

## 7. Mécanique de réception (intégration cognitive)

1. Les messages entrants sont mis en file (`incomingQueue`), bornée par `maxReceivesPerTick`.
2. À la réception, la **confiance du message** est ajustée par la **confiance du récepteur envers l'émetteur** (matrice de relations).
3. La croyance est révisée par le système de croyances (alignement / conflit / différentes sources).
4. La **confiance inter-entités** est mise à jour selon `trustDecay` et la vérification (véracité constatée).

(Monographie §3.16, prototype V2 §07)

## 8. Suivi et observabilité

- Les échanges sont enregistrés en table `messages` (SQLite) : `type`, `sender_id`, `receiver_id`, `content`, `confidence`, `hops`.
- Événements de communication typés (`ExternalEvent`) pour ECHOS (heatmap de communication, `NetworkCentrality`...).

---

## Points restés ouverts dans ce document
- Interception (décision n°8) : possible mais non documentée comme règle jusqu'à trancher.
- Coûts de production d'une pulsation (décision n°9) : chiffres à calibrer.
- Latence de signal (durée de traversée) : valeur à choisir.
- Coûts/bénéfices des livres (décisions n°18/19) et modèles d'héritage — hors protocole mais liés à la transmission de connaissance.