# ADR-001 : Installateur Avalonia séparé du Launcher, cœur sans interface

**Composant** : LIVEX (Installateur)
**Statut** : [Proposed]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `../../docs-launcher/adr/ADR-001-stack-dotnet-avalonia.md`
**Source Monographie** : —

---

## Contexte

Le jalon G6 exige une installation propre sur Windows et Linux avec un assistant
graphique (`../../docs-launcher/ROADMAP.md` §6.7). Quatre contraintes pèsent sur le
choix :

1. L'installateur doit **démarrer sur une machine sans aucun runtime** : il ne peut
   pas dépendre de ce qu'il installe.
2. Le projet dispose déjà d'une chaîne .NET 10 et d'Avalonia (Launcher) et d'une
   discipline de frontières d'assembly vérifiée par test.
3. Les exigences de test de G6 supposent des **installations répétables en CI sur
   machine vierge**, donc un mode sans interface.
4. `PACKAGING.md` §2 décrit le Launcher comme livré **sans** ses composants et ne
   prévoit pas qu'il les installe : l'installation des composants est un autre
   rôle que l'orchestration.

## Décision

**L'installateur est une application .NET distincte, publiée en self-contained
fichier unique, avec un assistant Avalonia et un cœur sans interface.**

- Projets `Setup.Core` (logique), `Setup.Platform.Windows`, `Setup.Platform.Linux`,
  `Setup.App` (assistant et CLI).
- Le même plan d'installation est exécuté par l'assistant et par `--unattended`.
- Aucune référence de projet vers SYNE, ECHOS ou PRISM ; frontières d'assembly
  vérifiées par test statique.
- Le Launcher **n'installe pas** les composants ; l'installateur est un produit
  séparé qui dépose le Launcher.

## Conséquences

### Positives
- Une seule compétence, une seule chaîne d'outils, un seul écosystème de test.
- Le mode non interactif permet les tests sur machine propre et les serveurs sans
  affichage.
- Identité visuelle cohérente avec le Launcher (même thème et mêmes assets).
- Le Launcher reste indépendant de la manière dont il a été installé.

### Négatives
- Un second exécutable à construire, signer et publier par OS.
- Le binaire self-contained de l'installateur est volumineux.
- Duplication limitée d'éléments d'interface avec le Launcher.

### Risques
- Le partage de code avec `Launcher.Infrastructure` crée un couplage de versions ;
  s'il devient gênant, extraction d'un assembly commun (`ARCHITECTURE.md` §2).
- Avalonia sous Linux dépend de bibliothèques système ; le contrôle de l'étape 3
  doit les détecter avant l'affichage (installation en mode non interactif sinon).

## Alternatives considérées

- **Inno Setup (Windows) + paquet `.deb` (Linux)** : natifs et familiers, mais deux
  bases de code, une logique de dépendances et de manifestes difficile à exprimer, et
  une expérience différente selon l'OS → refusé.
- **MSI ou MSIX + `.deb` seuls** : conformes aux conventions des systèmes, mais
  inadaptés à un assistant sur mesure (répertoires, options, taille, configuration
  de manifestes) → refusé comme mécanisme principal ; le `.deb` reste une option
  de livraison Linux (OI-01).
- **Installateur Electron ou web** : lourd, contraire à l'esprit de ADR-001 du
  Launcher (« aucune page web ») → refusé.
- **Scripts shell et PowerShell sans interface graphique** : insuffisants pour
  l'exigence d'assistant → conservés seulement comme outils de développement
  (`dev-stack.sh`).
- **Intégrer l'installation dans le Launcher** : le Launcher devient une
  dépendance technique de sa propre installation et mêle deux responsabilités →
  refusé.

## Validation / rejet

- Validation : TS-01 et TS-02 produisent des installations identiques sur machine
  propre ; l'analyse statique des frontières passe.
- Réouverture : si le couplage avec `Launcher.Infrastructure` oblige à publier
  l'installateur à chaque changement du Launcher, ou si Avalonia ne démarre pas de
  façon fiable sur les images Ubuntu cibles.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création | Spécification de l'installateur |
