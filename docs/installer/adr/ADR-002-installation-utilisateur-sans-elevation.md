# ADR-002 : Installation par utilisateur, sans élévation

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-001
**Source Monographie** : —

---

## Contexte

La spécification exige une installation « non privilégiée » lorsque le système
le permet, sans en faire une exigence absolue. Deux modèles coexistent dans
l'écosystème de bureau :

| Modèle | Emplacement typique | Enregistrement | Mise à jour |
| :-- | :-- | :-- | :-- |
| **Par utilisateur** | `%LOCALAPPDATA%`, `~/.local` | `HKCU`, XDG utilisateur | par l'utilisateur, sans UAC/sudo |
| **Machine** | `C:\Program Files`, `/opt` | `HKLM`, paquets système | administrateur |

Le produit s'adresse à un chercheur ou un utilisateur non technique qui doit
pouvoir installer sans administrateur, déplacer son espace de travail, et
désinstaller sans laisser de traces système. Aucun besoin fonctionnel ne
justifie une installation machine : les composants écrivent uniquement dans le
répertoire utilisateur et sur la boucle locale.

Contraintes concrètes :

- Windows : écrire sous `%LOCALAPPDATA%` et `HKCU` ne demande aucune élévation.
  Un emplacement choisi par l'utilisateur (par exemple `D:\LIVEX`) est
  généralement accessible sans élévation, mais pas garanti.
- Linux : `~/.local` est inscriptible par l'utilisateur ; `/opt` et
  `/usr/local` ne le sont pas. Aucun `sudo` ne doit être demandé par un
  installateur graphique lancé depuis une session utilisateur.
- Un installateur qui demande une élévation pour installer dans un répertoire
  utilisateur crée une attente de sécurité inutile et rend la désinstallation
  dépendante d'un compte administrateur.

## Décision

L'installateur **n'installe que pour l'utilisateur courant**, sans élévation, et
n'effectue aucune modification système globale.

### Emplacements par défaut

| Plateforme | Répertoire logiciel par défaut | Enregistrement | Intégrations |
| :-- | :-- | :-- | :-- |
| Windows x64 | `%LOCALAPPDATA%\Programs\LIVEX` | `HKCU` (voir ADR-005) | menu Démarrer utilisateur, bureau (option) |
| Linux x64 | `~/.local/opt/livex` | XDG (`~/.local/share/livex`) | `~/.local/share/applications`, icône XDG, bureau (option) |

L'utilisateur peut choisir un autre répertoire logiciel **à condition qu'il soit
inscriptible par son compte**. Le préflight vérifie ce droit par une écriture
réelle (fichier sonde créé puis supprimé) avant toute modification.

### Refus explicites

L'installateur **refuse** un répertoire cible qui :

- n'est pas inscriptible par l'utilisateur courant ;
- se trouve à l'intérieur du répertoire de données ou de l'espace de travail
  (et réciproquement) ;
- est la racine d'un volume, le répertoire personnel, ou un répertoire système
  (`C:\Windows`, `/usr`, `/etc`, `/opt`, `/bin`, …).

Le message indique la cause, le chemin concerné et une alternative par défaut.
Aucune demande d'élévation n'est proposée : le produit ne sait pas installer au
niveau machine, et ne fait pas semblant.

### Périmètre exclu

- Installation machine (`HKLM`, `/opt`, paquets système) : hors périmètre V0.2.
- Service système, tâche planifiée, démarrage automatique : hors périmètre.
- Modification du `PATH`, des associations de fichiers ou des variables
  d'environnement globales : interdite par défaut (voir spécification).
- Création de comptes, de pare-feu ou de règles réseau : hors périmètre.

## Conséquences

### Positives

- Aucun écran UAC, aucun `sudo`, aucune dépendance à un compte administrateur.
- Désinstallation complète par l'utilisateur, sans conflit avec d'autres
  comptes de la machine.
- Plusieurs utilisateurs d'une même machine peuvent installer des versions
  différentes sans se marcher dessus (un enregistrement par utilisateur).
- Le périmètre de test est celui de la CI : un runner Windows et un runner
  Linux, sans privilèges élevés.

### Négatives

- L'application n'est pas visible pour les autres comptes de la machine.
- Un administrateur ne peut pas préparer une installation pour tous les
  utilisateurs ; il doit installer par compte.
- Les emplacements `%LOCALAPPDATA%` et `~/.local` sont moins « découvrables »
  qu'un menu Démarrer machine ou `/opt` ; les raccourcis compensent.
- Un répertoire choisi sur un volume réseau ou amovible peut être lent ou
  indisponible ; le préflight le signale sans l'interdire.

### Risques

- **Écriture dans un répertoire partagé** : si l'utilisateur choisit un chemin
  inscriptible mais partagé (par exemple un dossier synchronisé), deux
  installations peuvent se concurrencer. Mitigation : l'enregistrement est par
  utilisateur et l'installateur refuse d'écraser une installation valide
  détectée.
- **Chemin trop long sous Windows** : les composants embarquent des
  arborescences profondes (runtime Python). Mitigation : préflight de longueur
  de chemin et message explicite.

## Alternatives considérées

- **Installation machine par défaut** : plus proche des habitudes Windows ;
  rejetée car elle impose l'élévation et contredit le principe de moindre
  privilège du produit.
- **Double mode (utilisateur ou machine)** : la spécification le permettrait à
  terme ; rejetée en V0.2 car elle double les parcours, les enregistrements,
  les tests et les cas de désinstallation pour un besoin non démontré.
- **Installation portable sans enregistrement** : simple ; rejetée car elle
  empêche la détection fiable d'une installation existante dans un répertoire
  personnalisé, exigence explicite de la spécification.

## Validation / rejet

- Sur Windows : installation dans le répertoire par défaut puis dans
  `D:\LIVEX-demo` sans UAC ; vérification qu'aucune clé `HKLM` n'est écrite.
- Sur Linux : installation dans `~/.local/opt/livex` puis dans
  `~/Documents/LIVEX` sans `sudo` ; vérification qu'aucun fichier hors du
  répertoire personnel n'est créé.
- Refus vérifié pour `C:\Program Files`, `/opt`, `/usr` et pour un répertoire
  non inscriptible.

Réouverture si un besoin d'installation multi-utilisateur ou gérée par
l'organisation apparaît.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | — |
