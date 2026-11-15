# ADR-014 : Plafond de réponse par tick et pagination à curseur

**Composant** : PRISM (hôte, contrat) et SYNE (client)
**Statut** : [Accepted]
**Dernière mise à jour** : 11 novembre 2026
**Dépend de** : ADR-012 (transport HTTP/WebSocket JSON), ADR-013 (convention spatiale)
**Complète** : ADR-012 — le plafond et la pagination sont des règles de la liaison de référence, pas un second transport

---

## Contexte

La réponse d'un tick porte une observation par agent, chacune plafonnée par le
contenu (`perception.maxObservedPerAgent`, 256 entrées dans le pack `minimal`) :

| Grandeur mesurée (11 novembre 2026, 1 000 agents, graine 7, pack minimal, production) | Valeur |
| :-- | --: |
| Réponse du tick 0 | **40 865 839 caractères** (40,9 Mo) |
| Ticks 1 et 2 | 40,6 Mo / 40,2 Mo |
| Régime établi (ticks 4 à 24) | 2,8 à 9,3 Mo |
| Par agent à saturation (256 entrées) | ≈ 40 Ko, soit ≈ 160 caractères par changement |

Le volume est donc **légitime** : ce sont les changements perçus, pas une fuite.
Mais il n'était borné par rien. Le profil P9 relevait déjà 8 Mo de réponse à
1 000 agents et un arrêt du run au tick 9 sur
`HTTP 400 invalid_request — « The JSON value of length 222 938 004 is too large
and not supported »` — message de `System.Text.Json`, dont l'écrivain refuse un
jeton de plus de 125 Mo : la réponse dépassait ce que le sérialiseur accepte, et
l'hôte la rejetait en `invalid_request`. La mesure du 11 novembre (relevé
`payloadChars` de l'hôte, 25 ticks — le champ s'appelle `payloadBytes` depuis le 14 novembre 2026, la mesure portant sur les octets réellement écrits) ne reproduit plus cet arrêt — 25/25 ticks,
empreinte `05a214079ff47bab` — mais **quatre réponses sur vingt-cinq dépassent
8 Mo** et le premier tick en demande six fois plus.

Trois contraintes encadrent la solution :

1. **la taille d'une réponse ne doit pas dépendre du nombre d'agents** ;
2. **le déterminisme prime** : la réduction ne peut dépendre ni du temps, ni de
   l'ordre d'itération d'un dictionnaire ;
3. **aucune troncature silencieuse** : un dépassement est signalé, jamais rogné.

## Décision

Le propriétaire du projet retient un **plafond dur de 8 Mio par réponse de tick**
avec **pagination à curseur**, découpage en tranches contiguës du flux.

- **Plafond** : `TickTransportLimits.MaximumTickResponseBytes = 8 × 1024 × 1024`,
  enveloppes comprises.
- **Tick sous le plafond** : la réponse reste le `TickResult` actuel, octet pour
  octet. Les petits ticks — et donc toutes les empreintes d'octets existantes —
  ne changent pas.
- **Tick au-delà** : réponse `TickPageEnvelope`
  `{ protocolVersion, tick, page{index, pageCount, complete, cursor?},
  chunk{observations[], events[]} }`. Le contenu est sous `chunk` et la position
  sous `page`, jamais à la racine : une page **ne peut pas** se désérialiser
  comme un `TickResult`, donc un client qui ignore la pagination échoue au lieu
  d'accepter un tick partiel.
- **Suite** : `GET /data/tick/result?cursor=…`, en lecture seule. Servir une page
  n'avance pas le monde, ne rejoue aucun calcul, et les pages restent valables
  jusqu'au tick suivant (`page_expired` ensuite, `page_invalid` pour un curseur
  illisible).
- **Curseur** : `tick.page.pageCount.fnv1a64` — déterministe, validé par l'hôte,
  sans état secret. Il désigne une page d'une séquence connue, il ne porte aucune
  décision.
- **Dépassement non paginable** : un élément seul plus gros que le plafond (une
  observation ne se coupe pas en deux) fait échouer la construction :
  `413 payload_too_large`, avec la taille fautive et la mention qu'aucun corps
  partiel n'a été envoyé. La liaison lockstep WebSocket, qui ne pagine pas,
  refuse de la même façon (`{ error: "payload_too_large" }`).
- **Client** : SYNE reconnaît la forme, draine les pages, vérifie le chaînage
  (index continus, `pageCount` constant, dernière page complète, nombre de pages
  reçues) puis concatène. Une séquence incomplète lève
  `PrismTickPageSequenceException` : le tick n'est pas appliqué.

Le découpage est une tranche contiguë, jamais un tri ni un choix : deux
exécutions du même tick produisent les mêmes pages, et les pages recollées
redonnent le flux d'origine à l'octet près (critère PRISM-107).

## Alternatives écartées

| Voie | Pourquoi non |
| :-- | :-- |
| **Coalescing borné des changements par agent/classe** | Rejette de l'information : un changement non retenu ne revient jamais, donc la trajectoire SYNE change — contraire au critère « contenu identique ». Et la taille d'une réponse resterait **proportionnelle au nombre d'agents**, ce que la règle interdit |
| **Compression explicite du corps (gzip/brotli)** | Réduit d'un facteur ≈ 5 sans rien borner : à 2 000 agents le plafond est de nouveau franchi. Ajoute une négociation de contenu et un coût CPU par tick ; ADR-012 réserve un codec binaire à la preuve que JSON dépasse le budget, pas à un contournement de taille |
| **Baisser `perception.maxObservedPerAgent`** | Change la sémantique de perception (contrat de contenu), pas le transport : cela déplacerait le problème en modifiant ce que les agents savent du monde |
| **Toujours envelopper la réponse en page** | Uniforme, mais casse inutilement tous les consommateurs et les empreintes d'octets des ticks qui tiennent déjà sous le plafond, pour un gain nul sur les runs de référence (100 agents : 4 Mo) |
| **Un seul corps, avec en-tête `X-PRISM-Next-Cursor`** | Une page resterait un `TickResult` valide : un client qui ignore l'en-tête appliquerait un tick partiel **en silence**. C'est précisément ce que la règle 3 interdit |

## Conséquences

- La taille d'une réponse est bornée par construction et **indépendante du nombre
  d'agents** : à 1 000 agents, le tick 0 part en 6 pages, le régime établi tient
  en une ou deux.
- Aucune perte d'information : le flux reconstruit est identique au flux produit.
- Coût : une sérialisation complète du résultat par tick (déjà présente dans le
  chemin de mesure) sert de mesure de taille et, si le tick tient, de corps de
  réponse ; au-delà, une passe de mesure par élément pour placer les frontières
  de page. À 1 000 agents, cela reste sous 0,5 s sur un tick de 7 à 10 s.
- L'hôte retient les pages du dernier tick jusqu'au suivant : la mémoire
  supplémentaire est de l'ordre de la taille du résultat, déjà en mémoire.
- Un client qui ne connaît pas la pagination échoue franchement (413, ou erreur
  de désérialisation parce que `observations` est absent de la racine). C'est
  voulu : mieux vaut un run qui s'arrête qu'un monde divergent en silence.

## Preuves

- Contrat : `TickPagingContractTests` (8 tests) — position et curseur, dernière
  page complète, refus d'une page qui se prétend complète trop tôt, refus d'une
  page incomplète sans curseur, refus d'observations d'un autre tick, refus d'une
  page hors de la séquence annoncée, **refus de lire une page comme un
  `TickResult`**, plafond à 8 Mio.
- Hôte : `TickPayloadCeilingTests` (6 tests) — pagination et recollement à
  l'octet près au-dessus du plafond (chaque réponse ≤ plafond), refus `413` d'un
  élément insécable, curseur d'un tick précédent refusé, curseur illisible
  refusé, pagination transparente sur le monde réel (même flux, même empreinte de
  monde), et **acceptation 1 000 agents × 25 ticks** : aucune réponse au-dessus de
  8 Mio, les huit premiers ticks identiques au run de référence de 8 ticks.
- Client : `PrismClientTests` — drainage complet et flux reconstruit dans l'ordre,
  refus d'une séquence trouée (`PrismTickPageSequenceException`).
- Run réel (11 novembre 2026, `Simulation.Console --prism-url`, 1 000 agents,
  25 ticks, graine 7, pack minimal, production) : 25/25 ticks, empreinte couplée
  **`05a214079ff47bab`**, identique au run de référence d'avant le chantier
  (mêmes huit premiers ticks, mêmes trajectoires) ; la plus grosse réponse passe
  de 40,9 Mo à moins de 8 Mio (tick 0 servi en 6 pages de 8 354 790 à 8 385 958
  caractères, puis une page de 371 369).
- Suites : **PRISM 444/444** et **SYNE `Simulation.Console.Tests` 213/213**.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 11 novembre 2026 | Création ; plafond 8 Mio et pagination à curseur livrés | PRISM-142 (mur du payload) |
