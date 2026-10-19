# Annexe G. Chronologie du projet

Cette annexe retrace les étapes du projet, du prototype initial à l'état décrit par ce livre. Elle sert deux usages. D'abord, elle permet de situer chaque composant dans le temps : un lecteur qui découvre une version de fichier ou une décision peut la rapporter à une phase. Ensuite, elle rend visible une caractéristique de LIVEX : le projet a connu **plusieurs écritures successives**, et l'état actuel n'est pas la première ni la seule tentative.

Les dates antérieures à septembre 2026 proviennent de la monographie du prototype. Les dates de la phase d'implémentation proviennent du dépôt de code.

## 1. Prototype initial (mars à août 2026)

| Date | Étape |
| :-- | :-- |
| Mars 2026 | Création du dossier du prototype V1 |
| Mars à mai 2026 | Développement du prototype V1 (environ quatre-vingt-dix jours) |
| Mai 2026 | Premières observations : apparition d'une classe désignée comme « clan » |
| Juin 2026 | Réécriture complète de la monographie à partir du document de spécification |
| Juillet 2026 | Documentation des objectifs de la seconde version : montée à l'échelle (500 à 1 000 entités), architecture cognitive BDI, persistance SQLite |
| Août 2026 | Fin du prototype V1 : quatre-vingt-dix-huit tests passants, moteur déterministe validé |
| Août 2026 | Analyse complète des fichiers de la seconde version (quarante et un fichiers, vingt et une notes techniques) |
| Septembre 2026 | Rédaction de la monographie de la seconde version |

Cette première phase a établi trois choses qui n'ont pas été remises en cause : un monde minimal suffit à produire des comportements non triviaux ; le déterminisme est réalisable ; et l'observation doit être séparée du moteur. Elle a aussi laissé des problèmes ouverts, au premier rang desquels la montée à l'échelle et la reproductibilité fine.

## 2. Phase d'implémentation décrite par ce livre (septembre à octobre 2026)

| Période | Étape |
| :-- | :-- |
| Septembre 2026 | Ouverture de la branche de développement et reprise de l'implémentation sous forme de composants séparés |
| Septembre à octobre 2026 | Développement incrémental : contrats d'observabilité, cognition BDI, persistance SQLite, communication par pulsations, systèmes collectifs, déterminisme bit-à-bit |
| Septembre à octobre 2026 | Calibration du scénario de référence : survie, stabilité sur 2 500 ticks, échelle temporelle configurable (ADR-014 à ADR-017) |
| Octobre 2026 | Développement d'ECHOS : moteurs de métriques, catalogue versionné, ingénierie analytique, API de lecture seule, schéma SQLite v6 |
| Octobre 2026 | Développement du Launcher : orchestration, paquet `.livexp`, supervision et réseau |
| Octobre 2026 | Rédaction et révisions de la présente monographie |

L'activité d'implémentation de cette phase est concentrée sur quelques semaines, avec un rythme élevé de fusions de branches. Chaque composant a été développé avec son propre versionnage, ce qui explique que les numéros de version ne se correspondent pas : SYNE 0.17.0, ECHOS 0.1.0, Launcher 0.1.0.

## 3. État de référence au 18 octobre 2026

Cette photographie documentaire actualise les versions et ajoute le résultat des vérifications locales exécutées le 18/10. Les événements antérieurs restent datés selon leurs sources.

| Composant | Version / contrat | État relevé |
| :-- | :-- | :-- |
| SYNE | 0.17.0 (observabilité 0.5.0, monde 1.1, snapshot schemaVersion 5) | Disponible; Core 604/604 et Console 91/91 dans la vérification locale. |
| ECHOS | 0.1.0 (`SCHEMA_VERSION = 6`; métriques 2.0.0) | Service d'analyse sans interface; 382 réussis, 14 ignorés, couverture 91,30 %. La migration BL-09 dite v7 n'a pas incrémenté le compteur global. |
| Launcher | 0.1.0 (paquet `.livexp`) | Unit 174/174, Integration 20/20; E2E 31/33 avec SYNE publié, les deux J3 restant en échec. Le rapport de campagne du 17/10 est un run distinct à 33/33. |
| PRISM / PRISM-LDK | Version de travail; contrats consommés 0.5.0 / monde 1.1 | Specs UE documentées, non réexécutées dans ce relevé; rendu interactif complet reste une porte distincte. |
| `syne-mock` | 0.1.0 | Outil de test; 51 réussis, 2 ignorés sous Windows. |
| Installateur | (aucune version) | Spécification, non implémenté. |

Le livre garde sa date de référence historique du 12 octobre 2026 pour ses descriptions narratives et campagnes de cette date. Le présent tableau n'actualise pas rétroactivement leurs protocoles ou résultats; consulter [`LIVEX-STATUS.md`](../../governance/LIVEX-STATUS.md) pour le détail complet des vérifications et limites.

## 4. Ce que cette chronologie montre

Trois enseignements se dégagent de ces dates.

**Le projet a d'abord été un prototype, puis une architecture.** La première phase a produit un monde qui fonctionnait ; la seconde a produit un système à composants, avec des contrats, des versions et des tests. Ce passage d'un artefact à un système est le mouvement le plus visible de l'histoire du projet.

**La calibration est un événement, pas un réglage.** Elle apparaît dans la chronologie sous la forme de plusieurs décisions consignées, et elle a modifié le comportement du monde de façon mesurable. Le livre y revient à plusieurs reprises : un paramètre mal placé peut rendre une action structurellement perdante, un besoin toxique, ou une population non viable.

**Le déterminisme et la reproductibilité sont venus tôt.** Ils n'ont pas été ajoutés après coup comme une propriété de confort : ils ont été posés avant que le monde ne soit complexe, ce qui a permis de les vérifier tout au long du développement. C'est ce qui rend aujourd'hui possible la comparaison de runs décrite dans la dernière partie du livre.

**Avertissement.** Cette chronologie est descriptive, non exhaustive : elle recense les étapes structurantes, et non les centaines de modifications mineures qui les ont accompagnées. Les dates antérieures à septembre 2026 proviennent d'un document rédigé pendant le prototype et n'ont pas été vérifiées par une source indépendante.
