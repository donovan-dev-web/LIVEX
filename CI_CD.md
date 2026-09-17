# CI_CD.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `GITFLOW.md`, `VERSIONING.md`
**Source Monographie** : §7.1 (stack CI/CD), Annexe J (jalon 12 « CI/CD & Déploiement »)

---

## 1. Objectif

Ce document décrit le pipeline d'intégration et de déploiement continu de LIVEX, exécuté avec **GitHub Actions**, sur un monorepo à trois composants (.NET pour SYNE/ECHOS/Parts, React/TS pour l'interface, Docker pour la conteneurisation, Godot pour PRISM).

## 2. Principes

- **CI sur tout événement** : chaque PR et chaque push sur `main`/`develop` lance la validation.
- **Séparation par composant** : les jobs sont discriminés par dossier (`simulation-core/`, `analyzer/`, `echos-ui/`, `godot-renderer/` dans la version prototype ; `syne/`, `echos/`, `prism/` dans la cible monorepo), pour ne valider que ce qui a changé et paralléliser les builds.
- **Échec = blocage** : un job rouge bloque la fusion (protection de branche).
- **Conteneurisation** : Docker multi-stage ; registre GHCR (GitHub Container Registry).

## 3. Pipeline d'intégration continue (`ci.yml`)

| Étape | Outil | Critère de succès |
| :-- | :-- | :-- |
| Récupération | `actions/checkout` | − |
| SDK .NET | `actions/setup-dotnet` (version pinnée `global.json`, SDK 10.0.400 [HÉRITÉ]) | version présente |
| Restore | `dotnet restore` | succès |
| Build | `dotnet build --no-restore` | succès, warnings tolérés mais tracés |
| Tests unitaires | `dotnet test` (xUnit + Moq) | 100% vert |
| Couverture | Coverlet (`coverlet.*.runsettings`) | **≥ 80%** sur le code couvert (objectif V2, Annexe I.3) |
| Analyse statique | ex. `dotnet format` / analyzers .NET | sans erreur bloquante |
| Front (interface) | `npm ci` + Vitest/ESLint/Prettier (héritage prototype) | lint + tests |
| Docker | `docker build` multi-stage par composant | image construite |

> Tolérance connaissant la phase du projet : en phase d'exploration V0.1, le pipeline peut être lancé sur les seuls composants modifiés via `paths:` dans GitHub Actions, tout en gardant un job de validation transverse (build de l'assemblage complet) pour détecter les incompatibilités de contrat.

## 4. Pipeline de release (`release.yml`)

Déclencheur : **tag SemVer** posé selon `VERSIONING.md` (`syne-v*`, `echos-v*`, `prism-v*`, `livex-v*`).

1. Vérification finale : build + tests + couverture (mêmes étapes que `ci.yml`).
2. Construction des images Docker.
3. Publication des images dans **GHCR** avec retag `latest` (si le tag le permet).
4. Génération d'une **GitHub Release** avec les notes automatiques (changelog) et l'assemblage des artefacts.

## 5. Orchestration locale

- `compose.yml` à la racine orchestre : `simulation-core` (ou `syne`), `analyzer` + interface (ou `echos`), `godot-renderer` (ou `prism`).
- Jalon V2 (Annexe J.2, jalon 12) : critère de validation `docker compose up` démarre et fonctionne en < 30 secondes.

## 6. Secrets et sécurité

- Les secrets (jetons GHCR, etc.) sont stockés dans les **GitHub Secrets** ; jamais de secret en clair dans le dépôt.
- Règle `SECURITY.md` : aucun secret commité ; les clés détectées sont révoquées immédiatement.

---

## Points restés ouverts dans ce document
- Couverture `≥ 80%` : objectif V2 issu de l'Annexe I.3 — la valeur effective du seuil bloquant pourra être ajustée pendant l'exploration V0.1 avant d'être rendue exigeante.
- Pipeline PRISM (Godot) : le runner GitHub Actions pour Godot .NET reste à configurer ; l'écran de test Godot (headless) est en cours de définition.
- Le déclenchement `livex-v*` (release assemblée) sera raffiné lors de la première release complète.