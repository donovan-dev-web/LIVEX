---
title: "Launcher : orchestration, campagnes reproductibles et paquets .livexp"
description: "Le Launcher orchestre SYNE, ECHOS, PRISM de bout en bout, exécute des campagnes reproductibles, gère la reprise et scelle des paquets .livexp traçables."
date: 2026-10-07
tags: ["launcher", "orchestration", "campagnes", "livexp", "reprise"]
draft: false
---

Le **Launcher** est l'orchestrateur de bout en bout de LIVEX. Il détecte, configure, démarre, supervise et arrête SYNE, ECHOS et PRISM, tout en garantissant **traçabilité, reproductibilité et reprise exacte** des campagnes.

## Objectifs

- **Orchestration déterministe** : dérivation explicite des graines, application cohérente des profils/surcouches.
- **Supervision** : sondes health/readiness, gestion d'états, arrêt propre (HTTP + jeton), confinement des processus (Job Object Windows / groupe de processus Unix).
- **Campagnes reproductibles** : exécution séquentielle, politique d'échec explicite (stop/continue/retry/tolerate), reprise sans rejouer ce qui est déjà validé.
- **Traçabilité** : `provenance.json`, `run.json` enrichi, versions des composants consignées, journal de session JSON Lines.
- **Archivage scellé** : paquets `.livexp` (ZIP) normatifs, déterministes, vérifiables (hachage en flux), avec frontières contrôlées.

## Détection et configuration

Le Launcher détecte les composants via **manifestes** (`component.json`) déclarant exécutable par plateforme (`linux`/`windows` ou `path`). Il :

- **Alloue les ports** avec pré-vol et priorité au manifeste (plage interne 5200-5399 pour sa propre surface).
- **Résout les profils** (reference/prism) avec verrou Immersion évalué selon les exigences PRISM.
- **Applique la surcouche config** de manière cohérente (start/prepare/reset suivent les mêmes règles).
- **Dérive `run_id`** au format canonique `run-<seed>-<12hex>`, traçable entre tous les composants.
- **Consigne la provenance** à la création du run.

## Orchestration des processus

- **Démarrage ordonné** : résolution → allocation ports → démarrage dans l'ordre de résolution ; arrêt dans **l'ordre inverse** strict.
- **Supervision active** : boucle de sondes honorant `health.path`, `intervalMs`, `startupMs`. Perte de contact après 3 sondes → action conforme à la politique.
- **Arrêt propre** : `/control/shutdown` (HTTP) + jeton d'annulation ; en dernier recours, arrêt borné à l'arbre du composant (pas terminaison du Job Object global).
- **Portabilité** : `.cmd/.bat` lancés via `cmd.exe /c` sous Windows ; groupe de processus Unix pour confinement ; Job Object Windows avec garde anti-collatéral.
- **Singleton** : garde d'instance unique pour éviter conflits de ports/fichiers.

## Campagnes et reprise

Le Launcher exécute des **campagnes séquentielles** (ADR-005). Caractéristiques :

- **Graines dérivées** : traçables, cohérentes entre runs d'une campagne.
- **Politiques d'échec** : `stop|continue|retry|tolerate` configurables par contexte.
- **Reprise exacte** : via `runs/index.json`, le Launcher **ne rejoue pas** les runs déjà validés. Empreintes, journaux et artefacts sont vérifiés.
- **Analyse déléguée** : appels HTTP vers ECHOS pour `ingest/run`, `analysis/run`, `analysis/experiment`, `analysis/report`. Délais adaptés (jusqu'à 30 min pour opérations lourdes).
- **Traçabilité ECHOS** : `AnalysisComponentVersion` (version d'ECHOS ayant produit le rapport) est consignée dans le manifeste du paquet.

## Paquets `.livexp`

Les campagnes aboutissent à un **paquet scellé** `.livexp` (ZIP64 vivant puis scellé) :

- **Ordre normatif** : écriture déterministe (même contenu → même archive).
- **Flux, pas mémoire** : copie/hachage **en flux** (supporte gros `stream.jsonl` > 2 Gio). Jamais de buffer complet en RAM.
- **Scellement** : réécriture Create → hachage intégrité → vérification.
- **Frontières contrôlées** : seules les entrées sous `runs/RUN-xxxx/` autorisées ; traversal refusé ; zip-bomb bornée ; schéma inconnu refusé.
- **Contenu minimal traçable** : `runs/index.json`, `runs/RUN-*/run.json`, `runs/RUN-*/provenance.json`, `runs/RUN-*/logs/*.log`, artefacts ECHOS, rapport archivé **octet-à-octet** identique à la réponse ECHOS.
- **Vérifiable** : `VerifyRunIntegrity` hache en flux ; comparaison octet-à-octet entre rapport généré et archivé validée par tests bout-en-bout.

## Interface (UI)

L'interface Avalonia (desktop) privilégie clarté et traçabilité :

- **Navigation à 3 modes** (poids égal), colonne d'état permanente.
- **Cartes COMPOSANTS** avec **cycle de vie réel** (démarrer/arrêter via `/control/shutdown`), états de supervision.
- **Tableau des expériences** depuis paquets réels (`.livexp`).
- **Journal de session** en temps réel (lecture partagée sous Windows).
- **Ressources système** échantillonnées (RAM/CPU/stockage).
- **Mode Immersion** : déverrouillage évalué dynamiquement selon exigences PRISM.

## Tests bout-en-bout rigoureux

Le Launcher valide contre **composants réels** :

- **Parcours J3** : SYNE publié `win-x64` + ECHOS réel. Interruption → reprise sans rejouer RUN-0001 (entrée identique octet-à-octet). Rapport archivé identique à régénération. Analyse marquée « indisponible » si ECHOS absent, **jamais approximée**.
- **Windows natif** : lancement `livex-launcher.exe` via apphost, lecture partagée des journaux, arrêt borné à l'arbre, pas de terminaison de Job Object global.
- **Stubs pour tests unitaires/intégration** : `Stub.Syne`, `Stub.Echos`, `Stub.Prism` conformes à `INTEGRATION_CONTRACT.md`.

## Position

Le Launcher est la **colonne vertébrale opérationnelle** de LIVEX. Il transforme un moteur déterministe (SYNE), un observateur rigoureux (ECHOS) et un rendu perceptif (PRISM) en un **dispositif expérimental cohérent** : campagnes reproductibles, reprise sûre, paquets traçables. L'objectif : faire en sorte que **chaque expérience puisse être rejouée, vérifiée et archivée sans ambiguïté**.
