# ADR-005 : Exécution séquentielle et mono-espace en V0.1

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ADR-004-format-de-paquet-livexp.md`
**Source Monographie** : —

---

## Contexte

Une campagne comporte plusieurs runs reproductibles. Deux dimensions de freedom
structurent leur exécution : le nombre de runs menés **en parallèle**, et le nombre
d'espaces de travail **vivant simultanément**.

Ces deux dimensions sont doubles coûts pour un composant dont la valeur est
l'orchestration : elles multiplient les états à suivre, les cas de reprise, les
conflits de ressources, et la surface de conception de l'interface.

Or ces deux dimensions ne sont pas définies par un besoin de recherche connu. Le
besoin réel est qu'une campagne de plusieurs runs soit reproductible, reprenable et
partageable. La parallélisation et le multi-espace sont des commodités
d'utilisation, non des exigences scientifiques.

LeLauncher est par ailleurs un composant neuf, dont le format de paquet n'est pas
encore éprouvé. Construire simultanément la générique de l'exécution parallèle et
le format de données rendrait les deux difficiles à valider.

## Décision

**En V0.1, les runs d'une campagne s'exécutent séquentiellement et le Launcher gère
un seul espace de travail.**

Les éléments structurants :

- **Séquentialité.** Un run n'est jamais lancé tant que le précédent n'est pas
  terminé. Un seul moteur est actif pour toute la campagne.
- **Mono-espace.** Une seule pile de composants, un seul profil actif, une seule
  campagne active à la fois. Ouvrir une seconde campagne demande de clore la
  précédente.
- **Ordonnancement stable.** Les runs sont numérotés et exécutés dans l'ordre
  numérique, ce qui rend la séquence d'exécution lisible dans le journal de paquet.
- **Graine par run.** `graine(n) = baseSeed + n` par défaut. Chaque run est
  reproductible isolément, sans référence à la campagne entière.
- **Report des fonctionnalités.** La parallélisation et le multi-espace sont des
  évolutions prévues, non livrées. Elles ne sont ni interdites, ni partiellement
  amorcées.

## Conséquences

### Positives
- La reprise est simple : un seul run peut être actif, donc un seul run à relancer.
- Le journal de paquet se lit comme une liste, ce qui le rend vérifiable à l'œil.
- Le format de paquet est validé dans un cadre à faible concurrence, où chaque
  anomalie est attribuable.
- L'interface n'a qu'un espace de campagne à représenter, ce qui évite les fenêtres
  multiples et la gestion de focus complexe.
- Les cibles de performance de `TESTING.md` §11 sont atteignables, un seul moteur
  tournant à la fois.

### Négatives
- Une campagne de plusieurs dizaines de runs peut être longue : le temps total est
  la somme des temps unitaires, sans recouvrement.
- L'utilisateur ne peut pas lancer deux campagnes en parallèle sur deux sujets de
  recherche distincts.
- L'utilisateur ne peut pas conserver plusieurs sessions de travail ouvertes, par
  exemple pour comparer deux profils.
- La réactivité de l'interface pendant un run long est un point de vigilance, car le
  même poste exécute la simulation.

### Risques
- **La demande de parallélisation apparaît vite.** Un premier volume de campagne
  réel rend le délai séquentiel supportable. Mitigation : la décision est
  explicitement provisoire, le modèle de données ne comporte aucun élément qui
  l'interdise, et la voie est tracée dans `ROADMAP.md`.
- **Le multi-espace est demandé pour comparer deux profils.** Sans lui, la
  comparaison suppose de changer de profil, ce qui interrompt la session.
  Mitigation : le besoin est tracé dans `ISSUES.md`, avec la remarque qu'il porte
  peut-être sur un outil d'observation, pas sur l'orchestrateur.
- **La réactivité se dégrade sur poste modeste.** Mitigation : la boucle
  d'orchestration est asynchrone, aucune attente bloquante n'est sur le fil de
  l'interface, et les cibles de `TESTING.md` §11 sont vérifiées en campagne de
  stabilité de 72 heures.

## Alternatives considérées

- **Exécution parallèle en V0.1** : refusé. Elle multiplie les cas de reprise, exige
  un ordonnancement par créneaux, rend le journal de paquet non séquentiel, et
  entre en concurrence directe avec la validation du format de paquet.
- **Multi-espace en V0.1** : refusé pour la même raison, avec un coût supplémentaire
  de conception d'interface et de gestion de session.
- **Un nombre fixe de runs, imposé à la création** : refusé. Le nombre de runs doit
  rester une propriété de la campagne, pas une constante du Launcher, afin de ne pas
  figer une limite arbitraire.
- **Aucune borne, même en séquentiel** : refusé. Sans borne, un paquet peut croître
  au-delà de ce qui reste partageable. Le nombre de runs est donc borné par le
  format, et non par l'orchestrateur.
- **Exécution parallèle facultative, désactivée par défaut** : refusé. Une
  fonctionnalité à moitié applicable, dont le comportement change selon un
  paramètre, coûte plus à concevoir et à tester qu'une fonctionnalité absente.

## Validation / rejet

- **Modèle de données** : le modèle du paquet ne comporte aucun élément propre à la
  concurrence, et aucun test ne suppose l'exécution simultanée de deux runs. La
  levée du verrou ne demandera donc pas de migration de format, mais une
  extension.
- **Interface** : un seul espace de campagne est affiché, sans gestion de focus
  multiple.
- **Journaux** : la séquence des événements de `journal.ndjson` est strictement
  ordonnée, ce qui est vérifié par un test de propriété.
- **Réouverture** : la décision est réexaminée dès qu'un besoin réel de parallélisation
  ou de multi-espace est établi et documenté. Le déclencheur est un besoin, non un
  calendrier.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création | Livrer un format validé avant d'introduire la concurrence |
