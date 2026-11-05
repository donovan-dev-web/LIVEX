# Chapitre 9. L'installateur : distribuer sans confondre les versions

Un système peut fonctionner sur la machine qui l'a construit et rester
inutilisable pour les autres. L'installateur transforme les composants
publiés en une pile qu'on peut déposer, configurer, mettre à jour et réparer.
Il ne contient ni moteur de simulation ni analyse ; il livre des composants
et laisse le Launcher interpréter leurs manifestes.

Ce chapitre décrit l'installateur de la release publique LIVEX 1.0.0. Il
était d'abord documenté comme spécification ; il a depuis été implémenté et
livré pour Windows et Linux. L'intégration et la distribution des composants
PRISM V2 constituent un travail distinct, encore ouvert au 4 novembre 2026.

## 1. Une frontière de distribution

L'installateur ne connaît pas les règles internes de SYNE, ECHOS ou PRISM. Il
installe les composants et leurs manifestes. Le Launcher lit ces manifestes
pour découvrir les programmes, leurs capacités, leurs ports et leurs sondes
de santé. Cette répartition permet de faire évoluer un composant sans
transformer l'installateur en moteur de simulation.

La release V1 comprend l'installateur avec les paquets Windows et Linux. Les
parcours graphiques et en ligne de commande prennent en charge l'installation
et les opérations de maintenance prévues par la version publiée. Les suites
xUnit de l'installateur comptaient 118 réussites dans le relevé de release
(72 Unit, 34 App, 6 Integration et 6 EndToEnd). Ce total décrit cette
validation V1, pas un test du packaging PRISM V2.

## 2. Le cycle de vie d'une installation

L'utilisateur choisit les composants à installer, leur emplacement et les
options compatibles avec son système. L'installateur vérifie les entrées,
prépare les fichiers, applique les manifestes puis expose le résultat au
Launcher. Une mise à jour ou une réparation porte sur les composants
distribués et respecte les données de travail, qui appartiennent à leur
composant ou au Launcher.

La séparation entre programmes et données de campagne importe. Une
réinstallation ne doit pas être confondue avec la suppression d'archives de
runs ; celles-ci ont leur propre cycle de vie. De même, le Launcher reste
l'autorité pour l'orchestration, tandis que l'installateur reste un outil de
distribution.

## 3. Le passage de PRISM V1 à PRISM V2

La release 1.0.0 distribue l'architecture V1, dont PRISM Unreal et le plugin
PRISM-LDK. PRISM V2 est un nouveau composant .NET headless composé d'un host,
d'un noyau, d'un compilateur de contenu, de bots et d'un viewer facultatif.
Le contrat est gelé en `1.0.0`, mais toutes les validations et le parcours
de release ne sont pas achevés.

Le manifeste du host PRISM V2 est raccordé au Launcher générique et les scripts
de publication autonome existent. Cela ne signifie pas que les paquets V2
soient déjà intégrés à l'installateur public. La lane INSTALLER-200 doit
définir les composants distribuables, leur provenance et leurs options avant
que la release V2 puisse être installée comme une pile complète.

Il faut donc distinguer trois affirmations : l'installateur V1 est livré ; un
host PRISM V2 peut être publié par les outils de développement ; la distribution
complète de LIVEX V2 reste à produire et à valider.

## Ce que ce chapitre permet d'affirmer

LIVEX possède un installateur livré pour sa release V1. Ses tests attestent les
comportements qu'ils couvrent. Ils ne valident pas l'acquisition et le
packaging de PRISM V2, ni le fonctionnement d'une installation complète avec
SYNE, PRISM, ECHOS et Launcher V2. Cette frontière évite de transformer un
produit publié en preuve prématurée d'une distribution encore en préparation.
