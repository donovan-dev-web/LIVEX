# SYSTEMS_SPEC — Chaînes de production, structures, chantiers et coopération

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — LEDGER ET SOCLE DES ACTIONS/CONTENEURS IMPLÉMENTÉS]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`DATA_MODEL.md`](DATA_MODEL.md), [`CONTENT_SPEC.md`](CONTENT_SPEC.md), [`SIMULATION_LOOP.md`](SIMULATION_LOOP.md)
**Source Monographie** : —

---

## 1. Conservation de la matière

**Invariant.** Aucun objet n'est dupliqué ni créé sans règle déclarée ; aucun n'est détruit sans règle déclarée. Pour chaque type d'objet :

`présents(t) = présents(0) + créés(t) − détruits(t)`, où `présents` = inventaires + conteneurs + sol.

| Événement | Règle (`ruleKind`) |
| :-- | :-- |
| Création | `mine` (drops d'un bloc), `harvest` (drops d'une culture), `recipe` (sorties), `generator` (source `infinite`, remplissage de seau) |
| Destruction | `consume` (consommation), `recipe` (entrées), `wear` (outil épuisé), `spoil` (péremption), `place` (pose d'un bloc) |
| Déplacement | `transfer`, `drop`, `pickup`, `deposit`, `take` |

Le **registre d'audit** (`MatterLedger`) compte créations et destructions par `ruleId`. En build de test, l'invariant est vérifié tous les `auditIntervalTicks` ; sa violation fait échouer le run. Il fonde aussi les métriques d'ECHOS (production, consommation, pertes).

La consommation d'un objet retire exactement une unité et l'enregistre sous `consume`. Boire auprès d'un matériau `drinkable` perçu et adjacent transmet ses effets via `item_consumed` sans détruire ni modifier la source, qui est infinie dans le contenu minimal.

## 2. Outils et durabilité

Un outil s'use à chaque action réussie qui l'emploie (`durability` décrémentée de `usePerAction`, défaut 1). La durabilité restante est portée par sa pile, qui ne peut contenir qu'un outil ; dépôts et retraits de conteneur la préservent. À zéro, l'outil est retiré et détruit au ledger sous `wear`. Le socle applique cette règle au minage, au labourage avec houe et aux recettes déclarant un outil. Cela crée une **demande continue** d'outils, donc un échange possible. Le palier d'outil détermine les blocs minables ; la vitesse de minage est `hardness × baseTicksPerHardness ÷ (1 + tierBonus)`, réduite par la maîtrise.

## 3. Péremption et stockage

Chaque objet périssable a `shelfLifeTicks`. La fraîcheur décroît au rythme du conteneur (agent : 1 000 ‰ ; coffre : 800 ‰ ; grenier : 500 ‰). Un objet périmé est détruit (`spoil`) ; l'événement est publié. Cuire (`flatbread`) augmente valeur nutritive et durée de conservation : **stocker et cuire deviennent utiles**.

Le socle d'exécution conserve la fraîcheur restante en permille-ticks et le tick de dernière matérialisation. À l'accès ou au transfert, la durée écoulée est appliquée au taux du contenant source ; un transfert matérialise d'abord ce taux avant que le contenant de destination soit utilisé pour la suite. À zéro, la pile entière est retirée, un événement de péremption est retourné et `MatterLedger` enregistre `spoil`. Le sweep périodique peut appeler la même opération d'accès pour éviter un parcours exhaustif à chaque tick.

## 4. Les sept mécanismes qui rendent la coopération rentable

L'hypothèse de l'auteur tient *sous conditions* : les chaînes **créent** des situations où coopérer rapporte plus qu'agir seul, elles ne le **garantissent** pas.

| Mécanisme | Fonctionnement | Règle de monde qui le déclenche | Mesure pour ECHOS |
| :-- | :-- | :-- | :-- |
| Coûts fixes partagés | Un four, un puits ou un grenier coûtent cher à construire et servent plusieurs agents | Structures à coût élevé, effet utilisable par tous | Utilisateurs distincts par structure ; coût par usage |
| Division du travail | Maîtrise élevée → durée et pertes réduites ; spécialiser puis échanger rapporte plus | Maîtrise par catégorie | Indice de spécialisation, volume d'échanges |
| Lissage du risque | Délai travail → récolte ; un stock commun amortit les mauvaises périodes | Croissance lente, péremption, greniers | Variance des stocks, survie en période creuse |
| Logistique | Inventaire borné et distances : le transport est un travail | Capacité de transport limitée | Distance transportée, trajets par objet |
| Biens collectifs | Un chemin ou un puits vaut plus quand beaucoup l'utilisent ; l'entretien est partagé | Structures non rivales, usure | Fréquentation, contributions à l'entretien, passagers clandestins |
| Partage d'information | La position d'un filon ou d'un champ mûr vaut de l'échange | Mémoire spatiale locale, messages locaux (rayon 50) | Messages informatifs suivis d'une action |
| Travail parallèle | Un grand chantier est trop long pour un seul agent | Durée de chantier proportionnelle au volume | Contributeurs simultanés, durée de chantier |

### 4.1 Conditions d'échec (à surveiller)

- **Un seul agent suffit** : si la durée d'un chantier ou d'une chaîne reste tenable seul, aucune coopération n'est nécessaire → augmenter coûts fixes ou réduire capacité.
- **Tout est trop facile ou trop dur** : extinction rapide ou surabondance font disparaître la question.
- **Les agents ne perçoivent pas l'opportunité** : problème de cognition (SYNE), pas de monde ; à distinguer en ablation.

## 5. Structures, chantiers, propriété

### 5.1 Une structure a un effet mesurable

| Structure | Effet mesurable | Qui en profite |
| :-- | :-- | :-- |
| Abri (murs et toit) | Récupération de fatigue plus rapide dedans (`sheltered` transmis à SYNE) | Occupants |
| Grenier / entrepôt | Capacité accrue, péremption ralentie | Le groupe qui l'approvisionne |
| Établi, four | Stations ; sans elles, recettes impossibles | Tous les utilisateurs |
| Puits | Eau accessible près de l'habitat, irrigation des cultures voisines | Tous ceux qui passent |
| Chemin | Déplacement plus rapide (`pathSpeedupFactor`) | Tous, sans rivalité |
| Escalier, pont | Franchissement d'un obstacle ou dénivelé | Tous |

Une structure sans effet mesurable est refusée par le validateur de pack (`effect` obligatoire sauf arbres).

### 5.2 Chantiers et stigmergie

États : `planned → supplied → inProgress → completed | abandoned`. Plusieurs agents peuvent contribuer sans se parler : le chantier est **visible dans la perception** (plan, matériaux manquants, avancement). Les blocs du gabarit sont posés **progressivement** (couche par couche) selon l'avancement, pour que l'ouvrage soit lisible. Un chantier non alimenté pendant `siteAbandonAfterTicks` passe à `abandoned` ; ses matériaux déposés restent au sol au pied du chantier (conservation).

### 5.3 Gabarits plutôt que construction libre

Voir ADR-010. Les agents choisissent gabarit, position, rotation (4) et moment.

### 5.4 Propriété et accès (Q-02)

PRISM stocke pour chaque structure et conteneur une `accessList` : identifiants autorisés (agents ou **identifiants de groupe opaques**) avec droits `use`, `take`, `put`, `build`. Une liste vide laisse l'accès ouvert ; une liste configurée refuse tout principal non autorisé, tandis que le propriétaire conserve tous les droits. **PRISM ne sait pas ce qu'est un groupe** : SYNE traduit l'appartenance en identifiants et met la liste à jour par commande (`DéfinirAccès`, réservée au propriétaire). Sans règle d'accès, vol et passager clandestin n'existent pas ; avec elle, ils deviennent possibles et mesurables (refus `access_denied` observables).

Le socle de conteneur implémente la capacité par emplacements, l'accès propriétaire/agent/groupe opaque et les opérations atomiques `Prendre`/`Deposer`. La fraîcheur est portée par les piles d'inventaire et au sol, et les fusions refusent de mélanger deux états de fraîcheur différents. Un conteneur applique son taux de décroissance à la prise, détruit les piles périmées et les compte dans le ledger (`spoil`) ; la prise échoue alors avec `item_spoiled`. Les inventaires d'agents sont balayés à chaque tick de cycle de vie (1 000 ‰/tick) et chaque péremption publie un événement `item_spoiled` avec le type d'objet et la quantité détruite.

Le socle de chantier porte aussi un propriétaire et une ACL agent/groupe : seul le propriétaire peut la remplacer, une liste vide reste ouverte et le droit `build` contrôle les contributions. `DefinirAcces` est dispatchée par le host et l'état survit au checkpoint ; le droit `use` est appliqué par la consommation d'effets d'abri (propriétaire, agent ou groupe), les autres usages restent à raccorder.

Le socle de fabrication compile les recettes vers des identifiants stables, développe les tags en ensembles d'objets admissibles, prévalide les ingrédients et la place de sortie, puis échange les entrées/sorties tout-ou-rien avec le ledger. Les recettes à station vérifient le matériau présent, la portée, la perception et une réservation jusqu'au tick de fin ; le temps est ajusté par la maîtrise. La finalisation différée des actions et leur raccordement au tick sont en place (`action_started`/`action_completed`, canal `DeferredActionResolutions`) ; l'usure des outils est appliquée à chaque usage réussi.

### 5.5 Usure et entretien (optionnel)

Une structure peut perdre de la durabilité avec le temps ou l'usage (`wear.structure.decayPerTickPermille`). Désactivée par défaut ; activée **après** la validation des phases précédentes, car elle ajoute charge de simulation et de calibrage.

## 6. Maîtrise

Chaque agent progresse par catégorie (`mine`, `craft`, `farm`, `build`, `smelt`). Chaque action réussie ajoute `gainPerActionPermille`, plafonné par `maxSpeedupPermille` : la durée de l'action diminue d'autant, le rendement peut augmenter. La maîtrise est **l'état physique du savoir-faire** (PRISM) ; le **savoir des recettes** reste dans SYNE.

## 7. Mortalité et naissances

`Incarner(agentId, profile, position)` crée le corps ; `Retirer(agentId, reason)` retire le corps et **dépose l'inventaire au sol** (conservation). Les sites de naissance et les règles de naissance restent de la compétence de SYNE ; PRISM refuse seulement une position invalide.

## Points restés ouverts dans ce document

Q-02 (modèle d'accès) à trancher à la porte G4.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 3 novembre 2026 | ACL de chantier raccordées à `DefinirAcces` ; finalisation différée et usure des outils documentées comme en place | Audit de cohérence avec le code |
| 2 novembre 2026 | Registre de création/destruction et audit de conservation amorcés dans Prism.Core | PRISM-027 |
| 1er novembre 2026 | Création | Refonte V2 |
