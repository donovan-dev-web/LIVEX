# Harmoniser le vocabulaire de version dans tous les documents LIVEX

**Labels suggérées** : `doc`, `fr`, `architecture`, `adrs`, `versionnement`

## Description

La notification de marketing a souligné une incohérence dans le vocabulaire de version entre différents documents du projet LIVEX :

1. **ADR-001/002/006** : utilisent « V1 »
2. **RELEASE_AND_UPDATE §11.1** : indique « Périmètre V0.1 »
3. **ADR-002 titre** : « en V1 »
4. **Site web** : parle de « V0.2 » pour le projet

Un lecteur ne peut pas savoir si l'installateur vise sa V0.1, la V0.2 du projet, ou une V1.

## Proposition de résolution

Mettre en place la convention de versionnement décidée en ADR-001 suivant le schéma **VMAJ.MINE.PCHA** (ex: V0.1.0, V0.2.0, V1.0.0) et s'assurer que tous les documents respectent cette convention.

## Critères d'acceptation

- [ ] Tous les documents ADR, SPECIFICATION.md et RELEASE_AND_UPDATE.md mentionnent leur version au format VMAJ.MINE.PCHA
- [ ] La version globale du produit est correctement gérée dans `site/src/data/release.ts`
- [ ] Aucun document n'utilise d'ancien format de versionnement

## Tâches liées

- Créer/À jour les ADR concernés (ADR-001, ADR-002)
- Vérifier la cohérence de SPECIFICATION.md et RELEASE_AND_UPDATE.md
- Mettre à jour les documents qui ne respectent pas la nouvelle convention

## Accords

Voir ADR-001 pour la convention de versionnement officielle.