# FAQ.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : `VISION.md`, `GLOSSARY.md`
**Source Monographie** : Partie 8 (portée, risques, éthique) — reformulée en FAQ

---

## 1. Qu'est-ce que LIVEX cherche à démontrer ?

LIVEX construit un monde simulé où des entités autonomes (besoins, mémoire, croyances, utilité) interagissent, et observe les **structures collectives qui émergent** — groupes, échanges, conflits, propagation du savoir. LIVEX n'affirme pas que cette émergence prouve une « intelligence » : il fournit des **observateurs et des métriques mesurables**.

## 2. LIVEX prouve-t-il que les sociétés émergent « comme ça » ?

**Non.** Un score d'émergence est une **mesure particulière**, pas une preuve. ECHOS documente explicitement ses limites (`docs/docs-echos/LIMITATIONS.md`) : les catégories de mesure reflètent les choix du développeur, et un phénomène rare peut passer inaperçu.

## 3. Pourquoi tant d'importance au déterminisme ?

Le déterminisme **bit-à-bit** (même seed + config + version moteur → même trajectoire) est indispensable à la **répétabilité scientifique** : comparer deux expériences en changeant un seul paramètre n'a de sens que si le reste est reproductible. Voir `docs/docs-syne/DETERMINISM.md`.

## 4. Les entités communiquent-elles « entre elles » ?

Oui, au sein du monde simulé — via des pulsations lumineuses locales, non confidentielles, dégradées (7 types de messages). C'est **distinct** de la communication inter-composants (SYNE↔ECHOS↔PRISM par WebSocket/HTTP). Voir `docs/docs-syne/COMMUNICATION_PROTOCOL.md` vs `COMMUNICATION.md`.

## 5. Peut-on « tricher » en donnant la vue du monde aux entités ?

Non par conception : l'**observabilité partielle** est une fonctionnalité de premier plan. Une entité ne voit que ce que ses capteurs perçoivent (rayon, ligne de vue), et sa mémoire/croyances se dégradent. L'anti-triche est vérifié par tests.

## 6. Y a-t-il une modélisation physique (collisions, corps, morphologie) ?

Non (ADR-010). Les entités sont des **entités logiques** (position, vitesse, santé) sans représentation physique détaillée ; elles peuvent se chevaucher. Le rendu PRISM stylise ce modèle.

## 7. Quelle est la limite d'échelle visée ?

Objectifs V2 (Annexe I) : 50 entités ≥ 30 t/s ≤ 15 Mo, 500 ≥ 20 t/s ≤ 40 Mo, 1000 ≥ 10 t/s ≤ 80 Mo, couverture ≥ 80 %. La scalabilité au-delà est un axe de recherche (grille spatiale, LOD, culling).

## 8. LIVEX est-il multi-joueur ?

Pas en V0.1. PRISM prévoit un futur **mode joueur-habitant** (incarner une entité), mais c'est une évolution lointaine (§5.15).

## 9. Quelle est la différence entre LIVEX, PRISM et PRISM-LDK ?

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM** est le projet Unreal final de LIVEX. Il intègre
**PRISM-LDK** (*LIVEX Development Kit*), le plugin Unreal qui expose les
contrats SYNE à Blueprint (module technique `PrismLdk`). LDK désigne le
plugin, pas un projet complet distinct. Le `prism/LDK/LDK.uproject` présent
dans le dépôt est un hôte technique de développement/build du plugin.

Le choix d'Unreal et le rejet de Godot, Unity et de la non-décision sont
argumentés dans l'[ADR-002](docs/docs-prism/adr/ADR-002-choix-unreal-prism-ldk.md).

PRISM privilégie les fonctionnalités natives d'Unreal et Blueprint. Le C++
du plugin reste minimal et limité au transport, aux contrats et au parsing.

Le moteur de simulation reste découplé d'Unreal : SYNE prend les décisions et
conserve l'état faisant autorité ; Unreal transforme les données reçues en
représentation et en interactions.

## 10. Pourquoi existe-t-il un mock SYNE ?

`syne-mock/` est un serveur Node.js léger pour avancer sur le plugin et le
projet Unreal final sans faire tourner le moteur .NET complet. Il permet de
tester l'initialisation du monde, le contrôle, les WebSockets, les snapshots
globaux et le cycle d'un run avec un scénario reproductible (50 agents et
jusqu'à 400 ticks par défaut).

Le mock ne reproduit pas tous les mécanismes de SYNE : sa délibération, ses
systèmes sociaux et son pathfinding sont simplifiés. Il sert aux tests
d'intégration et d'interface, pas aux résultats de simulation ou aux tests
scientifiques finaux. Valider le client contre le vrai moteur reste nécessaire.

## 11. Quelle est la licence et qui possède le projet ?

MIT (voir `LICENSE`), dépôt solo pour l'instant. Les règles de contribution sont dans `CONTRIBUTING.md`.

## 12. Que LIVEX ne cherche-t-il **pas** à prouver ?

- Qu'une société s'est formée « à l'insu » du concepteur (les composants sont définis par lui).
- Qu'une métrique vaut une preuve d'émergence.
- Que les comportements des entités reproduisent des faits sociaux réels.

## 13. Où sont les risques éthiques documentés ?

Dans `docs/ETHICS_AND_SCOPE.md` (portée, risques, éthique), issu de la Partie 8 de la Monographie.

---

## Points restés ouverts dans ce document
- Les réponses ci-dessus sont alignées sur l'état de conception V0.1 ; elles évolueront avec les décisions d'implémentation.