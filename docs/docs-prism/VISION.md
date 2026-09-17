# VISION.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../VISION.md` (racine)
**Source Monographie** : §5.1

---

## Rôle

**PRISM** — *Perceptual Rendering & Interactive Simulation Module* — est la couche qui rend le monde **perceptible et interactif**. Il représente graphiquement l'état fourni par SYNE et fournit les moyens de navigation, de caméra, d'inspection et d'interaction.

**PRISM est un reflet du monde simulé, jamais un co-auteur de la simulation.** Il observe, il affiche, il permet d'interagir — il ne décide pas.

## Ce que PRISM fait

1. Rendu du monde (sol, obstacles, environnement).
2. Représentation des entités (entités, ressources).
3. Caméra et navigation de l'utilisateur.
4. Représentation des constructions et territoires (futur).
5. Inspection d'une entité (croyances, besoins, décisions).
6. Affichage de données ECHOS (métriques, phénomènes).
7. Outils de debug visuel.
8. Interaction utilisateur.
9. Préparation du futur mode joueur-habitant.

## Ce que PRISM ne doit PAS faire

- **Posséder l'état canonique** d'une entité.
- **Calculer les règles sociales**.
- **Déterminer la vérité d'une croyance**.
- **Modifier directement le monde** sans passer par les mécanismes prévus par SYNE.
- **Introduire des comportements** non présents dans le modèle de simulation.

## Pourquoi un framework intermédiaire

Unreal Engine et Unity ont chacun leur organisation des objets, composants, scènes, physique et logique de gameplay. Si le modèle de simulation était construit sur les abstractions d'un moteur graphique, le projet serait lié à ce moteur pour toujours.

```text
Modèle LIVEX (SYNE)
    ↓ adaptation
PRISM (framework intermédiaire)
    ↓ bindings
Godot / Unity / Unreal (moteur graphique)
```

Le moteur graphique **définitif reste volontairement ouvert** : si le moteur change (Godot → Unity → Unreal), seuls les **adaptateurs** changent, pas le modèle de simulation.

## Principe invariant (quel que soit le moteur)

- **Réflexion passive** : PRISM n'expose jamais de décisions, il reflète l'état du moteur.
- **Commandes relayées** : le contrôle du moteur passe par l'API HTTP de SYNE.
- **Modèle propre** : LIVEX conserve son propre modèle de données, indépendant du moteur graphique.

---

## Points restés ouverts dans ce document
- Aucun : les interdits et le principe invariant sont des contraintes fermes.