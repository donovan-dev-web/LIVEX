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

### 2.1 Protection de branche

`main` et `develop` sont couverts par deux rulesets repository (`mainRules` et
`developRules`), tous deux en `enforcement: active` et sans acteur de contournement.

| Règle | Effet |
| :-- | :-- |
| `pull_request` | aucune modification directe de la branche ; merge par **squash uniquement**, conversations résolues |
| `required_status_checks` | `Composants modifiés` et `Structure & conventions (transverse)` doivent être vertes |
| `strict_required_status_checks_policy` | la branche doit être à jour : le dernier commit de la branche est testé |
| `non_fast_forward` | pas de force-push |
| `deletion` | pas de suppression de la branche |

Seuls les deux jobs qui s'exécutent **toujours** sont exigés. Les jobs par
composant sont filtrés par chemin et rapportent `skipped` quand le composant
n'est pas touché : les exiger ne apporterait rien, tout en couplant la protection
à la liste des jobs.

`required_signatures` a été retiré de `mainRules` : aucun commit du dépôt n'est
signé, la règle aurait rendu `main` impossible à mettre à jour. Elle peut être
réactivée quand la signature de commits sera en place.

> Renommer un des deux jobs exigés, ou changer son `name`, bloque tous les
> merges jusqu'à ce que le ruleset soit mis à jour.

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
| Intégration mock → ECHOS | `npm ci` dans `syne-mock/` + `pytest echos/tests/test_syne_mock_integration.py` (gate `LIVEX_MOCK_E2E=1`) | le client ECHOS ingère le flux du mock jusqu'au stockage et à l'API, sans build .NET |
| Intégration U8 (SYNE → ECHOS) | `dotnet build --configuration Release` (syne) + `pytest echos/tests/test_syne_echos_integration.py` (gate `LIVEX_SYNE_E2E=1`) | le même trajet contre le moteur .NET réel |

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