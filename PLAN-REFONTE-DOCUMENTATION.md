# PLAN DE REFONTE DE LA DOCUMENTATION LIVEX

**Date** : 08 octobre 2026
**Objectif** : Refondre intégralement la documentation du projet LIVEX à partir du code réellement implémenté, en distinguant clairement deux supports de documentation indépendants.

---

## 1. Principes directeurs

- **Refonte, non réorganisation.** On ne conserve ni la structure actuelle, ni les noms des fichiers existants. Seul le code source fait foi.
- **Vérité par le code.** Documenter uniquement ce qui est réellement implémenté dans `syne/`, `echos/`, `launcher/`, `prism/LDK/`, `syne-mock/`, `scripts/`, `.github/workflows/`. Aucun élément futur, « envisagé », « prévu » ou « à venir ».
- **Deux documentations indépendantes.** La documentation du projet (GitHub/technique) et la monographie (livre exhaustif imprimable) sont deux entités distinctes. **Aucun lien** entre elles.
- **Nettoyage strict.** Seuls les ADRs `Accepted` décrivant un choix architectural/fonctionnel réellement implémenté et toujours en vigueur pour l'état V0.1 sont conservés. Les pistes de réflexion (Electron, Godot abandonné...), idées non traduites en code ou sans intérêt pour comprendre l'état actuel sont exclues.
- **Exclusions.** Roadmaps, rapports d'audit, inventaires de status, éléments obsolètes ne sont pas intégrés. Les artefacts/cache ne sont pas concernés par ce plan (déjà prévus pour nettoyage).
- **Snapshot monographie figé.** `LIVEX-Monographie_SnapV0-1.md` et `.pdf` doivent rester intacts. Ils seront archivé dans `DocsOld/` (prévu hors périmètre rédactionnel de ce plan).
- **Sélection, pas conservation.** Beaucoup de documents actuels ne seront pas réutilisés. On part de zéro, en puisant uniquement l'information pertinente depuis le code.

---

## 2. Distinction entre les trois supports

| Support | Rôle | Emplacement | Caractéristiques |
|---|---|---|---|
| **README racine** | Présentation GitHub | Racine (`README.md`) | Minimal, clair, orienté visiteur. Présente brièvement LIVEX, son état V0.1 et renvoie vers la documentation du projet. |
| **Documentation du projet** | Documentation technique/pragmatique | `docs/` | Cible les développeurs, utilisateurs et contributeurs. Guides d'installation, build, tests, architecture technique, contrats, configuration... Destinée à un usage quotidien sur GitHub. |
| **Monographie** | Livre exhaustif à part | `docs/monographie/` | Ouvrage complet (300+ pages visé), imprimable, destiné à tous (grand public, développeur, communicant, scientifique). Rédigée à part, sans aucun lien avec `docs/`. S'appuie sur le code réel et sur la documentation du projet une fois celle-ci finalisée. |

---

## 3. Structure de la documentation du projet

Seuls les documents obligatoires suivants restent à la racine. Tout le reste de la documentation du projet ira dans `docs/`.

### 3.1 Fichiers obligatoires à la racine

| Fichier | Description |
|---|---|
| `README.md` | Présentation minimal GitHub. Nettoyé, basé sur l'état réel V0.1. |
| `LICENSE` | Inchangé (MIT). |
| `CONTRIBUTING.md` | Guide de contribution aligné sur le projet réel. |
| `CODE_OF_CONDUCT.md` | Éthique. |
| `SECURITY.md` | Politique de signalement. |
| `CHANGELOG.md` | Unique, reformatté au format Keep a Changelog + SemVer (0.15.0). Uniquement faits réels. |
| `VERSIONING.md` | Règles de versionnage uniques. |

### 3.2 Structure cible dans `docs/`

```text
docs/
├── README.md                # Portail de la documentation du projet
├── presentation/            # Présentation grand-public orientée projet
│   ├── vision.md
│   ├── objectif.md
│   ├── faq-grand-public.md
│   └── lexique.md
├── technique/               # Documentation technique/pragmatique (usage quotidien)
│   ├── architecture.md
│   ├── installation.md
│   ├── configuration.md
│   ├── build-test.md
│   ├── communication.md
│   ├── determinisme.md
│   ├── ci-cd.md
│   └── gitflow.md
├── reference/               # Référence contractuelle pointue
│   ├── api-echos.md
│   ├── api-syne-console.md
│   ├── integration-launcher.md
│   ├── packaging-livexp.md
│   ├── contrats-mock.md
│   └── schemas.md
├── composants/              # Détails par composant (SYNE/ECHOS/PRISM/LAUNCHER/INSTALLER)
│   ├── syne.md
│   ├── echos.md
│   ├── prism.md
│   ├── launcher.md
│   └── installer.md
└── adr/                     # ADRs conservés (filtrés strictement)
    ├── README.md
    ├── 0000-template.md      # Template unique
    └── accepted/             # Uniquement ADR Accepted valides pour V0.1
```

**Règles pour la documentation du projet :**
- Rédigée uniquement à partir du code réel.
- Axée pragmatique : répondre à « comment ? », « avec quoi ? », « comment lancer/build/test ? »
- Pas de narration historique, pas d'éléments futurs.
- ADRs filtrés : uniquement `Accepted` + choix réellement implémenté + toujours en vigueur. Exclure sans regret tout ce qui concerne des pistes abandonnées.
- Vulgarisation y est présente mais ciblée à la présentation du projet, distincte de la monographie.

---

## 4. Structure de la monographie

La monographie est **indépendante**. Elle ira dans `docs/monographie/` et ne contiendra **aucun** lien vers la documentation du projet.

### 4.1 Arborescence

```text
docs/monographie/
├── 00-SOMMAIRE-GENERAL.md     # Sommaire maître (uniquement titres/numérotation)
├── 01-introduction.md
├── 02-contexte-philosophie.md
├── 03-fondements.md
├── 04-approche-methodologique.md
├── 05-syne.md
├── 06-echos.md
├── 07-prism.md
├── 08-launcher.md
├── 09-installer.md
├── 10-integration-systeme.md
├── 11-reproductibilite-determinisme.md
├── 12-experimentation.md
├── 13-resultats-et-observables.md
├── 14-limites-reelles.md
├── 15-conclusion.md
├── 16-bibliographie.md
└── annexes/
    ├── annexes-contrats.md
    ├── annexes-metrics.md
    ├── annexes-architecture-detaillee.md
    └── annexes-schemas-reels.md
```

**Note importante :** L'arborescence ci-dessus est indicative. Le découpage final sera affiné lors de l'analyse du code réel pour s'assurer qu'il couvre **intégralement** l'implémentation. On pourra ajouter des fichiers intermédiaires si nécessaire (préférer le fractionnement au trop volumineux).

### 4.2 Approche à 3 niveaux intégrés de manière fluide

La monographie doit être lisible par trois publics, **sans découpage explicite en sous-sections** (pas de `X.1 Vulgarisation`, `X.2 Technique`, `X.3 Scientifique`).

**Méthode :**
- **Progression fluide.** Chaque chapitre commence par une explication accessible (grand public), puis s'enrichit naturellement avec le détail technique (implémentation réelle, structures, algorithmes, contrats), pour atteindre enfin l'approfondissement scientifique (précision, rigueur) dans la continuité du texte.
- **Un seul fil narratif.** Pas d'ancres, pas de sauts forcés. Le lecteur avance dans le même document.
- **Accessible et exhaustif.** Un lecteur non technique doit pouvoir comprendre l'essentiel en lisant le chapitre. Un développeur trouvera les détails concrets. Un chercheur trouvera les précisions justifiées par l'implémentation.
- **Factuel.** Se baser exclusivement sur ce qui existe dans le code. Éviter toute spéculation.
- **Imprimable.** Destiné à être mis en page et imprimé à terme (300+ pages). Pas de liens internes vers la documentation du projet. Liens internes uniquement entre fichiers de la monographie si strictement nécessaires (même principe : minimal).

### 4.3 Objectifs de la monographie

- **Exhaustive.** Reprendre le projet dans son intégralité (contexte, philosophie, fonctionnement réel, implémentation par composant, intégration, reproductibilité, limites réelles...).
- **Indépendante.** Rédigée à part, après (ou en parallèle de réflexion sur) la documentation du projet, en s'appuyant sur le **code réel** et sur **la documentation du projet finalisée**.
- **À 3 niveaux fluide.** Grand public + technique + scientifique intégrés harmonieusement.
- **Sans duplication inutile.** La monographie ne répète pas bêtement les guides pragmatiques, mais développe l'explication narrative, conceptuelle et fonctionnelle exhaustive du projet tel qu'il est.
- **Cohérente avec V0.1 réel.** Zéro perspective post-V0.1 non justifiée par l'existant.

---

## 5. Stratégie pour les ADRs

**Critère de conservation (validé) :** Ne conserver **que** les ADRs dont le statut est **`Accepted`**, qui décrivent un **choix architectural ou fonctionnel réellement implémenté et toujours en vigueur** dans l'état V0.1 du code.

**Exemples à conserver (typique) :** Choix déterministes/PRNG, contrats d'intégration effectifs, protocoles réellement utilisés, choix Unreal/PrismLdk effectifs s'ils correspondent à l'implémentation.
**Exemples à exclure :** Electron shell (superseded/retiré), Godot (abandonné), réflexions exploratoires, propositions non implémentées, ADRs sans impact direct sur l'état implémenté.

**Application :**
- Regrouper uniquement les ADRs retenus dans `docs/adr/accepted/`
- Créer `docs/adr/README.md` expliquant le filtre appliqué
- Unifier avec `docs/adr/0000-template.md` (unique)
- Les ADRs non retenus ne seront **pas** recopiés dans la nouvelle documentation. Ils restent dans l'historique git (et iront dans `DocsOld/` si déplacement déjà prévu, mais ne sont pas intégrés à la doc cible).

---

## 6. Phases d'exécution

L'ordre est strict pour éviter de réorganiser au lieu de refondre.

| Phase | Action | Détails |
|---|---|---|
| **Phase 0** | **Lecture et compréhension** | Relire ce plan, poser uniquement les questions vraiment bloquantes. Objectif : alignement total. |
| **Phase 1** | **Analyse code réel** | Parcourir exhaustivement `syne/`, `echos/`, `launcher/`, `prism/LDK/`, `syne-mock/`, `scripts/`, `.github/workflows/`. Extraire uniquement ce qui est implémenté (ports, APIs, contrats, structures, boucles, déterminisme...). Ne pas lire les vieux docs pour « recopier », uniquement pour comprendre le contexte si nécessaire. |
| **Phase 2** | **Nettoyage périmètre** | Identifier précisément les fichiers/documents à ne **pas** intégrer (roadmaps, rapports, ADRs hors filtre). Lister les éléments non pertinents (document à part dans le rapport final). |
| **Phase 3** | **Rédaction documentation du projet** | Créer la structure `docs/` cible + fichiers racine obligatoires. Rédiger `docs/presentation/`, `docs/technique/`, `docs/reference/`, `docs/composants/`, `docs/adr/accepted/`. Axé pragmatique, factuel. |
| **Phase 4** | **Vérification doc projet** | Cohérence entre doc projet et code réel. Correction incohérences versions (0.13.0→0.15.0 si pertinent). Suppression tout élément futur. |
| **Phase 5** | **Rédaction monographie (indépendante)** | Créer `docs/monographie/` avec `00-SOMMAIRE-GENERAL.md` + fichiers par chapitre. Rédiger dans l'approche 3-niveaux fluide. S'appuyer sur la doc projet finalisée **et** sur le code réel. Aucun lien avec `docs/`. |
| **Phase 6** | **Vérification croisée** | Vérifier exhaustivité relative à l'implémentation V0.1. Aucune spéculation. Cohérence interne monographie. Respect strict indépendance. |
| **Phase 7** | **Rapport final** | Résumer : structure créée, fichiers rédigés, ADRs conservés/exclus, éléments non intégrés, état final aligné sur vision. |

---

## 7. Règles rédactionnelles

- **Français** comme langue de référence.
- **Factuel avant littéraire.** Décrire ce qui est, pas ce qui pourrait être.
- **Minimal mais exhaustif.** Aller à l'essentiel sans sacrifier la précision nécessaire.
- **Progressif fluide.** Pour la monographie, montée en complexité naturelle, sans marquage explicite des 3 niveaux.
- **Aucun lien croisé.** Monographie ↛ docs, docs ↛ monographie.
- **Pas de commentaires inutiles.** Respect strict des consignes projet.
- **Cohérent avec 0.15.0.** Aligner versions uniquement si justifié par l'état réel.
- **Markdown propre.** Lisible, structuré, orienté impression future pour la monographie.
- **Code-first.** En cas de doute entre doc existante et code : **code prime**.

---
