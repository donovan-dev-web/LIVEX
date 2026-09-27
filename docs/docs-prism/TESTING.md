# TESTING — PRISM / PrismLdk

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`ARCHITECTURE.md`](ARCHITECTURE.md), [`TRANSPORT_API.md`](TRANSPORT_API.md)

---

## 1. Objectif

Valider que PRISM intègre et compile le plugin PRISM-LDK (`PrismLdk`), expose les contrats attendus à Blueprint et
transmet correctement les messages SYNE sans introduire de logique de
simulation concurrente.

## 2. Vérifications

| Niveau | Vérifications |
| :-- | :-- |
| Build Unreal | Compiler PRISM-LDK depuis l'hôte technique et depuis le projet Unreal PRISM |
| API Blueprint | Vérifier l'accès au subsystem, les types/enums, les dispatchers et les fonctions de contrôle attendus |
| Transport | Vérifier connexion/déconnexion, perte inattendue, reconnexion configurée, JSON invalide et erreur HTTP |
| Contrats | Vérifier l'ordre `world_initialized` avant snapshots, parsing de snapshots/deltas/événements et conservation de valeurs inconnues |
| Cycle de vie | Vérifier `Prepare` → réception du monde → `Ready` → résultat positif → `Start`, ainsi que pause/reprise/arrêt/reset/statut |
| Projet PRISM | Vérifier que la scène reste cohérente avec les données initiales puis les snapshots, sans doubler les mutations |

Les résultats réseau sont asynchrones ; vérifier les callbacks `OnControlResult`
et `OnError`, et ne pas considérer un appel Blueprint comme confirmation de
succès avant le retour correspondant.

## 3. Usage de syne-mock pour les tests de développement

`syne-mock` est utile pour tester les connexions et l'intégration Blueprint
sans exécuter SYNE. Il permet de parcourir les routes, les transitions de
contrôle et les messages qu'il implémente. Son comportement est déterministe
dans les limites de son modèle, mais ses délibérations et systèmes sociaux
sont approximatifs ; son détour local d'obstacle n'est pas l'A* de SYNE, et
ses trajectoires ne sont pas garanties bit à bit. Le replay rejoue les
messages enregistrés sans restaurer l'état interne de SYNE.

Par conséquent :

- le mock peut valider une liaison de transport ou un rendu à partir des
  données émises par lui ;
- il ne prouve pas l'équivalence des décisions, trajectoires ni résultats de
  simulation ;
- les contrats et tests finaux doivent aussi être validés avec SYNE réel.

Référence : [`../../syne-mock/README.md`](../../syne-mock/README.md).

## 4. Critères d'acceptation

- Le projet Unreal PRISM compile avec le plugin PRISM-LDK activé.
- Le flux préparé respecte l'ordre prévu et les erreurs de cycle de vie sont
  visibles côté Blueprint.
- Un snapshot complet actualise l'état affiché ; événements et deltas ne
  doublent pas ses mutations.
- Les données inconnues restent diagnostiquables plutôt que converties
  silencieusement en état simulé valide.
- Les tests de comportement décisionnel et de trajectoire sont exécutés contre
  SYNE, jamais inférés du seul succès du mock.
