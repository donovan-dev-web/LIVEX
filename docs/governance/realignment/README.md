# Dossier de gouvernance — réalignement et preuves

**Périmètre :** réalignement LIVEX d’octobre 2026, de SYNE 0.15.0 à 0.17.0 et validations adjacentes.

Ce dossier conserve les décisions, résultats synthétiques, critères, versions, limites et validations qui restent utiles à la compréhension du logiciel. Il ne conserve pas les sorties brutes de tests, jeux de données volumineux, scripts d’analyse, projets temporaires ni binaires. Les contrats et tests pérennes des composants restent leurs sources de vérification opérationnelle.

## Rapports

| Rapport | Périmètre |
|---|---|
| [SYNE 0.17.0 — décisions et réalignement](SYNE-REALIGNMENT-0.17.0.md) | Arbitrages A0-1 à A0-4, changements moteur, statut BL-01 à BL-37, validation et limites. |
| [SYNE 0.17.0 — accès décisionnels et primitives](ACCESS-DECISIONS-0.17.0.md) | Cartographie BL-06/07/19/28 : classes d’accès, sondes avant/après et audit des primitives optionnelles. |
| [Validation expérimentale — campagnes et verdicts](EXPERIMENTAL-VALIDATION-2026-10.md) | Run de référence, répétitions, échantillonnage, comparaison, protocole A0-4 et verdicts. |
| [ECHOS — acceptation de la phase 5](ECHOS-ACCEPTANCE-2026-10.md) | Ingestion batch et streaming, provenance et distinction des états de données. |
| [PRISM — acceptation de la phase 5](PRISM-ACCEPTANCE-2026-10.md) | Rejeu de flux troué, autorité de l’état et limites de staleness. |
| [Launcher — acceptation de la phase 5 et intégration](LAUNCHER-ACCEPTANCE-2026-10.md) | SYNE réel, paquet `.livexp`, alignement des données dans la fenêtre d’analyse et réserves de test. |

## Règle de maintenance

Pour toute nouvelle preuve d’avancement, consigner ici ou dans un rapport versionné du composant : date, version/configuration, protocole ou critères fixés avant exécution, résultats, portée, limites et liens vers les tests contractuels maintenus. N’ajouter une sortie brute que si elle est nécessaire à une reproduction durable et qu’elle est explicitement conservée dans le dépôt. Les roadmaps indiquent l’état et les portes restantes; les changelogs gardent l’historique des changements par composant.
