# ADR-002 : Deux modes d'utilisation de poids égal

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ADR-003-analyse-propriete-de-echos.md`
**Source Monographie** : —

---

## Contexte

SYNE est le moteur unique de LIVEX. Deux composants consomment ce moteur pour des
objectifs sans rapport : ECHOS pour l'analyse, la reproductibilité et la
calibration ; PRISM pour la représentation temps réel du monde simulé.

Cette dualité crée une ambiguïté de conception pour l'orchestrateur. Sans
décision explicite, deux dérives sont également plausibles.

1. **La dérive hiérarchique.** PRISM devient le produit visible, et ECHOS un outil
   accessoire pour l'inspecter. Ou l'inverse, si l'analyse est perçue comme le cœur
   de la recherche et l'immersion comme une démonstration. Dans les deux cas, le
   Launcher encode une hiérarchie qui n'existe pas dans le projet.
2. **La dérive de fusion.** Les deux modes sont traités comme un seul, avec une
   interface commune et des vues partagées. L'orchestrateur devient alors un
   quatrième moteur, qui consulte une sémantique scientifique pour laquelle il n'a
   pas qualité.

Le projet a par ailleurs un besoin explicite de coexistence : un chercheur peut
vouloir observer un monde pendant qu'une campagne tourne.

## Décision

**ECHOS et PRISM sont deux modes d'utilisation de SYNE, de poids égal, sélectionnables
par l'utilisateur et activables simultanément.**

Les éléments structurants :

- **Égalité de poids.** Aucun mode n'est principal, secondaire, accessoire ou de
  démonstration. Cette égalité est structurelle, pas éditoriale.
- **Sélection par l'utilisateur.** Le Launcher propose des profils, mais
  l'utilisateur peut combiner librement les modes et enregistrer sa combinaison.
- **Coexistence.** Les deux modes peuvent être actifs dans la même session, sur le
  même moteur.
- **Symétrie de traitement.** Chaque mode a son propre type de composant, son
  manifeste, ses exigences d'intégration, ses états, sa vue et sa supervision. Le
  Launcher ne symétriquise pas les traitements : il les traite selon les mêmes
  règles.
- **Mode Contrôle comme socle.** Un troisième mode, non scientifique, porte le
  paramétrage, l'exécution isolée et les diagnostics. Il est disponible dès que le
  moteur est présent.
- **Absence de hiérarchie implicite.** L'ordre de navigation ne doit pas suggérer de
  primauté. Le mode Contrôle vient en premier parce qu'il est le socle technique, et
  non parce qu'il dominerait les deux autres.

## Conséquences

### Positives
- La conception ne privilégie aucune des deux finalités du projet, ce qui évite
  d'asservir un usage aux dépens de l'autre.
- La coexistence répond à un besoin réel de recherche : observer un monde pendant
  qu'une campagne s'exécute.
- L'égalité rend le Launcher remplaçable : aucun composant n'est dans un statut
  particulier dans l'orchestration.
- La symétrie de traitement simplifie la documentation : une règle s'applique aux
  deux modes, ce qui limite les cas particuliers.

### Négatives
- Deux modes à parts égales obligent à maintenir deux jeux de vues, deux contrats
  et deux mécanismes de déverrouillage. Le coût de surface est réel.
- Le mode Contrôle, bien que socle, peut être perçu comme un troisième mode de même
  poids, ce qui brouille la distinction entre orchestration et utilisation.
- La symétrie impose de traiter PRISM avec la même rigueur documentaire qu'ECHOS
  alors qu'il n'est pas encore disponible.

### Risques
- **Hiérarchie réintroduite par l'implémentation.** Un menu, un ordre de démarrage
  ou un libellé peut réintroduire une primauté que la décision refuse.
  Mitigation : relecture de `USER_INTERFACE.md` à chaque évolution, et test
  d'interface vérifiant que les deux modes sont présentés de façon symétrique.
- **Le mode Contrôle absorbe les vues des modes.** Par souci de commodité, les vues
  d'orchestration pourraient migrer dans le mode Contrôle, qui n'a que faire de leur contenu.
  Mitigation : la règle « un mode, ses propres vues », avec exception explicite
  documentée.
- **Égalité de principe, inégalités de fait.** Un mode indisponible en pratique
  crée une égalité théorique. Mitigation : la conception complète de PRISM, afin que
  son indisponibilité soit un fait d'avancement et non un défaut de conception.
  Voir `ADR-006-prism-verrouille-en-attente.md`.

## Alternatives considérées

- **Un mode unique, avec deux vues** : refusé. Cela fond ECHOS et PRISM en un seul
  produit conceptuel, alors qu'ils répondent à des objectifs distincts, et cela
  impose au Launcher de comprendre les deux sémantiques.
- **Un mode principal, l'autre en complément** : refusé. Le projet n'identifie aucun
  mode dominant. Cette décision reviendrait à en désigner un implicitement, et à
  faire dépendre l'égalité entre les deux objectifs d'un choix arbitraire.
- **Un Launcher par mode** : refusé. Deux points d'entrée détruisent le bénéfice
  principal de l'orchestrateur, qui est la vision unifiée de l'état de la pile, et
  dupliquent la logique de cycle de vie.
- **ECHOS orchestrateur de PRISM** : refusé. Cela placerait un composant scientifique
  au-dessus d'un autre, contredisant l'égalité, et lierait l'orchestration au
  destin d'un composant mode.
- **Trois composants, PRISM sans statut particulier** : refusé. Le mode Immersion
  serait alors indistinct d'un utilitaire, alors qu'il porte une finalité de projet
  égale à celle du mode Analyse.

## Validation / rejet

- **Documentation** : `USER_INTERFACE.md` décrit les deux modes avec des sections de
  structure équivalente, et `COMPONENTS.md` §7 leur applique les mêmes règles de
  cycle de vie.
- **Interface** : le test d'interface « symétrie des modes » vérifie qu'aucun des
  deux modes n'est présenté en surbrillance permanente par rapport à l'autre.
- **Non-objectifs** : toute fonctionnalité d'analyse dans le Launcher est refusée
  au titre de `ADR-003-analyse-propriete-de-echos.md`.
- **Réouverture** : la décision est réexaminée seulement si un des deux modes
  disparaît du projet, ou si le projet désigne explicitement un mode dominant. Une
  simple difficulté d'implémentation ne suffit pas.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création | Établir l'égalité des deux modes et le mode Contrôle comme socle |
