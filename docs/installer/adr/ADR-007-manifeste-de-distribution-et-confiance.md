# ADR-007 : Manifeste de distribution, intégrité et chaîne de confiance

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-001, ADR-006
**Source Monographie** : —

---

## Contexte

Le contrat de composant (`launcher/contracts/component-manifest-v1.schema.json`)
décrit ce qu'un composant expose au Launcher : identifiant, version, exécutable,
points d'accès, capacités, sonde de santé, délais. Il ne contient **ni**
empreinte, **ni** taille, **ni** licence, **ni** texte d'interface, **ni**
plateformes déclarées. Il ne peut donc pas servir de manifeste d'installation.

L'installateur a besoin d'une source unique et vérifiable qui réponde à :

- quels fichiers livrer, pour quelle plateforme, avec quelle empreinte ;
- quels composants sont obligatoires ou facultatifs, avec quels textes FR/EN ;
- quelles dépendances un composant déclare ;
- quelles licences et mentions accompagner ;
- quel contenu exact a été signé.

La spécification exige par ailleurs de vérifier l'intégrité **avant** toute
modification et d'interrompre l'installation en cas d'altération. Aucun
mécanisme de signature n'existe aujourd'hui : la CI de release ne produit aucun
artefact, et `launcher/PACKAGING.md` note explicitement que la signature reste
à définir.

Depuis l'ADR-008, le manifeste décrit aussi des **actifs distants** (modules
facultatifs téléchargés depuis la release). Une signature qui ne couvrirait que
les fichiers embarqués laisserait ces archives hors de la chaîne de confiance :
le manifeste doit donc engager leur taille et leur SHA-256, vérifiés après
téléchargement.

## Décision

### 1. Un manifeste de distribution, distinct du manifeste de composant

Le paquet de distribution contient `distribution.json`, seul document qui
décrit la charge utile :

```json
{
  "schema": 1,
  "product": "LIVEX",
  "version": "0.2.0",
  "channel": "stable",
  "platform": "win-x64",
  "releasedAt": "2026-10-21T00:00:00Z",
  "components": [
    {
      "id": "launcher",
      "version": "0.1.0",
      "required": true,
      "displayName": { "fr": "Lanceur LIVEX", "en": "LIVEX Launcher" },
      "description": { "fr": "…", "en": "…" },
      "license": "GPL-3.0-or-later",
      "requires": [],
      "files": ["launcher/livex-launcher.exe", "launcher/…"]
    }
  ],
  "files": [
    { "path": "launcher/livex-launcher.exe", "size": 0, "sha256": "…" }
  ]
}
```

Règles :

- **Une seule source** pour l'interface et pour la copie : un composant affiché
  existe dans `components` et tous ses fichiers existent dans `files`.
- Les textes d'interface appartiennent au manifeste, pas aux composants : le
  Launcher n'a pas à connaître l'installateur, et inversement.
- `component.json` reste la propriété du composant installé ; l'installateur le
  copie tel quel et ne le réécrit pas.
- Un module facultatif acquis en ligne porte un bloc `acquisition` (`url`,
  `size`, `sha256`, `format`) ; un module embarqué porte `kind: "payload"`. Le
  schéma interdit un actif distant sans taille ni empreinte (ADR-008).
- Le manifeste est **canonique** : UTF-8 sans BOM, clés triées, aucune
  indentation, horodatages UTC. C'est la forme qui est signée.

### 2. Vérification avant écriture

Ordre strict, avant toute modification du disque :

1. vérifier la signature du manifeste (clé publique embarquée dans
   `livex-setup`) ;
2. vérifier que chaque fichier déclaré existe dans la charge utile, ou que
   chaque actif distant déclaré porte une URL, une taille et une empreinte ;
3. vérifier taille et SHA-256 de chaque fichier embarqué ;
4. vérifier l'espace disque et les droits d'écriture ;
5. seulement alors, construire le plan d'installation et commencer.

Pour un actif distant, la vérification de taille et d'empreinte a lieu **après
réception complète et avant extraction** ; un actif non conforme est supprimé
du cache et l'installation du module échoue, sans toucher au socle.

Un échec à l'une de ces étapes interrompt l'opération avec un message explicite
et **aucune** écriture dans le répertoire cible. Les archives sont lues en
flux, sans extraction préalable dans un répertoire temporaire non maîtrisé.

### 3. Chaîne de confiance par plateforme

| Plateforme | Mécanisme | Portée |
| :-- | :-- | :-- |
| Toutes | signature **Ed25519** de `distribution.json`, clé publique embarquée | intégrité et authenticité de la charge utile, vérifiables hors ligne par l'installateur |
| Toutes | `SHA256SUMS` + signature détachée | vérification manuelle par l'utilisateur avant exécution |
| Windows | **Authenticode** SHA-256 sur `livex-setup.exe`, `livex-launcher.exe`, `Simulation.Console.exe` | confiance du système, réduction des avertissements SmartScreen |
| Linux | signature détachée (GPG) du paquet et des sommes | vérification par l'utilisateur et par les distributions |

La clé Ed25519 de release est distincte de la clé de signature de paquet
`.livexp` (qui n'existe pas encore et relève d'une autre décision). Sa rotation
est documentée : une nouvelle clé est publiée dans `SECURITY.md` et
`docs/installer/RELEASE_AND_UPDATE.md` avant la première release qui l'utilise.

La signature du manifeste couvre les empreintes des actifs distants : un actif
remplacé sur la release est refusé même si l'URL et le nom sont identiques.

### 4. Signature Windows : condition de publication, pas de prototype

Une release **publique** Windows ne peut pas être diffusée sans signature
Authenticode. Le certificat (OV ou Azure Trusted Signing), son coût et son
automatisation sont instruits dans la roadmap ; tant qu'ils ne sont pas
disponibles, les builds internes sont marqués `dev` dans le manifeste et
l'installateur affiche un avertissement non bloquant.

### 5. Ce que l'installateur ne prétend pas faire

- Il ne vérifie pas sa propre authenticité : sur Linux, c'est à l'utilisateur
  de vérifier la signature détachée avant exécution ; sur Windows, c'est
  Authenticode et SmartScreen.
- Il ne remplace pas un antivirus ni un contrôle de vulnérabilités.
- Il ne signe pas les composants qu'il installe : chaque artefact est signé au
  build, pas à l'installation.

## Conséquences

### Positives

- Une seule source de vérité pour l'interface, la copie et l'intégrité.
- Vérification complète hors ligne, sans dépendre d'un service.
- Le manifeste peut évoluer (nouveaux champs optionnels) sans casser les
  installateurs anciens, grâce au champ `schema`.
- La séparation manifeste de distribution / manifeste de composant évite de
  dénaturer le contrat du Launcher.

### Négatives

- Duplication partielle : version et identifiant d'un composant figurent dans
  les deux manifestes. Elle est assumée et vérifiée (test de cohérence).
- La génération du manifeste et la signature ajoutent une étape de build
  obligatoire, donc un point de défaillance à surveiller.
- La gestion de clés (création, stockage, rotation, révocation) crée une
  responsabilité opérationnelle nouvelle pour le projet.

### Risques

- **Clé compromise** : une clé Ed25519 embarquée ne peut pas être révoquée
  dans les binaires déjà distribués. Mitigation : clé de release à rotation
  courte, publication des empreintes, et possibilité de révoquer une version
  dans la documentation.
- **Signature Windows indisponible** : la release publique est bloquée.
  Mitigation : instruire le certificat tôt (roadmap P5), ne pas promettre de
  date.
- **Manifeste désynchronisé du payload** : le build doit échouer, pas
  l'installation. Mitigation : génération et vérification dans la même étape de
  CI.
- **Actif distant remplacé après publication** : l'empreinte signée le refuse ;
  la règle de rétention de l'ADR-008 interdit de toute façon la suppression ou
  le remplacement d'un actif référencé.

## Alternatives considérées

- **Réutiliser `component.json` comme manifeste d'installation** : impossible
  sans le dénaturer (il ne porte ni licence, ni taille, ni empreinte, ni textes).
- **Signer chaque fichier individuellement** : coûteux et lent ; une signature
  du manifeste couvre l'ensemble par transitivité.
- **Pas de signature, seulement des sommes de contrôle** : détecte la
  corruption, pas l'altération malveillante ; insuffisant pour une distribution
  publique.
- **Signature uniquement Authenticode** : ne couvre pas Linux et ne permet pas
  à l'installateur de vérifier la charge utile fichier par fichier.

## Validation / rejet

- Un fichier modifié après signature fait échouer l'installation avant toute
  écriture, avec le nom du fichier fautif.
- Un manifeste dont la signature ne correspond pas est refusé, même si toutes
  les empreintes de fichiers sont valides.
- Un manifeste valide mais dont un fichier déclaré manque est refusé.
- La vérification fonctionne sur une machine sans réseau pour le socle.
- Un actif facultatif dont l'empreinte ne correspond pas est refusé avant
  extraction, et aucun fichier du socle n'est modifié.

Réouverture si le projet adopte une infrastructure de signature centralisée
(Sigstore, Azure Trusted Signing) qui rend la clé embarquée inutile.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Absence de manifeste d'installation et de chaîne de confiance |
