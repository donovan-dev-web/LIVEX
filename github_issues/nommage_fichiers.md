# Issue: Convention de nommage de fichiers pour les releases LIVEX

**Labels suggérées** : `doc`, `fr`, `release`, `nommage`

## Description

Avant la première release publique, il faut décider d'une convention de nommage de fichiers claire, tout en laissant la flexibilité nécessaire avant le lancement.

## Points à décider

1. **Format standard pour SYNE** : `syne-<MAJOR>.<MINOR>.<PATCH>-<os>-x64.zip` vs `syne-<version>-<os>-x64.zip`
2. **Format pour ECHOS** : `echos-<MAJOR>.<MINOR>.<PATCH>-<arch>.tar.gz`
3. **Format pour le launcher** : `livex-launcher-<MAJOR>.<MINOR>.<PATCH>-<os>-x64.zip`
4. **Avant la première release** : Utiliser des suffixes `-dev` et `-pre` pour les versions de développement et pré-release
5. **Interdiction** : Aucun fichier ne doit porter `livex-v*` sauf intention spécifique

## Proposition

Adopter le format standard VMAJ.MINE.PCHA pour les releases officielles, avec des suffixes `-dev` et `-pre` pour le développement avant V1.0.0.

## Critères d'acceptation

- [ ] Format de nommage défini pour tous les types de fichiers (SYNE, ECHOS, launcher)
- [ ] Règles pour les versions de développement et pré-release documentées
- [ ] Interdiction du préfixe `livex-v*` documentée
- [ ] Mise à jour des scripts de génération de paquets

## Tâches liées

- Décider de la convention de nommage dans ADR-010
- Mettre à jour les scripts de génération de paquets
- Documenter les règles pour les versions avant V1.0.0

## Accords

Voir ADR-010 pour la convention de nommage officielle.