# VISION — PRISM

**Composant** : PRISM
**Implémentation actuelle** : projet Unreal PRISM intégrant le plugin PRISM-LDK (`PrismLdk`)
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`../../VISION.md`](../../VISION.md)

---

## Rôle

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM** (*Perceptual Rendering & Interactive Simulation Module*)
est son projet Unreal final pour rendre le monde perceptible et interactif.
PRISM intègre **PRISM-LDK** (*LIVEX Development Kit*), plugin Unreal dont le
nom technique de module est `PrismLdk`. LDK désigne le plugin, pas un projet
complet distinct. Le fichier `prism/LDK/LDK.uproject` fourni dans ce checkout
est un hôte technique de build/test ; il ne représente pas un produit LIVEX
séparé.

PRISM reflète l'état de SYNE ; **il n'est jamais co-auteur de la simulation**.

## Responsabilités

- Se connecter aux interfaces de transport SYNE et convertir les messages en
  contrats/types Unreal utilisables depuis Blueprint.
- Exposer l'état de connexion, les commandes de contrôle et les événements de
  transport en Blueprint.
- Permettre à PRISM d'afficher le monde et d'inspecter les informations
  reçues et de présenter les résultats de SYNE.
- Garder la présentation et les conventions de rendu dans le projet Unreal
  PRISM, plutôt que de faire du plugin un jeu ou une simulation autonome.

## Frontière avec SYNE

SYNE reste l'autorité pour l'état canonique du monde, les décisions, les
actions et l'évolution des entités. Le plugin ne doit pas :

- posséder un état de simulation concurrent ou prendre des décisions à la
  place de SYNE ;
- recalculer les règles de comportement, les croyances ou la vérité du monde ;
- modifier directement le monde simulé : les commandes passent par l'API de
  contrôle SYNE ;
- transformer les projections visuelles Unreal (NavMesh, hauteur, animation)
  en vérité simulée ou les renvoyer comme état autoritaire.

## Conception du plugin

Le C++ de `PrismLdk` doit rester mince. Il sert de couche d'intégration :
contrats, `USTRUCT`/`UENUM`, conversion du transport, fonctions Blueprint,
état de connexion et dispatchers d'événements. Le rendu, les acteurs, les
widgets, les choix d'expérience et toute logique propre au produit relèvent du
projet Unreal PRISM. Toute logique de décision et l'état canonique restent
dans SYNE.

```text
Projet complet LIVEX
  ├─ SYNE : moteur décisionnel et état autoritaire
  ├─ ECHOS : observation et analyse
  └─ PRISM : projet Unreal final
       ├─ présentation, monde, acteurs, UI et interactions
       └─ PRISM-LDK / PrismLdk : plugin d'intégration
            ├─ contrats et types exposés à Blueprint
            ├─ réception WebSocket SYNE
            └─ commandes HTTP vers SYNE
```

Les détails d'intégration sont dans [`ARCHITECTURE.md`](ARCHITECTURE.md) et
[`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md).
