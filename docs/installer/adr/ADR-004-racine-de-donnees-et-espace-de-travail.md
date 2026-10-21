# ADR-004 : Racine de données, espace de travail et portées de désinstallation

**Composant** : LIVEX (installateur, Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-002
**Source Monographie** : —

---

## Contexte

La spécification de base demande deux emplacements distincts — le logiciel et
« l'espace de travail » — et la suppression de `livex.json`. Elle laisse ouverts
le mécanisme de persistance, la définition exacte de l'espace de travail et la
sémantique des options de désinstallation.

Le code existant impose une partie du modèle :

- `launcher/Launcher.Infrastructure/WorkspaceLayout.cs` définit une **racine de
  données** : `LIVEX_DATA` si définie, sinon `~/.livex-data` ;
- l'espace de travail y est **dérivé** : `<racine>/workspace` ; aucun choix
  utilisateur n'est persisté aujourd'hui ;
- les paquets (`.livexp`), les archives scellées, les journaux de session, la
  base d'analyse ECHOS et le cache vivent déjà dans cette racine
  (`packages/`, `archives/`, `sessions/`, `echos/`, `cache/`) ;
- `docs/launcher/PACKAGING.md` confirme que le paquet est un **résultat de
  recherche** qui ne doit jamais être supprimé sans action explicite.

Deux contradictions apparaissent si l'on confond « espace de travail » et
« racine de données » :

1. Supprimer l'espace de travail supprimerait alors les paquets et les
   archives, ce que la spécification interdit ;
2. Les journaux de session, nécessaires au diagnostic, disparaîtraient avec
   l'option de désinstallation la plus complète.

Il manque donc une décision de modèle de données, pas une reformulation.

## Décision

### 1. Une racine de données unique

La racine de données est `LIVEX_DATA` si la variable est définie et non vide,
sinon `~/.livex-data` (Windows : `%USERPROFILE%\.livex-data`). Elle est créée
par le premier lancement ou par l'installateur.

```text
<LIVEX_DATA>/
├── settings.json          # préférences du Launcher (dont l'espace de travail)
├── workspace/             # espace de travail par défaut
├── packages/              # paquets .livexp vivants
├── archives/              # paquets scellés conservés par l'utilisateur
├── sessions/              # journaux de session
├── logs/                  # journaux de l'installateur et du diagnostic
├── cache/                 # données régénérables
└── echos/                 # base d'analyse ECHOS (analytics.sqlite)
```

### 2. L'espace de travail est un répertoire d'expériences, pas la racine

L'espace de travail contient les expériences, les campagnes en cours et les
rapports que l'utilisateur organise. Il est **choisi** à l'installation, avec
`<LIVEX_DATA>/workspace` comme valeur par défaut. Il peut se trouver n'importe
où ailleurs (`D:\Recherche\LIVEX`, `~/Documents/LIVEX`, …).

Contraintes vérifiées par l'installateur et par le Launcher :

- l'espace de travail ne peut pas être la racine de données elle-même, ni un
  ancêtre de celle-ci (sinon une suppression de l'espace de travail emporterait
  les paquets et les archives) ;
- il ne peut pas contenir le répertoire logiciel, ni se trouver à l'intérieur
  de celui-ci ;
- il doit être inscriptible ; s'il n'existe pas, il est créé avec l'accord de
  l'utilisateur.

### 3. Les préférences vivent dans `settings.json`

```json
{
  "schema": 1,
  "workspace": "D:/Recherche/LIVEX",
  "locale": "fr",
  "updatedAt": "2026-10-21T09:00:00Z"
}
```

- Fichier UTF-8, sans BOM, écrit **atomiquement** (fichier temporaire, puis
  renommage).
- L'installateur **fusionne** : il ne remplace que les clés qu'il gère et
  préserve les clés inconnues écrites par le Launcher ou une version
  ultérieure.
- Un fichier illisible ou invalide n'est jamais écrasé silencieusement : il est
  conservé sous `settings.json.corrupt-<horodatage>` et l'utilisateur est
  informé.
- Ce fichier **n'est pas** un succédané de `livex.json` : il ne contient ni
  chemin d'installation, ni registre de composants, ni état d'installation. Il
  ne sert qu'aux préférences utilisateur.

**Précédence de l'espace de travail** :

1. argument `--workspace <chemin>` du Launcher (usage ponctuel) ;
2. `settings.json.workspace` ;
3. `<LIVEX_DATA>/workspace`.

`LIVEX_DATA` déplace la racine de données ; elle ne remplace pas le choix de
l'espace de travail. Poser `LIVEX_DATA` ne doit donc pas rendre le réglage
utilisateur inopérant.

Le Launcher doit être adapté pour lire cette précédence : c'est une évolution
identifiée de son côté, pas une conséquence automatique de l'installateur.

### 4. Trois portées de désinstallation, plus une extension explicite

| Portée | Contenu supprimé | Contenu conservé |
| :-- | :-- | :-- |
| **A — Logiciel** | répertoire logiciel, raccourcis, icône, enregistrement, entrée « Applications et fonctionnalités » | `settings.json`, espace de travail, paquets, archives, sessions, journaux, cache, base ECHOS |
| **B — Logiciel et configuration** | A + `settings.json` + `cache/` | espace de travail, paquets, archives, sessions, journaux, base ECHOS |
| **C — Logiciel, configuration et espace de travail** | B + le répertoire d'espace de travail, après affichage du chemin exact et confirmation | paquets, archives, sessions, journaux, base ECHOS |

L'option C comporte une **extension facultative**, décochée par défaut :
« supprimer aussi les résultats de recherche » (paquets `.livexp`, archives,
journaux de session, base d'analyse ECHOS). Si elle est cochée, l'assistant
affiche le nombre de paquets, le nombre d'archives, la taille totale et exige
une seconde confirmation distincte. Les journaux de l'installateur
(`logs/installer-*.log`) sont conservés dans tous les cas, pour le diagnostic.

Aucune suppression n'est engagée sans que le chemin exact et le contenu aient
été affichés. Un espace de travail introuvable ou inaccessible n'est **jamais**
supprimé par approximation : l'assistant le signale et propose de le localiser.

## Conséquences

### Positives

- Le modèle est cohérent avec le code existant : une seule racine de données,
  pas de troisième emplacement pour les préférences.
- La désinstallation ne peut plus détruire un résultat de recherche par
  accident, tout en offrant une suppression réellement complète si l'utilisateur
  la demande explicitement.
- La précédence des chemins est unique, testable, et `LIVEX_DATA` ne neutralise
  plus le choix utilisateur.
- Le fichier de préférences est portable, lisible et sans API système
  spécifique, ce qui simplifie les tests Windows/Linux.

### Négatives

- Le Launcher doit être modifié pour lire `settings.json` et honorer
  `--workspace` ; tant que ce n'est pas fait, le choix de l'installateur n'a pas
  d'effet.
- Un utilisateur qui déplace sa racine de données perd le pointeur vers son
  espace de travail s'il était stocké dedans ; l'assistant traite ce cas comme
  un espace introuvable, sans supprimer.
- L'option C reste longue à expliquer : trois portées, une extension, deux
  confirmations. C'est le prix d'une suppression sans ambiguïté.

### Risques

- **Divergence entre installateur et Launcher** sur la lecture de
  `settings.json`. Mitigation : schéma versionné, test de contrat partagé,
  comportement de repli explicite si le fichier est absent.
- **Fusion destructive** d'un fichier de préférences écrit par une version
  ultérieure. Mitigation : préservation des clés inconnues et test dédié.

## Alternatives considérées

- **Espace de travail = racine de données** : plus simple à expliquer ; rejeté
  car il rend l'option C destructrice de paquets et contredit la spécification.
- **Préférences dans `%APPDATA%` / XDG config** : plus « natif » ; rejeté car
  il ajoute un troisième emplacement, sépare le pointeur des données, et
  complique la portabilité. Le caractère « adapté au système » est porté par
  l'emplacement par défaut de la racine de données, pas par le format.
- **Registre Windows pour l'espace de travail** : natif mais non portable et
  non testable en CI Linux ; rejeté.
- **Variable d'environnement `LIVEX_WORKSPACE`** : utile pour l'automatisation,
  mais insuffisante comme persistance pour un utilisateur non technique ;
  rejetée comme mécanisme principal (l'argument `--workspace` couvre l'usage
  ponctuel).

## Validation / rejet

- Installation avec l'espace de travail par défaut, puis avec un répertoire
  externe ; vérification que `settings.json` contient le chemin et que le
  Launcher le relit au lancement suivant.
- Refus d'un espace de travail égal à la racine de données ou ancêtre de
  celle-ci, avec message explicite.
- Désinstallation A : `settings.json`, espace de travail et paquets intacts.
- Désinstallation B : `settings.json` supprimé, espace de travail et paquets
  intacts.
- Désinstallation C : espace de travail supprimé après confirmation ; paquets
  et archives intacts.
- Désinstallation C avec extension : paquets, archives et sessions supprimés
  après seconde confirmation ; journaux de l'installateur conservés.
- Espace de travail déplacé : signalé, jamais supprimé.

Réouverture si le Launcher change de modèle de persistance ou si un besoin de
profils multiples apparaît.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Arbitrage du modèle de données et des portées de désinstallation |
