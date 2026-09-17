# ADR-002 — C#/.NET pour le Simulation Core

## Statut

Accepté pour la V1 (réévaluable après benchmarks).

## Contexte

Le projet demande une bonne productivité, du typage fort, de bonnes performances et une architecture indépendante d'un moteur 3D.

## Décision

Utiliser C#/.NET pour le premier prototype.

## Alternatives

- Python : excellent pour l'analyse et le prototypage scientifique, moins adapté au calcul massif en boucles Python pures.
- TypeScript/Node.js : très productif et pertinent pour les services réseau.
- Rust : excellent potentiel de performance et de concurrence, mais coût d'apprentissage supérieur.
- C++ : performances excellentes mais complexité supérieure.

## Conséquence

La décision pourra être réévaluée à partir de benchmarks réels.
