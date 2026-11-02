# Feuille de route de l'installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [V1.0.0 LIVRÉE — feuille de route des évolutions]
**Dernière mise à jour** : 31 octobre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md)

---

## 1. Principe

La feuille de route est exprimée **en ordre, sans dates**, conformément à la
roadmap générale du projet (`docs/governance/ROADMAP.md` §1). Une phase ne
commence que lorsque les critères de sortie de la précédente sont satisfaits.
Les issues GitHub sont le suivi opérationnel ; ce document fixe la séquence et
les portes.

Chaque phase a une **sortie vérifiable** : un artefact, une mesure ou une
recette. Une phase n'est pas « terminée » parce que le code est écrit.

## 2. Vue d'ensemble

| Phase | Intitulé | Sortie principale | Bloque | État au 31 octobre 2026 |
| :-- | :-- | :-- | :-- | :-- |
| **P0** | Cadrage | spécification, architecture, ADR, roadmap, issues | P1 | **fait** (ADR-001 à 010) |
| **P1** | Socle sans interface | `Setup.Core` testé, plan et transaction | P2 | **fait** en code ; couverture non mesurée |
| **P2** | Charge utile, actifs et confiance | manifeste, paquets et vérification d'intégrité | P3 | **livré dans LIVEX 1.0.0** ; artefacts et sommes sont attachés à la release |
| **P3** | Assistant, acquisition et intégration | installation guidée Windows/Linux, téléchargement vérifié | P4 | **livré** ; installer et acquisition de modules disponibles dans les variantes distribuées |
| **P4** | Cycle de vie | mise à jour, réparation, maintenance, désinstallation | P5 | **implémenté et livré** ; les scénarios et preuves d'exécution doivent être consultés dans les rapports de recette s'ils sont publiés |
| **P5** | Distribution publique | release publiée et vérifiée sur machines propres | P6 | **release publique `livex-v1.0.0` publiée** ; les recettes manuelles machines propres restent distinctes du simple packaging |
| **P6** | Ouverture | PRISM dans le Launcher, mises à jour différentielles, évolutions | — | **travaux d'évolution** ; hors de la livraison V1 actuelle |

La V1 est publiée sous le tag [`livex-v1.0.0`](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0). [`RELEASE_AND_UPDATE.md`](RELEASE_AND_UPDATE.md) décrit la chaîne utilisée et les opérations à reprendre pour les versions suivantes.

## 3. Phase 0 — Cadrage

**Objectif** : remplacer la documentation retirée par une base unique, cohérente
avec le dépôt, et découper le travail en issues vérifiables.

Livrables :

- `README.md`, `COMPONENTS.md`, `SPECIFICATION.md`, `ARCHITECTURE.md`, `ROADMAP.md` ;
- ADR-001 à ADR-008 ;
- `TESTING.md`, `RELEASE_AND_UPDATE.md`, `GUIDE-UTILISATEUR.md` ;
- issues GitHub avec labels `component/installer` et `component/launcher`.

Critères de sortie :

- aucune contradiction entre spécification, architecture et ADR ;
- chaque exigence `INS-F-*` est traçable vers un test ou une issue ;
- les écarts avec la spécification de base sont justifiés et listés
  (`SPECIFICATION.md` §7) ;
- les labels manquants sont créés et les issues sont créées sans doublon.

## 4. Phase 1 — Socle sans interface

**Objectif** : disposer d'un moteur d'installation testable, sans interface,
avant de dessiner un écran.

Livrables :

| Élément | Contenu | Vérification |
| :-- | :-- | :-- |
| `Setup.Core.Distribution` | modèle, JSON canonique, empreintes | tests unitaires, vecteurs figés |
| `Setup.Core.Planning` | plan d'installation, de mise à jour, de désinstallation | tests de plan sur arborescences factices |
| `Setup.Core.Preflight` | huit contrôles, sortie stable | tests par contrôle, cas d'échec |
| `Setup.Core.Execution` | transaction, sauvegarde, restauration | tests d'échec injecté, résidus |
| `Setup.Core.Lifecycle` | enregistrement, inventaire, détection | tests avec magasins en mémoire |
| `Setup.Core.Journal` | format, rotation, UTF-8 | tests de contenu interdit |
| `Setup.Core.Acquisition` | cache, reprise, vérification, extraction | tests avec serveur local et archives piégées |
| `Setup.Core.CommandLine` | arguments, codes de sortie | tests de table |

Critères de sortie :

- `Setup.Core` ne référence aucune interface et aucun assemblage du Launcher ;
- un plan d'installation complet est produit et rejoué sans écrire ;
- une panne injectée au milieu d'une transaction est annulée et signalée ;
- couverture des modules de planification et de transaction ≥ 80 %, seuil du
  dépôt pour ECHOS, à confirmer pour ce projet ;
- `dotnet test` vert sous Windows et Linux.

## 5. Phase 2 — Charge utile et confiance

**Objectif** : produire une distribution réelle, autonome et vérifiable, pour
les deux plateformes.

Livrables :

- publications self-contained : `livex-launcher`, `syne` ;
- ECHOS avec CPython 3.12 et dépendances figées ;
- **charge utile de base** : build **PRISM packagée** par RID (`payload/`) ;
- **actifs facultatifs** : archive ZIP du LDK, archive de documentation,
  archive SYNE-Mock avec runtime Node ;
- `installer/build/build_distribution.py` et `distribution.json` canonique,
  profils `core` et `full`, blocs `acquisition` ;
- entrées externes épinglées (`external-inputs.json`) et vérification par le
  même code que l'installateur ;
- `SHA256SUMS` et attestations de provenance ;
- job CI `installer` (matrice Windows/Linux) et vérification des URL d'actifs.

Critères de sortie :

- la distribution s'assemble sans réseau et sans fichier manquant ;
- la vérification refuse un fichier modifié et un actif absent des empreintes
  épinglées ;
- chaque actif facultatif publié a une taille et une empreinte dans le
  manifeste, et une URL qui répond ;
- le profil `full` s'assemble sans aucun actif manquant ;
- `livex-launcher --check` s'exécute depuis la charge utile assemblée ;
- la taille mesurée du paquet par plateforme est publiée dans les notes de
  release (pas d'estimation dans la documentation) ;
- aucun binaire framework-dependent ne subsiste dans la charge utile.

## 6. Phase 3 — Assistant et intégration système

**Objectif** : rendre l'installation utilisable par un public non technique,
sur les deux systèmes, sans élévation.

Livrables :

| Élément | Contenu |
| :-- | :-- |
| Assistant | huit écrans, navigation, validation, récapitulatif, progression |
| i18n | ressources FR/EN, repli anglais, clés manquantes journalisées |
| Windows | `HKCU`, entrée « Applications et fonctionnalités », raccourcis |
| Linux | `installation.json` XDG, `.desktop`, icône, bureau optionnel |
| Détection | installation existante, enregistrement obsolète, adoption |
| Préférences | `settings.json`, fusion, écriture atomique |
| Acquisition | téléchargement HTTPS reprenable, cache, vérification, extraction confinée |
| Hors ligne | archive locale, `--offline-assets`, `--no-network`, profil `full` |
| Vérification finale | `livex-launcher --check` informative, journalisée |

Critères de sortie :

- installation complète sans UAC ni `sudo` sur les deux plateformes ;
- aucune écriture hors des emplacements documentés ;
- l'installation est détectée au lancement suivant ;
- l'assistant est utilisable entièrement au clavier ;
- les deux langues sont complètes ;
- `--unattended` produit une installation identique à l'assistant ;
- une installation `core` sans module facultatif n'émet aucune requête réseau ;
- un module facultatif se télécharge, se vérifie et s'installe sans toucher au
  socle ; une coupure réseau est reprise sans installation partielle.

## 7. Phase 4 — Cycle de vie

**Objectif** : traiter l'installation comme un objet durable, pas comme une
copie unique.

Livrables :

| Élément | Contenu |
| :-- | :-- |
| Mise à jour | en place, conservation des données, refus de downgrade |
| Réparation | vérification de l'inventaire, restauration depuis la distribution |
| Maintenance | ajout/retrait d'un module facultatif (cache, manifeste conservé ou archive locale), modification de l'espace de travail |
| Désinstallation | portées A/B/C, extension « résultats de recherche », résidus |
| Processus | détection des processus LIVEX, refus explicite |
| Récupération | enregistrement orphelin, inventaire incomplet, `settings.json` invalide |

Critères de sortie :

- tous les cas de désinstallation de `SPECIFICATION.md` §6.4 sont vérifiés ;
- une mise à jour interrompue laisse une installation utilisable ou un état
  signalé, jamais un succès ;
- aucun paquet `.livexp` ni archive scellée n'est supprimé par A, B ou C sans
  l'extension explicite ;
- un fichier étranger dans le répertoire logiciel est conservé et signalé ;
- le Launcher lit l'espace de travail configuré (issue Launcher close).

## 8. Phase 5 — Distribution publique

**Objectif** : publier une version que l'on peut recommander sans réserve.

Livrables :

- certificat Authenticode et signature de `livex-setup.exe`, `livex-launcher.exe`
  et `Simulation.Console.exe` (si un certificat est disponible) ;
- procédure de vérification publiée dans `SECURITY.md` et
  `RELEASE_AND_UPDATE.md` (sommes et attestations) ;
- `release.yml` : construction, sommes, attestations, artefacts attachés à la
  release GitHub sur tag `livex-v*` ;
- tests sur machines propres Windows et Linux (VM ou runner neuf) ;
- guide utilisateur et procédure de dépannage ;
- notes de release avec taille mesurée, versions des composants et limites.

Critères de sortie :

- une installation publique ne déclenche pas d'avertissement SmartScreen sur
  Windows 11 à jour ;
- la vérification Linux documentée fonctionne ;
- les scénarios de `TESTING.md` §7 passent sur machine propre ;
- aucune régression des tests unitaires, d'intégration et de bout en bout ;
- la documentation utilisateur ne contient aucune taille ni durée non mesurée.

## 9. Phase 6 — Ouverture

**Objectif** : étendre le périmètre sans refondre le socle.

Candidats, par ordre de valeur :

1. **PRISM dans le Launcher** : bouton d'ouverture ou détection, si PRISM expose
   un contrat de service. Point ouvert INS-O-10.
2. **Mise à jour différentielle** : réduire le poids des mises à jour et des
   actifs facultatifs (delta d'archive).
3. **Miroir d'actifs** : second hôte de téléchargement pour les réseaux qui
   filtrent GitHub. Point ouvert INS-O-08.
4. **Installation machine** : si un besoin organisationnel est démontré.
5. **Format Linux natif** : `.deb` ou Flatpak, si la distribution par paquet
   devient la cible.

Ces évolutions ne bloquent pas la release LIVEX 1.0.0 publiée ; elles restent des pistes postérieures, à prioriser selon les besoins constatés.

## 10. Risques de planification

| Risque | Impact | Mitigation |
| :-- | :-- | :-- |
| ECHOS embarqué plus complexe que prévu | bloque P2 | prototype CPython + pyarrow dès P1, avant l'interface |
| Certificat Windows indisponible | bloque P5 | instruire le coût dès P2, accepter un canal `dev` explicite en attendant |
| Dérive de l'assistant | retarde P3/P4 | `Setup.Core` d'abord, interface sans logique |
| Divergence avec le Launcher sur l'espace de travail | installation incohérente | issue Launcher prioritaire, test de contrat partagé |
| Taille du paquet | rejet utilisateur | mesure par release, documentation honnête, modules facultatifs à la demande |
| Build PRISM indisponible pour une plateforme | **release bloquée** : PRISM fait partie de la charge utile du socle | build obligatoire en P2 ; aucun contournement par téléchargement (INS-L-01) |
| GitHub inaccessible (proxy, panne) | module facultatif non installable | archive locale, profil `full`, message explicite |

## 11. Suivi

Les issues GitHub sont créées avec les labels :

- `component/installer` pour l'installateur ;
- `component/launcher` pour la lecture de l'espace de travail et la tolérance à
  l'absence de `docs/` ;
- `type/feature`, `type/docs`, `type/test`, `priority/P*` selon le cas.

Une issue fermée sans preuve de recette est rouverte : la porte de phase est un
critère, pas une déclaration.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle base documentaire |
| 29 octobre 2026 | Colonne « État au 29 octobre 2026 » | Check-up documentaire : les phases ne sont pas « terminées parce que le code est écrit » |
