# SYNE 0.17.0 — rapport de réalignement

**Composant :** SYNE · **Version moteur :** 0.17.0 · **Schéma d’état :** 5 · **Contrat d’observabilité :** 0.5.0  
**État de référence :** campagne du 17 octobre 2026, branche `feature/livex-realignement-divergences-v0.2`. Ce rapport consolide le diagnostic initial, les arbitrages, les changements réalisés et les validations durables.

## 1. Résumé

Le diagnostic a identifié un écart entre plusieurs mécanismes présents dans le code et leur rôle effectif dans la cognition individuelle : la mémoire et les croyances n’alimentaient pas les décisions, les objectifs n’avaient pas de destination concrète, les actions de déplacement n’allaient pas vers une cible connue, l’ordre d’exécution rendait l’ordre des agents causal, et certaines traces confondaient absence et zéro. Il ne concluait pas à la nécessité de réécrire tout le dépôt.

Le réalignement a reconnecté la chaîne de connaissance, rendu les objectifs ciblés et révisables, fermé la fenêtre causale, borné les interactions par les connaissances disponibles, instrumenté les décisions et clarifié les choix de modèle des ressources et des groupes. Les versions adjacentes ont été acceptées par leurs propres tests ciblés. Ces résultats établissent le comportement couvert par les tests et campagnes cités; ils ne prouvent pas l’émergence générale d’une société, d’une intelligence ou d’une structure non programmée.

## 2. Arbitrages de modèle (ADR-018)

- **A0-1 — Sémantique du tick.** Tous les agents perçoivent et délibèrent sur l’état figé au début du tick; les effets applicables sont commités à la fermeture. Le prélèvement d’un gisement reste séquentiel, exception explicitement bornée. `CausalOrderInvarianceTests` vérifie l’invariance à l’ordre d’insertion sur les ticks 10, 30 et 60; `CausalWindowTests` vérifie le commit différé.
- **A0-2 — Ressources.** Le profil `reference` utilise les gisements localisés (`resources.localized = true`), après franchissement du gate de viabilité BL-35. Les réserves globales demeurent un modèle baseline explicite, testé séparément; elles ne sont pas décrites comme une quête spatiale.
- **A0-3 — Groupes.** Le calcul central des groupes est un observateur déclaré. La propagation d’un objectif collectif dans l’utilité individuelle a été supprimée. La décision collective demeure exposée dans les snapshots et l’événement `group_decision`. La proximité explicite n’est pas une clause de `AreBonded`; les distances des paires liées sont mesurées et exposées, avec `null` pour non mesuré.
- **A0-4 — Revendications d’émergence.** Une revendication exige cumulativement l’absence de programmation explicite de la structure, une chaîne causale remontable, une reproduction multi-graines, un contraste/contre-factuel préenregistré et des métriques dont la provenance distingue mesuré, absent et zéro observé. Le protocole et les verdicts expérimentaux sont consolidés dans [le rapport expérimental](EXPERIMENTAL-VALIDATION-2026-10.md).

## 3. Modifications durables apportées à SYNE

### Cognition, buts et navigation

- Le chemin de décision actif construit des connaissances depuis croyances et souvenirs rappelés; des connaissances différentes peuvent produire des choix différents (`CognitionKnowledgeTests`).
- `Goal` porte une cible observée. La poursuite persiste jusqu’à arrivée ou abandon lorsque la cible n’est plus crue; l’intention et sa cible sont restaurées à la reprise.
- La décision sur une ressource localisée dépend d’un gisement connu et accessible. `Eat`/`Drink` consomment à portée d’interaction; le profil global reste disponible comme abstraction déclarée.
- Les primitives inter-entités utilisent des cibles crues, filtrées par rayon perceptuel et ligne de vue; l’ancien scan global `NearestOtherEntity` a été supprimé.
- Les messages compris peuvent alimenter une croyance traçable. La confiance épistémique, la confiance relationnelle et le taux de réussite d’action ont des sémantiques distinctes.
- Les besoins Social et Curiosity ont des exutoires à l’arrivée; la dérive de sécurité va vers l’insécurité. Les boucles et seuils ont été mesurés (`NeedsLoopTests`, six tests).

### Groupes, causalité et preuve

- `PropagateObjectives`, `GroupObjective`, `MindState.CollectiveObjective` et `collectiveAlignBonus` ne font plus partie du chemin actif.
- Les distances maximales et moyennes des liens sont publiées dans l’état, l’observabilité et `group_formed`; une mesure absente est `null`, jamais zéro.
- `DecisionRecord` comprend état, cible, preuves, candidats rejetés et résultat. `utility` est nullable; un score de holdover est transmis séparément comme `heldUtility`.
- `RunIdentity` porte des empreintes canoniques de configuration et d’état. La reprise conserve la cible et la trace décisionnelle selon le contrat testé.
- Le flux WebSocket reste best-effort et peut perdre des trames; l’archive batch `stream.jsonl` n’est pas best-effort et signale une erreur plutôt qu’une troncature silencieuse.

### Configuration et versions

Le profil de référence localisé est explicitement défini dans `SimulationProfiles.Reference()` et `configs/simulation/reference.json`. Les dérives Social/Curiosity sont respectivement 0,003 et 0,002; la parité entre défauts, profil et fichier est gardée par `ReferenceProfileParityTests` (3 tests). La version moteur passe à **0.17.0**, le schéma d’état à **5**, et le contrat d’observabilité à **0.5.0**. Les pins dorés existants ont été revérifiés et sont restés inchangés après A0-3.

La comparaison documentée d’octobre oppose SYNE 0.15.0 (`707f86c`) et 0.17.0 selon cinq critères fixés avant comparaison. À 500 ticks sur la graine 12345, le moteur historique ne produisait ni `SeekFood`/`SeekWater`, ni `Explore`/`Socialize`, et représentait l’absence d’utilité par zéro; 0.17.0 produit une quête spatiale, des exutoires cognitifs et des traces distinguant `null` de zéro. Les deux versions conservent 100 agents sur les dix graines testées à 2 500 ticks. Cette comparaison mesure une différence entre versions, non l’effet causal isolé de chaque changement. Le rapport expérimental contient les chiffres détaillés et les réserves.

## 4. Audit d’accès et primitives optionnelles

La cartographie complète des accès et des sondes avant/après est conservée dans [Accès décisionnels et audit des primitives — SYNE 0.17.0](ACCESS-DECISIONS-0.17.0.md). En synthèse, les classes sont : accès local accepté, abstraction globale décidée, observateur déclaré et fuite fermée par test. L’audit BL-19 a vérifié les sept primitives optionnelles (`plans`, `inventory`, `commitments`, `salience`, `territories`, `seasons`, `books`) : toutes restent désactivées par défaut; aucune ne réintroduit une politique prescrite ni une information globale dans la décision individuelle. Les tests-gardes `OptionalPrimitivesAuditTests` passent 3/3. Les six sondes AC4 avaient relevé 9/9 faits attendus en échec sur SYNE 0.15.0 (`707f86c`), puis 9/9 verts sur SYNE 0.17.0; les assertions sont désormais entretenues par les tests du composant.

## 5. Statut des tâches de réalignement

Les tâches BL-01 à BL-37 sont closes au relevé du 17 octobre 2026. Les travaux principaux se répartissent ainsi : baseline et sondes d’avant/après (BL-05 à BL-07), boucle cognitive et objectifs ciblés (BL-08 à BL-17), arbitrage de communication (BL-18), audit des primitives (BL-19), identité, reprise et données manquantes (BL-20 à BL-23), acceptation ECHOS/PRISM/Launcher (BL-24 à BL-27), campagne d’accès et comparaison (BL-28 à BL-32), exécution des arbitrages A0-1 à A0-3 (BL-33 à BL-35), protocole/campagne A0-4 (BL-04 et BL-36) et transpositions documentaires (BL-37). Les résultats quantitatifs et réserves sont dans le rapport expérimental transversal.

## 6. Validation moteur enregistrée

Au contrôle du 17 octobre : `Simulation.Core.Tests` **602/602**, `Simulation.Console.Tests` **91/91**, `Launcher.Tests.Unit` **168/168**, exit 0. Les sondes historiques sur `707f86c` ont produit **9 échecs attendus sur 9 faits**; le contre-relevé sur le moteur réaligné est **9/9 vert**. Le test de débit `ScaleTargetsTests` est sensible à la charge machine : un échec sous charge a été reproduit sur le worktree pré-lot, puis le run propre a réussi; le plancher CI n’a pas été réépinglé. Les tests confirment leurs assertions, pas une conformité universelle à la monographie.

## 7. Limites et portes restantes

- Le gate de viabilité localisée porte sur 3 graines et 2 500 ticks; il ne démontre pas la tenue à long horizon, à la raréfaction ou à l’hiver. La roadmap SYNE conserve ces étapes comme travaux ultérieurs.
- Le gate scientifique de campagne a été instruit; le verdict reste « émergence non établie » pour les phénomènes étudiés. Une quête spatiale a été confirmée par contraste; le cycle homéostatique social est non confirmé sous le protocole préenregistré.
- L’émission automatique d’`Information` demeure une loi assumée (ADR-018 D3), et non un candidat décisionnel.
- Les groupes forment un observateur agrégateur global déclaré; cela reste un choix du modèle, pas une affirmation que toutes les lois sont locales.
- Les mesures de campagne et de composants sont des observations bornées par leur configuration, leurs seeds et leurs horizons.
