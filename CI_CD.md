# CI_CD.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : `GITFLOW.md`, `VERSIONING.md`
**Source Monographie** : §7.1 (stack CI/CD), Annexe J (jalon 12 « CI/CD & Déploiement »)

---

## 1. Objectif

Ce document décrit le pipeline d'intégration et de déploiement continu de LIVEX, exécuté avec **GitHub Actions**. Le dépôt contient SYNE (.NET), ECHOS (Python + React/TypeScript), PRISM (projet Unreal qui intègre le plugin PRISM-LDK/`PrismLdk`), ainsi que `syne-mock` (Node.js, outil d'intégration).

## 2. Principes

- **CI sur tout événement** : chaque PR et chaque push sur `main`/`develop` lance la validation.
- **Séparation par composant** : les jobs ciblent les chemins `syne/`, `echos/`, `prism/`, `syne-mock/` et les documents concernés. Les anciens chemins de prototypes sont historiques.
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
| Mock SYNE | `npm ci` + `npm test` dans `syne-mock/` | tests du contrat et du comportement simulé |

Le build du plugin PRISM-LDK dépend de la version d'Unreal Engine et du
toolchain disponible. Le `LDK.uproject` du checkout sert d'hôte local de
build/test ; aucune disponibilité d'un runner Unreal dans la CI n'est
présumée. La validation doit aussi être effectuée dans PRISM.

> Tolérance connaissant la phase du projet : en phase d'exploration V0.1, le pipeline peut être lancé sur les seuls composants modifiés via `paths:` dans GitHub Actions, tout en gardant un job de validation transverse (build de l'assemblage complet) pour détecter les incompatibilités de contrat.

## 4. Pipeline de release (`release.yml`)

Déclencheur : **tag SemVer** posé selon `VERSIONING.md` (`syne-v*`, `echos-v*`, `prism-v*`, `livex-v*`).

1. Vérification finale : build + tests + couverture (mêmes étapes que `ci.yml`).
2. Construction des images Docker.
3. Publication des images dans **GHCR** avec retag `latest` (si le tag le permet).
4. Génération d'une **GitHub Release** avec les notes automatiques (changelog) et l'assemblage des artefacts.

## 5. Orchestration locale

- L'orchestration locale SYNE/ECHOS est décrite dans `INSTALLATION.md`. Le
  mock se lance séparément avec `npm start` dans `syne-mock/`. Le plugin PRISM
  se valide depuis l'hôte Unreal de build/test puis dans le projet PRISM ; ne
  pas supposer qu'il est lancé par la pile serveur.
- Jalon V2 (Annexe J.2, jalon 12) : critère de validation `docker compose up` démarre et fonctionne en < 30 secondes.

## 6. Secrets et sécurité

- Les secrets (jetons GHCR, etc.) sont stockés dans les **GitHub Secrets** ; jamais de secret en clair dans le dépôt.
- Règle `SECURITY.md` : aucun secret commité ; les clés détectées sont révoquées immédiatement.

---

## Points restés ouverts dans ce document
- Couverture `≥ 80%` : objectif V2 issu de l'Annexe I.3 — la valeur effective du seuil bloquant pourra être ajustée pendant l'exploration V0.1 avant d'être rendue exigeante.
- Build automatisé de PRISM-LDK : un runner compatible Unreal Engine 5.8 et son SDK/toolchain restent à mettre en place avant de prétendre à une validation CI du plugin et du projet PRISM.
- Le déclenchement `livex-v*` (release assemblée) sera raffiné lors de la première release complète.