# PRISM V2. Le monde physique devient un système simulé

Une simulation peut être représentée par une scène, mais elle n'a pas besoin
d'une scène pour exister. PRISM V2 pousse cette distinction jusqu'au code :
un processus .NET headless possède l'état physique du monde, et un viewer
optionnel se contente de le lire. Ce choix transforme PRISM. Le nom qui
désignait la représentation Unreal en V1 désigne maintenant le monde simulé
lui-même.

Cette version n'est pas une simple mise à jour du plugin historique. Elle
remplace le modèle où SYNE détenait tout l'état physique par une simulation
de monde autoritaire, décrite par des données et commandée par l'intermédiaire
d'un contrat versionné. La migration est en cours : le host PRISM et le
couplage cognitif fonctionnent, mais l'extraction de tous les systèmes de
monde hors de SYNE et leur reprise commune ne sont pas achevées.

## Une simulation qui tourne sans rendu

La solution se divise en contrats, noyau, contenu, host, bots et viewer. Le
noyau `Prism.Core` porte les règles déterministes. `Prism.Content` compile un
pack JSON en règles exécutables. `Prism.Host` prépare le monde, distribue les
commandes, déroule les phases du tick et expose le contrôle et les flux.
`Prism.Bots` exécute des campagnes sans passer par une scène graphique.
`Prism.Viewer` construit une représentation visuelle facultative.

Le monde est une grille horizontale bornée de colonnes 2,5D. Chaque colonne
possède une altitude et des couches déterminées par le contenu. Les régions
organisent le stockage et le calcul ; les positions restent des coordonnées
entières. Il n'y a ni cavernes, ni surplombs, ni volume 3D dense. Ce modèle
réduit le coût du terrain tout en gardant des pentes, des constructions et des
ressources accessibles en surface.

La génération s'appuie sur une graine et des opérations entières. Les
empreintes canoniques du monde permettent de vérifier qu'une même graine, une
même version de génération et un même pack produisent le même état. Les
modifications clairsemées sont rangées dans un ordre canonique, ce qui rend
l'empreinte indépendante de leur ordre d'insertion.

## Du tick aux actions matérielles

Le tick est une suite ordonnée de phases. Il traite le déplacement et la
perception, reçoit les commandes, résout les actions immédiates ou différées,
met à jour les cultures et les structures, puis publie les observations et
événements. Une action qui dure occupe son agent jusqu'à son échéance. Une
macro-action n'est validée que lorsque l'action différée a réellement produit
son résultat.

Le pack de contenu transforme les intentions en chaînes matérielles. Une
matière extraite peut être transformée en outil ; des graines sont plantées,
poussent selon les conditions prévues, puis donnent une récolte ; les stations
de fabrication convertissent les objets ; des chantiers consomment
progressivement leurs matériaux pour bâtir des structures. Les conteneurs
portent la fraîcheur de leurs denrées, qui décroît avec le temps. Un effet de
stockage comme celui d'un grenier ralentit cette péremption.

Le code comporte des scénarios d'acceptation de la chaîne d'outils, de
l'agriculture et de la cuisson sur le pack minimal. Le puits fournit un effet
`water_access` utilisable pour boire. Le stockage et les effets ont une base
fonctionnelle, mais les parcours complets par bot restent à écrire pour
plusieurs chaînes. La différence entre « le système existe » et « un agent
peut accomplir toute la chaîne » reste importante : seule la seconde
affirmation demande un scénario qui couvre le parcours entier.

## Deux contrats pour relier le monde

Le contrat SYNE↔PRISM a la version `1.0.0`. Les commandes transportent les
intentions des agents vers PRISM. PRISM renvoie des observations locales,
l'état de soi et les événements qui décrivent les effets matériels ou les
échecs. Un flux de lecture séparé publie des snapshots, des deltas, des
entités, des métriques et les états de chantier à ECHOS et au viewer.

Le host expose un contrôle HTTP, un WebSocket de tick en pas de verrouillage
et un WebSocket de lecture. Les campagnes locales peuvent appeler le même
contrat directement, sans payer de coût réseau. Les messages de commande sont
idempotents ; les réponses de tick sont versionnées et leur séquence est
validée. Le viewer n'est pas nécessaire pour avancer le temps du monde.

Dans la boucle couplée actuelle, SYNE envoie des commandes, lit les
observations PRISM et les convertit en croyances. `PrismObservationMapper`,
`CognitionPipeline.ApplyExternalObservations`, `PrismCoupledRunner` et le
paramètre CLI `--prism-url` composent ce chemin. Le transport a été exercé
sur 1 000 ticks sans crash. Cette preuve atteste le fonctionnement du transport
sur cette durée ; elle n'atteste ni les 5 000 ticks du run R2, ni sa
reproductibilité sur plusieurs exécutions, ni la persistance coordonnée de
l'état SYNE et PRISM.

## Déterminisme, performances et limites des preuves

Au 4 novembre 2026, la solution PRISM compte 403 tests réussis : Core 183,
Viewer 62, Content 48, Host 68, Contracts 15, Architecture 8 et Bots 19. Le
build relevé à cette date ne rapporte aucun avertissement. La campagne
permanente réduite exécute trois graines, 200 ticks et trois bots ; ses neuf
empreintes sont épinglées. La campagne complète de release, trois graines,
5 000 ticks et trois bots, a été rejouée avec les mêmes neuf empreintes après
plusieurs changements de contenu et d'optimisation.

Une optimisation du tick a abaissé localement le temps de préparation du
monde de 21–27 secondes à 2,0–2,4 secondes. Avec un agent inactif, le tick
est passé d'environ 1,26–1,49 seconde à 13,6 millisecondes sur la machine
mesurée. Ces valeurs décrivent un profil, un poste et une version précis.
Elles ne prouvent pas encore le budget à 1 000 agents, le comportement sur
Linux, ni la viabilité du monde à long terme.

Le prochain test d'intégration important est R2 : 5 000 ticks avec SYNE,
plusieurs exécutions identiques et comparaison bit à bit. Il faut aussi
un point de reprise cohérent entre les deux moteurs. Tant que ces preuves
manquent, PRISM V2 est un système fonctionnel en cours d'intégration, pas une
release complète.

## La frontière historique avec PRISM V1

PRISM V1 est le projet Unreal livré avec LIVEX 1.0.0. Son plugin PRISM-LDK
lit les contrats d'observabilité SYNE et expose les données au rendu Unreal.
Dans cette version, SYNE reste l'autorité de la simulation et PRISM est une
couche de présentation. Le chapitre V1 conservé dans cette partie décrit ce
produit historique. Ses facteurs d'échelle Unreal, son cycle de préparation
et ses tests ne constituent pas le contrat ni les preuves de PRISM V2.

## Ce que ce chapitre permet d'affirmer

PRISM V2 simule déjà un monde physique autonome, reçoit des commandes,
exécute des actions matérielles, publie des observations et se couple à la
cognition SYNE. Les tests, campagnes et relevés cités précisent la portée de
ces affirmations. Ils n'établissent pas encore que l'intégration complète est
reproductible à 5 000 ticks, que les portes de viabilité sont franchies ou que
des formes générales d'émergence ont été observées.
