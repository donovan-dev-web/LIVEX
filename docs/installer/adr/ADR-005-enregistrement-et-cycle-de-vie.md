# ADR-005 : Enregistrement, détection et cycle de vie de l'installation

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-002, ADR-004
**Source Monographie** : —

---

## Contexte

La spécification exige de détecter une installation existante **même dans un
répertoire personnalisé**, sans dépendre de `livex.json`, de ne jamais écraser
silencieusement une installation valide, et de traiter explicitement un
enregistrement absent ou obsolète. Elle exige aussi une désinstallation qui
retrouve l'espace de travail et supprime les intégrations créées.

Trois informations différentes doivent être distinguées, faute de quoi le
modèle se dégrade en « fichier global » :

| Information | Nature | Où elle vit | Qui l'écrit |
| :-- | :-- | :-- | :-- |
| **Identité de l'installation** | métadonnée d'installateur | enregistrement système + `.livex-install.json` dans le répertoire logiciel | installateur |
| **Inventaire des fichiers installés** | métadonnée d'installateur | `inventory.json` dans le répertoire logiciel | installateur |
| **Préférences utilisateur** | configuration applicative | `<LIVEX_DATA>/settings.json` | installateur et Launcher |

Le Launcher ne lit ni l'enregistrement système ni l'inventaire : il détecte ses
composants depuis son propre emplacement (`components/`) et les sources
déclarées (`LIVEX_HOME`, `~/.livex/components`, `components.registry`). Le
fichier `livex.json` reste supprimé du périmètre.

## Décision

### 1. Enregistrement par utilisateur

**Windows** — deux clés `HKCU`, aucune écriture `HKLM` :

```text
HKCU\Software\LIVEX\Installer
    InstallId        REG_SZ   ULID de l'installation
    InstallLocation  REG_SZ   chemin du répertoire logiciel
    Version          REG_SZ   version du produit installé
    Channel          REG_SZ   stable | dev

HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\LIVEX
    DisplayName      "LIVEX"
    DisplayVersion   version du produit
    Publisher        "LIVEX"
    InstallLocation  chemin du répertoire logiciel
    DisplayIcon      <install>\assets\livex.ico
    UninstallString  "<install>\livex-setup.exe" --uninstall
    ModifyPath       "<install>\livex-setup.exe" --maintenance
    EstimatedSize    taille installée en Ko
    InstallDate      AAAAMMJJ
    NoModify         0
    NoRepair         1
```

**Linux** — fichiers XDG, aucun paquet système :

```text
$XDG_DATA_HOME/livex/installer/installation.json   # défaut ~/.local/share
$XDG_DATA_HOME/applications/livex.desktop
$XDG_DATA_HOME/icons/hicolor/scalable/apps/livex.svg
```

`installation.json` contient le même identifiant, le même chemin et la même
version que les clés Windows, avec un champ `schema` versionné.

### 2. Enregistrement local dans l'installation

Le répertoire logiciel contient deux fichiers créés par l'installateur :

- `.livex-install.json` : identité (schéma, `installId`, produit, version,
  plateforme, date, composants installés) ;
- `.livex-inventory.json` : liste des fichiers installés avec taille et
  empreinte SHA-256.

Ils ne sont **pas** un fichier de configuration global : ils ne contiennent
aucun réglage utilisateur, aucun chemin d'espace de travail, et le Launcher ne
les lit pas. Ils servent à la désinstallation sélective, à la réparation et à
la récupération d'un enregistrement système perdu.

### 3. Détection et récupération

Algorithme :

1. Lire l'enregistrement système (registre ou `installation.json`).
2. Vérifier : répertoire présent, `.livex-install.json` lisible et de schéma
   connu, `installId` concordant, binaire `livex-launcher` présent.
3. Si tout est valide → installation **existante** : proposer mise à jour,
   réparation, ajout/retrait de composant, désinstallation. Une seconde
   installation simultanée est refusée par défaut.
4. Si l'enregistrement est absent, obsolète ou incohérent → ne jamais conclure
   « non installé ». L'assistant affiche le détail et propose :
   - **localiser** le répertoire de l'installation et adopter son
     `.livex-install.json` ;
   - **réparer** l'enregistrement à partir de ce fichier ;
   - **supprimer** l'entrée obsolète, après confirmation explicite.
5. Si aucun enregistrement n'existe, chercher uniquement dans le répertoire par
   défaut un éventuel `.livex-install.json` (cas d'un enregistrement perdu). Ne
   jamais parcourir le disque entier.

### 4. Entrée de maintenance installée

Le répertoire logiciel reçoit une copie du binaire d'installation
(`<install>/livex-setup[.exe]`), sans la charge utile. C'est le point d'entrée
de `UninstallString` et de `ModifyPath`, et le support de `--repair`.

Conséquences :

- la désinstallation fonctionne hors ligne, même si le dossier de distribution
  d'origine a été supprimé ;
- la réparation vérifie l'inventaire et signale les fichiers manquants ou
  corrompus ; leur restauration exige le dossier de distribution d'origine
  (charge utile non dupliquée dans l'installation) ; un module facultatif peut
  aussi être réinstallé depuis le cache ou une archive locale (ADR-008) ;
- le poids ajouté à l'installation est celui du binaire d'installation seul,
  pas celui de la charge utile.

### 5. Mise à jour, réparation, ajout/retrait

| Opération | Déclencheur | Règle |
| :-- | :-- | :-- |
| **Mise à jour** | exécution d'un paquet de version supérieure | en place, données conservées ; downgrade refusé sans option explicite |
| **Réparation** | `--repair` ou entrée « Modifier » | vérifie l'inventaire ; restaure depuis la distribution d'origine si présente |
| **Ajout/retrait de composant** | `--maintenance` | seuls les composants facultatifs sont modifiables ; les obligatoires restent verrouillés |
| **Désinstallation** | `UninstallString` ou `--uninstall` | portées A/B/C de l'ADR-004 |

Avant toute opération qui remplace des fichiers, l'installateur vérifie qu'aucun
processus LIVEX ne s'exécute depuis le répertoire logiciel (Launcher, SYNE,
ECHOS). Il ne tue jamais un processus sans accord : il demande sa fermeture,
ou échoue proprement en mode non interactif.

### 6. Nettoyage de l'enregistrement

La désinstallation supprime les clés ou fichiers qu'elle a créés, et seulement
ceux-là. Une entrée `HKCU\Software\LIVEX\Installer` préexistante d'une autre
application n'est jamais touchée ; l'absence de l'entrée attendue est traitée
comme un résidu signalé, pas comme un succès.

## Conséquences

### Positives

- Détection fiable dans un répertoire personnalisé, sans fichier global lu par
  le Launcher.
- Récupération explicite des enregistrements obsolètes : l'utilisateur ne perd
  ni son installation ni ses données par surprise.
- Désinstallation et réparation disponibles hors ligne depuis l'installation.
- Séparation nette entre métadonnées d'installateur et préférences
  applicatives.

### Négatives

- Deux mécanismes d'enregistrement à maintenir (registre Windows, XDG Linux) et
  donc deux jeux de tests d'intégration.
- Une copie du binaire d'installation dans le répertoire logiciel.
- L'inventaire des fichiers est un fichier supplémentaire à tenir à jour à
  chaque mise à jour ; un inventaire obsolète doit être détecté, pas suivi
  aveuglément.

### Risques

- **Enregistrement orphelin** après suppression manuelle du répertoire :
  l'assistant doit proposer le nettoyage, jamais réinstaller par-dessus.
- **Écriture de registre refusée** (stratégie d'entreprise) : l'installation
  doit échouer avant d'écrire le répertoire, avec un message explicite, plutôt
  que de laisser une installation non désinstallable.
- **Inventaire incomplet** après une panne : la désinstallation retombe sur le
  contenu du répertoire logiciel, en conservant les fichiers non reconnus et en
  les signalant.

## Alternatives considérées

- **Détection par `livex.json`** : exclue par la spécification.
- **Détection par balayage du disque** : lente, non déterministe, et dangereuse
  sur des répertoires homonymes ; rejetée.
- **Uniquement un fichier dans le répertoire logiciel** : simple, mais ne
  permet ni l'entrée « Applications et fonctionnalités », ni la détection dans
  un répertoire personnalisé sans que l'utilisateur le désigne ; rejetée comme
  mécanisme unique.
- **Enregistrement machine (`HKLM`, `/usr/share`)** : exclu par ADR-002.

## Validation / rejet

- Installation dans le répertoire par défaut puis dans un répertoire
  personnalisé : détection correcte dans les deux cas, entrée de
  désinstallation présente.
- Suppression du répertoire logiciel puis relance : l'assistant signale
  l'enregistrement orphelin et propose le nettoyage.
- Suppression de l'enregistrement puis relance : l'assistant retrouve
  l'installation par le répertoire par défaut et propose de réparer
  l'enregistrement.
- Désinstallation : plus aucune clé `HKCU\Software\LIVEX` ni fichier XDG
  appartenant à LIVEX ; les fichiers non reconnus sont signalés.
- Mise à jour vers une version inférieure : refusée sans option explicite.

Réouverture si un canal de distribution Linux par paquet (`.deb`, Flatpak)
devient la cible principale : l'enregistrement serait alors porté par le
gestionnaire de paquets.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | — |
