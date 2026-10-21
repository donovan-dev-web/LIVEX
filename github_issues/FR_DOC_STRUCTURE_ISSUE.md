# Structure de la documentation LIVEX

**Labels suggérées** : `doc`, `fr`, `architecture`, `structure`

## Description

Organiser et structurer l'ensemble de la documentation du projet LIVEX pour qu'elle soit cohérente, navigable et conforme à la nouvelle spécification de l'installateur.

## Éléments à structurer

1. **Documentation de l'installateur** (`docs/installer/`) : S'assurer que tous les fichiers, ADRs et la spécification sont présents et à jour.
2. **ADRs** (`docs/adr/`) : Vérifier que toutes les ADRs nécessaires sont présentes et alignées sur la spécification.
3. **Roadmap** (`docs/governance/ROADMAP.md`, `docs/launcher/ROADMAP.md`, `docs/syne/ROADMAP.md`) : Aligner les roadmaps sur la spécification et les issues.
4. **Issues** (`docs/syne/ISSUES.md`, `docs/echos/ISSUES.md`, `docs/launcher/ISSUES.md`) : Aligner les issues sur la roadmap et les ADRs.
5. **Synthèses** (`docs/syne/SYNTHESIS_SPECS.md`, `docs/echos/ECHOS_SYNTHESIS.md`, `docs/launcher/LAUNCHER_SYNTHESIS.md`) : S'assurer que les synthèses sont complètes et à jour.
6. **GitHub Issues** (`github_issues/`) : Aligner les issues GitHub sur la documentation et les décisions du projet.

## Proposition

Créer un index de la documentation, vérifier la cohérence entre tous les éléments et mettre à jour ce qui est nécessaire pour qu'ils soient tous alignés sur la nouvelle spécification de l'installateur version 0.2.

## Critères d'acceptation

- [ ] Tous les répertoires de documentation nécessaires sont présents
- [ ] Tous les ADRs requis sont créés et alignés
- [ ] Les roadmaps sont cohérentes entre elles et avec la spécification
- [ ] Les issues sont alignées sur les roadmaps et les ADRs
- [ ] Les synthèses sont complètes et à jour
- [ ] Les issues GitHub sont alignées sur la documentation projet

## Tâches liées

- Faire un audit complet de la documentation
- Mettre à jour les éléments en désaccord
- Créer les éléments manquants
- Aligner tous les niveaux de la documentation

## Accords

La structure de documentation suivra le modèle établi par la spécification de l'installateur version 0.2, avec des références claires entre tous les éléments.