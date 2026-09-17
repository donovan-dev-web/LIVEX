# ADR-001 — Séparation Simulation / Presentation

## Statut

Accepté pour la V1.

## Contexte

La simulation doit pouvoir fonctionner sans rendu graphique, être testée rapidement et potentiellement être utilisée par plusieurs clients.

## Décision

Le Simulation Core est indépendant du moteur graphique.

## Conséquences

Positives :

- tests plus simples
- simulation headless
- possibilité de changer de renderer
- analyse indépendante
- meilleure séparation des responsabilités

Négatives :

- protocole de communication supplémentaire
- synchronisation à gérer
- architecture légèrement plus complexe
