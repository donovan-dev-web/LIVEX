# Issue: Vérifier la cohérence de la procédure de remplacement LIVEX

**Labels suggérées** : `doc`, `fr`, `release`, `processus`

## Description

La notification de marketing a vérifié la procédure de remplacement (RELEASE §11.2) par rapport au guide (§5). Quatre points critiques ont été identifiés qui doivent être cohérents :

### Point (a) : Fermeture obligatoire de LIVEX
- Vérifier que la procédure impose bien la fermeture de LIVEX avant tout remplacement

### Point (b) : Refus de remplacer des fichiers si un processus LIVEX tourne
- Vérifier que la procédure refuse le remplacement si un processus LIVEX est en cours

### Point (c) : Conservation de `settings.json`, de l'espace de travail et des `.livexp`
- Vérifier que ces éléments sont bien conservés lors du remplacement

### Point (d) : Réinstallation à partir du reçu
- Vérifier que la procédure prévoit une réinstallation à partir du reçu

## Proposition de résolution

S'assurer que RELEASE_AND_UPDATE.md §11.2 et le guide §5 sont entièrement cohérents sur ces quatre points. Le cas échéant, mettre à jour l'un ou l'autre pour qu'ils soient alignés.

## Critères d'acceptation

- [ ] Les quatre points (a), (b), (c), (d) sont couverts dans RELEASE_AND_UPDATE.md §11.2
- [ ] Le guide §5 reflète exactement ces quatre points
- [ ] Aucune contradiction entre les deux documents sur ces points

## Tâches liées

- Vérifier RELEASE_AND_UPDATE.md §11.2
- Vérifier le guide §5
- Mettre à jour l'un ou l'autre document si incohérence

## Accords

Voir ADR-002 pour la procédure de remplacement officielle.