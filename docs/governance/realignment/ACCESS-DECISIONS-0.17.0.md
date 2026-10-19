# Accès décisionnels et audit des primitives — SYNE 0.17.0

**Date du relevé :** 17 octobre 2026 · **Version moteur :** 0.17.0 · **Schéma d’état :** 5 · **Contrat d’observabilité :** 0.5.0  
**Portée :** consolidation de BL-06, BL-07, BL-19 et BL-28. Ce rapport conserve les décisions et constats utiles; les sorties brutes des sondes et les sources exécutables temporaires ne sont pas archivées ici.

## Grille de classification

- **LOCAL — ACCEPTÉ** : accès par perception, connaissance, état propre ou portée/ligne de vue, vérifié par test.
- **ABSTRACTION GLOBALE — DÉCIDÉE** : accès global assumé et déclaré, notamment le modèle des réserves globales.
- **OBSERVATEUR — DÉCLARÉ** : mesure du collectif sans écriture dans le monde ni injection dans l’utilité individuelle.
- **FUITE — FERMÉE (testée)** : accès global ou injection historiquement non conforme, supprimé et protégé par test.
- **NON AUDITÉ** : primitive sans audit; ne peut être activée par défaut. À la clôture BL-19, aucune des sept primitives examinées ne demeure dans cette classe.

La règle durable est de classer toute nouvelle porte d’accès au monde ou au collectif avant activation. Une suite verte ne suffit pas à justifier un accès global non déclaré.

## Cartographie synthétique

| Domaine | Décision au relevé 0.17.0 | Garde ou preuve maintenue |
|---|---|---|
| Perception, croyances et mémoire | Les candidats et cibles utilisent la connaissance de l’agent (croyances, puis mémoire rappelée), jamais une recherche dans le monde réel pour compenser une ignorance. | `CognitionKnowledgeTests`, `PerceptionTests`, `BeliefTests`. |
| Délibération et utilité | La connaissance conditionne la faisabilité des candidats. Le repli sur le stock global n’existe que lorsque la connaissance est absente, cas déclaré du modèle global; l’objectif collectif n’entre plus dans l’utilité individuelle. | `CognitionKnowledgeTests`, `AdrGroupObserverTests`, `UtilityEvaluatorTests`. |
| Actions, cibles et déplacement | Les interactions entre entités ciblent une entité connue, bornée par portée perceptuelle et ligne de vue; les trajets suivent la cible de l’intention. `Eat`/`Drink` sont localisés dans le profil de référence, alors que le profil global reste une abstraction explicitement testée. | `LocalizedResourceActionTests`, `AdrCognitivePrimitivesTests`, tests `ResourceStocks`. |
| Économie et environnement | Réserves globales = abstraction de modèle déclarée. Ressources localisées = profil `reference` après franchissement du gate BL-35. Saisons = loi environnementale déterministe; elles ne prescrivent aucune action. | `ReferenceProfileParityTests`, tests ressources/saisons, rapport [expérimental](EXPERIMENTAL-VALIDATION-2026-10.md). |
| Groupes et territoires | L’agrégation des groupes, la mesure des distances et les territoires sont des observateurs; les décisions collectives sont exposées mais jamais injectées dans les scores. | `AdrGroupObserverTests`, `TerritoryTests`, `OptionalPrimitivesAuditTests`. |
| Communication | Émission locale bornée (rayon, ligne de vue et quotas); un message compris peut alimenter une croyance traçable. L’émission automatique d’`Information` demeure une loi assumée, non une décision candidate. | Tests communication, `TransmissionChainTests`. |
| Causalité, événements et persistance | Perception/délibération sur état figé, effets commités à la fermeture du tick (exception bornée du prélèvement); les événements observent sans décider. Absence de mesure = `null`, distincte de zéro et d’un score reporté. | `CausalWindowTests`, `CausalOrderInvarianceTests`, `ObservabilityNonIntrusionTests`, `RunIdentityTests`, tests de persistance. |

## Sondes d’avant/après (AC4, BL-07)

Six sondes ont vérifié les écarts sur le moteur historique `707f86c` (SYNE 0.15.0) et sur le moteur réaligné 0.17.0. Le bilan est **9 faits attendus en échec sur 9** sur la base historique, puis **9/9 verts** sur le moteur réaligné. Ce sont neuf assertions factuelles réparties entre six sondes, et non neuf sondes. Elles couvraient notamment la connaissance des ressources et des pairs, le partage d’information et la distinction entre absence et zéro. Les écarts relevés sont désormais couverts par des tests de non-régression dans SYNE; les résultats bruts et le projet de sondes étaient des artefacts temporaires.

## Audit BL-19 des primitives optionnelles

Au relevé, `plans`, `inventory`, `commitments`, `salience`, `territories`, `seasons` et `books` sont toutes désactivées par défaut dans les valeurs par défaut et le profil `reference`. L’audit n’a identifié ni politique prescrite ni information globale injectée dans les décisions individuelles.

| Primitive(s) | Classe et portée |
|---|---|
| `inventory`, `commitments`, `salience` | **LOCAL — ACCEPTÉ** : état propre ou cible connue; salience déclenche une reconsidération mais ne choisit pas l’action. |
| `plans` | Candidats évalués par l’utilité; état propre et abstraction globale déclarée de réserve. Cible d’interaction bornée par croyance, portée et ligne de vue. |
| `territories` | **OBSERVATEUR — DÉCLARÉ** : mesure d’appartenance et événements/snapshot, sans effet sur les décisions. |
| `seasons` | **ABSTRACTION GLOBALE — DÉCIDÉE** : fonction déterministe du tick qui module l’environnement, pas une entrée de politique individuelle. |
| `books` | **LOCAL — API explicite** : aucun appel automatique du chemin de décision. |

Les tests `OptionalPrimitivesAuditTests` passent 3/3 au relevé : défauts désactivés sur les deux profils, activation des primitives purement observationnelles sans changement de décision, et déterminisme des primitives d’action activées. `AdrDeterminismNeutralTests` vérifie également l’inertie des drapeaux désactivés et le déterminisme lorsqu’ils sont activés.

## Conditions de maintien

Avant d’activer une primitive par défaut, revérifier ses entrées de décision, préserver sa classe ci-dessus et actualiser les tests-gardes. Toute nouvelle observation du collectif doit rester sans écriture ni injection, sauf décision d’architecture explicite, versionnée et testée. Les réserves globales et le profil localisé restent deux modèles distincts; une donnée absente ne devient jamais un zéro mesuré.