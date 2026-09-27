# Documentation d'intégration SYNE → Unreal

Cette documentation décrit le contrat consommé par un plugin Unreal minimal,
dont la logique d'intégration est exposée à Blueprint autant que possible.

## Parcours recommandé

1. Lire [UNREAL_PLUGIN.md](UNREAL_PLUGIN.md) pour l'architecture et la
   séparation entre le petit noyau C++ et les usages Blueprint.
2. Lire [CONTRACT_REFERENCE.md](CONTRACT_REFERENCE.md) pour les payloads
   réellement émis par `syne-mock`.
3. Suivre [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) pour
   l'implémentation, les tests et les scénarios de reconnexion.

## Source de vérité

En cas de divergence, appliquer cet ordre :

1. le code et les tests de ce dossier (`src/` et `test/`) pour le comportement
   du mock ;
2. les contrats SYNE partagés dans [`docs/docs-syne/`](../../docs/docs-syne/)
   pour les champs et événements du moteur réel ;
3. la présente documentation pour le mapping Unreal.

Le plugin doit ignorer les champs JSON inconnus et conserver un événement
générique pour les types non encore mappés. Les chaînes reçues doivent rester
accessibles afin de permettre l'évolution additive du contrat sans recompilation
immédiate du jeu.

## Principe Unreal

Le plugin ne doit pas recréer le moteur SYNE. Il fournit uniquement :

- une connexion WebSocket et des commandes HTTP ;
- une file thread-safe entre le transport et le game thread ;
- des `USTRUCT(BlueprintType)`, `UENUM(BlueprintType)` et delegates ;
- un stockage du dernier snapshot et des événements reçus.

La projection visuelle, les Actors, les Local Blueprints, l'interface et la
logique de gameplay restent dans le projet Unreal.
