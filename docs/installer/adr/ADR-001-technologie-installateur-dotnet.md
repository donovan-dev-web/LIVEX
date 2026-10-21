# ADR-001 : Installateur .NET autonome, et non Qt Installer Framework

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : —
**Source Monographie** : —

---

## Contexte

La spécification de base (v0.2) retient « Qt Installer Framework » (QIFW) comme
technologie *envisagée* et demande d'en vérifier les capacités avant
implémentation. Aucun code d'installateur n'existe dans le dépôt : ni projet
`Setup.*`, ni script de packaging, ni artefact signé. Le jalon de livraison du
Launcher est explicitement inachevé (`launcher/README.md`, G6).

Le besoin réel dépasse l'assistant standard :

1. **Deux chemins indépendants** (logiciel et espace de travail) et une
   validation croisée de ces chemins.
2. **Détection d'une installation existante dans un répertoire personnalisé**,
   avec récupération d'un enregistrement absent ou obsolète.
3. **Persistance d'un choix utilisateur** (l'espace de travail) dans un fichier
   de préférences lu par le Launcher.
4. **Avertissements conditionnels** à la sélection des composants.
5. **Modes non interactifs** (`--check`, `--unattended`) pour la CI et le
   diagnostic.
6. **Cycle de vie complet** : installation, mise à jour en place, réparation,
   ajout/retrait de composant, désinstallation à trois portées.

Le dépôt est mono-chaîne .NET 10 (Launcher, SYNE), Python (ECHOS) et Node
(`syne-mock`) ; sa CI (`.github/workflows/ci.yml`) teste déjà Windows et Linux,
et ses conventions de test sont xUnit (`launcher/Launcher.Tests.*`). Aucun code
C++/Qt n'est présent, et aucun outil Qt n'est installé par la CI.

Le framework QIFW apporte un assistant et un outil de maintenance, mais :

- ses pages personnalisées (second chemin, avertissements, récapitulatif métier)
  exigent un **plugin C++/Qt**, donc une seconde chaîne d'outils, une seconde
  politique de licence et une seconde stratégie de test ;
- sa logique d'installation s'écrit en **Qt Script**, hors xUnit et hors des
  conventions du dépôt ;
- son **outil de maintenance** porte une promesse de mise à jour automatique que
  le périmètre exclut ;
- il n'apporte **ni** la détection d'installation personnalisée, **ni** la
  persistance de l'espace de travail, **ni** la validation des chemins : ces
  fonctions resteraient à écrire autour de lui.

## Décision

Nous réalisons l'installateur comme une **application .NET 10 autonome**,
`livex-setup`, découpée en deux assemblages :

| Assemblage | Nature | Responsabilité |
| :-- | :-- | :-- |
| `Setup.Core` | bibliothèque sans interface | modèle de distribution, préflight, planification, transactions, enregistrement, préférences, désinstallation, journalisation |
| `Setup.App` | exécutable | assistant, ressources FR/EN, progression ; modes `--check`, `--unattended`, `--uninstall`, `--repair` |

`Setup.App` s'appuie sur **Avalonia 11**, la pile d'interface déjà utilisée par
le Launcher (`launcher/Directory.Packages.props`), afin de ne pas introduire de
chaîne graphique supplémentaire. Ce choix est **redécidé ici** : il ne reprend
aucune décision d'une documentation antérieure, désormais retirée.

Règles qui découlent de la décision :

- `Setup.*` ne référence **aucun assemblage du Launcher** (`Launcher.App`,
  `Launcher.Infrastructure`, …). Il applique les contrats publiés — schéma
  `component-manifest-v1`, format du fichier d'installation, conventions de
  chemins — et peut donc réparer un Launcher cassé.
- L'installateur est publié **self-contained** par RID (`win-x64`, `linux-x64`)
  et ne requiert aucun runtime .NET préinstallé.
- Un seul binaire porte l'assistant et les modes non interactifs ; le mode
  headless n'initialise jamais l'interface.
- QIFW n'est pas retenu ; il reste l'alternative de référence si le besoin d'un
  outil de maintenance standardisé réapparaît.

## Conséquences

### Positives

- Une seule chaîne d'outils (.NET 10), un seul langage, les conventions du
  dépôt (avertissements = erreurs, xUnit, CI Windows/Linux existante).
- Logique d'installation **testable unitairement** : planificateur, validation
  de chemins, fusion des préférences, comparaison de versions, transactions.
- Maîtrise complète du parcours : deux chemins, avertissements, récapitulatif,
  options de désinstallation, messages FR/EN.
- Indépendance vis-à-vis du Launcher : l'installateur peut diagnostiquer et
  réparer une installation dont le Launcher ne démarre plus.
- Modes non interactifs exploitables en CI et sur machine sans affichage.

### Négatives

- L'assistant est à construire : pages, navigation, progression, traductions,
  accessibilité. QIFW en fournissait une partie.
- Le binaire self-contained est volumineux (ordre de grandeur : plusieurs
  dizaines de Mo par plateforme, à mesurer) ; il est copié dans l'installation
  comme point d'entrée de maintenance, sans la charge utile (ADR-005).
- Aucun « outil de maintenance » standard : mises à jour et réparations sont des
  modes de notre binaire, à documenter et à tester.

### Risques

- **Dérive de périmètre** : reconstruire un framework d'installation. Mitigation :
  `Setup.Core` reste sans interface et se limite aux six responsabilités du
  tableau ; l'interface ne contient aucune règle métier.
- **Divergence de contrat avec le Launcher** : l'installateur réimplémente la
  validation du manifeste. Mitigation : la source de vérité est le schéma JSON
  versionné, et un test de conformité compare les deux validations sur un jeu
  d'exemples commun.
- **Poids de la copie de maintenance** : voir ADR-005, où le binaire installé
  sert d'entrée de désinstallation et de réparation ; une variante allégée
  (sans interface) est une optimisation de roadmap, pas un prérequis V0.2.

## Alternatives considérées

- **Qt Installer Framework** : assistant, composants, traductions et
  désinstallateur prêts ; rejeté comme choix principal à cause du plugin C++/Qt
  nécessaire aux pages personnalisées, de la logique en Qt Script hors tests, et
  de l'absence des fonctions de détection et de persistance. Réévaluable si
  l'équipe standardise Qt.
- **WiX (MSI) sous Windows + paquet `.deb` sous Linux** : intégration système
  native et taille réduite ; rejeté pour deux chaînes distinctes, deux
  parcours, et l'impossibilité d'installer sans élévation avec un `.deb`.
- **Scripts shell/PowerShell + archive auto-extractible** : très simple ;
  rejeté : pas d'assistant utilisable par un public non technique, pas de
  désinstallation guidée, pas de rollback testable.
- **Mode d'installation intégré au Launcher** : réutilisation maximale ; rejeté
  car un Launcher défaillant ne pourrait plus être réparé, et la distribution
  devrait déjà contenir un Launcher fonctionnel pour s'installer.

## Validation / rejet

La décision est validée si, sur une machine vierge Windows et une machine
vierge Linux :

1. `livex-setup` démarre sans runtime .NET installé ;
2. l'assistant se déroule sans page personnalisée manquante ;
3. `--unattended` produit la même installation que l'assistant ;
4. une installation dont le Launcher a été supprimé est détectée et réparée
   par l'installateur seul.

Réouverture si l'équipe adopte Qt pour un autre composant, ou si le coût de
maintenance de l'assistant dépasse celui d'un framework standard.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Remplace la technologie « envisagée » de la spécification de base |
