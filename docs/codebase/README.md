# Cartographie interactive du codebase

`index.html` affiche la cartographie générée par `generate_graph.py`, en vue
orbitale 3D ou en plan 2D. Le parcours interactif `flow.html` présente le
fonctionnement d'un run comme une machine à états 2D animée : orchestration,
handshake, boucle de tick SYNE, branches de fréquence du cycle cognitif, retour
au tick suivant et observabilité. Les nœuds et transitions sont sélectionnables
et sourcés ; les cadences par défaut sont distinguées des valeurs de profils.
Le graphe
couvre les fichiers présents et non ignorés du dépôt, leurs déclarations
statiques reconnues et leurs paramètres, avec des liens de contenance, des
références documentaires, des appels candidats, les notions nommées présentes
à la fois dans le code et les documents, et les flux inter-composants
explicitement décrits dans `docs/ARCHITECTURE.md`. Les dépendances des sorties
composites ECHOS sont aussi reliées depuis la déclaration de
`COMPOSITE_DEPENDENCIES`.

## Ouvrir la cartographie

La page charge `graph-data.js` localement et Three.js ainsi que ses contrôles
orbitaux depuis jsDelivr. Il faut donc une connexion Internet et un serveur HTTP
local (les modules JavaScript ne sont pas chargés de façon fiable depuis
`file://`). Depuis la racine du dépôt :

```powershell
python -m http.server 8000
```

Ouvrir ensuite `http://localhost:8000/docs/codebase/`. La page ne transmet pas
de données du dépôt à un service distant ; seul le code de la bibliothèque
Three.js est chargé depuis le CDN.

## Régénérer les données

Depuis la racine du dépôt :

```powershell
python docs\codebase\generate_graph.py
```

Le générateur actualise `graph-data.js`, qui est embarqué en ressource locale
par la page. Il analyse les fichiers suivis et non ignorés présents dans le
checkout et exclut le dossier `docs/codebase` lui-même. Relancer le serveur et
recharger la page après génération.

Les liens de contenance et les relations Markdown sont déterministes. Les
relations d'appel sont des candidates par nom et par portée de fichier ; elles
ne remplacent pas une analyse sémantique du compilateur et peuvent être
ambiguës en présence de surcharge, de résolution dynamique ou de noms répétés.
Les notions croisées sont des identifiants camelCase/PascalCase communs au code
et à la documentation ; elles donnent des pistes de navigation, pas une
interprétation sémantique exhaustive. Les dépendances de calcul déclarées par
ECHOS sont extraites de la structure Python statique.
Les binaires et les fichiers d'actifs sont cartographiés comme fichiers, sans
extraction de symboles.

Les étiquettes apparaissent selon le niveau de zoom afin de préserver la
lisibilité. La sélection d'un nœud révèle ses liens directs même si un autre
filtre de relation est actif. Les relations sont sélectionnables : leur
inspecteur explique le type de lien et permet d'ouvrir sa source. Le fil
d'Ariane remonte la hiérarchie ; l'action d'exploration du voisinage recentre le
graphe sur les relations de l'élément.

En 2D, les composants forment des colonnes et la profondeur du dépôt devient
une succession de couches. Glisser déplace le plan, la molette ou le pincement
à deux doigts zoome, et les nœuds comme les relations restent sélectionnables.
Une recherche cadre et met en évidence le voisinage du résultat ; le bouton
« Voisins du nœud » permet d'activer ou de lever ce focus. La page du parcours ne
démarre pas de simulation : elle anime un schéma explicatif des flux et de
l'ordre documentés dans le code et les contrats. Les deux ticks rejoués sont
pédagogiques, pas une limite réelle du run.
