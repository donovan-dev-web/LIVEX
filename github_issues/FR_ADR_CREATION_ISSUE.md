# Création des ADRs pour l'installateur LIVEX

**Labels suggérées** : `doc`, `fr`, `adrs`, `creation`

## Description

Création des ADRs (Architecture Decision Records) pour l'installateur LIVEX basés sur la nouvelle spécification fonctionnelle. Les ADRs couvrent les décisions technologiques, procédurales et structurelles validées dans la spécification version 0.2.

## ADRs à créer/mettre à jour

1. **ADR-001** : Choix technologie installateur (Qt IFW + NSIS + JSON)
2. **ADR-002** : Procédure remplacement utilisateur sans élévation
3. **ADR-003** : Dépendances embarquées
4. **ADR-004** : Racine données et espace travail
5. **ADR-005** : Enregistrement et cycle de vie
6. **ADR-006** : Périmètre composants v0.2

## Proposition

S'assurer que toutes les ADRs sont créées, suivent le format standard (titre, contexte, décision, conséquences, alternatives, validation, mises à jour) et sont cohérentes entre elles et avec la spécification version 0.2.

## Critères d'acceptation

- [ ] Toutes les ADR nécessaires sont créées (au moins ADR-001 à ADR-006)
- [ ] Chaque ADR suit le format standard attendu
- [ ] Les ADRs sont cohérentes entre elles (pas de contradictions)
- [ ] Chaque ADR référence les spécifications concernées
- [ ] Les mises à jour des ADRs existants sont effectuées si nécessaire

## Tâches liées

- Créer les ADRs manquants
- Vérifier la cohérence entre toutes les ADRs
- Mettre à jour les ADRs existants pour qu'elles réfèrent à la nouvelle spécification
- S'assurer que les ADRs sont correctement référencées dans la documentation

## Accords

Toutes les ADRs suivent le modèle standard défini dans le projet, avec les sections : Contexte, Décision, Conséquences, Alternatives, Validation, Mises à jour.