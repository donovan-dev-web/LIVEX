# Guide utilisateur — installer LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [LIVRÉ] — guide utilisateur de l'installateur LIVEX 1.0.0
**Dernière mise à jour** : 31 octobre 2026

---

## 1. À qui s'adresse ce guide

À toute personne qui veut installer LIVEX sur son ordinateur sans manipuler
les fichiers à la main : chercheur, utilisateur non technique, développeur qui
veut une pile propre. Aucune connaissance de .NET, de Python ou d'Unreal
Engine n'est nécessaire pour l'installation décrite ici.

> **Disponibilité :** l'installateur LIVEX 1.0.0 est publié pour Windows x64
> et Linux x64 dans la [release `livex-v1.0.0`](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0).

## 2. Avant de commencer

### Ce dont vous avez besoin

- un ordinateur **Windows 10/11 64 bits** ou **Linux 64 bits** (glibc) ;
- l'espace disque nécessaire, indiqué dans les notes de release (il dépend de
  la plateforme **et des modules cochés** ; il n'est pas estimé ici) ;
- le dossier de distribution téléchargé (`livex-setup`, `payload/`,
  `distribution.json`, signatures) ;
- **aucun accès Internet pour le socle** : Launcher, SYNE, ECHOS et PRISM
  s'installent hors ligne ;
- une connexion Internet **seulement si vous cochez un module facultatif**
  (PRISM-LDK, documentation, SYNE-Mock), sauf si vous utilisez la variante
  complète `full`, qui embarque les actifs facultatifs. Le chargement depuis
  une archive locale et les options `--offline-assets` / `--no-network` ne
  sont pas encore reliés au moteur d'installation.

### Ce que vous n'avez pas besoin d'installer

- .NET ;
- Python ;
- Node.js (même pour SYNE-Mock : son archive embarque son runtime) ;
- Unreal Engine, sauf si vous voulez utiliser **PRISM-LDK** : il se copie alors
  dans un projet Unreal que vous possédez déjà. PRISM seul n'en a pas besoin.

### Ce que l'installateur ne fera pas

- il ne demande pas de mot de passe administrateur ;
- il ne modifie pas le `PATH`, les associations de fichiers ni les variables
  d'environnement ;
- il ne télécharge jamais le socle, ne vérifie pas les mises à jour et ne
  télécharge rien en arrière-plan ;
- il n'installe pas PRISM-LDK dans un projet Unreal : il dépose l'archive, vous
  la copiez vous-même dans le projet de votre choix.

## 3. Vérifier le paquet (recommandé)

### Windows

Clic droit sur `livex-setup.exe` → **Propriétés** → **Signatures numériques**.
La signature doit être valide et émise au nom du projet LIVEX. Si la release
n'est pas signée (aucun certificat disponible), les notes de release le disent
et SmartScreen peut afficher un avertissement : vérifiez alors les sommes avant
d'exécuter le fichier.

### Linux

```bash
sha256sum -c SHA256SUMS
gh attestation verify livex-1.0.0-linux-x64-core.tar.gz -R donovan-dev-web/LIVEX
```

Les sommes et les attestations remplacent la signature GPG en V1. Si une
vérification échoue, **n'exécutez pas** l'installateur et signalez le problème.
La procédure est reprise dans `SECURITY.md`.

## 4. Installer

1. Lancez `livex-setup` (double-clic, ou `./livex-setup` sous Linux).
2. **Accueil** : l'assistant présente LIVEX. Si une installation existe déjà, il
   ouvre l'écran de maintenance au lieu d'une nouvelle installation.
3. **Langue** : français ou anglais. Le changement s'applique immédiatement.
4. **Licence** : lisez la licence GPL-3.0-or-later et acceptez-la pour
   poursuivre. Les mentions des composants tiers sont accessibles depuis cet
   écran.
5. **Composants** : le Launcher, SYNE et ECHOS sont inclus et ne peuvent pas
   être décochés. **PRISM** est un composant de base, **coché par défaut** ; les
   trois modules facultatifs sont proposés **décochés par défaut** :
   - **PRISM** — déjà présent dans la distribution :
     l'application de rendu et d'interaction, packagée pour votre système ;
     elle ne demande pas Unreal Engine ;
   - **PRISM-LDK** — le plugin pour un projet Unreal Engine que vous possédez ;
     il est déposé sous forme d'archive, prête à copier ;
   - **Documentation hors ligne** — les documents consultables sans réseau ;
   - **SYNE-Mock** — l'émulateur du moteur pour le développement.
   Si PRISM n'est pas coché, l'assistant précise que le rendu et l'interaction
   3D ne seront pas disponibles ; vous pouvez continuer. Un module absent de la
   version publiée apparaît comme **indisponible**, avec la raison, sans case.
6. **Chemins** : choisissez deux dossiers différents.
   - **Logiciel** : là où LIVEX est installé (par défaut
     `%LOCALAPPDATA%\Programs\LIVEX` ou `~/.local/opt/livex`).
   - **Espace de travail** : là où vous organiserez vos expériences et vos
     rapports (par défaut `<données>/workspace`).
   L'assistant vérifie que les deux dossiers sont utilisables avant de
   continuer.
7. **Récapitulatif** : vérifiez les dossiers, les composants et l'espace
   disque. Revenez en arrière si nécessaire.
8. **Installation** : la progression s'affiche étape par étape. Ne fermez pas
   la fenêtre pendant cette phase.
9. **Résultat** : en cas de succès, « Lancer LIVEX » est coché ; décochez-le si
   vous préférez démarrer plus tard.

### Profils et composants individuels

Deux paquets d'installation sont publiés pour chaque système :

| Profil | Contenu | Réseau |
| :-- | :-- | :-- |
| **`core`** (défaut) | Launcher, SYNE, ECHOS, PRISM et leurs supports ; les modules facultatifs sont proposés au téléchargement | requis pour un module coché |
| **`full`** | les mêmes composants **et** les trois archives facultatives | aucun |

Choisissez `full` sur un poste isolé ; `core` suffit si vous avez Internet au
moment d'installer un module.

Chaque composant est aussi publié séparément (`livex-launcher`, `syne`,
`echos`, `prism`, `prism-ldk`, `livex-docs`, `syne-mock`), sous forme d'archive
décompressable à la racine d'une installation. C'est un usage **manuel et
avancé** : l'installateur reste la voie normale, et seule l'archive du LDK, de
la documentation ou de SYNE-Mock est lue par lui.

### Si l'installation échoue

L'assistant affiche la cause et le chemin du journal. Il indique aussi les
éléments qu'il n'a pas pu supprimer. Copiez le journal avant de demander de
l'aide : il contient les étapes et les erreurs, jamais vos données
d'expérience.

## 5. Premier lancement

LIVEX démarre avec le Launcher. Il détecte SYNE et ECHOS installés et affiche
leur état. Pour vérifier l'installation sans ouvrir l'interface :

```bash
<répertoire logiciel>/livex-launcher --check
```

La commande affiche une ligne par contrôle et retourne `0` si tout est
conforme. Un code différent indique un contrôle en échec ; la ligne
correspondante donne la cause.

## 6. Où sont mes données

| Donnée | Emplacement |
| :-- | :-- |
| Logiciel | répertoire choisi à l'installation |
| Préférences | `<racine de données>/settings.json` |
| Espace de travail | dossier choisi à l'installation |
| Modules facultatifs | `<répertoire logiciel>/components/prism/`, `components/syne-mock/`, `plugins/prism-ldk/`, `docs/` |
| Téléchargements en cache | `<racine de données>/cache/downloads/` |
| Paquets `.livexp` | `<racine de données>/packages/` |
| Archives scellées | `<racine de données>/archives/` |
| Journaux de session | `<racine de données>/sessions/` |
| Journaux d'installation | `<racine de données>/logs/` |
| Base d'analyse ECHOS | `<racine de données>/echos/` |

La **racine de données** est `LIVEX_DATA` si cette variable est définie, sinon
`~/.livex-data` (Windows : `%USERPROFILE%\.livex-data`).

## 7. Mettre à jour

LIVEX ne se met pas à jour tout seul. Pour passer à une version plus récente :

1. téléchargez la nouvelle distribution et vérifiez-la ;
2. lancez `livex-setup` : l'installation existante est détectée ;
3. choisissez **Mettre à jour** ;
4. vérifiez le récapitulatif, puis lancez l'opération.

Vos préférences, votre espace de travail, vos paquets et vos archives sont
conservés. Une version antérieure ne peut pas être réinstallée par-dessus une
version plus récente sans une procédure de dépannage explicite.

## 8. Réparer une installation

Si un fichier a été supprimé ou corrompu :

1. lancez `livex-setup` ;
2. choisissez **Réparer** ;
3. l'assistant compare l'installation à son inventaire et signale les fichiers
   manquants ou modifiés.

La restauration utilise la distribution d'origine : gardez le dossier
téléchargé tant que vous pouvez avoir besoin de réparer ou d'ajouter un
composant.

## 9. Ajouter ou retirer PRISM ou un module facultatif

Lancez `livex-setup`, choisissez **Gérer les composants**, puis cochez ou
décochez PRISM, PRISM-LDK, la documentation ou SYNE-Mock. Les composants
obligatoires ne sont pas modifiables. Cocher PRISM ne demande aucun
téléchargement : il vient de la distribution déjà téléchargée.

Pour un ajout, l'installateur utilise d'abord le cache local, puis l'adresse de
la release d'origine. Sans réseau, il vous propose de choisir une archive
téléchargée à la main, vérifiée par empreinte, ou de renoncer au module.

**PRISM-LDK** : après l'ajout, l'archive se trouve dans
`<répertoire logiciel>/plugins/prism-ldk/`. Copiez-la dans le dossier
`Plugins/` de votre projet Unreal, puis ouvrez le projet : le plugin est
compilé par Unreal. LIVEX n'écrit jamais dans votre projet.

## 10. Désinstaller

Lancez `livex-setup --uninstall`, ou passez par **Applications et
fonctionnalités** (Windows) / le menu des applications (Linux).

Trois choix vous sont proposés :

| Choix | Supprime | Conserve |
| :-- | :-- | :-- |
| **A — Le logiciel** | LIVEX et ses raccourcis | préférences, espace de travail, paquets, archives, journaux |
| **B — Le logiciel et la configuration** | A + préférences et cache | espace de travail, paquets, archives, journaux |
| **C — Le logiciel, la configuration et l'espace de travail** | B + le dossier d'expériences, après confirmation | paquets, archives, journaux |

L'option A supprime aussi **PRISM** et les modules facultatifs installés
(PRISM-LDK, documentation, SYNE-Mock) et conserve le cache de téléchargement ;
l'option B supprime également ce cache.

L'option C affiche le chemin exact et le contenu avant de supprimer. Elle
comporte une case supplémentaire, décochée par défaut, pour supprimer aussi les
**résultats de recherche** (paquets `.livexp`, archives, sessions, base
d'analyse) ; cette case exige une seconde confirmation.

Les journaux d'installation sont conservés dans tous les cas, pour le
diagnostic.

## 11. Dépannage

| Symptôme | Cause probable | Action |
| :-- | :-- | :-- |
| « Répertoire non inscriptible » | dossier protégé ou appartenant à un autre compte | choisir un dossier sous votre profil utilisateur |
| « Espace de travail invalide » | dossier égal à la racine de données ou contenant le logiciel | choisir un dossier distinct |
| « Une installation existe déjà » | LIVEX est déjà installé | utiliser la maintenance, ou désinstaller d'abord |
| « Enregistrement introuvable » | entrée système supprimée | laisser l'assistant retrouver l'installation, ou la localiser |
| « Fichier corrompu » | distribution incomplète | retélécharger et vérifier les signatures |
| « libicu introuvable » (Linux) | distribution minimale | installer le paquet `libicu` de votre distribution |
| « Connexion impossible » pendant un module | réseau, proxy ou GitHub inaccessible | réessayer avec un réseau autorisant GitHub ou utiliser la variante `full` |
| « Empreinte incorrecte » | archive altérée ou téléchargement incomplet | retélécharger le paquet `full` ou l'actif concerné et vérifier son empreinte ; ne pas forcer l'installation |
| Le Launcher ne démarre pas | installation incomplète | lancer `livex-setup` puis **Réparer** |
| L'installation s'arrête sans message | erreur inattendue | consulter `<racine de données>/logs/installer-*.log` |

Deux commandes aident au diagnostic sans rien modifier : `livex-setup --check`
(contrôles préalables, aucune écriture) et `livex-setup --repair`. La liste
complète des options, leurs codes de sortie et l'emplacement du journal sont
décrits dans [`COMMANDES.md`](COMMANDES.md).

## 12. Questions fréquentes

**Ai-je besoin d'Internet ?**
Pas pour le socle : Launcher, SYNE et ECHOS s'installent avec la distribution
seule, et la vérification des signatures se fait hors ligne. Un module
facultatif coché est téléchargé depuis la release ; sans réseau, choisissez la
variante complète `full`. L'installation depuis une archive locale n'est pas
encore disponible dans le parcours livré.

**Puis-je installer sans être administrateur ?**
Oui. L'installateur n'écrit que dans votre profil utilisateur et ne demande
aucune élévation.

**Puis-je installer sur un autre disque ?**
Oui, à condition que le dossier soit inscriptible par votre compte. Les
répertoires système sont refusés.

**Où est passé `livex.json` ?**
Il n'existe plus. Les chemins sont retrouvés autrement : le Launcher détecte
ses composants depuis son emplacement, et l'espace de travail est mémorisé dans
`settings.json`.

**Puis-je déplacer mon espace de travail ?**
Oui, mais LIVEX ne le déplace pas automatiquement. Déplacez le dossier, puis
relancez l'installateur pour indiquer le nouveau chemin. Si l'ancien chemin
n'existe plus, LIVEX le signale et ne supprime rien.

**PRISM est-il installé ?**
PRISM est un **composant de base** : il est livré avec l'installateur, sous forme
d'application packagée pour Windows et Linux, et ne demande pas Unreal Engine.
Il est coché par défaut ; si vous le décochez, l'assistant vous prévient que le
rendu 3D ne sera pas disponible, mais l'installation reste possible.
PRISM-LDK est un module distinct : c'est un plugin en archive, téléchargé à la
demande et destiné à un projet Unreal que vous possédez.

**Mes résultats de recherche peuvent-ils disparaître lors d'une
désinstallation ?**
Non, sauf si vous cochez explicitement l'extension de l'option C et confirmez
une seconde fois.

## 13. Obtenir de l'aide

- consultez d'abord le journal d'installation ;
- ouvrez une issue GitHub avec le label `component/installer` ;
- joignez la version (`livex-setup --version`), la plateforme, le message
  exact et le journal.

Ne joignez jamais vos données d'expérience à une issue publique.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Guide de la version à venir |
| 29 octobre 2026 | Profils `core`/`full`, archives de composants, vérification par sommes et attestations, SmartScreen si non signé | Plan d'alignement V1 (D3, D4, ADR-010) |
| 29 octobre 2026 | §11 : renvoi vers `COMMANDES.md` | Les commandes de diagnostic existent |
