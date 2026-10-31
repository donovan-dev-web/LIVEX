# ROADMAP Correctif
Liste des taches a effectuer pour valider V1
## CI
### Workflow
Workflow de release refonte, Plus de build/signature en workflow, creation de release manuel avec build local puis compression zip et uploads du fu zip dans la release
## installer
- Bug component.json creer uniquement pour Echos, les autres composant ne peuvent pas etre detecter.  
- Bug Icone assigner a livex-setup.exe mais pas au launcher  
- Bug après phase Recapitulatif, lors du clique sur "installer" freeze de l'app (Ne repond pas) donc phase d'installation non visible puis passage directement au l'ecran resultat une fois linstallation faite (corriger le flux et optimisation (limitation (rate limite)) pour eviter freeze et voir la progression réel de lenregistrement
- livex-setup.exe non mis lors de l'installation donc obliger de retourner chercher le livex-setup.exe télécharger pour la desinstallation ce qui nest pas logique
- tous les fichier de linstallateur et du launcher a la racine de linstallation, a corriger dans un sous dossier "core/installateur et core/launcher" par exemple, ne garder a la racine que les executable (livex-setup.exe et NOTICE/THIRD-PARTY ect)
- Impossible de set le chemin de data dans linstallateur (/user/.livex-data).  
- Choix des emplacements : Revoir les emplacement par defaut, On ne selectionne par defaut qu'un seul chemin de repertoire (puis creation des sous emplacement (logiciel, Espace de travail, Racine de donées) puis case a cocher si on veux peronnaliser le chemin d'un repertoire en particulier)
- Choix de la langue ne doit pas etre un ecran, actuellement on arrive sur la page accueil puis ensuite on selectionne la langue, Il faut plutot un bouton (dropdown) de selection de lanque toujours visible (dans le panneau lateral des étapes)
- La case "Lancer LIVEX à la fin de l'installation ne sert a rien puisque l'on met un bouton "Lancer LIVEX" a la fin
- Reduire le nombre de fichier (fonciton de unzip pour ne garder que les zip de chaque composant ?) point a etudier et corriger pour simplifier et alleger le zip final publique

## Laucher 
 - Pas d'icone assigner alors que le launcher est censé avoir son icone ainsi que les fichiers .livexp