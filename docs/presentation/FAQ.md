# FAQ.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 4 novembre 2026
**Dépend de** : `VISION.md`, `GLOSSARY.md`
**Source Monographie** : Partie 8 (portée, risques, éthique) — reformulée en FAQ

> Cette FAQ couvre les garanties scientifiques communes au projet et distingue
> PRISM V1 de l'intégration PRISM V2 en cours. Pour les étapes et validations
> datées, consulter le [statut transversal](../governance/LIVEX-CURRENT-STATUS.md).

---

## 1. Qu'est-ce que LIVEX cherche à démontrer ?

LIVEX construit un monde simulé où des entités autonomes (besoins, mémoire, croyances, utilité) interagissent, et observe les **structures collectives qui émergent** — groupes, échanges, conflits, spécialisations. Le cadrage du projet est celui d'une émergence collective dite faible : les motifs ne sont pas prescrits comme un scénario et leurs causes restent reliées aux règles explicites et aux interactions locales.

LIVEX ne cherche pas à étudier l'apprentissage ou l'amélioration individuelle comme phénomène central. La mémoire, les croyances ou une maîtrise peuvent changer si les règles le prévoient ; ces évolutions individuelles ne sont pas, à elles seules, l'émergence collective étudiée. LIVEX n'affirme pas non plus que les motifs observés prouvent une « intelligence » : il fournit des **observateurs et des métriques mesurables**.

## 2. LIVEX prouve-t-il que les sociétés émergent « comme ça » ?

**Non.** Un score d'émergence est une **mesure particulière**, pas une preuve. ECHOS documente explicitement ses limites (`../echos/LIMITATIONS.md`) : les catégories de mesure reflètent les choix du développeur, et un phénomène rare peut passer inaperçu.

## 3. Pourquoi tant d'importance au déterminisme ?

Le déterminisme **bit-à-bit** (même seed + config + version moteur → même trajectoire) est indispensable à la **répétabilité scientifique** : comparer deux expériences en changeant un seul paramètre n'a de sens que si le reste est reproductible. Voir `../syne/DETERMINISM.md`.

## 4. Les entités communiquent-elles « entre elles » ?

Oui, au sein du monde simulé — via des pulsations lumineuses locales, non confidentielles, dégradées (7 types de messages). C'est **distinct** de la communication inter-composants (SYNE↔ECHOS↔PRISM par WebSocket/HTTP). Voir `../syne/COMMUNICATION_PROTOCOL.md` vs `../COMMUNICATION.md`.

## 5. Peut-on « tricher » en donnant la vue du monde aux entités ?

Non par conception : l'**observabilité partielle** est une fonctionnalité de premier plan. Une entité ne voit que ce que ses capteurs perçoivent (rayon, ligne de vue), et sa mémoire/croyances se dégradent. L'anti-triche est vérifié par tests.

## 6. Y a-t-il une modélisation physique (collisions, corps, morphologie) ?

Dans le monde autonome SYNE V1, les entités sont des états logiques et peuvent se chevaucher. PRISM V2 ajoute un corps et une position physiques dans son monde 2,5D, avec ses propres règles de déplacement et d'interaction ; ce n'est pas une simulation de corps ou de collisions réalistes.

## 7. Quelle est la limite d'échelle visée ?

Objectifs V2 (Annexe I) : 50 entités ≥ 30 t/s ≤ 15 Mo, 500 ≥ 20 t/s ≤ 40 Mo, 1000 ≥ 10 t/s ≤ 80 Mo, couverture ≥ 80 %. La scalabilité au-delà est un axe de recherche (grille spatiale, LOD, culling).

## 8. LIVEX est-il multi-joueur ?

Pas en V0.1. PRISM prévoit un futur **mode joueur-habitant** (incarner une entité), mais c'est une évolution lointaine (§5.15).

## 9. Quelle est la différence entre LIVEX, PRISM et PRISM-LDK ?

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM V1** est le projet Unreal de la release 1.0.0 ; il intègre
**PRISM-LDK** (*LIVEX Development Kit*), le plugin Unreal qui expose les
contrats SYNE à Blueprint (module `PrismLdk`). **PRISM V2** est une simulation
.NET headless qui porte l'autorité du monde physique ; ce n'est pas une
extension du plugin Unreal. LDK désigne le plugin V1, pas un projet complet
distinct.

Le choix d'Unreal et le rejet de Godot, Unity et de la non-décision sont
argumentés dans l'[ADR-002](../prism/adr/ADR-002-choix-unreal-prism-ldk.md).

PRISM-LDK privilégie les fonctionnalités natives d'Unreal et Blueprint. Le
C++ du plugin V1 reste limité au transport, aux contrats et au parsing. PRISM
V2 repose sur le host .NET et le contrat SYNE↔PRISM versionné en `1.0.0` ; le
transport couplé a été exercé sur 1 000 ticks, tandis que R2 (5 000 ticks)
reste ouvert.

En V1, SYNE prend les décisions et conserve l'état faisant autorité ; Unreal
représente ses données. En V2, PRISM conserve l'état physique faisant autorité
et SYNE applique à sa cognition les observations renvoyées par PRISM.

## 10. Pourquoi existe-t-il un mock SYNE ?

`../../syne-mock/` est un serveur Node.js léger pour tester les contrats V1
sans faire tourner le moteur .NET complet. Il permet de vérifier
l'initialisation du monde, le contrôle, les WebSockets, les snapshots globaux
et le cycle d'un run avec un scénario reproductible. Les campagnes de PRISM V2
utilisent `Prism.Bots`, et la validation de couplage utilise SYNE réel.

Le mock ne reproduit pas tous les mécanismes de SYNE : sa délibération, ses
systèmes sociaux et son pathfinding sont simplifiés. Il sert aux tests
d'intégration et d'interface, pas aux résultats de simulation ou aux tests
scientifiques finaux. Valider le client contre le vrai moteur reste nécessaire.

Le mock suit le contrat d'observabilité de SYNE (moteur 0.15.0, contrat
snapshot 0.4.0) : snapshots avec champ `seed`, identifiants de run au format
canonique `run-<seed>-<12hex>` — il est donc compatible avec l'ingestion
ECHOS telle quelle.

## 11. Quelle est la licence et qui possède le projet ?

GNU GPL v3.0 ou ultérieure (GPL-3.0-or-later, voir `LICENSE`) pour SYNE, ECHOS, le Launcher, syne-mock et le site vitrine ; le projet Unreal PRISM reste soumis à l'EULA d'Epic Games et n'est pas couvert par la GPL. Dépôt solo pour l'instant. Les règles de contribution sont dans `../../CONTRIBUTING.md`.

## 12. Que LIVEX ne cherche-t-il **pas** à prouver ?

- Qu'une société s'est formée « à l'insu » du concepteur (les composants sont définis par lui).
- Qu'une métrique vaut une preuve d'émergence.
- Que les comportements des entités reproduisent des faits sociaux réels.

## 13. Où sont les risques éthiques documentés ?

Dans `ETHICS_AND_SCOPE.md` (portée, risques, éthique), issu de la Partie 8 de la Monographie.

---

## Points restés ouverts dans ce document
- Les réponses ci-dessus sont alignées sur l'état de conception V0.1 ; elles évolueront avec les décisions d'implémentation.
