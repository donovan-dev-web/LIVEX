# AGENT_INTERFACE — Impact sur les agents BDI (côté SYNE)

**Composant** : PRISM (impact SYNE)
**Statut** : [SOCLE DE TRANSPORT — ADAPTATION COGNITIVE NON IMPLÉMENTÉE]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`API_CONTRACTS.md`](API_CONTRACTS.md), [`adr/ADR-009-planification-dans-syne-savoir-des-recettes.md`](adr/ADR-009-planification-dans-syne-savoir-des-recettes.md), `../syne/COGNITIVE_ARCHITECTURE.md`
**Source Monographie** : —

---

## 1. Principe

Le BDI reste le moteur de décision. Ce qui change, c'est la **taille de l'espace d'actions** : il faut un planificateur pour enchaîner les étapes d'une chaîne de production, et une mémoire spatiale pour ne pas scanner le monde. Ce document est la spécification cognitive côté SYNE ; l'implémentation est dans le périmètre de la phase P9.

## 2. Croyances

Un agent croit des choses sur un monde qu'il ne voit qu'en partie.

| Croyance | Source | Remarque |
| :-- | :-- | :-- |
| **Mémoire spatiale** : carte mentale par région — « du fer vu ici, il y a tant de ticks », confiance décroissante | Observations proche/moyen | 1 000 agents × 256 régions = 256 k entrées ; à 16 octets l'entrée ≈ 4 Mo. Entrée = `(regionId, masque de tags u32, tickVu u32, confiance u8, divers)` : **un masque de tags par région**, pas une entrée par tag |
| **Inventaire et outils** | Observation de soi (PRISM) | Source de vérité : PRISM |
| **Recettes connues** | Savoir (§7) | Variante simple ou riche |
| **Chantiers et structures observés / accessibles** | Observations | Avec droits d'accès connus |
| **Croyances sociales** : qui maîtrise quoi, qui possède quoi, qui a tenu ses engagements | Interactions, événements | Déjà dans SYNE (confiance), étendues aux compétences |

La décroissance de confiance suit le mécanisme existant d'oubli (ADR-007 de SYNE) ; elle est de classe A (multipliée par `dt`).

## 3. Désirs et besoins

Les besoins existants (faim, soif, fatigue) restent dans SYNE. Ils engendrent des **désirs instrumentaux** que le planificateur dérive sans script : « avoir une pioche », « avoir un stock de nourriture », « avoir un abri ». Un besoin de **sécurité alimentaire à horizon long** (niveau de stock) évite que les agents ne vivent qu'au jour le jour. Les valeurs d'effet d'un objet consommé arrivent de PRISM dans l'événement `item_consumed`.

## 4. Planification

Un planificateur **intégré à la délibération** construit la séquence de macro-actions par **chaînage arrière** à partir des recettes connues. Le choix algorithmique, laissé ouvert ici, est tranché par **[SYNE ADR-019](../syne/adr/ADR-019-planificateur-chainage-arriere-borne.md)** (6 novembre 2026) : chaînage arrière **borné sur règles de décomposition** (HTN léger), pas GOAP, pour la V2 — coût et déterminisme à 1 000 agents, et état du monde non supposé complet. Le plan est mis en cache et recalculé **sur événement d'échec**, pas à chaque tick. L'agent ne délibère que sur événement (fin d'action, échec, perception notable).

| Point | Règle |
| :-- | :-- |
| Profondeur | Bornée (`planMaxDepth`) |
| Cache | Par `(but, état d'inventaire connu)` |
| Replanification | Sur `action_failed` (la raison oriente le replan), `site_destroyed`, outil cassé |
| Traçabilité | `Action ← Plan ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception` |
| Budget | Mesuré par B7 (`PERFORMANCE.md` §3) |

## 5. Macro-actions et information locale

SYNE n'envoie pas un ordre par bloc : il émet des macro-actions (`ObtenirObjet`, `Fabriquer`/`FabriquerMacro`, `Cultiver`, `DemarrerChantier`/`Contribuer`, `Demander`/`Offrir`).

**Règle d'honnêteté épistémique.** Une macro-action porte un **indice** (`hint`) tiré des croyances de l'agent (position ou chunk). PRISM ne cherche jamais « le fer le plus proche » dans le monde entier ; il exécute vers l'indice et ne reconnaît que des sources **perçues par cet agent**. Si rien n'est trouvé, il échoue avec `source_not_found`, et SYNE **révise la croyance** (le fer n'est plus là). Cela préserve le principe « information locale » et rend l'exploration utile.

### 5.1 Ce que SYNE émet réellement (6 novembre 2026, étape 3b)

Le write-back P9 a longtemps parlé le strict minimum (un `Aller` par décision, un
`Consommer` par soif) : la faim ne pouvait pas aboutir, faute de commande pour
prélèvement ou consommation d'objet. État livré par l'étape 3b :

| Décision SYNE | Commande PRISM | Condition |
| :-- | :-- | :-- |
| `SeekFood`, `SecureFood` | **`ObtenirObjet`** `{ tag: "edible", quantity: 1, hint, hintRadius: 8 }` | catalogue d'objets lisible (`items/*.json`) et au moins un objet comestible déclaré |
| `SeekFood`, `SecureFood` | `Aller` (repli) | catalogue absent ou sans comestible |
| `Eat` | **`Consommer`** `{ item }` | l'agent porte un objet **identifié comme comestible** par le catalogue (le plus petit identifiant, choix déterministe) |
| `Drink` | `Consommer` `{ target }` | bloc d'eau connu (boire à la source) |
| `SeekWater`, `Explore`, `Socialize`, `Flee` | `Aller` | cible crue |

Deux conséquences à connaître :

- **L'indice reste l'opinion de l'agent.** `hint` est le bloc qu'il *croit* être une
  source ; `hintRadius` borne la recherche. Si PRISM échoue (`source_not_found`),
  c'est la croyance qui est à réviser, pas la consigne.
- **Un objet comestible au sol devient une cible nommée** (`resource-food-<id>`)
  quand le catalogue le reconnaît ; sans catalogue, il reste « l'objet 4 » et la
  faim ne trouve aucune cible. Le nom est la seule forme que `Knowledge.KindFromLabel`
  sait relire.

**Ce que SYNE fait d'un refus.** Un échec **durable** (`path_not_found`,
`source_not_found`) est attribué à la consigne (le `commandId` de l'événement est
rapproché de la commande émise) puis mémorisé : la même consigne n'est pas réémise
pendant 300 ticks. Un échec **transitoire** (`not_adjacent`) ou **defectueux**
(`invalid_payload`) reste retenté — le masquer cacherait un défaut de SYNE. La
conséquence assumée : SYNE **cesse de redemander** mais ne révise pas encore la
croyance qui visait cette cible — la décision reste la même, donc l'agent n'agit
pas sur ce désir tant que la croyance ne change pas, et la cible redevient
demandable après la fenêtre. La mesure ne montre aucune perte d'action aboutie
(86 avant comme après) : ce sont des répétitions qui disparaissent, pas du
comportement.

Restent à câbler : `FabriquerMacro`/`Cultiver` (chaînes de production),
`DemarrerChantier`/`Contribuer` (chantiers), `Transferer` (dons/échanges). Aucune
décision SYNE ne les produit aujourd'hui — ce sont les désirs correspondants qui
manquent, pas le vocabulaire PRISM.

## 6. Communication et groupes

- Communication par pulsations : inchangée dans SYNE ; le rayon V2 est **50 blocs** (distance horizontale, non occultée en V2) calculé sur les positions du tick précédent.
- **Groupes** : notion SYNE ; PRISM ne reçoit que des listes d'identifiants autorisés. Transferts : un accord est conclu côté SYNE, puis `Transférer` est exécuté atomiquement par PRISM (distance maximale `transferMaxDistance`, par défaut 3 blocs).
- Les **messages informatifs** (« du fer au nord ») ont une valeur réelle : ils mettent à jour la mémoire spatiale de l'agent qui les reçoit, avec une confiance dérivée de la confiance sociale.

## 7. Savoir des recettes

| Variante | Fonctionnement | Usage |
| :-- | :-- | :-- |
| **Simple** (défaut) | Tous les agents connaissent toutes les recettes dès le départ | Référence de calibrage |
| **Riche** | Les recettes se **découvrent** par essai, observation d'un autre agent ou enseignement ; peuvent être consignées dans des **livres** (`Book` de SYNE) | Condition d'expérience ; la transmission du savoir devient une raison supplémentaire de former des groupes |

Recommandation : commencer par la variante simple, activer la riche par configuration d'expérience (`knowledge.mode = "rich"`).

## 8. Compatibilité avec l'existant

Le contrat PRISM peut être validé avec la **cognition actuelle de SYNE** avant la fin de la V2 : l'interface compte plus que la sophistication de l'agent. `Simulation.Console.Integration.PrismClient` fournit maintenant le transport HTTP authentifié prepare/start/tick/stop et valide les réponses lockstep. Il n'est pas encore branché au contrôleur ou à la boucle BDI et ne traduit pas les intentions de déplacement, manger ou boire en commandes PRISM ; SYNE conserve encore son monde local. Le LLM dans la boucle reste hors périmètre ; un usage hors boucle (génération de recettes ou de noms, figés ensuite dans les données) est une piste ultérieure.

## 9. Impact sur le déterminisme de SYNE

Le planificateur doit être déterministe : ordre d'exploration fixe (identifiants croissants), départages explicites, aucun `HashSet` itéré sans tri. Toute modification de la boucle cognitive incrémente `engineVersion` de SYNE (`VERSIONING.md` §3).

## Points restés ouverts dans ce document

Choix GOAP vs HTN : **tranché** par [SYNE ADR-019](../syne/adr/ADR-019-planificateur-chainage-arriere-borne.md) (chaînage arrière borné, 6 novembre 2026) ; le spike était renvoyé à P9 (ISSUES `SYNE-2xx`).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 6 novembre 2026 | **§5.1 ajoutée** : ce que SYNE émet réellement depuis l'étape 3b — `ObtenirObjet` (tag `edible`) pour la faim, `Consommer { item }` pour manger un objet porté identifié, `Aller` en repli ; objets comestibles au sol nommés `resource-food-<id>` | Le document décrivait la cible (macro-actions + indice) sans dire ce qui était branché, et la faim n'aboutissait pas en mode couplé |
| 3 novembre 2026 | Vocabulaire des macro-actions aligné (`FabriquerMacro`, `DemarrerChantier`) | Audit de cohérence avec le code |
| 2 novembre 2026 | Ajout du client HTTP lockstep ; mapping d'actions et extraction du monde restent ouverts | SYNE-210/220 |
| 1er novembre 2026 | Création | Refonte V2 |
