# ADR-003 : ECHOS calcule, le Launcher présente

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ADR-002-modes-analyse-et-immersion-de-poids-egal.md`
**Source Monographie** : —

---

## Contexte

Le Launcher doit coordonner une campagne : enchaîner des runs, collecter ce qu'ils
produisent, les analyser et **présenter le résultat**. Le rapport d'émergence est un
livrable du Launcher — il est archivé avec l'expérience, exporté, partagé.

Cette mission ouvre une tentation récurrente : **le Launcher analyse**.

1. **Le résumé.** Afficher dans l'interface un décompte de runs, une moyenne, une
   durée, un écart. Chacune paraît anodine, et leur accumulation produirait un
   tableau de bord analytique.
2. **Le classement.** Trier les runs, les campagnes, les résultats, pour aider
   l'utilisateur à s'y retrouver. Classer est déjà interpréter.
3. **L'interprétation.** Conclure qu'une campagne est « concluante », qu'un paramètre
   « explique » une tendance, qu'une valeur est « anormale ».

Or ECHOS est le composant d'analyse du projet. Il possède la sémantique des
résultats, les métriques, les statistiques et la calibration. Si le Launcher en
produisait, deux propriétaires cohabiteraient pour la même chose, avec le risque de
divergence silencieuse : le Launcher afficherait un chiffre qu'ECHOS contredirait.

Mais l'inverse est tout aussi vrai : si le Launcher n'affiche jamais rien, il
n'est qu'un orchestrateur de fichiers et l'utilisateur perd le bénéfice de son
propre travail au moment où il en a le plus besoin. **Un rapport qui n'est pas
lisible dans l'outil qui a lancé la campagne est un rapport mal livré.**

## Décision

**La frontière porte sur le calcul, pas sur l'affichage. ECHOS calcule, le Launcher
présente.**

- **ECHOS possède le calcul.** Toute analyse scientifique — métriques, statistiques,
  détection de motifs émergents, comparaison inter-runs, anomalies — est produite
  par ECHOS, à partir des données de run. Le Launcher n'en calcule aucune.
- **Le Launcher orchestre la demande.** Il appelle `AnalyzeRun(runPath)`,
  `AnalyzeExperiment(experimentPath)` et `GenerateReport(experimentPath)` selon le
  contrat d'intégration, attend le résultat, et le rattache à l'expérience.
- **Le Launcher présente le rapport.** La visualisation du rapport d'émergence dans
  l'interface du Launcher est une fonctionnalité de premier plan. Le Launcher rend
  ce qu'ECHOS a produit ; il ne le recalcule pas.
- **Aucune métrique scientifique dans le Launcher.** Ni moyenne, ni écart-type, ni
  corrélation, ni seuil de significativité, ni indicateur composite.
- **Les compte-rendus d'orchestration ne sont pas des résultats.** Le nombre de runs
  terminés, la durée écoulée et la progression sont des faits d'exécution, lisibles
  dans n'importe quel outil. Ils ne portent aucun sens scientifique.
- **Aucune interprétation.** Le Launcher ne classe pas, ne note pas, ne conclut pas.
  Le tri d'une liste est une tris d'affichage, jamais un jugement.
- **L'interface d'ECHOS est une télémétrie optionnelle.** Pendant une simulation,
  l'utilisateur peut ouvrir l'interface web d'ECHOS pour observer l'analyse en
  direct. C'est un complément, pas un prérequis : le Launcher reste pleinement
  fonctionnel si elle n'est jamais ouverte, et la campagne ne dépend d'aucune
  fenêtre de navigateur.

### Ce que cela implique concrètement

| Opération | Propriétaire |
| :-- | :-- |
| Enchaîner les runs, ordonnancer, reprendre | Launcher |
| Calculer métriques et statistiques | **ECHOS** |
| Produire les données d'un run | SYNE |
| Écrire le fichier `emergence_report.md` | **ECHOS**, sur demande du Launcher |
| Afficher le rapport d'émergence | **Launcher** |
| Afficher progression, états, alertes | Launcher |
| Ouvrir l'interface interactive d'ECHOS | Launcher, **sur action de l'utilisateur** |

## Conséquences

### Positives
- Une seule sémantique des résultats dans le projet, donc aucune divergence entre ce
  que l'utilisateur lit dans ECHOS et ce qu'il lit dans le Launcher.
- Le Launcher reste mince : il est remplaçable sans perte de valeur de recherche.
- Le rapport est lisible sur place, au moment de la campagne, sans changement
  d'outil.
- La frontière est testable : un test peut vérifier qu'aucune vue du Launcher ne
  calcule de valeur scientifique, et que le rapport affiché provient bien d'un
  fichier écrit par ECHOS.
- L'interface d'ECHOS devient un **plus** de télémétrie, pas une dépendance.

### Négatives
- Le Launcher dépend d'ECHOS pour tout contenu de rapport. Sans ECHOS, le Launcher
  montre des faits d'exécution et l'absence de rapport, ce qui doit être explicite.
- Toute demande d'analyse dans le Launcher doit être formulée en termes de
  contrat d'orchestration, ce qui allonge la conception.
- L'affichage du rapport impose de définir un format de présentation stable, qui
  doit évoluer avec ECHOS.

### Risques
- **Fuite progressive du calcul.** Une première moyenne « provisoire », puis un
  classement « pratique », produisent un tableau de bord analytique par
  accumulation. Mitigation : la règle porte explicitement sur le **calcul** ; toute
  valeur dérivée d'un résultat scientifique est interdite dans le Launcher, quelle
  que soit sa présentation, et cette décision est citée en revue de conception.
- **Affichage qui dérive du contenu.** La couche de présentation du Launcher peut
  « embellir » un rapport, produisant un écart entre le fichier et l'écran.
  Mitigation : la vue rend le contenu produit par ECHOS, et un test compare le
  fichier affiché à sa source.
- **Dépendance à la disponibilité d'ECHOS.** Si ECHOS est arrêté, aucun rapport n'est
  produit. Mitigation : l'absence est affichée explicitement, jamais comblée par une
  approximation locale.

## Alternatives considérées

- **Le Launcher n'affiche aucun résultat, l'analyse reste entièrement dans
  ECHOS** : refusé. C'était la version précédente de cette décision, et elle
  contredit l'usage attendu : le rapport d'émergence est un livrable du Launcher,
  archivé et exporté par lui. L'utilisateur doit le lire dans son outil de travail.
- **Le Launcher calcule ses propres statistiques en plus de celles d'ECHOS** :
  refusé sans hésitation. Deux propriétaires pour la même grandeur, avec divergence
  silencieuse.
- **Le Launcher héberge l'interface d'ECHOS dans une vue embarquée** : refusé.
  L'intégration lui ferait porter la présentation d'ECHOS et assumer ses évolutions
  visuelles. L'interface d'ECHOS reste accessible sur action, comme télémétrie.
- **Le Launcher lit la base de données d'ECHOS pour ses propres besoins** : refusé,
  sauf pour vérifier la présence d'un artefact et son empreinte. Présenter un
  résultat n'est pas l'analyser ; vérifier une intégrité ne l'est pas non plus.
- **ECHOS calcule et présente aussi, le Launcher ne fait qu'orchestrer** : refusé.
  Le rapport doit être attaché à l'expérience et exportable par le Launcher, qui est
  le seul à connaître le cycle de vie complet de la campagne.

## Validation / rejet

- **Test d'interface** : les vues du Launcher sont inventoriées ; une vue qui
  calcule une valeur scientifique fait échouer le test. Une vue qui *affiche* un
  rapport est conforme.
- **Test de provenance** : le rapport affiché dans le Launcher est comparé au
  fichier `emergence_report.md` écrit par ECHOS. Toute divergence échoue.
- **Test de dépendance** : `Launcher.Domain` ne référence aucun type de métrique
  scientifique, vérifié par analyse statique.
- **Test d'indépendance** : le cycle complet — campagne, rapport, export — est
  exécuté sans jamais ouvrir l'interface web d'ECHOS.
- **Contrat d'archivage** : `INTEGRATION_CONTRACT.md` exige une écriture atomique
  et une provenance, sans exiger aucune interprétation. Le Launcher n'exige rien sur
  le contenu scientifique.
- **Réouverture** : réexaminée si ECHOS disparaît du projet, si l'analyse cesse
  d'être une responsabilité de composant, ou si le format de rapport change de
  nature. Elle n'est pas réexaminée pour une simple question d'affichage.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création initiale : ECHOS seul propriétaire de l'analyse, aucun affichage dans le Launcher | Garantir une seule sémantique des résultats |
| 30 septembre 2026 | **Réécriture** : la frontière porte sur le calcul, pas sur l'affichage. Le Launcher présente le rapport d'émergence ; l'interface ECHOS devient une télémétrie optionnelle. | La version précédente interdisait la visualisation des rapports dans le Launcher, ce qui contredit le rôle du Launcher comme porteur du livrable de campagne |
