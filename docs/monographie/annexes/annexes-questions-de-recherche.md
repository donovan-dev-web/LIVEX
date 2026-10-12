# Annexe H. Questions de recherche détaillées

Cette annexe formule explicitement ce que le projet cherche à savoir. Elle rassemble la question directrice, un ensemble de questions secondaires, et la façon dont chacune serait considérée comme traitée. Elle est volontairement séparée du récit : un livre qui explique un système doit aussi dire, à un endroit précis, ce qu'il attend d'une réponse.

## 1. La question directrice

> **Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?**

Cette question est celle qui a donné naissance à LIVEX. Elle n'est pas une question d'ingénierie mais une question de possibilité : elle demande si la chose est faisable, pas comment la réaliser au mieux. Elle comporte quatre termes, chacun exigeant sa propre précision, ce qui justifie le chapitre de décomposition de la question placé dans la première partie du livre.

Une réponse positive à cette question n'aurait pas la forme d'un oui. Elle aurait la forme d'un dispositif : un monde dont les lois sont peu nombreuses, aveugles et sans exception narrative ; des entités dont la décision procède d'un calcul et non d'un script ; des comportements traçables, rejouables, et néanmoins non écrits d'avance. C'est exactement ce que le livre décrit.

## 2. Questions secondaires

### Q1. Émergence

> Dans un monde minimal et persistant, observe-t-on des phénomènes émergents révélant des structures complexes issues d'entités relativement simples ?

- **Enjeu** : c'est la question fondatrice. Elle conditionne toutes les autres.
- **Méthode envisagée** : comparaison des runs à des seuils définis, avec plusieurs critères concordants plutôt qu'un score unique.
- **Ce qui a été observé dans le prototype** : une population minimale a produit une division du travail sans consigne globale.
- **Statut** : non tranché. L'observation du prototype est une **piste**, non un résultat établi : elle n'a pas été reproduite sur une campagne complète, avec conditions contrôlées et analyse de sensibilité.

### Q2. Mémoire et persistance

> La reprise exacte du monde est-elle un gage de fiabilité des résultats observés, ou seulement un critère technique ?

- **Enjeu** : déterminer si le déterminisme sert la science ou l'ingénierie.
- **Méthode envisagée** : comparer des runs identiques avec persistance complète, avec mémoire partielle et sans mémoire.
- **Hypothèse issue du prototype** : la persistance stabiliserait le comportement sur les runs longs, au prix d'une diversité d'issues plus faible.
- **Statut** : non tranché. La reprise bit-à-bit est **implémentée et vérifiée** ; son effet sur la complexité observée n'a pas été mesuré par une campagne dédiée.

### Q3. Communication imparfaite et diversité

> La communication imparfaite (confiance, dégradation, incompréhension) produit-elle une diversité d'actions supérieure à une communication parfaite ?

- **Enjeu** : c'est la question méthodologique du projet. Si l'imperfection augmentait la diversité, elle ne serait pas un défaut à corriger mais un moteur à préserver.
- **Méthode envisagée** : comparer des runs à communication parfaite et à dégradation croissante.
- **Hypothèse issue du prototype** : la communication imparfaite augmenterait la diversité comportementale et réduirait l'homogénéité.
- **Statut** : non tranché. C'est la question la plus directement testable avec les instruments actuels, puisque la dégradation par saut et l'incompréhension sont des paramètres publiés.

### Q4. Montée à l'échelle

> Au-delà de plusieurs centaines d'entités, l'émergence observée est-elle un artefact de la petite taille, ou un phénomène robuste ?

- **Enjeu** : une propriété qui disparaît quand la population grandit n'est pas une propriété du monde, mais de l'échelle.
- **Méthode envisagée** : augmenter la population à paramètres constants, et vérifier la tenue du budget de temps par tick.
- **Objectif chiffré issu du prototype** : tenir une dizaine de ticks par seconde à mille entités, condition d'une analyse statistique robuste.
- **Statut** : non tranché. Le budget de tick est mesuré, la robustesse de l'émergence ne l'est pas.

## 3. Évaluation des questions

| Question | Enjeu | Priorité |
| :-- | :-- | :-- |
| Q1. Émergence | fondamental : définit le projet | haute |
| Q2. Mémoire et persistance | technique : détermine la fiabilité des résultats | haute |
| Q3. Communication imparfaite | méthodologique : influence la variabilité des observations | moyenne |
| Q4. Montée à l'échelle | pratique : conditionne la faisabilité | haute |

## 4. Ce qu'une réponse exigerait

Ces questions ont en commun une même exigence de méthode, rappelée dans la partie consacrée aux campagnes. Une réponse ne peut pas reposer sur un run spectaculaire. Elle exige : une hypothèse formulée avant l'observation, un dispositif reproductible (même graine, même version), une comparaison entre conditions, une mesure assortie de sa provenance, et l'exposition des cas où le résultat attendu n'apparaît pas. C'est ce que le projet appelle « ne croire que ce qui se rejoue ».

Il faut souligner une conséquence souvent oubliée : une **absence de détection n'est pas une absence du phénomène**. Si une structure émerge mais qu'aucune métrique ne la capture, le dispositif conclura à tort qu'il n'y a rien. C'est pourquoi le catalogue des métriques est publié, ses trous sont assumés, et le livre prend soin de dire, pour chaque famille de mesures, ce qu'elle ne permet pas de voir.

## 5. Une question qui n'est pas posée

Il faut préciser, pour éviter un malentendu fréquent, une question que le projet **ne pose pas** : celle de savoir si les entités sont conscientes, si elles souffrent, ou si elles « veulent » quelque chose au sens humain. Le projet n'affirme rien de tel, et ses instruments ne pourraient rien en dire. La question directrice porte sur des comportements et des structures observables, non sur une expérience intérieure. Cette restriction n'est pas un confort : c'est ce qui rend la question testable.
