# ADR-006 : PRISM conçu mais verrouillé

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ADR-002-modes-analyse-et-immersion-de-poids-egal.md`
**Source Monographie** : —

---

## Contexte

`ADR-002` déclare ECHOS et PRISM comme deux modes d'utilisation de poids égal du
moteur SYNE. Cette égalité crée une difficulté opérationnelle : **PRISM n'est pas
encore disponible**.

Deux erreurs symétriques sont possibles, et le projet a intérêt à les éviter toutes
les deux.

1. **Concevoir par omission.** Écrire la documentation du Launcher en traitant
   PRISM comme un composant futur lointain, avec des exigences vagues. Au moment où
   PRISM arrive, l'orchestrateur n'est pas prêt, et l'égalité de principe se révèle
   être une égalité de façade.
2. **Concevoir par fiction.** Documenter PRISM comme s'il était livré, et faire
   porter au Launcher l'illusion d'une capacité qui n'existe pas. L'utilisateur
   découvre le verrou au moment de l'usage, ce qui est la pire manière de le
   découvrir.

Une troisième voie existe : concevoir PRISM **au niveau du modèle**, avec le même
rigueur que les autres composants, sans simuler son implémentation, et en
exposant son indisponibilité de façon explicite.

## Décision

**PRISM est conçu comme un composant à part entière, au même niveau de détail que
SYNE et ECHOS, et son accès est verrouillé tant que son implémentation n'est pas
disponible.**

Les éléments structurants :

- **Conception complète.** PRISM a un type de composant, un manifeste, des exigences
  d'intégration publiées (`INTEGRATION_CONTRACT.md` §11), des états, un cycle de
  vie, un profil et une vue spécifiée (`USER_INTERFACE.md` §3.3).
- **Verrouillage d'accès, pas de non-conception.** Le verrou concerne la
  sélection du mode, pas la conception. `COMPONENTS.md` §7.3 décrit le mode
  Immersion au même titre que les deux autres.
- **Verrou visible et motivé.** L'entrée de navigation existe, porte un cadenas et
  affiche la raison ainsi que le jalon attendu. Elle n'est jamais simplement inerte.
- **Verrou évalué, pas codé en dur.** Le verrou est la conséquence d'une condition
  d'implémentation, évaluée au démarrage et à chaque changement de profil. Il n'est
  pas une constante du code.
- **Règle conservée la plus restrictive.** Aucun composant n'est redémarré
  automatiquement, y compris en vue du déverrouillage. Voir `OBSERVABILITY.md` §8.
- **Condition de levée.** Le déverrouillage est conditionné par la disponibilité
  réelle de PRISM et par la satisfaction de ses exigences d'intégration, pas par une
  date.

## Conséquences

### Positives
- L'égalité entre les deux modes est réelle dès la conception, et non un objectif de façade.
- Au moment où PRISM est livré, son intégration est une formalité : le modèle, le
  contrat et la vue existent déjà.
- L'utilisateur comprend la situation au lieu de la découvrir à l'usage.
- Le verrou ne crée pas de dette : il la porte explicitement.

### Négatives
- Le Launcher implémente une fonctionnalité d'interface qui n'est pas utilisable, ce
  qui est un coût incompressible.
- La documentation doit maintenir une description d'un composant qui n'existe pas,
  avec le risque de diverger de son implémentation réelle.
- Le jalon de déverrouillage est hors du contrôle du Launcher : il dépend de PRISM,
  donc le Launcher ne peut pas garantir un calendrier.

### Risques
- **Le verrou s'éternise.** Un verrou sans propriétaire identifié devient un
  oubli. Mitigation : le jalon attendu est affiché, et le déverrouillage est
  conditionné, non daté, pour que la condition reste la seule voie de sortie.
- **La conception de PRISM diverge de son implémentation réelle.** Le modèle
  documenté pourrait ne pas correspondre à ce que PRISM offrira.
  Mitigation : les exigences de `INTEGRATION_CONTRACT.md` §11 sont des exigences
  d'orchestration, techniquement minimales, à rejouer à l'arrivée de PRISM plutôt
  qu'à supposer sa forme finale.
- **Le mode Immersion est perçu comme un simple placeholder.** Mitigation :
  `USER_INTERFACE.md` §3.3 décrit une vue complète, et `COMPONENTS.md` §7.3 énumère
  les règles du verrou pour montrer qu'il s'agit d'un état conçu.

## Alternatives considérées

- **Ne pas documenter PRISM tant qu'il n'est pas livré** : refusé. L'égalité
  proclamée par `ADR-002` deviendrait fausse, et le Launcher ne serait pas prêt à
  l'accueillir.
- **Documenter PRISM comme livré, sans verrou** : refusé. L'interface prometrait une
  capacité absente, ce qui est une faute plus grave qu'un verrou visible.
- **Retirer le mode Immersion jusqu'à l'arrivée de PRISM** : refusé. Cela
  contredirait `ADR-002` et ferait de l'égalité de poids un simple voeu théorique.
- **Verrouiller PRISM au niveau du Launcher, par une constante** : refusé. Un
  verrou codé en dur survit à la livraison du composant, ce qui produirait un mode
  définitivement inaccessible et un défaut difficile à diagnostiquer.
- **Masquer complètement le mode** : refusé. Un composant absent peut se lire comme
  un composant abandonné. Le mode visible et motivé distingue les deux.

## Validation / rejet

- **Complétude de conception** : PRISM dispose d'un profil (`COMPONENTS.md` §9), de
  exigences (`INTEGRATION_CONTRACT.md` §11), d'une vue (`USER_INTERFACE.md` §3.3) et
  d'une matrice de tests (`TESTING.md` §7). Ces quatre éléments sont revus à chaque
  évolution du Launcher.
- **Verrou motivé** : l'interface affiche la raison et le jalon attendu. Le test
  d'interface « mode verrouillé » vérifie que cette raison est présente et exacte.
- **Verrou évalué** : la condition de verrouillage est une propriété testable, pas
  une branche de code figée.
- **Levée** : à l'arrivée de PRISM, ses exigences d'intégration sont rejouées, et le
  déverrouillage n'a lieu qu'après satisfaction complète. Un déverrouillage
  anticipé est un défaut.
- **Réouverture** : la présente décision est réexaminée à la livraison de PRISM. Elle
  ne l'est pas pour un simple retard, qui ne change rien à la conception.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création | Concevoir PRISM au niveau du modèle tout en signalant son indisponibilité |
