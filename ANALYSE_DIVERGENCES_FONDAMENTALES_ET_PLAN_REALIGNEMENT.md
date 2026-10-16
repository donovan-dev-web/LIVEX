# Analyse des divergences fondamentales entre vision originelle et projet actuel

*Date : 16 octobre 2026*

> **Périmètre et méthode.** Ce document confronte la vision normative de `docs/monographie/` (version décrite au 12 octobre 2026), le snapshot historique `LIVEX-Monographie_SnapV0-1.md`, l’implémentation actuellement présente dans `syne/` et le prototype temporaire `SSE-emergent-simulation/`. Le prototype n’est utilisé que comme témoin historique et contre-exemple possible, jamais comme spécification. Les constats de code ci-dessous sont fondés sur les fichiers actuellement présents et cités ; ils priment sur les affirmations, exemples de calcul et schémas de la monographie quand ceux-ci ne correspondent pas au code. Les tests qui passent prouvent les assertions testées, pas la conformité intégrale de SYNE à la vision.

**Échelle de qualification.** « Confirmée » signifie directement visible dans l’implémentation ou test ; « partielle » signifie qu’un mécanisme existe, mais ne satisfait qu’une partie de l’exigence ; « à vérifier » indique une question non tranchée à partir de la lecture ciblée. « Critique » désigne un écart qui invalide l’interprétation de la simulation au regard de la question directrice, « majeur » un écart qui fragilise des résultats ou la validité d’un sous-système, et « modéré » une limite contenue ou correctement déclarée. Le nombre d’items n’est pas un décompte de bugs indépendants : plusieurs décrivent des conséquences d’une même rupture causale.

**Conclusion corrigée d’emblée :** l’analyse ne conclut pas que le dépôt entier est « presque à recommencer ». SYNE possède déjà un monde déterministe, une grille spatiale, des tests de trajectoire, un PRNG sérialisé, une persistance et des interfaces headless significatives. En revanche, la cognition active ne relie pas encore les composants annoncés en une boucle de décision à information imparfaite : la perception ne nourrit pas la sélection d’action par souvenirs/croyances, certains besoins agissent comme déclencheurs d’action, et des opérations d’interaction consultent globalement le monde. Le noyau décisionnel doit être réaligné et ses contrats de preuve resserrés ; cela ne justifie pas de jeter tout le dépôt.

## 1) Résumé exécutif

Le constat est sans ambiguïté : **le cœur décisionnel actif de SYNE s'est écarté de l'objectif fondamental**, même si plusieurs fondations techniques et composants restent réutilisables.

L'objectif originel n'était **pas** de "créer un simulateur multi-agents fonctionnel", mais de **construire un dispositif expérimental** permettant de répondre à une question précise :

> *Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?*

Pour y répondre valablement, le projet exige : **lois locales explicites, autonomie fonctionnelle traçable, observabilité partielle stricte, déterminisme reproductible bit-à-bit, séparation rigoureuse décider/mesurer/montrer/archiver**.

Le noyau actuel possède déjà de nombreux éléments de dispositif expérimental (déterminisme, persistance, perception partielle, traces et mesures), mais leur articulation cognitive ne réalise pas encore la chaîne épistémique annoncée. L’écart central est donc **une rupture entre le modèle déclaré et les causes actives de certaines décisions**, pas une opposition globale entre « théâtre » et « microscope ».

**Recommandation :** ne pas réécrire l’intégralité du dépôt. **Réaligner d’abord la chaîne décisionnelle de SYNE** : faire entrer l’information partielle (perception, souvenirs, croyances) dans la génération des candidats et le choix de cibles/actions; fermer les recherches globales dans les chemins agentiques optionnels; appliquer la fenêtre causale décrite par le chapitre III; compléter la trace causale. Conserver les briques déjà prouvées (grille/rayon/occlusion, `Goal` et utilité décomposable, PRNG, reprise, calcul collectif décrit, transports et gaps/provenance ECHOS). Les aspects PRISM, Launcher et autres composants restent secondaires à ce diagnostic : les documents les donnent comme partiellement validés, mais une validation en attente n’est pas en soi une divergence de code.

## 2) Objectif réel du projet

### 2.1 Question directrice

Extraite de `LIVEX-Monographie_SnapV0-1.md` et confirmée par `docs/monographie/01-introduction.md` :

> **Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?**

### 2.2 Exigences découlant de cette question

D'après `docs/monographie/02b-question-decomposee.md`, chaque terme impose des exigences concrètes et testables :

| Terme | Exigence | Signification pratique |
|---|---|---|
| **Environnement virtuel** | Espace à conséquences | Tout déplacement/action coûte. Ressources limitées. Rien n'est gratuit. |
| **Entités autonomes** | Décision sans commande externe, explicable | Besoin génère un **Objectif**, pas une action. L'utilité tranche entre candidats. Traçable. |
| **Lois fondamentales** | Règles du monde identiques et explicites; les mécanismes agentiques ne doivent pas imposer de macro-résultat | Les règles locales sont centrales à la vision; cependant, le chapitre SYNE VII décrit un système d’observation global des relations et une propagation de décision collective. La portée exacte de « local » doit être conciliée avec ce mécanisme déclaré. |
| **Comportements/structures non explicitement programmés** | Émergence **faible** traçable & rejouable | Doit pouvoir remonter `action <- intention <- objectif <- besoin <- croyance <- mémoire <- perception`. Rejouable à graine identique. |

### 2.3 Posture méthodique

`docs/monographie/00-AVANT-PROPOS.md` insiste lourdement sur :

- **Séparer fait / interprétation / hypothèse**. Un groupe observé = fait. L'appeler "société" = interprétation. Tirer une loi générale = hypothèse.
- **Distinguer "observable" de "démontré"**. Le dispositif doit **pouvoir** observer. L'observation effective doit être prouvée.
- **Distinguer "zéro observé" / "zéro non-mesuré"**. Ne jamais combler un trou de mesure.
- **Traçabilité > spectaculaire**. Mieux vaut modeste et expliqué qu'impressionnant et opaque.
- **Reproductibilité scientifique**. Déterminisme exigé.

## 3) Vision architecturale prévue

`docs/monographie/04b-projet-vision-architecture.md` formalise 4 responsabilités avec **interdits** non-négociables.

### 3.1 Séparation stricte

| Composant | Rôle | Interdit fondamental |
|---|---|---|
| **SYNE** | *Décider* – détient l'unique vérité du monde | Être mis en scène. Être influencé par rendu/analyse. |
| **ECHOS** | *Mesurer sans toucher* – observatoire en lecture seule | Modifier le monde. Interpoler. Inventer des données. |
| **PRISM** | *Montrer sans décider* – couche visuelle | Calculer décisions. Modifier état. Extrapoler pour "lisser". |
| **Launcher** | *Archiver sans relayer* – orchestration + scellement | Relayer le flux WebSocket ou s’interposer entre SYNE et consommateurs; la conformité est une porte d’acceptation à tester, non un constat établi par cette analyse. |

### 3.2 Principe d'indépendance

> *"SYNE peut exister sans ECHOS et PRISM. ECHOS et PRISM peuvent évoluer indépendamment tant qu'ils respectent les contrats définis avec SYNE."*

Le flux doit rester **unidirectionnel** : `SYNE -> (snapshot + events) -> ECHOS` et `SYNE -> PRISM`. Le pilotage est séparé via port de contrôle.

### 3.3 Les principes fondamentaux

La monographie regroupe sept principes : absence de narration imposée, autonomie fonctionnelle, observabilité partielle, déterminisme reproductible, persistance, indépendance du rendu et observabilité scientifique. Pour l’audit de SYNE, ils se traduisent en contrats mesurables; ils ne signifient pas que toutes les sous-couches utilisent des calculs locaux. La question de la compatibilité entre agrégation globale des groupes et principes locaux est traitée comme arbitrage de spécification en §5.2 et §9.

## 4) Comparaison des sources et valeur probante

Les sources n’ont pas le même statut et ne doivent pas être fusionnées en une « vision » indifférenciée.

| Source | Rôle dans cette analyse | Ce qu’elle établit | Ce qu’elle n’établit pas |
|---|---|---|---|
| `LIVEX-Monographie_SnapV0-1.md` | Snapshot de conception historique | La question fondatrice, l’intérêt pour l’autonomie, le BDI, l’observation partielle, le monde persistant et la recherche de structures non scriptées | Les détails de la version courante ni le statut actuel d’une fonction |
| `docs/monographie/` | Référence normative principale | Les exigences testables, la séparation des composants, les mécanismes et les limites revendiqués pour l’état décrit | Que chaque phrase du livre corresponde effectivement au code présent aujourd’hui : la monographie demande elle-même de faire primer le code |
| `SSE-emergent-simulation/` | Prototype temporaire, témoin historique | Les hypothèses techniques testées avant la version courante : ressources localisées, mémoire, Utility AI, moteur .NET et intégration | Une spécification à recopier, ni une preuve scientifique d’émergence, ni une version plus fidèle à tous les principes |
| `syne/`, `echos/` et tests présents | Réalité technique vérifiable | Ce que l’implémentation fait, ainsi que la portée exacte des tests | Que les comportements produits répondent à la question scientifique, au-delà des assertions vérifiées |

Le prototype dispose de sources de nourriture/eau localisées et de candidats ciblés bâtis depuis les identifiants connus par l’agent; sa perception ajoute ces identifiants au fil des observations. Il n’est toutefois pas une norme : son `DecisionSystem.Score` applique un fort biais de survie par seuil, et son calcul d’attaque consulte la nourriture totale et la population globale. La version actuelle spécifie explicitement des réserves globales, et la feuille de route récente laisse les sources spatiales ouvertes. Il faut comparer ces portées à la question directrice, sans copier le prototype ni prétendre que son économie locale est déjà la norme du projet.

Les fondations réutilisables du code actuel sont réelles : grille et perception locale, occlusion, mémoire à décroissance, structures de croyances, formule d’utilité décomposée, groupes déterministes, PRNG sérialisé, reprise et instrumentation ECHOS. La divergence majeure n’est pas « ces noms/classes manquent », mais que plusieurs d’entre eux **ne participent pas à la politique active** ou ne sont pas utilisés avec la sémantique promise. Les constats détaillés et référencés suivent.


## 5) Constat d’implémentation : ce qui est établi, et ce qui ne l’est pas

Cette section remplace les généralisations de la première analyse par des constats vérifiables. Elle distingue les écarts certains des risques à mesurer. L’objectif n’est pas de condamner tout mécanisme déjà codé, mais de déterminer si chaque mécanisme sert réellement la chaîne causale promise.

### 5.1 SYNE : cognition, choix et observabilité partielle (priorité absolue)

| ID | Constat dans l’implémentation | Référence de code ou preuve | Qualification | Gravité |
|---|---|---|---|---|
| S-01 | La perception est réellement bornée par rayon via `SpatialGrid.QueryCircle`, filtrée par ligne de vue, triée et étagée par rotation. Il existe des tests de portée, de masquage, d’ordre et de rotation. Ce socle est aligné ; ce n’est pas un défaut à réécrire. | `syne/Simulation.Core/Perception/Perception.cs`, `syne/Simulation.Core/World/SpatialGrid.cs`, `syne/Simulation.Core.Tests/Perception/PerceptionTests.cs` | Confirmée, alignée | — |
| S-02 | Les observations d’entités sont stockées dans `Memory` et converties en croyances de position. En revanche, la sélection d’objectifs et les bénéfices d’action reçoivent `BodyNeeds`, traits, taux de succès, objectif collectif et stocks globaux, pas de `MemoryRecall` ni de croyances individuelles. `GroupSystem` lit bien les croyances pour former/relier des groupes, mais cela ne rend pas ces croyances causales dans la délibération individuelle. | `CognitionPipeline.StepEntity/Deliberate`, `StoreObservation`, `DesireFactory.Generate`, `UtilityEvaluator.Evaluate`; `Memory.Recall` n’est pas appelé dans `Simulation.Core` ; `GroupSystem.SortedBeliefsOf/SharedBeliefCount` est le consommateur cognitif actuel | Confirmée | **Critique** |
| S-03 | `DesireFactory` transforme un besoin déclenché de faim/soif directement en `Eat`/`Drink` si la réserve globale existe, sinon en `SeekFood`/`SeekWater`. La disponibilité de réserve décide donc une action terminale, et les objectifs ne sont pas des plans dérivés d’une croyance locale sur une cible. `SeekFood` / `SeekWater` ne conduisent pas à une cible alimentaire observée : le déplacement est pseudo-aléatoire local. | `syne/Simulation.Core/Cognition/DesireFactory.cs`; `CognitionPipeline.StepEntity`; `ActionExecutor.MoveTowardDeterministicTarget` | Confirmée | **Critique** |
| S-04 | L’utilité est décomposée dans `UtilityScore` (bénéfice, coût, risque, confiance, personnalité, urgence) et des tests vérifient la formule. C’est un bon socle. Toutefois le facteur `Confidence` de cette formule provient de `SuccessRate`, non de la confiance dans les croyances/perceptions. Ce taux est ajusté uniformément pour chaque type de désir selon que l’agent a observé au moins un objet (+0,15) ou non (−0,01); le résultat `ActionResult` est enregistré, mais ne met pas à jour ce taux. | `syne/Simulation.Core/Cognition/UtilityEvaluator.cs`, `MindState.AcknowledgePerceptions/SuccessRate/RecordAction`; `CognitionPipeline.StepEntity` | Partielle, sémantique trompeuse | **Majeure** |
| S-05 | Le moteur possède bien `Goal`, une génération de candidats, un calcul d’utilité, un départage et des mécanismes d’hystérésis/interruption : il ne s’agit pas d’une absence de décision BDI ou d’arbitrage numérique. Mais les candidats du scénario nominal sont essentiellement générés par seuils de besoins (plus `Idle`), et leur score ne consomme ni le rappel mémoire ni une croyance individuelle sur cible/faisabilité. | `DesireFactory.Generate`; `CognitionPipeline.Deliberate`; `UtilityEvaluator.Evaluate`; `PriorityConflictResolver` | Partielle : décision structurée, fondement épistémique absent | **Critique** |
| S-06 | L’intention est un `Goal` de type `DesireKind` avec tick de naissance, âge, holdover, interruptions et réévaluation périodique. Elle ne porte toutefois ni cible concrète ni plan multi-étapes; la bibliothèque de plans qui ajoute certaines primitives est optionnelle et désactivée par défaut. | `Goal`, `MindState.Intention`, `ActiveGoals`, `PlansSettings`, `CognitionPipeline.StepEntity` | Partielle | **Majeure** |
| S-07 | La mémoire stocke des chaînes d’observation avec une confiance, calcule une saillance exponentielle et évince le moins saillant. C’est cohérent comme mécanisme de rétention. Cependant, aucune lecture de ces souvenirs n’alimente les objectifs, la recherche de cible ou la décision courante ; elle n’est donc pas encore une mémoire fonctionnelle au sens de la cognition annoncée. | `Memory.Store`, `Recall`, `SalienceOf`; `CognitionPipeline.StoreObservation`; appelants réels de `Recall` | Confirmée, usage incomplet | **Critique** |
| S-08 | La révision des croyances ne met pas en œuvre la formule décrite au chapitre XI : `BeliefRevision.Revise` existe et ses tests unitaires la couvrent, mais le pipeline appelle `BeliefSet.ApplyEvidence`, qui applique bonus de source identique, moyenne dans un cas et pénalité de conflit dans un autre. `UpdateStrength` et `MaxChangePerSnap` n’agissent donc pas sur les observations du pipeline. L’expiration plafonne la confiance après décroissance, mais ne supprime pas la croyance. | `syne/Simulation.Core/Cognition/Belief.cs`, `syne/Simulation.Core/Cognition/CognitionPipeline.cs` (`StoreObservation`), `syne/Simulation.Core.Tests/Cognition/BeliefTests.cs`; paramètres `BeliefSettings` | Confirmée | **Majeure** |
| S-09 | Les croyances d’agent décrivent les positions perçues; les positions des cibles bougent après observation, mais le système n’entretient pas une estimation active d’incertitude/âge qui soit utilisée pour agir. Les faits peuvent coexister avec des valeurs de position contradictoires sous la même relation sujet-prédicat. | `Fact`, `BeliefSet`, `StoreObservation`; absence d’accès décisionnel aux croyances | Confirmée | **Majeure** |
| S-10 | Le contrôle « confiance perceptuelle » baisse avec la distance, et le masquage physique est actif. Mais la confiance donnée à un message est multipliée par la confiance relationnelle avant `Trust.Interact`; pour une première réception, la confiance relationnelle par défaut est 0, de sorte que la confiance effective initiale est zéro. La valeur effective n’est pas appliquée à une croyance dans le pipeline décrit. | `CommunicationSystem.Receive`; `Relationships.TrustWith` et `Interact` | Confirmée, ordre/sémantique à tester | **Majeure** |
| S-11 | `WantsToShare` met en file une pulsation publique `Information` quand le premier objet vivant est perçu et la sociabilité ≥ 0,5 (trait neutre 1,0). L’émission est automatique plutôt qu’arbitrée par utilité/objectif. La réception stocke le message et sa compréhension dans `CommunicationState`, mais la confiance effective et le payload ne sont pas convertis en croyance par le pipeline. | `CognitionPipeline.WantsToShare/EnqueueSharePulse/StoreObservation`; `CommunicationSystem.Receive` | Confirmée | **Majeure** |
| S-12 | Les coûts des besoins sont appliqués séparément dans l’exécuteur. Le coût d’énergie peut être débité par `CommunicationSystem` à l’émission/réception ; l’énergie est écrêtée à zéro sans échec de communication. À paramètres par défaut les coûts sont nuls, mais des valeurs non nulles ouvrent un chemin de bilan où délibération, actions et coûts de communication ne sont pas une transaction atomique unique. | `CommunicationSystem.DispatchSingle/Receive`; `BodyNeeds.ExertEnergy`; défauts de configuration | Confirmée, surtout sur configurations non nominales | **Majeure** |
| S-13 | Social et Curiosity dérivent et déclenchent `Socialize`/`Explore`, mais ces actions déplacent l’agent sans décrémenter ces besoins; aucune autre satisfaction active n’est branchée. Safety diverge aussi du chapitre X : `Advance` lui ajoute le `SafetyDriftRate` positif (0,001 par défaut) et la borne à 1, alors que le modèle décrit une dérive vers l’insécurité; avec sa valeur initiale 1, elle ne baisse pas passivement. Elle ne peut diminuer que par menace explicite (par ex. attaque), et `Flee` n’est candidat que sous le seuil. `Give` est une primitive optionnelle, sans baisse de Social. | `BodyNeeds.Advance/IsTriggered/ThreatenSafety`; `ActionExecutor`; `SimulationOptions.NeedsSettings` et catalogue; `DesireFactory` | Confirmée, divergences de dynamique et de satisfaction | **Majeure** |
| S-14 | Les traits sont individualisés; sociabilité affecte partage, agressivité affecte attaque, bravoure/curiosité/sociabilité/avarice modulent des scores, et vitesse modifie le déplacement. `pessimism` et `strength` ne sont pas consommés par la décision, comme l’indique le chapitre IX. L’hétérogénéité de traits ne compense cependant pas l’absence de dépendance décisionnelle aux croyances et souvenirs rappelés. | `AgentFactors`, `UtilityEvaluator.PersonalityModifierOf`, `ActionExecutor`, chapitre SYNE IX | Confirmée, déclarée | Modérée |
| S-15 | L’ordonnancement effectif n’est pas la séquence conceptuelle d’un agent qui observe un état figé puis agit : les agents sont traités séquentiellement par id, et l’action d’un agent modifie immédiatement positions/états avant le traitement de l’agent suivant. Un agent suivant peut donc percevoir une position déjà déplacée dans le même tick. Les conséquences ne sont pas mises en lot avec commit à fin de tick. | `CognitionPipeline.Step` (boucle `ordered` puis `StepEntity`); `ActionExecutor` mutates `World.Grid` | Confirmée | **Critique** |
| S-16 | `Memory.RecallThreshold` sert au filtrage de `Recall`, mais aucun appel à `Recall` n’existe dans le code `Simulation.Core` hors définition/tests/observabilité. `BeliefSettings.UpdateStrength` et `MaxChangePerSnap` ne sont pas utilisés par `ApplyEvidence`; `SalienceSettings.InterruptionConditionWeight` est déclaré mais `ComputeSalience` renvoie `double.MaxValue` si un besoin est critique et ne cumule que les franchissements de seuil. | `Memory.Recall`; `BeliefSet.ApplyEvidence`; `CognitionPipeline.ComputeSalience`; `SimulationOptions` | Confirmée, paramètres inertes/usage incomplet | **Majeure** |

### 5.2 Monde, action et collectif

| ID | Constat dans l’implémentation | Référence de code ou preuve | Qualification | Gravité |
|---|---|---|---|---|
| S-17 | Les ressources sont explicitement définies comme des stocks globaux dans la monographie et le code; `DesireFactory` consulte leur viabilité et `Eat/Drink` les consomment sans cible spatiale. C’est conforme au modèle global décrit, mais cela ne permet pas d’étudier une convergence autour de gisements localisés. La feuille de route actuelle classe justement les sources spatiales comme question ouverte : il s’agit d’une limite de portée expérimentale à arbitrer, pas d’une divergence par rapport à la définition présente. | `syne/Simulation.Core/World/ResourceStocks.cs`, `DesireFactory.ResolveTerminal`, `ActionExecutor`, chapitre SYNE I §2, annexe feuille de route | Confirmée, limite de portée / décision ouverte | **Majeure pour les questions spatiales** |
| S-18 | Lorsque la réserve globale est vide, `SeekFood/SeekWater` entraîne un déplacement pseudo-aléatoire déterministe, non une navigation vers un emplacement cru : A* ne fait que contourner les obstacles rencontrés sur cette cible locale hachée. Cela ne contredit pas le modèle actuel de réserve globale, mais interdit d’interpréter `SeekFood` comme une quête d’une source alimentaire localisée. | `syne/Simulation.Core/Actions/ActionExecutor.cs` (`MoveTowardDeterministicTarget/PathStep`), `syne/Simulation.Core/Cognition/DesireFactory.cs` (`ResolveTerminal`), chapitre SYNE I §2-4 | Confirmée, représentation à ne pas surinterpréter | **Majeure** |
| S-19 | Le système de groupes est réellement fondé sur les paires de relations réciproques, buts partagés et croyances partagées, comme le chapitre VII le décrit. L’implémentation examine centralement toutes les paires et propage ensuite une décision collective à l’utilité individuelle; c’est une loi d’agrégation moteur, pas une décision locale de l’agent. En outre, les croyances partagées proviennent d’observations mais pas de messages reçus. La mécanique est cohérente avec le chapitre des groupes, mais sa compatibilité avec l’exigence plus générale de lois exclusivement locales reste un arbitrage de vision. | `syne/Simulation.Core/Social/GroupSystem.cs` (`ComputeComponents/AreBonded/SharedBeliefCount/PropagateObjectives`), `syne/Simulation.Core/Communication/CommunicationSystem.cs` (`Receive`); chapitre SYNE VII | Confirmée, tension entre spécifications | **Majeure, arbitrage de vision** |
| S-20 | `ActionExecutor.NearestOtherEntity` parcourt `_world.Entities` en entier sans rayon perceptuel, ligne de vue ou croyance mémorisée. Give/Trade/Attack sont liés à des primitives optionnelles et au plan/inventaire dans le profil de référence, mais lorsqu’ils sont activés leur cible est choisie dans le monde global. La partialité doit donc être garantie dans ces chemins avant de les revendiquer comme comportements autonomes locaux. | `syne/Simulation.Core/Actions/ActionExecutor.cs`, `NearestOtherEntity`, `PlansSettings`, `InventorySettings` | Confirmée, optionnelle | **Critique si activée** |
| S-21 | La communication et la perception sont géographiquement bornées par grille/ligne de vue et quotas; l’incompréhension est déterministe et le relais désactivé par défaut. À la réception, un message compris est enregistré dans l’état de communication, mais le contenu n’est pas appliqué aux croyances ni transformé en souvenir cognitif. La réception n’alimente donc pas la cognition de croyances annoncée. | `syne/Simulation.Core/Perception/Perception.cs`, `syne/Simulation.Core/Communication/CommunicationSystem.cs` (`Receive`), `CommunicationState`; absence d’appel de mise à jour des croyances depuis la réception | Partielle | **Majeure** |
| S-22 | Les coûts et stocks ont une base testée, des actions terminales Eat/Drink atomiques pour le prélèvement global, et des calculs de viabilité documentés. La survie n’est pas encore une économie locale : chaque repas consomme une unité du stock abstrait, la régénération est un flux global, et `SeekFood` ne paie pas de trajet vers une source. | `ResourceStocks.TryConsume`, `ActionExecutor.Execute`, `ResourceStocks.ApplyLifecycle` | Confirmée | **Majeure** |
| S-23 | Le système de groupes parcourt l’ensemble des esprits, calcule les liens par paires (O(N²)), forme les composantes globales puis peut propager un objectif collectif dans l’utilité individuelle. Le chapitre VII spécifie cette observation centrale; elle n’est donc pas une implémentation contraire à ce chapitre. La tension porte sur les formulations transversales d’émergence locale et sur l’effet réel d’un objectif collectif global dans la cognition individuelle. La spécification doit distinguer observateur agrégateur et loi d’action individuelle. | `syne/Simulation.Core/Social/GroupSystem.cs` (`ComputeComponents/PropagateObjectives`), `syne/Simulation.Core/Cognition/CognitionPipeline.cs`, `syne/Simulation.Core/Cognition/UtilityEvaluator.cs`; chapitre SYNE VII | Confirmée, portée de l’architecture à clarifier | **Majeure** |

### 5.3 Déterminisme, persistance et instrumentation (réviser les assertions existantes)

| ID | Constat dans l’implémentation | Référence de code ou preuve | Qualification | Gravité |
|---|---|---|---|---|
| S-24 | Le test établit l’égalité du journal construit (positions/besoins arrondis à 2 décimales, compteurs, intentions, nombre de souvenirs/relations/croyances) sur les scénarios testés, et un checksum golden est épinglé. Il n’effectue pas une comparaison de l’état complet à pleine précision chaque tick ni de tous les événements/champs cognitifs. Le déterminisme est testé, mais l’expression « bit-à-bit sur l’état observable complet » du commentaire dépasse l’assertion réelle. | `syne/Simulation.Core.Tests/Simulation/Ph10DeterminismBaselineTests.cs` (`BuildStateLog`, `FullPipeline_SameSeed_IsBitForBitReproducible`) | Confirmée, portée surestimée | **Majeure** |
| S-25 | L’état xoshiro complet (4 × 64 bits) est effectivement sérialisé, et les tests couvrent une reprise SQLite comparée à une exécution continue, ainsi que le hash des snapshots. C’est une force à conserver. La reprise d’un autre runtime, d’une autre plateforme ou après évolution de règles/config reste une question de compatibilité, pas prouvée par ces tests. | `SimulationSnapshotCodec`, `SimulationSnapshotRestorer`, `PersistenceTests` | Confirmée, portée bornée | — |
| S-26 | Le snapshot de simulation contient schéma moteur, tick, état PRNG, monde et cognition, mais pas un hash explicite de configuration canonique, ni une empreinte du code/commit. SQLite stocke la configuration sérialisée, tandis que les identifiants/horodatages de run incluent du temps mural ; l’archive de persistance n’est pas le paquet `.livexp` scellé du Launcher. | `SimulationSnapshot`, `SimulationSnapshotCodec`, `SqlitePersistenceStore.BeginRun/SaveTick` | Confirmée | **Majeure pour comparaison/archives** |
| S-27 | `DecisionRecord` conserve tick, agent, drapeaux, action retenue, utilité et scores des candidats, mais pas la chaîne perception→souvenir→croyance→but, les sources des composantes, cible/plan ni faits justificatifs. Le snapshot ne garde que le type de la dernière décision; `RestoreLastDecision` recrée un `UtilityScore` nul et ne rétablit pas `LastDecisionRecord` ni les scores. La trace diagnostique n’est donc pas restaurée à l’identique. | `DecisionRecord.cs`, `MindState.RecordDecision/RestoreLastDecision`, `MindSnapshotDto`, `SimulationSnapshotRestorer` | Partielle | **Critique pour l’explication; modérée pour l’état futur** |
| S-28 | L’événement `decision_made` publie la valeur de `mind.LastDecision` (qui peut être le score d’une délibération ancienne reportée) avec les besoins actuels, et publie `0.0` par défaut si aucune décision n’a eu lieu. Le champ `utility` peut donc être interprété à tort comme une mesure actuelle; l’absence est codée comme zéro observé. | `ExternalEvent.DecisionMade`; émetteur `ObservabilityTickEmitter` | Confirmée | **Critique pour honnêteté des données** |
| S-29 | ECHOS ne souffre pas d’une absence générale de gaps/provenance : le pipeline d’ingestion compte les discontinuités entre ticks enregistrés (`gaps_detected`), calcule des indicateurs `measured` via les déclarations de provenance des moteurs d’analyse, et persiste/sert un booléen par métrique et tick. La portée dépend toutefois du chemin d’ingestion et de la disponibilité du snapshot : ce mécanisme ne reconstitue ni les trames SYNE perdues ni leur contenu. L’absence alléguée doit être retirée; restent les tests contractuels bout en bout. | `echos/echos/storage/pipeline.py`, `echos/echos/analysis/__init__.py`, `echos/echos/storage/sqlite.py`, `echos/echos/tests/test_api_routes.py` | Confirmée, mécanisme existant à portée bornée | La divergence initiale est **à retirer** |
| S-30 | L’observabilité temps réel est « best effort » : clients lents/déconnectés entraînent une perte de trames et la simulation continue. C’est acceptable pour un flux de visualisation si les consommateurs savent détecter les trous et ne le confondent pas avec une archive exhaustive. Ce n’est pas acceptable comme seule source de preuve de trajectoire. | `syne/Simulation.Console/Observability/ObservabilityTickEmitter.cs` (`SafeBroadcastAsync`) | Confirmée, portée à rendre explicite | **Majeure** |
| S-31 | L’ordre causal effectif est séquentiel agent par agent : perception, besoin, décision et action d’un agent sont exécutées avant le suivant; communication et groupes suivent après la boucle. Les actions immédiates sur le monde peuvent donc influencer les perceptions suivantes dans le même tick. Le chapitre III promet au contraire un état partagé dont les conséquences d’action ne sont visibles qu’au tick suivant. | `syne/Simulation.Core/Simulation/SimulationLoop.cs` (`AdvanceOneTick`), `syne/Simulation.Core/Cognition/CognitionPipeline.cs` (`Step/StepEntity`), mutations de `syne/Simulation.Core/Actions/ActionExecutor.cs` | Confirmée, contradiction directe avec le contrat temporel | **Critique** |

### 5.4 Requalification des affirmations de la version antérieure

Les éléments qui suivent corrigent les formulations générales antérieures et guident la lecture des sections suivantes :

- **À retirer du diagnostic courant :** « ECHOS n’a pas de provenance / n’a pas de détecteur de trous ». Les indicateurs existent dans les chemins analysés (`gaps_detected`, `measured` par métrique/tick), sans reconstruire pour autant les trames perdues.
- **Faux ou non démontré :** « SYNE ne possède pas de déterminisme ou de persistance ». Il en possède, avec tests de golden run, reprise et état PRNG. La divergence porte sur la portée des assertions et l’instrumentation explicative.
- **Trop catégorique :** « aucune mémoire/croyance/BDI ». Les structures et algorithmes existent ; l’écart déterminant est que mémoire et croyances ne nourrissent pas la décision active, que la révision configurée est partiellement inopérante, et que les objectifs actuels sont pilotés principalement par des besoins.
- **Incorrect :** « décision pas du tout décomposable ». Les composantes numériques du score sont présentes; l’absence porte sur la chaîne causale, les observations qui justifient le score et l’identification des données manquantes.
- **À nuancer :** « aucune partialité ». La perception est partielle par rayon/ligne de vue; les ressources globales sont explicitement le modèle documenté, tandis que certaines primitives optionnelles font un scan global d’entités. Le système central de groupes est lui aussi explicitement décrit, mais sa compatibilité avec les principes généraux de localité doit être clarifiée plutôt que déclarée comme une simple fuite de code.
- **À nuancer :** « le prototype prouve le but initial ». Il constitue une origine technique et un témoin d’hypothèses; la monographie actuelle est la spécification normative. Le prototype a lui-même un Utility AI qui contient un biais de survie par seuils et un calcul global de rareté/population dans l’attaque (`DecisionSystem.Score` / `NeedUtil`). Il n’est pas une norme plus fidèle par défaut.

## 6) Divergences fondamentales consolidées

Les catégories ci-dessous remplacent les listes initiales trop générales. Leur priorité doit être lue avec les constats S-01 à S-31, pas comme une affirmation d’absence de fonctions.

### 6.1 Périmètre scientifique et posture de preuve

| ID | Divergence consolidée | Conséquence | Priorité |
|---|---|---|---|
| D-01 | La conformité technique (tests, transports, rendu) risque d’être confondue avec la réponse à la question d’émergence. | Un système fonctionnel n’établit pas qu’il observe des structures non programmées. | **Critique** |
| D-02 | Plusieurs paragraphes de la monographie expliquent des effets cognitifs que le pipeline actif ne produit pas encore, notamment l’usage des croyances et de la mémoire par la décision et l’effet cognitif des messages. | Le récit de résultats peut aller au-delà du mécanisme effectivement actif. | **Critique** |
| D-03 | Les rapports et mesures doivent différencier valeur mesurée, absence de décision/trace et zéro numérique; ECHOS possède déjà provenance/gaps de métriques et ingestion, mais l’événement `decision_made` de SYNE substitue actuellement `0.0` à l’absence et peut republier un score ancien. | Les données d’entrée peuvent déjà rendre une absence ambiguë avant ECHOS. | **Critique** |

### 6.2 Autonomie et chaîne BDI active

| ID | Divergence consolidée | Conséquence | Priorité |
|---|---|---|---|
| D-04 | La structure de décision existe (besoins→`Goal`→candidats→utilité→intention/action), mais la faim/soif est résolue en Eat/Drink dès que le stock global est disponible, sans cible crue, et les autres candidats proviennent essentiellement de seuils de besoin. | Chaîne de décision calculable mais qui ne confronte pas les objectifs à des connaissances individuelles du monde; elle n’est pas encore la chaîne BDI épistémique décrite. | **Critique** |
| D-05 | Les observations alimentent mémoire/croyances, mais ni rappel mémoire ni croyances ne nourrissent la génération de candidats ni l’utilité active. | « Voir, se souvenir, croire » existe en structures mais pas en causes de comportement. | **Critique** |
| D-06 | La trace décisionnelle expose les composantes du score, mais pas les observations, souvenirs, croyances, cible, provenance ni règle qui a produit chaque candidat. | On peut expliquer une arithmétique, pas reconstruire le raisonnement déclaré. | **Critique** |
| D-07 | Le score de confiance mesure surtout la présence récente d’observations (même signal pour tous les désirs), au lieu de la fiabilité d’une croyance/source et d’une réussite d’action correspondante. | Le nom du terme suggère une sémantique épistémique qui n’est pas celle du calcul. | **Majeure** |
| D-08 | L’intention est un `Goal` persistant avec âge, holdover, réévaluation et interruptions; il manque toutefois la cible/plan explicite et une révision motivée par les connaissances. Les plans et engagements ajoutent des primitives dans des configurations optionnelles, pas la planification générale active du profil de référence. | Les intentions sont plus qu’une étiquette, mais restent des catégories d’action sans plan local explicatif. | **Majeure** |

### 6.3 Partialité, monde et interaction

| ID | Divergence consolidée | Conséquence | Priorité |
|---|---|---|---|
| D-09 | La perception d’entités est locale et testée. Les réserves globales sont le modèle explicitement décrit, tandis que des primitives optionnelles choisissent le pair le plus proche par un scan de toutes les entités; le calcul central des groupes est également explicitement décrit au chapitre VII et peut propager un choix collectif dans l’utilité individuelle. | La partialité est réelle pour l’observation mais non uniforme pour chaque action/sous-système; l’accès global optionnel et le statut de l’agrégation collective exigent contrat et arbitrage, pas une condamnation indistincte de tous les accès globaux. | **Majeure / décision de vision** |
| D-10 | Le prototype plaçait des sources localisées et des cibles connues, mais il n’est qu’un témoin et avait lui-même un biais de survie par seuils et une consultation de population/stock global dans le score d’attaque. Le projet actuel spécifie au contraire des réserves globales; la feuille de route actuelle laisse les sources spatiales ouvertes. | Comparer les portées et les questions expérimentales; ne pas copier le prototype comme norme ni traiter le modèle global documenté comme une divergence de code. | **Décision de conception / portée** |
| D-11 | Ordre effectif agent-par-agent : déplacement/action d’un agent peut influencer la perception et l’état rencontré par les agents suivants au même tick. | Contradiction directe avec la fenêtre causale fermée explicitement promise au chapitre SYNE III; l’ordre d’identifiant devient une loi cachée du monde. | **Critique** |
| D-12 | Les groupes sont présentés comme structures émergentes issues de règles de paires, mais leur formation est calculée centralement sur l’ensemble des esprits; une décision collective peut ensuite influencer les scores individuels. Cette centralisation est décrite dans le chapitre VII, tandis que d’autres formulations générales valorisent les lois locales. | La spécification doit séparer clairement le détecteur/mesure de groupe global de la politique locale des agents et préciser si l’objectif collectif est une cause autorisée de décision. | **Majeure, décision de vision** |
| D-13 | La communication est locale, mais une réception comprise ne met pas à jour les souvenirs/croyances; l’émission `Information` est automatique au franchissement de la condition de sociabilité. | Le transport fonctionne sans réaliser encore la transmission de croyance et la causalité sociale annoncées. | **Majeure** |
| D-14 | Social et Curiosity dérivent sans satisfaction; leurs actions candidates sont des déplacements. Safety, initialisée à 1, reçoit par défaut une dérive positive puis est clampée à 1, contrairement à la dérive vers l’insécurité décrite par la monographie; seule une menace explicite la réduit. La calibration des deux premiers taux a limité leur domination, sans résoudre leur dynamique. | La viabilité d’un run ne prouve pas que les besoins forment des boucles homéostatiques cohérentes : certains entretiennent un manque sans le réduire, et un autre ne dérive pas dans le sens annoncé. | **Majeure** |
| D-15 | Le système collectif s’appuie sur intentions et croyances partagées issues des observations, mais pas d’une intégration des messages dans les croyances; les choix collectifs sont calculés à partir de l’état global. | La formation de groupes est un phénomène mesurable, mais elle n’établit pas à elle seule une transmission culturelle ou une émergence entièrement locale. | **Majeure** |

### 6.4 Ordre causal, déterminisme et continuité des preuves

| ID | Divergence consolidée | Conséquence | Priorité |
|---|---|---|---|
| D-16 | PRNG, hashes, golden runs et reprise sont implémentés; le test principal de trajectoire compare toutefois un journal arrondi et partiel, pas l’état complet bit-à-bit décrit dans son commentaire. | Le déterminisme existe et est bien étayé, mais la portée de la preuve est surestimée. | **Majeure** |
| D-17 | Snapshots conservent le PRNG et la configuration de run est stockée séparément; hash canonique de configuration et provenance de build ne figurent pas explicitement dans le snapshot. | Comparaison/archive à long terme moins robuste que la définition formelle d’un run. | **Majeure** |
| D-18 | Les données de décision de reprise perdent le détail du score/trace (seul le type est restauré) et l’événement par tick peut porter un score reporté. | La continuité de la trajectoire peut être exacte sans continuité de l’explication. | **Majeure** |
| D-19 | Le flux WebSocket est best-effort, contrairement à un journal durable complet; les gaps/provenance ECHOS sont déjà disponibles côté ingestion. | Un flux sans réconciliation/archivage exhaustif ne peut être l’unique preuve des événements perdus. | **Majeure** |
| D-20 | Plusieurs mécanismes de calibration, saisons, inventaire, livres, engagements et salience sont désactivés ou seulement partiellement connectés; les versions et profils influent sur l’interprétation. | « Livré » ne signifie pas « utilisé dans le run de référence » ni « calibré ». | **Majeure** |

### 6.5 Hors diagnostic sans preuve suffisante

Cette analyse **ne conclut pas** à une violation de séparation par ECHOS, PRISM ou Launcher dans leur intégralité. L’absence générale de provenance/gaps ECHOS alléguée dans la première version est contredite par l’existence d’indicateurs de gaps et de provenance par métrique; leur couverture bout en bout reste à vérifier. La monographie qualifie PRISM de version de travail dont l’interopérabilité avec SYNE réel n’est pas encore franchie, et le Launcher possède des portes d’acceptation explicitement non franchies. Ce sont des statuts de validation, pas la preuve d’une faute architecturale. Les questions restantes sont : complétude du flux, fraîcheur/perte, validation réelle SYNE↔PRISM/ECHOS, transport Launcher direct et scellement `.livexp`. Aucune réécriture n’est justifiée sans résultat d’audit ciblé.

## 7) Points problématiques concrets à corriger

Le backlog doit agir sur les écarts confirmés, sans prétendre que les briques existantes manquent.

### SYNE — cognition et action

- [ ] **Connecter perception à la cognition** : séparer observation courante, souvenir, croyance révisée et fait du monde; la génération des buts/actions doit consommer les états internes autorisés, non les collections globales.
- [ ] **Refondre l’arbitrage besoin→but** : besoin génère une intention/but, pas `Eat`/`Drink` directement; déterminer des candidats praticables à partir de cibles et croyances locales, et comparer alternatives par utilité.
- [ ] **Utiliser mémoire et croyances** : rappel/oubli doit affecter les cibles connues, les estimations de succès/risque et la possibilité de réviser/abandonner l’objectif.
- [ ] **Aligner la révision des croyances sur le contrat choisi** : si la formule du chapitre XI demeure normative, appeler `BeliefRevision.Revise` avec `UpdateStrength` et `MaxChangePerSnap` dans le chemin d’observation; sinon, réviser/versionner explicitement le chapitre et garder les tests en correspondance. Préciser expiration et conflits de faits mouvants.
- [ ] **Fermer les fuites d’information** : remplacer `NearestOtherEntity` global par une cible connue/perçue dans un rayon/canal autorisé; vérifier les consultations des stocks globaux et marquer clairement les abstractions intentionnelles.
- [ ] **Figer une sémantique du tick** : choisir et documenter une perception depuis un état commun au tick (actions appliquées en lot) ou reconnaître formellement un ordre séquentiel; en tout cas tester qu’un changement d’ordre d’insertion des agents n’altère pas une règle non spécifiée.
- [ ] **Corriger les besoins sans boucle cohérente** : donner une satisfaction définie à Social et Curiosity, ou les maintenir explicitement passifs; ne pas présenter `Socialize`/`Explore` (déplacements sans effet de satisfaction) comme résolvant le manque. Corriger ou re-spécifier Safety : sa dérive active doit suivre le sens voulu et son initialisation/menace doivent permettre au seuil de fuite d’être atteignable selon le modèle.
- [ ] **Distinguer confiance relationnelle, fiabilité du signal et réussite d’action**; décider le comportement pour un premier message et n’intégrer un message à une croyance qu’après réception/compréhension et révision traçable.
- [ ] **Générer la communication par décision** : si l’émission doit représenter un choix, faire entrer coût, contenu, portée, destinataires perceptibles et objectif dans des candidats évalués plutôt que dans un if de sociabilité.
- [ ] **Produire `DecisionTrace` causal** : candidats rejetés, cible, score et composantes, besoin/but, croyances/souvenirs sources avec âge/confiance, action réellement exécutée et résultat; distinguer décision nouvelle, holdover et absence de donnée.
- [ ] **Auditer primitives optionnelles** (plans, inventaire, engagements, salience) une par une avant activation par défaut : aucune ne doit réintroduire une information globale ou une politique prescrite.

### ECHOS, PRISM, Launcher — contrôle ciblé, pas refonte préjugée

- [ ] **ECHOS** : conserver les gaps d’ingestion et le marqueur `measured` par métrique et tick existants; vérifier les chemins stream/batch, les trous à reconnexion et les états zéro, non mesuré, absent, trou. S’assurer que la provenance accompagne les séries concernées; ne pas prétendre que cela reconstruit les trames perdues.
- [ ] **PRISM** : tester l’affichage de l’état reçu et les champs d’incertitude/staleness avec un flux réellement troué; ne modifier la logique de présentation qu’en présence d’une extrapolation avérée qui masque l’état du monde.
- [ ] **Launcher** : valider le flux direct SYNE→consommateurs et le scellement `.livexp` contre les contrats déjà documentés; ne pas confondre archive du Launcher et snapshot SQLite interne du moteur.

### Contrats de preuve et configuration

- [ ] **Déterminisme** : conserver les golden tests actuels et ajouter une comparaison canonique de snapshots complets (et événements) sur deux runs identiques; afficher l’état exact de la configuration utilisée.
- [ ] **Reprise** : tester continuation, y compris les traces décisionnelles si celles-ci font partie du contrat; inclure un cas avec messages/groupes/traits et profil temporel non neutre.
- [ ] **Config/run identity** : calculer un hash depuis la configuration effective normalisée et l’enregistrer avec seed, engineVersion, schemaVersion et état initial; garder hash de code/commit facultatif mais explicitement indisponible si absent.
- [ ] **Non-intrusion** : garder les tests actuels de contrôles/observation qui n’altèrent pas la trajectoire; documenter que le transport temps réel perd des trames en mode best-effort et ne remplace pas l’archive complète.

## 8) Inventaire de réalignement par composant

| Domaine | À conserver | À reprendre / modifier | Critère d’acceptation avant d’activer ou revendiquer |
|---|---|---|---|
| **SYNE — monde/perception** | `World`, `SpatialGrid`, rayon, rotation, occlusion, déterminisme d’ordre local et leurs tests | Assurer que toutes les requêtes de décision/interactions partent d’un état perçu/croyance autorisé; éliminer les scans globaux des primitives, ou les rendre explicitement hors politique agentique | Agent hors rayon/invisible ne peut ni être candidat, ni être cible; murs et champ partiel sont testés dans les décisions réelles, pas uniquement dans `PerceptionSystem` |
| **SYNE — cognition** | Types besoin, mémoire, croyance, facteurs, objectifs, utilité décomposable et tests unitaires | Raccorder pipeline réel de perception→mémoire→révision croyances→but→candidats ciblés→utilité→intention; corriger la sémantique de confiance/succès et paramètres inertes; fournir action d’abandon/révision | Test d’intégration démontre deux agents avec connaissances différentes faisant des choix différents malgré un état physique similaire; trace remonte la décision à ses entrées |
| **SYNE — monde/économie** | Réserves globales et calibration si le modèle choisi reste global; action atomique Eat/Drink | Décider explicitement si la question spatiale exige des sources localisées (piste du prototype, non autorité) ou si les réserves globales restent une abstraction; rendre coût, satisfaction et raréfaction interprétables dans les deux cas | Un scénario de calibration publié mesure viabilité et compétition/convergence selon le modèle décidé; aucune présentation de quête locale si le stock demeure global |
| **SYNE — causalité** | Tick discret, traitements triés, PRNG et tests existants | Réaligner de préférence sur l’état partagé/commit différé promis; si le séquentiel est retenu, réviser explicitement le contrat. Documenter l’état visible à chaque agent et dissocier l’ordre de l’ID s’il n’est pas une loi voulue | Tests vérifient la fenêtre causale ou la loi séquentielle choisie, l’invariance attendue aux permutations et le sens non ambigu de la perception dans un tick |
| **SYNE — communication/groupes** | Transport local, quota, ligne de vue, incompréhension déterministe, mécanisme de groupe bottom-up | Brancher la réception comprise sur une révision de croyance avec provenance/confiance; faire de l’émission un choix s’il est censé exprimer une décision; réviser le sens de confiance première rencontre | Test de chaîne A→B→C : même fait transmis, confiance dégradée, croyances distinctes et groupe éventuel attribuable; test d’incompréhension/hors portée |
| **SYNE — persistance/trace** | xoshiro complet, codec, restore, SQLite, reprise/golden existants | Ajouter config effective canonique/hash et conserver une trace suffisante si elle fait partie de la preuve; restaurer trace diagnostique ou isoler explicitement la trace du contrat d’état; distinguer absence de valeur de zéro | Hash complet state_t et journal des événements identiques après restauration; config hash égal aux options effectives; aucune trace ancienne présentée comme décision courante |
| **ECHOS** | Détection de trous `gaps_detected`, `measured` per metric/tick, stockage et API existants | Vérifier exhaustivement les chemins d’ingestion et provenance ; réparer seulement les métriques qui donnent une valeur sans base mesurée | Tests absent/zéro/non mesuré/trou sur batch et streaming; ECHOS ne pilote pas SYNE par le canal données; contrôle séparé reste documenté |
| **PRISM** | Couche de présentation et séparation décisionnelle visée | Audit contractuel par flux réel, représentation de staleness/gaps, inspection du contenu interprétatif; aucune refonte par conjecture | Rejeu d’un snapshot reçu seul montre l’état autoritaire; aucun changement de logique SYNE n’émane du rendu |
| **Launcher** | Orchestration/campagnes et paquet `.livexp` prévus | Vérifier que le flux va directement à ses consommateurs et que les artefacts sont scellés/normalisés, sans confondre avec persistance interne SQLite | Test d’acceptation de flux direct et de manifeste/hachage de paquet; marquer les portes non franchies selon la feuille de route |
| **Prototype SSE** | Historique d’hypothèses et code de test antérieur, si utile pour comparer | Ne pas restaurer son UI/architecture ni prendre les heuristiques d’Utility AI comme norme; comparer toute reprise aux principes actuels | Toute fonction réutilisée doit avoir une justification dans la monographie actuelle et ses propres tests de conformité |
| **Tests/outils** | Tests unitaires existants de perception, utilité, PRNG, groupes, snapshots | Ajouter l’intégration cognitive, invariance causale, absence de cible globale, trame événementielle, gaps ECHOS et comparaison state hash complet | Suite verte + tests nouveaux qui échouent avant correction et passent ensuite; résultats reproductibles avec scénario/config explicités |

## 9) Plan de réalignement pragmatique

L'objectif est de **réaligner le noyau sans jeter les fondations déjà testées**. Les choix ci-dessous sont ordonnés par dépendance : les arbitrages de modèle précèdent les modifications de code, et les composants hors SYNE ne sont corrigés qu'en cas d'échec d'un test contractuel. Les durées ne sont pas estimées ici : l'effort dépend des décisions de modèle ouvertes et de l'acceptation ou non de changements de format/persistance.

### Phase 0 — Arbitrages de spécification et de modèle
- [ ] Résoudre la contradiction de spécification sur le tick : le chapitre III impose que les conséquences d’action ne soient visibles qu’au tick suivant, alors que le code applique déjà séquentiellement des mutations pendant le traitement des agents. La solution par défaut pour se réaligner est le snapshot commun + commit différé, sauf décision explicite et révision de la monographie.
- [ ] Trancher si les ressources demeurent des stocks globaux abstraits ou deviennent des sources localisées; les ressources localisées sont une question ouverte de la feuille de route, pas une exigence déjà figée. Écrire conséquences pour partialité, déplacement et résultats expérimentaux.
- [ ] Trancher la frontière entre agrégation collective et décision locale : le chapitre VII prévoit explicitement l’observateur de groupe calculant les paires; préciser si l’objectif collectif peut ensuite causer la décision individuelle, et comment cela se concilie avec les formulations générales de lois locales.
- [ ] Définir ce que signifie une « émergence » revendiquée et quels observables seraient une preuve plutôt qu'une illustration.

**Livrable/gate :** décisions consignées en termes de modèle, justifiées par la question de recherche et compatibles avec `docs/monographie/`; aucune implémentation ne doit masquer ces arbitrages.

### Phase 1 — Baseline et contrats vérifiables
- [ ] Établir une configuration de référence versionnée (seed, options effectives, profil, taille du monde, population, durée) et conserver les résultats actuels comme baseline, sans les présenter comme preuve d'émergence.
- [ ] Cartographier les accès décisionnels au monde : stocks, entités, groupes, événements et perceptions; classer chacun comme local autorisé, abstraction globale décidée, ou fuite à fermer.
- [ ] Écrire des tests d'intégration qui capturent les défauts présents : mêmes candidats/choix malgré croyances différentes, cible d'action globale, perception intra-tick, premier message, besoin sans exutoire, décision holdover/absence.
- [ ] Confirmer les garanties déjà acquises de PRNG et de reprise, puis documenter la portée exacte des tests existants avant de les étendre.

**Gate :** les tests nouveaux échouent pour les raisons attendues sur le comportement actuel; les invariants visés sont écrits avant le changement du moteur.

### Phase 2 — Reconnecter la chaîne cognitive active (priorité cœur SYNE)
- [ ] Faire entrer observations courantes, souvenirs rappelés et croyances révisées dans la génération des candidats et leur évaluation; conserver le fait du monde séparé de ce qu'un agent en sait.
- [ ] Revoir les besoins→buts/action : le moteur possède déjà `Goal`, génération et évaluation par utilité, mais `DesireFactory` choisit `Eat`/`Drink` directement lorsque les stocks globaux sont disponibles. Faire dépendre la faisabilité/cible d’une information connue par l’agent, ou garder et déclarer cette économie globale comme abstraction sans la présenter comme quête locale.
- [ ] Définir la mise à jour des croyances (conflit, déplacement, âge, confiance, expiration) et brancher réellement les paramètres de configuration correspondants, ou retirer explicitement les paramètres sans effet.
- [ ] Séparer confiance relationnelle, fiabilité d'une observation/transmission et taux de réussite d'une action; définir et tester la confiance initiale d'un message.
- [ ] Donner un exutoire ou un statut explicitement passif aux besoins sans satisfaction dans le catalogue nominal; ne pas laisser une variable dérivante suggérer un objectif réalisable inexistant. Vérifier le signe et la saturation de `SafetyDriftRate` par rapport à la dérive d’insécurité promise.

**Gate :** à état physique identique mais connaissances différentes, des agents ont des candidats/évaluations différents quand ces connaissances sont pertinentes, pour des raisons traçables; la décision n'utilise pas de faits inconnus sauf abstraction explicitement retenue à la phase 0.

### Phase 3 — Fermer ou formaliser les fuites et la causalité
- [ ] Remplacer les recherches globales de cibles des primitives d'action par des cibles perçues/mémorisées selon le modèle retenu; vérifier Give/Trade/Attack et toute primitive optionnelle.
- [ ] Implémenter la sémantique de tick décidée à la phase 0; si le séquentiel est conservé, documenter l'ordre et prouver que c'est une loi assumée, non un artefact accidentel de l'ID.
- [ ] Connecter les messages compris à une révision de croyance identifiable; garder l'émission automatique seulement si elle est assumée comme loi, sinon en faire un candidat décisionnel avec coût et destinataire admissible.
- [ ] Évaluer le calcul central des groupes au regard de l'arbitrage de modèle : conserver avec contrat et portée explicites, ou remplacer par un mécanisme local testable.

**Gate :** tests de cible hors rayon/masquée, permutation d'ordre selon invariant choisi, chaîne de transmission et effets de groupe démontrent précisément la partialité/causalité attendue.

### Phase 4 — Rendre la décision et les preuves auditables
- [ ] Étendre `DecisionRecord` en trace causale : décision nouvelle ou maintenue, candidats rejetés, cible/plan, score et composantes, observations/souvenirs/croyances sources, âge/confiance et action/résultat.
- [ ] Représenter explicitement `missing`/absence de décision; ne pas émettre zéro pour une trace absente ni republier un score ancien comme décision courante.
- [ ] Ajouter une sérialisation canonique d'état complet et d'événements pour les runs de vérification; garder le test golden existant comme complément, pas comme preuve bit-à-bit complète.
- [ ] Persister l'identité du run à partir de la configuration effective normalisée, seed, version moteur et schéma; décider si les traces de diagnostic sont dans le contrat de reprise, puis tester la continuité choisie.

**Gate :** même configuration/seed donne la même empreinte canonique et les mêmes événements; après sauvegarde/reprise, trajectoire et données de diagnostic respectent le contrat publié.

### Phase 5 — Acceptation ciblée des composants adjacents
- [ ] **ECHOS :** conserver `gaps_detected` et `measured` existants; vérifier sur tous les chemins d'ingestion les états zéro mesuré, non mesuré, absent et trou, ainsi que la provenance de chaque famille de métriques. Corriger uniquement les cas révélés.
- [ ] **PRISM :** rejouer des snapshots et flux comportant trous/staleness; vérifier l'absence d'extrapolation interprétée comme état autoritaire. N'engager une modification que si le contrat échoue.
- [ ] **Launcher :** vérifier le flux direct vers les consommateurs et le scellement `.livexp` selon les contrats; ne pas confondre cette archive avec SQLite ou le snapshot SYNE.

**Gate :** les tests de contrat confirment séparation des responsabilités et indiquent explicitement les données perdues/absentes; aucun composant n'est déclaré conforme sur la seule base de son existence.

### Phase 6 — Validation expérimentale du moteur réaligné
- [ ] Exécuter le scénario de référence sur une durée préenregistrée et publier seed/configuration, version, limites, données manquantes et critères d'arrêt.
- [ ] Vérifier stabilité, viabilité et sensibilité aux paramètres sur des répétitions indépendantes; séparer les faits observés des interprétations d'émergence.
- [ ] Échantillonner des décisions et remonter chacune jusqu'à ses faits perçus/mémorisés, croyances et règles de score; publier les échecs et cas non expliqués.
- [ ] Comparer les résultats avant/après réalignement sans déplacer les critères de succès après observation.

**Gate final :** le moteur rend possible l'étude de la question de recherche avec une chaîne causale inspectable et des preuves reproductibles; cela ne prouve pas à lui seul que l’émergence recherchée a été observée. Sans protocole de verdict statistique, scénarios d’épreuve légitimes et campagne de calibration, le résultat scientifique reste « non établi », même si le logiciel fonctionne.

## 10) Recommandation finale

**Le constat justifie un réalignement substantiel de la cognition active de SYNE, pas une réécriture générale du dépôt.** Le problème central est la rupture entre l'information partielle stockée et les causes effectives des décisions; s'y ajoutent des choix globaux et temporels qui doivent être arbitrés, pas corrigés à l'aveugle.

### À conserver comme fondations (sous réserve des gates)
- Perception spatiale locale, grille, rayon, ligne de vue et tests associés.
- Structures de mémoire/croyance, calcul d'utilité décomposé, groupes, communication et actions comme matériaux à reconnecter; leur présence ne prouve pas leur rôle cognitif effectif.
- PRNG sérialisé, codec, snapshots, reprise et golden tests, en bornant les affirmations à ce qu'ils comparent réellement.
- Architecture headless et séparation SYNE/ECHOS/PRISM/Launcher comme contrats à valider.
- Prototype SSE comme trace d'hypothèses et de solutions explorées, jamais comme norme.

### Ordre de priorité
1. **Arbitrer** l'économie globale/localisée, la loi des groupes et la causalité intra-tick.
2. **Reconnecter la cognition** : besoin→but→candidats fondés sur mémoire/croyances→utilité→intention/action/résultat.
3. **Fermer ou déclarer les accès globaux** et rendre messages/groupes compatibles avec la partialité choisie.
4. **Rendre les décisions et l'identité des runs auditables**, distinguer absence et zéro, puis tester la reprise au niveau contractuel.
5. **Vérifier ECHOS, PRISM et Launcher par tests d'acceptation**, sans supposer qu'ils nécessitent une refonte.

La décision de recommencer un sous-système ne devrait intervenir qu'après les tests d'intégration de la phase 1 : si les fondations ne peuvent satisfaire ces contrats sans distorsion, une réécriture ciblée du pipeline décisionnel — et non automatiquement du monde, de la persistance ou des outils — sera justifiée.

**Le critère directeur reste : microscope, pas théâtre.** La réussite est une simulation dont les comportements peuvent être attribués à des règles et informations identifiables, répétée dans les mêmes conditions et rapportée sans transformer une absence de données en résultat.

## 11) Analyse détaillée du cœur SYNE — lois, principe, déplacement, besoin, objectif

Cette section complète les constats S-01 à S-31 et D-01 à D-20 par une lecture **axe par axe** du moteur SYNE, à la demande explicite de traçage des *lois fondamentales, principes, déplacement, besoins et objectifs*. Elle répond à deux questions : (1) quels points doivent être réalignés pour que SYNE respecte la question directrice ; (2) quels points sont **bloquants** (le dispositif ne peut pas produire le phénomène visé tant qu'ils subsistent). Chaque constat est daté par la version moteur lue (`syne/component.json` : **engineVersion 0.15.0**) et cite le fichier réel. Comme la monographie demande de faire primer le code, ces constats priment sur les chapitres `05-syne/*` quand ceux-ci décrivent un mécanisme plus riche que l'implémentation active.

Le verdict global de cette relecture est **nuancé** : la théorie SYNE (BDI, utilité décomposée, mémoire à décroissance, croyances, groupes, perception locale) est implémentée de façon souvent fidèle ; l'écart n'est presque jamais « la classe manque », mais « le mécanisme existe et **n'est pas causal** dans la politique active » — exactement le même diagnostic qu'aux sections 5 et 6, confirmé ici mécanisme par mécanisme. Le point le plus lourd est le **système de déplacement**, qui coupe à lui seul la quasi-totalité des comportements spatiaux visés.

### 11.0 Méthode de relecture

- **Fait** = visible dans le code cité. **Interprétation** = ce que ce fait implique pour la question directrice. **Hypothèse** = ce qu'il faudrait mesurer pour trancher.
- La **trajectoire de référence** (`SimulationProfiles.Reference()` / `configs/simulation/reference.json`) sert de base : c'est le scénario calibré, celui dont on parle quand on dit « SYNE tourne ». Un mécanisme désactivé par défaut (`plans`, `inventory`, `salience`, `commitments`, `books`, `territories`, `seasons`) n'est **pas** dans le run de référence ; il ne compte pas comme « fonctionnalité active ».
- Distinction systématique **« zéro observé » / « zéro non-mesuré »** appliquée aux besoins et aux décisions (voir 11.7).

### 11.1 Axe « Lois fondamentales » (localité, déterminisme, non-narration)

**Aligné.**
- **Déterminisme sans PRNG caché** : le pipeline cognitif ne consomme pas le générateur (un tirage par tick, hors cognition) ; les déplacements utilisent un hash `SplitMix64` de `(id, tick, désir)` (`ActionExecutor.DeterministicOffset`, `ActionExecutor.cs:493`) ; l'itération est triée par identifiant croissant (`CognitionPipeline.Step`, `CognitionPipeline.cs:131`).
- **Pas de narration imposée** : aucun `if tick == X` dans le chemin de décision ; les calendriers (saisons, dégradation) sont déclaratifs et désactivés en référence.
- **Perception locale** : rayon borné, ligne de vue, étalement par `id % 4` (`PerceptionSystem`).

**À réaligner.**
- **Fuite globale n°1 — ressources** : `ResourceStocks` est un **stock mondial unique** (`World.cs:27-31` ne contient ni entité « nourriture » ni « eau » ; `DesireFactory` et `ActionExecutor` lisent `_stocks` directement). Une loi « locale » ne peut pas consulter un compteur global d'eau disponible sur toute la carte. C'est le choix de modèle déjà signalé (S-17 / D-10), mais il faut acter qu'il **nie** la loi locale de recherche de ressource : « SeekFood » n'a aucune source à chercher (voir 11.5).
- **Fuite globale n°2 — groupes** : `GroupSystem.Step` calcule les composantes connexes sur **tous** les esprits en O(N²) (`GroupSystem.ComputeComponents`, `GroupSystem.cs:199`), puis `PropagateObjectives` réinjecte un objectif d'origine collective dans l'utilité **individuelle** (`GroupSystem.cs:254`, consommé en `CognitionPipeline.cs:601`). Une loi qui voit simultanément toute la population puis redescend dans la décision d'un agent contredit le principe « aucune règle ne mentionne le collectif ». C'est le conflit de principes D-09 / D-12 : à arbitrer (observateur autorisé documenté, ou processus local).
- **Ordre effectif = loi cachée** : les agents sont traités **un par un** et chacun **mute le monde immédiatement** (`CognitionPipeline.StepEntity` → `_executor.Execute` à la ligne 348 ; `ActionExecutor` mute `World.Grid`). L'agent `i+1` perçoit donc un monde déjà modifié par l'agent `i` dans le même tick. L'identifiant devient un ordre causal implicite — contraire au contrat du chapitre III (S-15 / S-31 / D-11).

### 11.2 Axe « Principe » (décider / mesurer / montrer / archiver, observabilité partielle)

**Fait.** SYNE décide et détient la vérité ; ECHOS mesure en lecture (gaps/provenance existants, S-29) ; PRISM montre ; Launcher archive. Pour ces trois derniers, la conclusion de la section 6.5 tient : **pas de refonte préjugée**, contrôle par tests d'acceptation. Le problème est **dans SYNE**.

**À réaligner dans SYNE.**
- **Observabilité partielle incomplète** : la perception est partielle, mais la **décision** lit deux faits non partiels — le stock global (disponibilité de nourriture/eau partout) et, pour les primitives optionnelles, un scan de **toutes** les entités (`ActionExecutor.NearestOtherEntity`, `ActionExecutor.cs:394`, sans rayon ni ligne de vue : S-20 / D-09). Tant que ces lectures existent, « l'agent décide sur ce qu'il perçoit » n'est pas vrai au sens fort.
- **Confusion sémantique confiance/fiabilité/succès** : le terme `Confidence` de la formule d'utilité (`UtilityEvaluator`) provient de `mind.SuccessRate(kind)` — un taux ajusté uniformément selon la présence d'observations (`CognitionPipeline.AcknowledgePerceptions`), **pas** de la confiance épistémique d'un fait. Le nom suggère une sémantique qui n'est pas celle du calcul (S-04 / D-07).

### 11.3 Axe « Besoin » (BodyNeeds)

**Fait.** Six besoins (faim, soif, fatigue 0-100 ; sécurité, social, curiosité 0-1) + énergie, seuils documentés (faim/soif 50, fatigue 70, énergie critique 10), dérive par tick, hystérésis et interruption. Aligné sur le chapitre X.

**À réaligner.**
- **Faim/soif → action terminale décidée par le stock global** : dès que le besoin franchit son seuil, `DesireFactory.ResolveTerminal` renvoie **`Eat`/`Drink` directement** si `catalog.IsViable(Eat, stocks)` — c'est-à-dire si le stock mondial n'est pas vide (`DesireFactory.cs:116-129`). L'agent « mange » donc **là où il est**, sans jamais aller vers une source. Le besoin ne produit pas un objectif (« atteindre une source alimentaire »), il produit une action d'homéostasie inconditionnellement disponible. C'est D-04 à l'état pur.
- **Besoins sans exutoire** (S-13 / D-14) : `Socialize` et `Explore` sont déclarés **mouvement** (`SimulationOptions.cs:226-227`) mais **aucune entrée du catalogue ne réduit `Social`/`Curiosity`**. Exécuter `Socialize` ne fait que déplacer l'agent et consommer de l'énergie. La dérive continue donc sans satisfaction ; le profil de référence a seulement **ralenti** la dérive (`socialDriftRate 0.0002`) pour éviter la saturation à 96 % de `Socialize`/`Explore` en fin de run (`SimulationProfiles.cs:41-43`) — la calibration masque l'absence d'exutoire, elle ne la corrige pas. `Safety` ne dérive pas (baisse ponctuelle à l'attaque) ; `Flee` n'est candidat qu'au franchissement de seuil.
- **Faim non « mesurée » vs non nulle** : l'événement `decision_made` publie l'utilité du dernier record, potentiellement reporté, ou `0.0` faute de décision (S-28). Une absence de décision est déjà indistinguable d'un zéro au niveau SYNE, avant même ECHOS — à corriger à la source (D-03).

### 11.4 Axe « Objectif » (Goal)

**Fait.** Un objectif naît d'un besoin franchi et **aucun objectif du même type n'est actif** (`DesireFactory.Generate`, `DesireFactory.cs:86-107`).

**À réaligner.** L'objectif SYNE **n'est pas un objectif** au sens BDI :

```
public readonly record struct Goal(DesireKind Kind, ulong BornTick)   // DesireFactory.cs:56
```

- Il ne porte **aucune cible**, **aucun lieu**, **aucun plan**, **aucune condition de faisabilité** liée au monde connu. `DesireKind` est un type d'action, pas un état du monde à atteindre. `Goal.Age()` ne sert qu'à l'hystérésis d'utilité.
- L'intention persistée est du même type : `public Goal? Intention { get; set; }` (`MindState`). Maintenir une intention = maintenir un **type d'action à rejouer**, pas un **but à mener jusqu'à satisfaction** (`ActiveGoals` prolonge le kind jusqu'à 100 ticks, `CognitionPipeline.cs:535-548`).
- Aucun objectif n'est dérivé d'une **croyance** (« je crois qu'il y a de l'eau au nord-ouest »). `memory.Recall` n'est pas appelé dans `Simulation.Core` (S-07) ; les croyances ne nourrissent ni la génération de candidats ni l'utilité (S-02 / D-05). La chaîne `objectif ← besoin ← croyance ← mémoire ← perception` promise par `02b-question-decomposee.md` s'arrête à `objectif ← besoin`.

### 11.5 Axe « Déplacement » (le point bloquant majeur)

C'est ici que la théorie et le code divergent le plus, et **ce seul point suffit à empêcher l'émergence spatiale** que la question directrice revendique.

**Ce que fait le code** (`ActionExecutor.MoveTowardDeterministicTarget`, `ActionExecutor.cs:422-450`) :

```
(double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);  // ligne 429
double targetX = entity.Position.X + dx;   // cible RECALCULÉE chaque tick
...
Position step = StepToward(entity.Position, target, speed);  // pas vers une cible éphémère
if (IsBlocked(step)) step = PathStep(entity, target, speed); // A* seulement pour contourner
```

et (`DeterministicOffset`, `ActionExecutor.cs:493-503`) :

```
ulong h = id; h ^= tick * SplitMix64.Gamma; h ^= (ulong)kind * 0xBF58476D1CE4E5B9UL; h = SplitMix64.Avalanche(h);
double angle  = ((h % 10000) / 10000.0) * 2.0 * Math.PI;
double radius = ((h >> 17) % 100) / 100.0 * PerceptionRadius * 0.5;  // ≤ 25 unités
return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
```

**Constat (fait).** La cible de déplacement est un **offset pseudo-aléatoire déterministe recomputé à CHAQUE tick** à partir de `currentTick`. Il n'existe :
- **aucune définition d'objectif de déplacement** : rien ne dit « va au point P » ; `Goal` ne porte pas de point (11.4) ;
- **aucun déplacement multi-tick** : la cible utilisée au tick `t` n'est jamais réutilisée au tick `t+1`. `PathStep` (A*) est appelé **au tick courant seulement** pour contourner un obstacle, et le chemin n'est **pas mémorisé** ;
- **aucun choix de destination** : la « destination » est un point dans un disque de rayon ≤ 25 autour de la position, tiré par hash. Toutes les actions de déplacement (`SeekFood`, `SeekWater`, `Socialize`, `Explore`, `Flee`) suivent le **même** schéma ; seule la constante `(ulong)kind` change — donc elles produisent la **même classe de mouvement isotrope**, sans lien avec la nature du besoin.

**Aggravant : la délibération est plus lente que le mouvement.** `ShouldDeliberate` ne délibère qu'un tick sur `Deliberation.IntervalTicks = 10` (`CognitionPipeline.cs:401`, `SimulationOptions.cs:270`) et **conserve le même `DesireKind`** entre deux délibérations (holdover). Mais `_executor.Execute(...)` est appelé **à chaque tick** (`CognitionPipeline.cs:348`). Résultat : pendant 10 ticks, l'agent garde la même **intention** mais **change de cible de déplacement tous les ticks**. Le « holdover » stabilise le choix d'action, pas la trajectoire — l'agent oscille donc sans avancer.

**Conséquence quantitative (interprétation forte).** Trait `speed` neutre = 1.0 (`TraitSet`, plage [0,2]) ; monde de référence **500 × 500** (`SimulationOptions.cs:26-27`) ; pas ≤ 1 unité/tick vers une direction re-tirée chaque tick ⇒ régime de **marche aléatoire** (déplacement net ≈ √N). Sur 2 500 ticks, le déplacement net attendu est de l'ordre de quelques dizaines d'unités dans un monde de 500 — les agents **restent globalement sur place**, se répartissent par diffusion, et ne peuvent **ni converger vers un lieu, ni maintenir durablement une proximité, ni occuper un territoire**. A* (SYNE-077) ne sert qu'au contournement d'obstacle, et le profil de référence est **sans obstacles** (`World.Obstacles` faux par défaut) — donc A* n'est même pas sollicité en référence : le déplacement se réduit à la marche aléatoire.

**Pourquoi c'est bloquant pour la question directrice.**
- **Formation de groupes** : la cohésion sociale exige des interactions **répétées et soutenues** (confiance mutuelle ≥ `groups.trustThreshold` = 0,3, `SimulationOptions.cs:731`). Or la confiance se gagne par contact (`CommunicationSystem.Receive` → `Trust.Interact`, `CommunicationSystem.cs:266`, qui pose la confiance initiale 0,5) et **décroît de ×0,9 par tick sans interaction** (`Relationships.Tick`). Avec une marche aléatoire, deux agents se croisent par hasard, se « rencontrent », puis **se séparent à la tick suivante** : la confiance retombe avant même la revue de groupes (toutes les `reviewIntervalTicks` = 10 ticks). Les groupes ne peuvent donc se former **que par bourrage** de rencontres aléatoires dans le même tick de revue — aucune cohésion durable n'est possible. **La marche aléatoire empêche précisément le mécanisme que SYNE VII décrit comme central** (« toutes les structures sociales reposent sur la confiance », « fusion », « leader », « territoire »).
- **Compétition spatiale** : impossible — les ressources sont globales (11.1), donc il n'y a rien à convoiter à un endroit donné.
- **Convergence spatiale / agrégation / territoire** : impossible — aucune règle n'attire deux agents l'un vers l'autre, ni un agent vers un lieu.
- **Lecture causale de « SeekFood »** : le nom promet une quête ; le code produit une dérive sans cible (S-03 / S-18).

C'est exactement le blocage signalé empiriquement (« absence de définition d'objectif, déplacement multi-tick, choix de destination »). Les trois sont confirmés par le code : pas d'objectif (`Goal` sans cible), pas de multi-tick (`currentTick` dans le hash), pas de destination (offset aléatoire).

### 11.6 Axe « Position / monde » (complément)

- Le monde est un plan 2D continu **non-toroidal** sans nœuds de ressource (`World.cs:25-31` : entités, obstacles, territoires, livres). Territoires, obstacles, livres sont **désactivés** en référence. En régime de référence, le monde se réduit donc à des points mobiles dans un carré vide.
- `StepToward` (`ActionExecutor.cs:469`) avance au plus de `speed` vers une cible éphémère ; `IsBlocked` teste les obstacles (aucun en référence). Aucune notion de **position mémorisée d'un objet d'intérêt** n'est utilisée pour se diriger.

### 11.7 Points bloquants (B-xx) — synthèse

« Bloquant » = tant que le point persiste, le dispositif ne peut pas produire le phénomène visé, indépendamment des autres correctifs.

| ID | Point bloquant | Preuve de code | Débloque quoi | Priorité |
|---|---|---|---|---|
| **B-01** | Déplacement = marche aléatoire à **cible re-tirée chaque tick** : pas de destination, pas de multi-tick, pas de plan de trajet. | `ActionExecutor.DeterministicOffset` (`ActionExecutor.cs:493`), `MoveTowardDeterministicTarget` (`:422`), `StepToward` (`:469`) | Trajectoires dirigées, convergence, agrégation spatiale | **Critique** |
| **B-02** | **Aucune cible dans l'objectif** : `Goal = (DesireKind, BornTick)` — pas de lieu/objet visé, pas de faisabilité liée au monde connu. | `DesireFactory.cs:56`, `MindState.Intention` | Chaîne besoin→but→candidats fondés sur cible | **Critique** |
| **B-03** | **Faim/soif tranchées par le stock global** : `Eat`/`Drink` dès que la réserve mondiale existe, sans trajet. | `DesireFactory.ResolveTerminal` (`DesireFactory.cs:116`) | Économie/spatialité de la recherche de ressource | **Critique** |
| **B-04** | **Ressources non spatiales** : stock mondial unique, aucune source localisée. | `World.cs:27-31`, `ResourceStocks`, `reference.json` | Compétition spatiale, navigation vers ressource | **Critique** |
| **B-05** | **Mémoire et croyances non causales** : `Recall` jamais appelé ; croyances absentes de la génération de candidats et de l'utilité. | `CognitionPipeline` (aucune lecture de `Memory`/`Beliefs` dans `Deliberate`), `DesireFactory.Generate`, `UtilityEvaluator.Evaluate` | `but ← croyance ← mémoire ← perception` | **Critique** |
| **B-06** | **Ordre séquentiel = loi cachée** : action d'un agent visible par l'agent suivant au même tick. | `CognitionPipeline.Step/StepEntity` (`:131`,`:202`), mutations `ActionExecutor` | Fenêtre causale fermée du chapitre III | **Critique** |
| **B-07** | **Agrégation globale des groupes** puis **réinjection** d'un objectif collectif dans l'utilité individuelle. | `GroupSystem.ComputeComponents` (`:199`), `PropagateObjectives` (`:254`), `CognitionPipeline.cs:601` | Collectif émergent sans oracle de population | **Critique** |
| **B-08** | **Besoins sans exutoire** : Social/Curiosity sans action de satisfaction ; `Socialize`/`Explore` = déplacements muets. | `SimulationOptions.cs:226-227`, catalogue, `SimulationProfiles.cs:41-43` | Dynamique de besoin fermée | **Majeure** |
| **B-09** | **`SuccessRate` déguisé en confiance** dans l'utilité. | `UtilityEvaluator`, `MindState.SuccessRate` | Sémantique épistémique honnête | **Majeure** |
| **B-10** | **Absence ≠ zéro** : `decision_made` publie `0.0` ou un score reporté. | `ExternalEvent.DecisionMade` / émetteur (S-28) | Données non ambiguës dès la source | **Majeure** |
| **B-11** | **Scan global de cible** pour primitives optionnelles. | `ActionExecutor.NearestOtherEntity` (`:394`) | Partialité stricte | **Majeure** |
| **B-12** | **Calibration masquant des trous conceptuels** (dérives ralenties pour éviter la saturation). | `SimulationProfiles.cs`, options | Viabilité honnête vs saturation cachée | **Majeure** |

### 11.8 Actions de réalignement du cœur SYNE (ordonnées)

**C — Critique (à traiter avant toute revendication de résultat)**
- [ ] **Rendre le déplacement dirigé et persistant** : introduire une **destination** (position cible) stockée dans l'intention, stable jusqu'à atteinte/abandon ; recalculer la cible **non pas chaque tick** mais à l'adoption de l'intention et selon un état de connaissance ; réutiliser le chemin A* d'un tick à l'autre ou suivre un cap constant. Critère : sur un test, un agent atteint une cible éloignée en un nombre de ticks borné, sans oscillation.
- [ ] **Faire porter une cible par l'objectif** : `Goal` doit référencer un objet/état visé (ex. « eau en P, connu à t »), distinct du type d'action. Critère : un but est falsifiable (« si je vais en P et qu'il n'y a rien, abandon/révision »).
- [ ] **Décider l'économie des ressources** : soit **sources localisées** (l'agent doit les trouver et s'y rendre), soit **stocks globaux assumés** — mais alors **cesser de nommer/quasi-modéliser `SeekFood`/`SeekWater`** comme une quête. Le choix conditionne tout le spatial.
- [ ] **Brancher mémoire et croyances dans la décision** : candidats et utilité doivent consommer le **connu** (souvenirs/croyances), pas les collections globales. Critère : deux agents à état physique identique mais connaissances différentes choisissent différemment, de façon traçable.
- [ ] **Figer la sémantique temporelle** : lot + commit en fin de tick, ou séquentiel documenté comme loi ; supprimer l'ambiguïté « ordre d'id = causalité ».
- [ ] **Arbitrer la loi des groupes** : soit observer la cohésion sans réinjecter d'objectif dans l'utilité, soit construire le collectif par échanges locaux ; documenter la décision de modèle.

**M — Majeure (fiabilise sans invalider)**
- [ ] Donner un **exutoire** (ou un statut passif explicite) à Social/Curiosity ; ne plus calibrer une dérive dont l'unique issue est un déplacement muet.
- [ ] Séparer **confiance relationnelle**, **fiabilité d'observation/transmission** et **taux de réussite d'action** ; renommer ou re-sourcer le facteur `Confidence` de l'utilité.
- [ ] Représenter explicitement **absence de décision** (jamais `0.0`, jamais score reporté) et étendre `DecisionRecord` en **trace causale** (cible, croyances/souvenirs sources, composantes, résultat).
- [ ] Remplacer `NearestOtherEntity` (scan global) par une **cible perçue/mémorisée** dans un canal autorisé.
- [ ] Auditer chaque option désactivée (`plans`, `inventory`, `commitments`, `books`, `territories`, `seasons`, `salience`) et **prouver** qu'aucune ne réintroduit d'information globale ni de politique prescrite **avant** de l'activer par défaut.

**Gate SYNE (acceptation avant reprise de résultats)** : un scénario publié (seed, options, version) où (1) des agents **se rendent** à des cibles connues, (2) des **groupes se forment puis se dissolvent selon des interactions locales traçables**, (3) chaque décision remonte à `action ← but ← croyance ← souvenir ← perception`. Sans ces trois, la sortie de SYNE reste une **illustration**, pas une preuve.

### 11.9 Ce qui est conservable dans SYNE (sous réserve des gates)

- **Perception locale** : grille spatiale, rayon, ligne de vue, étalement — alignée et testée (S-01). À conserver telle quelle.
- **Déterminisme** : PRNG sérialisé, hash stable de déplacement, itération triée, snapshots/reprise — socle solide (S-24/S-25), à borner dans ses **affirmations**.
- **Structures cognitives déjà écrites** : `BodyNeeds`, `Memory`, `BeliefSet`, `Relationships`, `Commitment`, `Inventory`, `GroupSystem`, `UtilityEvaluator` — **matériaux à reconnecter**, non à réécrire. La plupart sont même **plus avancés que la politique qui les appelle** (ex. `BeliefRevision.Revise` existe mais le pipeline appelle `ApplyEvidence` ; S-08).
- **Utilité décomposée** (`UtilityScore` : bénéfice, coût, risque, confiance, personnalité, urgence) — bon socle, à re-sourcer.
- **Séparation headless** et contrats SYNE/ECHOS/PRISM/Launcher — à valider par tests, pas à refondre.

**Synthèse de la section.** Le cœur SYNE n'est pas à réécrire de zéro : il est à **rebrancher**, et le levier n°1 est le **déplacement** (B-01) couplé à la **cible d'objectif** (B-02) et à l'**économie des ressources** (B-03/B-04). Ces trois points, corrigés, transforment la « marche homéostatique » actuelle en quête dirigée, sans laquelle ni les groupes (B-07), ni la compétition spatiale, ni une quelconque structure collective non scriptée ne peuvent apparaître autrement que par accident statistique.

## 12) Références utilisées

### Sources de vision et de méthode
- [Snapshot de monographie initiale](LIVEX-Monographie_SnapV0-1.md)
- [Avant-propos : posture méthodique](docs/monographie/00-AVANT-PROPOS.md)
- [Introduction : question directrice](docs/monographie/01-introduction.md)
- [Question décomposée en exigences](docs/monographie/02b-question-decomposee.md)
- [Vision et architecture du projet](docs/monographie/04b-projet-vision-architecture.md)
- [Reproductibilité et déterminisme](docs/monographie/11-reproductibilite-determinisme.md)
- [Limites réelles](docs/monographie/14-limites-reelles.md)
- [Contrats d’architecture](docs/monographie/annexes/annexes-contrats.md)
- [Du prototype à la version décrite](docs/monographie/annexes/annexes-du-prototype-a-la-version-decrite.md)
- [Feuille de route normative](docs/monographie/annexes/annexes-feuille-de-route.md)

### Sources SYNE principales
- [Contradiction de fenêtre causale : boucle de simulation](docs/monographie/05-syne/03-boucle-simulation.md)
- [Monde et moteur](docs/monographie/05-syne/01-monde-et-moteur.md)
- [Agents et cognition](docs/monographie/05-syne/02-agents-cognition.md)
- [Boucle de simulation](docs/monographie/05-syne/03-boucle-simulation.md)
- [Persistance](docs/monographie/05-syne/04-persistance.md)
- [Contrats](docs/monographie/05-syne/05-contrats.md)
- [Configuration](docs/monographie/05-syne/06-configuration.md)
- [Systèmes collectifs](docs/monographie/05-syne/07-systemes-collectifs.md)
- [Communication](docs/monographie/05-syne/08-communication.md)
- [Traits et tempéraments](docs/monographie/05-syne/09-traits-et-temperaments.md)
- [Besoins, seuils et dérive](docs/monographie/05-syne/10-besoins-seuils-et-derive.md)
- [Perception, mémoire et croyances](docs/monographie/05-syne/11-perception-memoire-et-croyances.md)
- [Utilité et risque](docs/monographie/05-syne/12-la-formule-dutilite-et-le-risque.md)

### Code et preuve consultés
Les références d’implémentation et de tests sont indiquées dans chaque ligne des tableaux S-01 à S-31; elles pointent vers les fichiers présents dans le dépôt au moment de l’analyse. Le prototype temporaire est consulté à titre historique : [README SSE](SSE-emergent-simulation/README.md) et [vision V2](SSE-emergent-simulation/docs/V2/01-VISION.md).