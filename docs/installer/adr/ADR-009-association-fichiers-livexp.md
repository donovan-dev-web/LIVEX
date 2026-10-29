# ADR-009 : Association des fichiers `.livexp` au lanceur

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 29 octobre 2026
**Dépend de** : ADR-001, ADR-002
**Source Monographie** : —

---

## Contexte

La spécification de base (§8.3) interdit par défaut toute association de
fichiers créée par l'installateur : « Aucune association de fichiers ni
modification globale des variables d'environnement ne doit être effectuée par
défaut. »

Or le format `.livexp` est désormais un artefact utilisateur : un paquet de
campagne produit par le Launcher, échangé entre utilisateurs, destiné à être
ouvert par double-clic. Sans association, un fichier `.livexp` reçu n'a
d'icône propre ni de programme d'ouverture : l'utilisateur doit passer par
« Ouvrir avec… » et choisir lui-même l'exécutable, ce qui est hors de portée
d'un public non technique.

Le projet dispose par ailleurs des icônes définitives (`docs/assets/icons/`) :

| Ressource | Variante sombre (défaut) | Variante claire |
| :-- | :-- | :-- |
| Application | `livex-app-icon.png` / `.svg` | `livex-app-icon-light.*` |
| Fichier `.livexp` | `livex-livexp-file-icon.png` / `.svg` | `livex-livexp-file-icon-light.*` |

**Décision de l'architecte, 29 octobre 2026** : associer les fichiers
`.livexp`.

## Décision

L'installateur **associe l'extension `.livexp`** au lanceur LIVEX, à
l'échelle de l'utilisateur courant uniquement, avec l'icône dédiée :

- **Windows** — `HKCU\Software\Classes` :
  - `.livexp` → `LIVEX.Package` (+ `OpenWithProgids`) ;
  - `LIVEX.Package\DefaultIcon` → `assets\livexp.ico` ;
  - `LIVEX.Package\shell\open\command` → `"<répertoire logiciel>\livex-launcher.exe" "%1"`.
- **Linux** — XDG utilisateur :
  - `$XDG_DATA_HOME/mime/packages/livex-livexp.xml` déclare
    `application/x-livex-package` avec le motif `*.livexp` ;
  - `$XDG_DATA_HOME/applications/livex-livexp.desktop` ouvre le lanceur ;
  - `update-mime-database` et `update-desktop-database` sont appelés au mieux
    s'ils sont présents ; leur absence n'échoue pas l'installation.

L'association est retirée par la désinstallation, **et seulement si elle
appartient encore à LIVEX** : sous Windows, la valeur de `.livexp` n'est
supprimée que si elle pointe toujours vers `LIVEX.Package` ; sous Linux, seuls
les deux fichiers créés sont supprimés. Une association préexistante ou
reconfigurée par l'utilisateur n'est jamais écrasée ni retirée.

## Conséquences

### Positives

- Double-clic fonctionnel sur un paquet `.livexp`, avec l'icône attendue.
- Aucune élévation : tout est écrit en `HKCU` / XDG utilisateur (ADR-002).
- Désinstallation propre et réversible, sans toucher aux associations
  étrangères.
- Le retrait est testable sans machine réelle : `SimulatedIntegration`
  enregistre les appels, et les tests vérifient la présence de
  `RemovePackageAssociationAction` dans les plans de désinstallation.

### Négatives

- **Écart assumé à la spécification §8.3.** Il est documenté ici, et la
  spécification devra être amendée pour distinguer « association par défaut
  interdite » de « association du format propre au produit ».
- Une association utilisateur ne suit pas l'utilisateur d'un compte à l'autre
  de la machine (cohérent avec ADR-002).
- Sous Linux, l'association n'est visible qu'après rafraîchissement de la base
  MIME ; certains environnements de bureau peuvent exiger une reconnexion de
  session.

### Risques

- **Écrasement d'une association existante** : mitigation ci-dessus (retrait
  conditionnel, écriture limitée à `HKCU`/XDG).
- **Exécutable déplacé** : l'association pointe vers un chemin absolu ; une
  installation déplacée à la main casse l'association. Le lanceur reste
  utilisable, et une réparation réécrit l'association.

## Alternatives considérées

- **Aucune association (statu quo)** : conforme à la spec, rejeté — le format
  produit devient inutilisable par double-clic.
- **Association machine (`HKLM`)** : visible pour tous les comptes, rejetée —
  impose l'élévation, contraire à ADR-002.
- **Ouverture par le Launcher enregistré comme gestionnaire de protocole** :
  hors sujet, `.livexp` est un fichier, pas un schéma d'URL.

## Validation / rejet

- Windows : après installation, un `.livexp` affiche l'icône LIVEX et s'ouvre
  avec le lanceur ; après désinstallation, l'association a disparu et une
  association préexistante vers un autre programme est intacte.
- Linux : `xdg-mime query filetype paquet.livexp` retourne
  `application/x-livex-package` et `xdg-open` lance le lanceur ; après
  désinstallation, les deux fichiers ont disparu.
- Mode démonstration : le plan contient `AssociatePackageAction`, l'exécution
  simulée n'écrit rien.

Réouverture si le format `.livexp` change de nom ou si une distribution Linux
native (`.deb`, Flatpak) prend le relais de l'association.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 29 octobre 2026 | Création | Décision de l'architecte d'associer `.livexp` (écart documenté à la spec §8.3) |
