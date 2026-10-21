# LIVEX — Spécification fonctionnelle et technique de l’installateur

**Version du document :** 0.2  
**Projet cible :** LIVEX  
**Statut :** Spécification révisée — principes fonctionnels validés, détails techniques à confirmer  
**Technologie envisagée :** Qt Installer Framework  
**Systèmes cibles :** Windows et Linux  
**Type de distribution :** Installateur autonome, utilisable hors ligne

---

## 1. Objectif et périmètre

### 1.1. Objectif

L’installateur LIVEX a pour objectif de permettre à un utilisateur d’installer, de configurer, de maintenir et de désinstaller LIVEX sans devoir manipuler manuellement les fichiers de ses différents composants.

Il doit fournir une procédure guidée, compréhensible par un utilisateur non technique, tout en assurant la cohérence de l’installation et la préservation des données de l’utilisateur.

L’installateur doit notamment permettre de :

- Installer le lanceur LIVEX et les composants obligatoires.
- Choisir les composants facultatifs à installer.
- Définir séparément le répertoire du logiciel et celui de l’espace de travail.
- Installer les dépendances nécessaires à l’exécution, dans les limites de leurs conditions de redistribution.
- Créer les raccourcis nécessaires.
- Détecter les installations existantes, y compris dans un emplacement personnalisé.
- Désinstaller le logiciel en permettant à l’utilisateur de choisir les données à conserver.
- Signaler les erreurs et produire des journaux exploitables.
- Fonctionner sans connexion Internet lorsque les fichiers nécessaires sont intégrés au paquet.

### 1.2. Principes directeurs

L’installateur doit respecter les principes suivants :

1. **Simplicité :** l’installation doit être guidée et compréhensible.
2. **Autonomie :** aucune connexion Internet ne doit être nécessaire pour installer les composants et dépendances embarqués.
3. **Installation non privilégiée :** l’installation doit fonctionner avec les droits de l’utilisateur courant lorsque les contraintes du système le permettent.
4. **Modularité :** les composants facultatifs peuvent être sélectionnés indépendamment.
5. **Séparation des données :** les fichiers du logiciel et les expériences de l’utilisateur sont gérés séparément.
6. **Intégrité :** les fichiers sont vérifiés avant leur installation.
7. **Réversibilité raisonnable :** après un échec, l’installateur tente de nettoyer les éléments créés sans promettre une restauration absolue.
8. **Transparence :** les erreurs, limitations et éventuels résidus doivent être signalés clairement.
9. **Protection des données :** aucune suppression des expériences ne doit être effectuée sans choix explicite de l’utilisateur.
10. **Absence de configuration superflue :** aucun fichier `livex.json` ni fichier de remplacement reproduisant inutilement son rôle ne doit être introduit.

### 1.3. Hors périmètre initial

Les fonctionnalités suivantes ne sont pas prévues dans cette version :

- Mise à jour automatique des composants.
- Système de mise à jour personnalisé.
- Téléchargement automatique de composants ou dépendances pendant l’installation.
- Déplacement automatique d’un espace de travail existant.
- Profils d’installation prédéfinis.
- Installation ou configuration globale du système nécessitant systématiquement des privilèges administrateur.
- Mise à jour automatique des expériences ou des données utilisateur.

Ces fonctionnalités pourront être étudiées ultérieurement si les besoins du projet le justifient.

---

## 2. Technologie et architecture générale

### 2.1. Technologie envisagée

Qt Installer Framework est retenu comme technologie envisagée pour réaliser l’installateur.

Il devra permettre de construire un assistant d’installation classique, comprenant les étapes de sélection, de configuration, de progression et de résultat.

Les capacités exactes du framework devront être vérifiées avant l’implémentation, notamment pour :

- La sélection des composants facultatifs.
- L’installation sans privilèges administrateur.
- La configuration de deux chemins indépendants.
- La détection d’une installation existante dans un répertoire personnalisé.
- La maintenance et la désinstallation sélective des données.
- La gestion des raccourcis selon le système d’exploitation.
- La gestion des erreurs et le nettoyage après échec.

Si une capacité ne peut pas être obtenue directement avec Qt Installer Framework, une solution complémentaire devra être définie avant son implémentation.

### 2.2. Compatibilité système

L’installateur doit être conçu pour Windows et Linux.

Les différences entre les deux systèmes doivent être prises en charge par des mécanismes adaptés, sans imposer à l’utilisateur de manipulations manuelles inutiles.

L’installation standard doit privilégier les répertoires accessibles à l’utilisateur courant. Les opérations nécessitant des privilèges supplémentaires devront être identifiées explicitement et ne devront pas bloquer les fonctionnalités indépendantes qui restent accessibles.

### 2.3. Installation hors ligne

Chaque distribution doit embarquer les fichiers nécessaires à l’installation des composants qu’elle propose, ainsi que les dépendances redistribuables requises.

L’installateur ne doit pas télécharger automatiquement des dépendances depuis Internet.

Toute dépendance externe qui ne peut pas être redistribuée ou dont les conditions de licence restent incertaines doit être vérifiée avant d’être intégrée à la distribution finale.

---

## 3. Gestion des composants

### 3.1. Composants obligatoires

L’installation standard comprend :

- **Launcher :** point d’entrée principal de LIVEX.
- **SYNE :** moteur du projet.
- **ECHOS :** composant d’analyse.
- Les contrats de communication nécessaires entre les composants.
- Les ressources indispensables au fonctionnement de l’installation.

Les fichiers indispensables au fonctionnement du lanceur et à la cohérence de l’installation ne doivent pas pouvoir être désélectionnés individuellement.

### 3.2. Composants facultatifs

Les composants suivants sont facultatifs et décochés par défaut.

| Composant | Description | Sélection par défaut |
|---|---|---|
| PRISM | Composant de visualisation et d’intégration à un moteur 3D. | Désactivé |
| PrismLDK | Plugin de développement destiné à Unreal Engine, distribué sous forme d’archive ZIP. | Désactivé |
| Documentation hors ligne | Documentation consultable sans connexion Internet. | Désactivé |
| SYNE-Mock | Émulateur destiné au développement et aux tests. | Désactivé |

Les dépendances propres à un composant facultatif ne doivent être installées que si elles sont nécessaires au fonctionnement de ce composant et peuvent être redistribuées.

### 3.3. Cas particulier de PRISM

PRISM est un composant facultatif. Il ne doit pas être rendu obligatoire par la présence d’autres composants.

Lorsque PRISM n’est pas sélectionné, l’installateur doit afficher une description simple et visible précisant que les fonctionnalités correspondantes ne seront pas disponibles.

Cette information doit être présentée avant la validation de l’installation, sans empêcher l’utilisateur de poursuivre.

La description doit informer l’utilisateur de la limitation fonctionnelle sans présenter PRISM comme une erreur ou une condition nécessaire au fonctionnement des composants installés.

### 3.4. Indépendance de PrismLDK

PrismLDK est un plugin de développement distinct du composant PRISM.

Son installation ne doit pas être automatiquement assimilée à celle de PRISM. Les dépendances et conditions nécessaires à son utilisation doivent être précisées dans sa propre description.

Le fait que PrismLDK soit présent ne doit pas modifier silencieusement la sélection de PRISM.

### 3.5. Organisation commune

Les composants sont regroupés sous une racine logicielle commune.

Aucun chemin d’installation indépendant ne doit être demandé pour chaque composant. L’utilisateur choisit un seul répertoire pour l’ensemble du logiciel.

Aucun profil prédéfini d’installation n’est prévu dans cette version.

---

## 4. Organisation des fichiers installés

### 4.1. Arborescence cible

L’arborescence logique proposée est la suivante :

    LIVEX/
    ├── Launcher/
    ├── Components/
    │   ├── SYNE/
    │   ├── ECHOS/
    │   └── PRISM/
    ├── Plugins/
    │   └── PrismLDK.zip
    ├── Docs/
    └── Assets/

Cette arborescence représente l’organisation fonctionnelle cible. La présence effective de chaque répertoire dépend des composants sélectionnés.

Les fichiers de configuration interne nécessaires aux différents composants pourront être présents si leur besoin est démontré. En revanche, aucun fichier global `livex.json` ne doit être créé.

Les artefacts réels distribués par chaque composant devront être respectés. L’installateur ne doit pas imposer une organisation incompatible avec leur mode de fonctionnement.

### 4.2. Séparation du logiciel et de l’espace de travail

Le répertoire du logiciel et le répertoire de l’espace de travail sont deux emplacements distincts.

- Le **répertoire logiciel** contient le lanceur, les composants installés, leurs ressources et les dépendances embarquées ou distribuées avec eux.
- Le **répertoire de travail** contient les données et expériences produites ou utilisées par l’utilisateur.

Un emplacement de travail par défaut pourra être proposé sous le répertoire LIVEX, par exemple `LIVEX/Workspace/`, si cela reste cohérent avec les droits d’accès et le fonctionnement des composants.

L’utilisateur doit pouvoir choisir un autre emplacement, notamment un répertoire personnel distinct du logiciel.

Aucun déplacement automatique d’un espace de travail existant n’est prévu.

---

## 5. Gestion des chemins et persistance

### 5.1. Choix des chemins pendant l’installation

L’installateur doit demander séparément :

1. Le répertoire d’installation du logiciel.
2. Le répertoire de l’espace de travail.

Les deux chemins doivent pouvoir être configurés indépendamment.

Les chemins proposés par défaut doivent être accessibles à l’utilisateur courant, dans la mesure permise par le système d’exploitation.

L’installateur doit vérifier que les chemins choisis sont utilisables avant de commencer les opérations qui pourraient modifier les fichiers.

### 5.2. Suppression définitive de `livex.json`

Le fichier `livex.json` est supprimé du périmètre fonctionnel.

Il ne doit être ni généré par l’installateur, ni exigé par le lanceur, ni utilisé comme mécanisme de détection de l’installation.

Aucun remplacement direct de ce fichier par un autre fichier global ayant exactement le même rôle ne doit être introduit sans nécessité technique démontrée.

La gestion des chemins repose sur des mécanismes distincts, adaptés à chaque besoin.

### 5.3. Détection des composants par le lanceur

Le lanceur doit retrouver les composants installés à partir de son propre emplacement et de l’organisation de fichiers prévue.

Cette détection doit éviter de dépendre d’un chemin d’installation absolu inscrit dans un fichier global.

Elle doit également permettre de repérer les composants facultatifs absents afin que le lanceur ne tente pas de les utiliser comme s’ils étaient installés.

Le comportement précis en cas de composant manquant ou incohérent devra être défini selon les contrats de communication entre les composants.

### 5.4. Mémorisation de l’espace de travail

Le chemin de l’espace de travail doit être mémorisé séparément, au moyen d’un mécanisme adapté au système d’exploitation.

Le choix de l’utilisateur doit être conservé entre les lancements, sans nécessiter de sélectionner de nouveau le même répertoire à chaque ouverture normale.

La solution technique retenue devra respecter les critères suivants :

- Persistance entre les sessions.
- Fonctionnement sans privilèges administrateur lorsque cela est possible.
- Compatibilité avec Windows et Linux.
- Absence de dépendance envers `livex.json`.
- Gestion d’un répertoire devenu inaccessible ou déplacé.
- Possibilité de modifier le chemin selon une procédure définie.
- Nettoyage approprié lors de la désinstallation.

L’emplacement précis de cette information et la technologie de persistance devront être arrêtés pendant la conception technique.

### 5.5. Répertoire de travail inaccessible

Si le chemin mémorisé ne correspond plus à un répertoire accessible, le lanceur ne doit pas supposer que les expériences ont été supprimées.

Il doit signaler le problème et permettre à l’utilisateur de sélectionner un répertoire valide selon les possibilités de l’application.

Aucune création ou suppression destructive ne doit être effectuée silencieusement pour tenter de résoudre cette situation.

---

## 6. Parcours d’installation

### 6.1. Étapes de l’assistant

L’installation doit suivre un parcours guidé comprenant les étapes suivantes.

**Étape 1 — Accueil**

Présentation de LIVEX et de l’objectif de l’installation.

**Étape 2 — Langue**

Choix de la langue de l’assistant parmi les langues prises en charge. Le français et l’anglais sont prévus.

**Étape 3 — Licence**

Présentation de la licence LIVEX et des informations nécessaires à son acceptation.

L’utilisateur doit accepter la licence avant de poursuivre.

**Étape 4 — Sélection des composants**

Présentation des composants obligatoires et facultatifs.

Les composants obligatoires ne peuvent pas être désélectionnés. Les composants facultatifs peuvent être activés ou désactivés indépendamment, sous réserve de leurs éventuelles dépendances techniques explicites.

Si PRISM n’est pas sélectionné, une description visible précise que ses fonctionnalités ne seront pas disponibles.

**Étape 5 — Choix des chemins**

Sélection du répertoire logiciel et du répertoire de travail.

**Étape 6 — Récapitulatif**

Présentation du répertoire logiciel, de l’espace de travail, des composants sélectionnés et des principales opérations qui seront effectuées.

**Étape 7 — Installation**

Affichage de la progression, des étapes en cours et des erreurs éventuelles.

**Étape 8 — Résultat**

Présentation du résultat final.

Lorsque l’installation réussit, l’assistant propose de lancer LIVEX. L’option « Lancer LIVEX » est cochée par défaut.

### 6.2. Langues

L’interface de l’installateur doit être disponible en français et en anglais.

Le choix de la langue doit s’appliquer aux écrans, descriptions, avertissements, messages d’erreur et récapitulatifs gérés par l’installateur.

La traduction des textes appartenant aux composants eux-mêmes relève de la responsabilité de ces composants, sauf décision contraire.

---

## 7. Paquet de distribution et dépendances

### 7.1. Contenu du paquet

Le paquet doit embarquer les fichiers nécessaires à l’installation des composants qu’il propose.

Cela comprend notamment, selon la distribution :

- Les fichiers du lanceur.
- Les artefacts des composants sélectionnés ou sélectionnables.
- Les ressources nécessaires.
- Les contrats de communication requis.
- Les dépendances redistribuables.
- Les fichiers de licence et mentions requises.
- Les éléments nécessaires à la vérification de l’intégrité.

L’organisation physique du paquet d’installation peut différer de celle du logiciel installé, à condition que l’installation produise l’organisation attendue.

### 7.2. Inventaire des dépendances

Les dépendances doivent être recensées par composant.

L’inventaire devra préciser pour chacune :

- Son nom et sa version.
- Le composant qui en dépend.
- Son caractère obligatoire ou facultatif.
- Le système d’exploitation concerné.
- Son mode de distribution.
- Sa licence.
- Ses conditions de redistribution.
- Les éventuelles contraintes d’installation ou de privilèges.

Aucun runtime particulier ne doit être considéré comme nécessaire avant vérification des besoins réels de SYNE, ECHOS, PRISM et PrismLDK.

### 7.3. Installation des dépendances

Les dépendances redistribuables nécessaires doivent être installées automatiquement lorsque cela est possible.

La progression de leur installation doit être visible.

Si une dépendance obligatoire ne peut pas être installée, l’opération doit échouer proprement avec un message explicite.

Une dépendance facultative ne doit pas provoquer l’échec de fonctionnalités indépendantes qui ne l’utilisent pas, lorsque leur installation reste cohérente.

### 7.4. Licences tierces

La licence du projet LIVEX ne remplace pas les licences propres aux dépendances.

Les textes de licence, mentions de copyright et autres éléments imposés par les conditions de redistribution doivent être intégrés ou rendus accessibles selon les obligations applicables.

Aucun élément dont la redistribution est interdite ou insuffisamment vérifiée ne doit être intégré à la distribution finale avant clarification.

---

## 8. Intégration au système

### 8.1. Principe général

L’intégration au système doit rester minimale et limitée aux éléments nécessaires au fonctionnement et à l’accès à LIVEX.

L’installation ne doit pas modifier globalement le système sans nécessité démontrée.

### 8.2. Raccourcis

L’installateur doit créer un raccourci vers LIVEX dans le menu des applications lorsque le système et le contexte d’installation le permettent.

La création d’un raccourci sur le bureau doit être proposée en option et être désactivée par défaut.

Aucun raccourci séparé vers les composants internes n’est prévu.

### 8.3. Associations et variables d’environnement

Aucune association de fichiers ni modification globale des variables d’environnement ne doit être effectuée par défaut.

Toute intégration supplémentaire devra répondre à un besoin identifié, être documentée et être conçue pour pouvoir être supprimée proprement.

### 8.4. Nettoyage des intégrations

Lors de la désinstallation, l’installateur doit supprimer les raccourcis et intégrations qu’il a lui-même créés.

Il ne doit pas supprimer un raccourci, une association ou une variable préexistante qui n’appartient pas à l’installation LIVEX.

### 8.5. Privilèges

L’installation doit privilégier les intégrations accessibles à l’utilisateur courant.

Si une opération requiert des privilèges supplémentaires, cette exigence doit être identifiée explicitement. Elle ne doit pas être présentée comme une exigence générale de l’installation lorsque les autres fonctionnalités peuvent fonctionner sans privilèges élevés.

---

## 9. Détection des installations existantes

### 9.1. Objectif

L’installateur doit détecter une installation existante de LIVEX, même si celle-ci se trouve dans un répertoire personnalisé.

Cette détection ne doit pas dépendre de la présence de `livex.json`.

### 9.2. Mécanisme par système

Le mécanisme d’identification doit être adapté au système d’exploitation.

- **Windows :** utiliser un mécanisme d’enregistrement adapté permettant d’identifier l’installation et son emplacement.
- **Linux :** utiliser un mécanisme compatible avec le mode de distribution retenu et privilégiant les données propres à l’utilisateur lorsque cela est pertinent.

Le mécanisme précis devra être défini après vérification des possibilités de Qt Installer Framework.

### 9.3. Installation déjà présente

Si une installation valide est détectée, l’installateur ne doit pas écraser silencieusement les fichiers existants.

Il doit signaler la présence de cette installation et proposer une procédure appropriée, telle que l’accès à la maintenance ou à la désinstallation, selon les capacités retenues.

L’installation simultanée de plusieurs instances non prévues ne doit pas être autorisée par défaut.

### 9.4. Enregistrement manquant ou obsolète

Si l’enregistrement de l’installation est absent, obsolète ou incohérent, l’installateur doit traiter explicitement cette situation.

Il ne doit pas conclure automatiquement qu’aucune installation n’existe.

La procédure de récupération devra chercher à éviter l’écrasement de données préexistantes et signaler les situations qui nécessitent une intervention de l’utilisateur.

### 9.5. Nettoyage de l’enregistrement

Lors de la désinstallation, les enregistrements créés par l’installateur doivent être supprimés.

Aucune donnée ou entrée étrangère à LIVEX ne doit être supprimée dans le cadre de ce nettoyage.

---

## 10. Désinstallation et conservation des données

### 10.1. Principes

La désinstallation doit être accessible à l’utilisateur et proposer un choix explicite concernant les données à conserver.

La suppression du logiciel ne doit pas entraîner automatiquement la suppression des expériences.

L’espace de travail peut se trouver en dehors du répertoire logiciel. La désinstallation doit donc pouvoir retrouver son emplacement à partir des informations disponibles et demander confirmation avant toute suppression des données concernées.

### 10.2. Parcours de désinstallation

L’assistant de désinstallation comprend les étapes suivantes :

1. Accueil et identification de l’installation concernée.
2. Choix des éléments à conserver ou supprimer.
3. Récapitulatif des opérations et des chemins concernés.
4. Confirmation explicite lorsque des expériences doivent être supprimées.
5. Progression de la désinstallation.
6. Présentation du résultat et, en cas d’échec, indication des erreurs et éventuels résidus.

### 10.3. Options de désinstallation

Trois options sont prévues.

**Option A — Supprimer le logiciel uniquement**

Supprime les fichiers du logiciel et les intégrations créées par l’installateur.

Les expériences sont conservées. Les éventuelles données de configuration utilisateur sont également conservées selon la définition finale des données de configuration.

Cette option doit permettre de désinstaller LIVEX sans perdre le travail de l’utilisateur.

**Option B — Supprimer le logiciel et sa configuration**

Supprime les fichiers du logiciel, les intégrations créées par l’installateur et les données de configuration associées à LIVEX.

Les expériences sont conservées.

Cette option doit distinguer la configuration de l’application des données produites par l’utilisateur.

**Option C — Suppression complète**

Supprime le logiciel, sa configuration et les expériences présentes dans l’espace de travail associé à l’installation.

L’installateur doit afficher le chemin exact de l’espace de travail et demander une confirmation explicite avant la suppression.

Aucune suppression ne doit commencer tant que cette confirmation n’a pas été obtenue.

### 10.4. Espace de travail externe

Lorsque l’espace de travail est situé en dehors du répertoire logiciel, l’assistant doit afficher son chemin exact avant toute suppression.

Si le chemin mémorisé n’est plus accessible, l’installateur ne doit pas supposer que les données sont absentes ni effectuer une suppression sur un chemin approximatif.

La suppression complète ne peut être considérée comme réussie que si les éléments concernés ont effectivement été supprimés conformément au périmètre confirmé.

### 10.5. Données résiduelles

En cas d’échec, les fichiers ou données qui n’ont pas pu être supprimés doivent être signalés.

Le journal nécessaire au diagnostic d’un échec doit pouvoir être conservé après la désinstallation, dans un emplacement documenté.

Cette conservation technique ne doit pas être confondue avec la conservation des données fonctionnelles de LIVEX.

### 10.6. Configuration native

Les informations persistantes nécessaires au fonctionnement du lanceur, notamment le chemin mémorisé de l’espace de travail, doivent être traitées selon leur nature.

L’option de désinstallation choisie doit déterminer les informations conservées ou supprimées.

Le comportement exact des options A et B à l’égard de cette configuration native devra être spécifié avec précision avant l’implémentation, sans réintroduire `livex.json`.

---

## 11. Gestion des erreurs, intégrité et nettoyage

### 11.1. Vérification préalable

Avant toute modification de l’installation, l’installateur doit vérifier l’intégrité des fichiers et archives embarqués.

Si une archive ou un fichier indispensable est corrompu, l’installation doit être interrompue avant toute modification susceptible de laisser une installation partielle.

### 11.2. Erreurs pendant l’installation

Une erreur bloquante doit provoquer l’arrêt de l’opération lorsque la poursuite compromettrait la cohérence de l’installation.

L’assistant doit afficher un message compréhensible et enregistrer les informations techniques nécessaires au diagnostic.

### 11.3. Nettoyage après échec

Après un échec, l’installateur doit tenter de supprimer les fichiers, raccourcis et enregistrements créés par la tentative.

Il doit protéger les fichiers et données qui existaient avant l’opération.

La restauration doit être réalisée dans la mesure du possible. Une restauration absolue de l’état précédent ne doit pas être promise.

### 11.4. Nettoyage incomplet

Si certains éléments ne peuvent pas être supprimés, l’installateur doit :

- Signaler que le nettoyage est incomplet.
- Identifier les éléments résiduels lorsque cela est possible.
- Indiquer leur emplacement.
- Conserver les journaux nécessaires au diagnostic.
- Éviter de déclarer l’installation ou la désinstallation entièrement réussie lorsque des opérations essentielles ont échoué.

### 11.5. Journalisation

Les journaux doivent permettre d’identifier :

- L’étape concernée.
- L’opération exécutée.
- L’erreur rencontrée.
- Le résultat du nettoyage.
- Les éventuels éléments résiduels.

Les journaux ne doivent pas contenir inutilement des informations personnelles ou des données issues des expériences.

---

## 12. Signature et confiance dans la distribution

### 12.1. Windows

Les exécutables et installateurs destinés à la distribution publique doivent être signés numériquement avant publication, selon une solution adaptée au projet.

La disponibilité du certificat, son coût, son mode d’utilisation et les exigences de distribution devront être évalués avant la publication.

### 12.2. Linux

Le mécanisme de vérification applicable à Linux dépendra du mode de distribution retenu.

La solution devra permettre à l’utilisateur de vérifier l’intégrité et, lorsque le mécanisme le permet, l’authenticité du paquet distribué.

La méthode précise reste à définir. Aucune solution de signature ou de distribution particulière n’est imposée à ce stade.

### 12.3. Échec de vérification

Si l’installateur détecte une altération d’un fichier indispensable, il doit interrompre l’opération et présenter une erreur explicite.

Les modalités exactes de vérification de l’authenticité de la distribution devront être documentées pour chaque système.

---

## 13. Licences et mentions légales

### 13.1. Licence LIVEX

La licence LIVEX doit être présentée pendant l’installation.

Son acceptation est obligatoire pour poursuivre.

Le texte complet de la licence doit également être accessible dans la distribution, conformément à l’organisation du projet.

### 13.2. Dépendances et composants tiers

Pour chaque dépendance ou composant tiers distribué avec LIVEX, il faut vérifier :

- La licence applicable.
- Les conditions de redistribution.
- Les obligations de mention et de copyright.
- Les éventuelles obligations de fournir le code source ou des modifications.
- Les contraintes liées à la distribution binaire.

La licence de LIVEX ne doit pas être considérée comme couvrant automatiquement ces éléments.

### 13.3. Éléments non vérifiés

Un élément dont les conditions de redistribution ne sont pas confirmées ne doit pas être intégré au paquet final tant que cette vérification n’est pas effectuée.

La documentation de distribution devra identifier les licences et mentions applicables.

---

## 14. Critères d’acceptation

L’installateur sera considéré comme conforme à cette spécification lorsque les critères suivants auront été vérifiés.

### 14.1. Installation

- [ ] Installation hors ligne sous Windows.
- [ ] Installation hors ligne sous Linux.
- [ ] Installation avec les droits de l’utilisateur courant lorsque le système le permet.
- [ ] Sélection des composants facultatifs indépendamment des composants obligatoires.
- [ ] PRISM désactivé par défaut.
- [ ] Affichage d’une description des fonctionnalités indisponibles lorsque PRISM n’est pas installé.
- [ ] PrismLDK traité comme un composant distinct de PRISM.
- [ ] Choix indépendant du répertoire logiciel et de l’espace de travail.
- [ ] Affichage d’un récapitulatif avant installation.
- [ ] Lancement de LIVEX proposé après une installation réussie.
- [ ] Interface disponible en français et en anglais.

### 14.2. Chemins et configuration

- [ ] Aucun `livex.json` n’est généré ou requis.
- [ ] Le lanceur retrouve les composants depuis son propre emplacement.
- [ ] Le chemin de l’espace de travail est mémorisé par un mécanisme adapté au système.
- [ ] Le chemin mémorisé est réutilisé au lancement suivant.
- [ ] Un espace de travail inaccessible est signalé sans suppression silencieuse des données.
- [ ] Le comportement en cas de déplacement de l’espace de travail est défini et testé.

### 14.3. Intégration au système

- [ ] Raccourci vers LIVEX créé dans le menu des applications lorsque possible.
- [ ] Raccourci sur le bureau facultatif et désactivé par défaut.
- [ ] Aucun raccourci vers les composants internes.
- [ ] Aucune association de fichiers ni modification globale de variables d’environnement par défaut.
- [ ] Suppression des intégrations créées par l’installateur lors de la désinstallation.

### 14.4. Détection des installations

- [ ] Détection d’une installation située dans le répertoire par défaut.
- [ ] Détection d’une installation située dans un répertoire personnalisé.
- [ ] Blocage d’un nouvel écrasement silencieux d’une installation existante.
- [ ] Traitement explicite des enregistrements absents ou obsolètes.
- [ ] Suppression des enregistrements créés par l’installateur lors de la désinstallation.

### 14.5. Désinstallation

- [ ] Désinstallation du logiciel en conservant les expériences.
- [ ] Suppression de la configuration avec conservation des expériences.
- [ ] Suppression complète avec confirmation explicite.
- [ ] Affichage du chemin exact de l’espace de travail avant suppression.
- [ ] Aucune suppression silencieuse d’un espace de travail externe.
- [ ] Signalement des données résiduelles en cas d’échec.
- [ ] Conservation des journaux de diagnostic nécessaires après un échec.

### 14.6. Intégrité et erreurs

- [ ] Détection d’une archive corrompue avant modification de l’installation.
- [ ] Arrêt propre en cas d’erreur bloquante.
- [ ] Nettoyage des fichiers créés par une tentative échouée lorsque cela est possible.
- [ ] Protection des données préexistantes.
- [ ] Journalisation des erreurs et des résultats du nettoyage.
- [ ] Signalement des résidus impossibles à supprimer.

### 14.7. Distribution et licences

- [ ] Inventaire des dépendances par composant.
- [ ] Vérification des conditions de redistribution.
- [ ] Présence des mentions et textes de licence requis.
- [ ] Signature numérique des exécutables et installateurs Windows avant distribution publique.
- [ ] Mécanisme de vérification Linux défini selon le mode de distribution.
- [ ] Procédure de vérification documentée.

---

## 15. Points techniques restant à confirmer

Les principes fonctionnels ci-dessus sont validés. Les éléments suivants nécessitent encore une décision technique ou une vérification avant implémentation.

| Référence | Point à confirmer | Résultat attendu |
|---|---|---|
| T01 | Capacités réelles de Qt Installer Framework | Confirmer les mécanismes natifs et identifier les compléments nécessaires. |
| T02 | Installation sans privilèges administrateur | Tester les chemins, raccourcis et opérations sur les deux systèmes. |
| T03 | Détection des installations existantes | Définir le mécanisme d’enregistrement et la récupération des enregistrements obsolètes. |
| T04 | Persistance native du chemin de travail | Choisir un mécanisme adapté à Windows et Linux sans `livex.json`. |
| T05 | Sémantique des options de désinstallation | Définir précisément les données de configuration conservées ou supprimées par les options A et B. |
| T06 | Inventaire des dépendances | Vérifier les besoins réels de chaque composant, les versions et les conditions de redistribution. |
| T07 | Signature Windows | Définir la solution de signature et les conditions nécessaires à la publication. |
| T08 | Vérification Linux | Définir le mécanisme selon le format et le canal de distribution retenus. |
| T09 | Nettoyage et retour arrière | Définir les opérations compensatoires possibles et tester les scénarios d’échec. |
| T10 | Licences tierces | Vérifier les licences et mentions de tous les éléments distribués. |
| T11 | Répertoire de travail déplacé | Définir le parcours permettant de sélectionner un nouvel emplacement. |
| T12 | Langues | Définir les ressources de traduction et le comportement en cas de traduction manquante. |

Ces points techniques ne remettent pas en cause les décisions fonctionnelles validées. Ils doivent être résolus avant de considérer l’implémentation comme conforme à la spécification.

---

## 16. Évolution de la spécification

Cette spécification constitue la nouvelle base documentaire de l’installateur LIVEX.

Elle remplace les anciennes versions de la documentation de l’installateur, conformément à la décision de repartir sur une spécification redéfinie.

Toute évolution ultérieure devra préserver les décisions validées ou faire l’objet d’une nouvelle validation explicite.

En particulier, les éléments suivants ne doivent pas être réintroduits sans décision :

- Le fichier `livex.json`.
- Une dépendance obligatoire envers PRISM.
- L’installation globale nécessitant systématiquement des privilèges administrateur.
- Le téléchargement automatique de dépendances.
- La suppression automatique des expériences lors d’une désinstallation standard.
- L’écrasement silencieux d’une installation existante.
- La modification globale du système sans besoin démontré.

**Fin de la spécification — version 0.2.**