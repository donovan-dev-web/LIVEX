# LIVEX : Monographie générale

**Question directrice : peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?**

Cette monographie est le livre exhaustif du projet LIVEX. Elle décrit ce que le projet est réellement, comment il est construit, ce qu'il permet d'observer et ce qu'il ne permet pas encore d'affirmer. Elle s'appuie sur le code réellement implémenté, sur les contrats versionnés et sur les preuves exécutables (tests, campagnes rejouables, rapports). Nous distinguons systématiquement ce qui est disponible, expérimental, en développement, envisagé ou hors périmètre.

État décrit : SYNE 0.15.0 (contrat d'observabilité 0.4.0), ECHOS 0.1.0 (schéma SQLite v6), Launcher 0.1.0 (paquet `.livexp`), PRISM et PRISM-LDK en version de travail, `syne-mock` 0.1.0 (outil), installateur à l'état de spécification. Date de référence de la description : 12 octobre 2026.

## Comment ce sommaire est organisé

Le livre se lit dans l'ordre. La partie I établit la question, ses fondements et la méthode. La partie II visite, composant par composant, le système tel qu'il est construit ; chaque composant y expose d'abord sa place, puis ses mécanismes, puis ses formules. La partie III relie les composants, traite la reproductibilité, les campagnes, les résultats observables et les limites. Les finales rassemblent la bibliographie et les annexes, qui conservent les matériaux exhaustifs (contrats, métriques, architecture détaillée, schémas, glossaire, décisions d'architecture, chronologie, paramètres, bases de données) sans interrompre le récit ; les trois dernières annexes reviennent sur la trajectoire du projet, ses mesures et son programme : ce qui a été hérité du prototype, ce que les benchmarks établissent réellement, et ce qui reste à faire.

Chaque chapitre s'ouvre par sa question, progresse sans rupture d'un niveau accessible vers la précision technique puis scientifique, et se termine par ce qu'il apporte à la question directrice et par ce qu'il ne permet pas encore de conclure.

Les **formules** ne sont pas regroupées dans un chapitre unique : chacune est traitée **dans le composant qui l'emploie**, et selon la même trame que la documentation technique, *Nom · Explication globale · Expression mathématique · Pourquoi cette équation · Usage dans le projet · Interprétation*. Une loi se raconte là où elle agit.

---

## Liminaires

- **Note de l'auteur**
- **Avant-propos. Pourquoi ce livre, comment le lire, ce qu'il ne promet pas**
- **Conventions de lecture**

## Partie I : la question et ses fondements

- **Chapitre 1. Introduction : la question qui exige un monde**
  - 1.1 Une question qu'on ne tranche pas en discutant
  - 1.2 Pourquoi les mondes actuels ne suffisent pas à y répondre
  - 1.3 Une troisième voie
  - 1.4 Ce que ce livre va faire, et ne pas faire
  - 1.5 Les trois promesses du livre
- **Chapitre 2. Pourquoi les mondes scriptés ne peuvent pas répondre à la question**
  - 2.1 L'illusion orchestrée : arbres de comportement et machines à états
  - 2.2 Le monde décoratif et l'agent omniscient
  - 2.3 L'IA opaque et ses quatre limites
  - 2.4 L'intuition naturaliste : des structures sans architecte
  - 2.5 Les sept principes
  - 2.6 Émergence faible : du surprenant mais analysable
- **Chapitre 2b. Décomposer la question, terme à terme**
  - 2b.1 « Environnement virtuel » : un monde sans matière, mais avec conséquences
  - 2b.2 « Entités autonomes » : décider sans recevoir d'ordres, et sans conscience
  - 2b.3 « Lois fondamentales » : peu de règles, aveugles, sans exception narrative
  - 2b.4 « Comportements et structures non explicitement programmés » : l'émergence faible comme critère
  - 2b.5 Exigences testables : la grille de lecture de tout le livre
- **Chapitre 3. Six lignées pour une question**
  - 3.1 Artificial Life
  - 3.2 Systèmes complexes
  - 3.3 Simulation multi-agents
  - 3.4 Intelligence collective
  - 3.5 Architectures cognitives BDI
  - 3.6 Agents génératifs et modèles de langage
  - 3.7 Ce que chaque lignée explique d'un choix du projet
- **Chapitre 3b. Concepts et références : ce que nous devons à l'histoire, formules à l'appui**
  - 3b.1 Émergence
  - 3b.2 Systèmes complexes
  - 3b.3 Multi-agents
  - 3b.4 Intelligence collective
  - 3b.5 BDI et utilité
  - 3b.6 Génératifs et langage
  - 3b.7 Table de référence des formules
  - 3b.8 Les formules comme engagements
- **Chapitre 4. La méthode : ne croire que ce qui se rejoue**
  - 4.1 Le code prime
  - 4.2 Le déterminisme comme contrat moral
  - 4.3 Les tests comme preuve
  - 4.4 Les campagnes rejouables
  - 4.5 Versionnage indépendant et filtre des décisions
  - 4.6 L'intégration continue et la distribution
- **Chapitre 4b. Le projet : pour construire ainsi, vision et architecture globale**
  - 4b.1 La vision en une phrase
  - 4b.2 Quatre responsabilités, quatre interdits
  - 4b.3 Architecture globale de référence
  - 4b.4 Les niveaux du Launcher
  - 4b.5 Le rôle des outils : mock et installateur
  - 4b.6 Ce que la découpe protège

## Partie II : les composants

- **Chapitre 5. SYNE : le monde qui doit pouvoir se passer de nous**
  - **SYNE I. La terre et son métabolisme** : espace, ressources, obstacles, rythmes, anatomie interne
  - **SYNE II. Comment Charlie décide de manger plutôt que de parler** : la cognition en action, de la perception à l'action
  - **SYNE III. La boucle de simulation** : tick, ordre causal, scheduler, niveau de détail
  - **SYNE IV. Persistance et reprise** : sauvegarde, chargement, reprise exacte, migrations
  - **SYNE V. Les contrats** : snapshots, événements, contrôle, compatibilité ascendante
  - **SYNE VI. Configuration et calibration** : fichiers, flags, profils, calibration du monde
  - **SYNE VII. Les systèmes collectifs** : groupes, confiance, territoires, saisons, reproduction, mort
  - **SYNE VIII. La communication par pulsations** : types de messages, dégradation, confiance
  - **SYNE IX. Les huit traits, ou ce qui fait qu'une entité n'est pas une autre** : tempéraments, plage et neutralité, modulateur de personnalité, héritage
  - **SYNE X. Les besoins : six manques, une énergie, des seuils** : dérive par tick, seuils de déclenchement, états critiques
  - **SYNE XI. Voir, se souvenir, croire : les formules d'une information faillible** : confiance perceptuelle, décroissance de la saillance, révision des croyances
  - **SYNE XII. La formule d'utilité et la prise de risque** : bénéfice, coût, risque, confiance, personnalité, urgence, sélection, hystérésis, interruption
- **Chapitre 6. ECHOS : observer sans toucher**
  - **ECHOS I. Architecture et ingestion**
  - **ECHOS II. Les métriques et le score d'émergence**
  - **ECHOS III. Analyse causale et comparaison**
  - **ECHOS IV. API, lecture seule et viabilité**
  - **ECHOS V. Les outils mathématiques partagés** : entropie de Shannon, entropie normalisée, moyenne, variance, rapport borné, borne, indice de concordance
  - **ECHOS VI. Les formules des métriques, famille par famille** : diversité cognitive, propagation de l'information, complexité sociale, convergence des objectifs, répétitions d'action, durabilité des ressources, dynamique des groupes
  - **ECHOS VII. L'indice de viabilité dynamique** : sept composantes, noyau géométrique, couverture, régimes, trajectoire, marges aux seuils, sorties publiées, phases d'implémentation et calibration
  - **ECHOS VIII. Composites, reproductibilité et causalité** : score d'émergence, complexité, phénomènes, distance entre runs, statistiques post-run, chaîne causale
- **Chapitre 7. PRISM : montrer sans décider**
  - **PRISM I. Le projet Unreal et la place du rendu**
  - **PRISM II. Le plugin PRISM-LDK** : transport, conversion, Blueprint
  - **PRISM III. L'échelle, la cadence et le transport** : facteur spatial `k`, ratio de temps simulé `R`, cycle de préparation, ce que le moteur exporte et ce que le rendu invente
  - **PRISM IV. Le flux de données et le rendu** : l'instantané comme source de vérité, construction du décor, projection, interpolation, les trois interdits du rendu
- **Chapitre 8. Le Launcher : archiver pour rejouer**
  - **Launcher I. Orchestration, supervision et réseau**
  - **Launcher II. Campagnes et paquet `.livexp`**
  - **Launcher III. Observabilité et supervision** : six niveaux d'observation, neuf états de composant, agrégation, contrat de santé, nommage des métriques
  - **Launcher IV. Le paquet, le flux de données et le réseau** : structure du paquet, manifeste et scellement, un seul écrivain, graines dérivées, plans de contrôle et de données
- **Chapitre 9. L'installateur : promettre sans affirmer**
- **Chapitre 9b. L'installateur : exigences, parcours et garanties** : exigences vérifiables, dix étapes dont six sans effet, modes d'exécution, atomicité des mises à jour

## Partie III : pratiques, preuves et limites

- **Chapitre 10. Intégration système**
- **Chapitre 11. Reproductibilité et déterminisme**
- **Chapitre 12. Les campagnes comme enquêtes**
- **Chapitre 13. Ce que cette version permet d'observer, prudemment**
- **Chapitre 14. Limites réelles**
- **Chapitre 15. Conclusion**

## Finales

- **Bibliographie et lectures : ce que ce livre doit à d'autres**
- **Annexe A. Contrats de transport**
- **Annexe B. Dictionnaire des métriques**
- **Annexe C. Architecture détaillée**
- **Annexe D. Schémas d'architecture**
- **Annexe E. Glossaire**
- **Annexe F. Décisions d'architecture (ADR)**
- **Annexe G. Chronologie du projet**
- **Annexe H. Questions de recherche détaillées**
- **Annexe I. Paramètres et bases de données**
- **Annexe J. Du prototype à la version décrite** : ce qui a été conservé, changé, abandonné, et ce que la calibration a corrigé
- **Annexe K. Benchmarks et performances** : machine de référence, résultats mesurés, planchers de non-régression, coût de l'observabilité
- **Annexe L. Feuille de route** : capacités livrées mais non activées, jalons non franchis, hors-périmètre assumé

---

> Les schémas (architecture globale, boucle de simulation, cycle de l'agent, pipeline de publication, réseau) et les formules (perception, mémoire, croyances, besoins, traits, utilité, risque, confiance, cohésion, entropie, densité, pente, viabilité) irriguent la prose. Le détail exhaustif vit dans les annexes, afin de ne pas interrompre le récit.
