---
title: "Launcher : orchestration, campagnes reproductibles et paquets .livexp"
seoTitle: "Launcher : campagnes reproductibles et paquets .livexp"
description: "Le Launcher enchaîne SYNE, ECHOS et PRISM, exécute des campagnes reproductibles, reprend sans rejouer et scelle des paquets .livexp traçables et vérifiables."
date: 2026-10-07
tags: ["launcher", "orchestration", "campagnes", "livexp", "reprise"]
draft: false
---

Il est deux heures du matin, une campagne de plusieurs runs tourne encore. Le premier run est validé, le deuxième entame ses 2 500 ticks, le troisième attend son tour. Puis la machine redémarre. Au matin, la question qui décide de tout est simple : faut-il tout rejouer depuis le début, ou reprendre là où l'on s'est arrêté — sans retoucher ce qui est déjà prouvé ?

La bonne réponse n'est pas une intuition, c'est une propriété du logiciel. Dans LIVEX, c'est le rôle du **Launcher** : transformer un moteur déterministe, un observateur rigoureux et un rendu perceptif en un dispositif expérimental que l'on peut enchaîner, interrompre, reprendre et archiver sans ambiguïté.

## Orchestrer, c'est tenir une promesse

Le Launcher est l'orchestrateur de bout en bout de LIVEX. Il détecte, configure, démarre, supervise et arrête SYNE, ECHOS et PRISM, tout en garantissant **traçabilité, reproductibilité et reprise exacte** des campagnes. Derrière cette formule, quatre engagements précis : une orchestration déterministe, où les graines sont dérivées de façon explicite et les profils comme les surcouches appliqués de manière cohérente ; une supervision active avec sondes et arrêt propre ; des campagnes reproductibles, avec une politique d'échec claire et une reprise qui ne rejoue pas ce qui est déjà validé ; et un archivage scellé, sous forme de paquets `.livexp` normatifs et vérifiables.

## Détecter, configurer, dériver

Tout commence par la découverte. Le Launcher repère les composants via leurs **manifestes** (`component.json`), qui déclarent l'exécutable par plateforme (`linux`, `windows` ou un `path` explicite). Il alloue ensuite les ports avec un pré-vol et une priorité au manifeste, en réservant sa propre surface à la plage interne 5200-5399. Il résout les profils `reference` et `prism`, en évaluant le verrou Immersion selon les exigences de PRISM, puis applique la **surcouche config** de façon uniforme : `start`, `prepare` et `reset` suivent les mêmes règles, ce qui évite les divergences silencieuses entre le début et la reprise d'un run.

Vient alors l'identité. Le Launcher dérive un `run_id` au format canonique `run-<seed>-<12hex>`, traçable entre tous les composants, et consigne la **provenance** dès la création du run. Dès cet instant, chaque artefact produit sait d'où il vient.

## Le cycle de vie d'un composant

Démarrer proprement n'est pas lancer un processus et espérer. Le Launcher démarre les composants dans l'**ordre de résolution** et les arrête dans l'**ordre inverse** stricte, parce qu'un consommateur ne doit pas survivre à sa source ni disparaître avant elle.

Pendant l'exécution, une boucle de sondes honore `health.path`, `intervalMs` et `startupMs` ; la perte de contact après trois sondes déclenche l'action prévue par la politique. L'arrêt propre passe par `/control/shutdown` en HTTP, complété par un jeton d'annulation ; en dernier recours seulement, l'arrêt reste borné à l'arbre du composant, sans jamais terminer le Job Object global. La portabilité est traitée au cas par cas : scripts `.cmd` et `.bat` lancés via `cmd.exe /c` sous Windows, groupe de processus sous Unix, Job Object Windows avec garde anti-collatéral. Une garde d'instance unique évite enfin les conflits de ports et de fichiers.

## Campagnes et reprise sans rejeu

Le Launcher exécute des **campagnes séquentielles** (ADR-005). Les graines y sont dérivées de manière traçable et cohérentes d'un run à l'autre ; la politique d'échec est configurable par contexte — `stop`, `continue`, `retry` ou `tolerate` — de sorte qu'une campagne sait d'avance comment réagir à un incident plutôt que de décider dans l'urgence.

Le cœur de la promesse est ailleurs : la **reprise exacte**. Via `runs/index.json`, le Launcher **ne rejoue pas** les runs déjà validés. Empreintes, journaux et artefacts sont d'abord vérifiés ; ce qui tient est conservé, ce qui manque est repris. L'analyse est déléguée à ECHOS par appels HTTP — `ingest/run`, `analysis/run`, `analysis/experiment`, `analysis/report` — avec des délais adaptés, jusqu'à 30 minutes pour les opérations lourdes. Et pour que le lecteur du paquet sache qui a produit quoi, la version d'ECHOS ayant généré le rapport (`AnalysisComponentVersion`) est consignée dans le manifeste.

## .livexp, un paquet scellé

Une campagne aboutit à un **paquet scellé** `.livexp`, une archive ZIP64 d'abord vivante, puis refermée. Sa construction obéit à des règles strictes, car un archive non maîtrisée est une source de faux résultats.

L'écriture suit un **ordre normatif** : même contenu, même archive. La copie et le hachage se font **en flux**, jamais par mise en mémoire complète, ce qui permet de traiter des `stream.jsonl` dépassant 2 Gio. Le scellement réécrit l'archive, calcule un hachage d'intégrité, puis le vérifie. Les **frontières sont contrôlées** : seules les entrées sous `runs/RUN-xxxx/` sont admises, la traversée de chemin est refusée, les bombes zip sont bornées et un schéma inconnu est rejeté.

Le contenu est volontairement minimal mais suffisant pour tout rejouer : `runs/index.json`, les `run.json` et `provenance.json` de chaque run, les journaux, les artefacts ECHOS et le rapport archivé **octet à octet** identique à la réponse d'ECHOS. La vérification `VerifyRunIntegrity` hache en flux, et la comparaison octet à octet entre rapport généré et rapport archivé est validée par des tests bout-en-bout. Un paquet `.livexp` n'est pas un dossier de sortie : c'est une pièce à conviction.

## Une interface qui montre l'état réel

L'interface Avalonia, sur poste de travail, applique la même philosophie : montrer l'état réel plutôt qu'une approximation rassurante. La navigation se répartit en trois modes de poids égal, avec une colonne d'état permanente. Les cartes **COMPOSANTS** reflètent le cycle de vie effectif — démarrer ou arrêter passe réellement par `/control/shutdown` — et affichent l'état de supervision. Le tableau des expériences se construit à partir de paquets `.livexp` réels, le journal de session défile en temps réel avec une lecture partagée sous Windows, et les ressources système (RAM, CPU, stockage) sont échantillonnées. Quant au mode **Immersion**, il se déverrouille dynamiquement selon les exigences de PRISM.

## La preuve sur composants réels

Ces engagements ne valent que s'ils sont tenus contre de vrais composants, pas contre des simulacres. Le **parcours J3** enchaîne un SYNE publié `win-x64` et un ECHOS réel : le run est interrompu, puis repris sans rejouer RUN-0001, dont l'entrée demeure identique octet à octet, et le rapport archivé est identique à sa régénération. Si ECHOS est absent, l'analyse est marquée « indisponible » — **jamais approximée**, car une valeur inventée vaut moins qu'une valeur manquante assumée.

Sous Windows natif, `livex-launcher.exe` est lancé via apphost, avec lecture partagée des journaux et arrêt borné à l'arbre du composant. Les suites Unit, Integration et End-to-end se terminent avec **0 échec**, y compris les parcours J3 réels. Pour les tests unitaires et d'intégration, des stubs — `Stub.Syne`, `Stub.Echos`, `Stub.Prism` — restent conformes à `INTEGRATION_CONTRACT.md` et permettent de vérifier les contrats isolément.

## La colonne vertébrale opérationnelle

Le Launcher est la **colonne vertébrale opérationnelle** de LIVEX. Il transforme un moteur déterministe (SYNE), un observateur rigoureux (ECHOS) et un rendu perceptif (PRISM) en un **dispositif expérimental cohérent** : des campagnes reproductibles, une reprise sûre, des paquets traçables. Son objectif est modeste et exigeant à la fois : faire en sorte que **chaque expérience puisse être rejouée, vérifiée et archivée sans ambiguïté**.

Le format `.livexp`, le cycle de vie des composants et les politiques de campagne sont détaillés dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les relevés d'acceptation et les évolutions du Launcher paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
