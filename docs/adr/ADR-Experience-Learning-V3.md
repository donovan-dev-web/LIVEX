# ADR — Système d'Apprentissage Expérientiel (ExperienceMod)

**Statut :** [OUVERT] — piste documentée pour V3, non implémentée
**Portée :** Extension du Système de Décision (§3.14), sans modification du Système de Mémoire (§3.10)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3, Partie 9.4)

---

## 1. Contexte

Le Système de Décision actuel (§3.14) calcule l'utilité d'une action selon :

```
Utility = f(Benefit, Cost, Risk, Confidence, Urgency, PersonalityMod)
```

`PersonalityMod` pondère déjà ce calcul selon les 8 traits de personnalité de l'entité (§3.7.4), mais ce poids est **statique** : il ne change pas avec l'expérience vécue par l'entité. Deux entités aux mêmes traits, placées dans des situations différentes, produiront toujours la même politique de décision relative.

La roadmap V3 (§9.4.1) envisage déjà des extensions comme la reproduction et un adaptateur LLM conversationnel. Cette ADR documente une piste complémentaire : permettre à une entité d'ajuster ses propres décisions futures en fonction du résultat de ses décisions passées — un apprentissage individuel, distinct de la mémoire épisodique (§3.10) et de la transmission par livres (§3.18).

## 2. Décision

Ajouter un terme **`ExperienceMod`** au calcul d'utilité :

```
Utility = f(Benefit, Cost, Risk, Confidence, Urgency, PersonalityMod, ExperienceMod)
```

`ExperienceMod` est dérivé d'une **table Q tabulaire par entité** (pas de réseau de neurones), mise à jour par une règle de Q-learning standard :

```
Q(state, action) ← Q(state, action) + α × [reward + γ × max(Q(state', a')) − Q(state, action)]
```

- **state** : représentation simplifiée du contexte (besoin dominant + catégorie de situation perçue — pas l'état brut du monde, cohérent avec l'observabilité partielle, §6.11)
- **action** : type d'action au sens de §3.15.2 (pas les paramètres fins)
- **reward** : delta de satisfaction des besoins (§3.12) + progression vers un objectif actif (§3.13) mesuré après exécution de l'action
- **α, γ** : taux d'apprentissage et facteur d'actualisation, constants (pas appris), donc paramétrables comme le reste (§3.27)

## 3. Pourquoi tabulaire et pas du deep RL

Ce choix découle directement de la doctrine existante (§9.6.3) :

| Exigence doctrine | Ce que le RL profond casserait | Ce que le Q-learning tabulaire préserve |
|---|---|---|
| Point 5 — décisions traçables | Poids de réseau non interprétables | Chaque `Q(state, action)` est un nombre inspectable, loggable dans le Decision Record (§3.14.12) |
| Point 15 — primitives simples | Architecture réseau complexe | Une table + une règle de mise à jour |
| §3.25 — déterminisme | Entraînement par descente de gradient stochastique, non-reproductible facilement | Mise à jour déterministe ; exploration pilotée par le PRNG xoshiro256\*\* déjà seedé (§3.6.3) → reproductible bit-à-bit comme le reste du moteur |
| §2.2.2 — indépendance des modules | Dépendance forte à un framework ML externe | Implémentable nativement en C#, sans dépendance lourde |

L'exploration (ε-greedy) utilise le même PRNG que le reste de SYNE : aucune perte de la garantie de reproductibilité bit-à-bit (§3.23.5), contrairement à l'adaptateur LLM conversationnel qui, lui, est explicitement noté comme entraînant une perte de déterminisme (§9.4.1).

## 4. Points à trancher (à ajouter à la liste des 30 décisions à figer, §9.6.4)

1. **Granularité de l'état** — arbitrage entre taille de la table (mémoire, temps d'apprentissage) et pouvoir de généralisation. Un état trop fin ne convergera jamais à l'échelle d'une vie d'entité.
2. **Taille maximale de la table Q par entité** — même logique de plafond que la mémoire (§3.10.5, 1000 entrées) ; au-delà, stratégie d'éviction à définir (LRU ? entrées les moins visitées ?).
3. **Fenêtre d'attribution du reward** — certaines actions ont un effet différé (ex. construire, écrire un livre) ; il faut définir sur combien de ticks on rattache un changement de besoin à l'action qui l'a causé.
4. **Explicabilité** — `ExperienceMod` doit apparaître comme composante distincte et signée dans le Decision Record (§3.14.12), au même titre que `PersonalityMod`, pour rester audit-able.
5. **Transmission intergénérationnelle** — hériter partiellement de la table Q d'un parent lors de la fusion consentie (§6.6.2/6.6.3) constituerait une forme d'apprentissage culturel individuel, à distinguer explicitement :
   - de la mémoire (savoir épisodique, §3.10)
   - des livres (savoir explicite transmissible, §3.18)
   - de l'héritage des traits de personnalité (§3.7.4)
6. **Isolation architecturale** — le système doit rester un sous-composant optionnel/désactivable du Système de Décision, jamais une dépendance dure de SYNE, conformément au principe d'indépendance des modules (§2.2.2).
7. **Interaction avec l'hystérésis** — le mécanisme anti-oscillation existant (§3.14.10) doit être revérifié : un `ExperienceMod` mal calibré pourrait le contredire ou l'amplifier.

## 5. Alternatives considérées

- **Réseau de neurones (deep RL)** — rejeté pour cette étape : perte d'explicabilité, dépendance à un framework, risque sur le déterminisme (entraînement) — cohérent avec le rejet de l'IA neuronale pour le cœur du moteur (§8.8.2).
- **Bandit contextuel gelé** (évoqué en amont de cette ADR) — reste une option pour un scope encore plus restreint (ajuster un seul paramètre, ex. `decayRate` de mémoire) ; le Q-learning tabulaire proposé ici est plus général car il couvre le choix d'action lui-même, pas un seul paramètre.
- **Statu quo (PersonalityMod statique seul)** — ne répond pas au besoin exprimé d'un apprentissage individuel par l'expérience.

## 6. Conséquences

**Positives :**
- Ouvre une forme d'individuation comportementale au-delà des traits de personnalité fixes — deux entités aux mêmes traits divergeront selon leur vécu.
- Reste compatible avec tous les invariants du moteur (déterminisme, explicabilité, indépendance des modules).
- Synergie naturelle avec la reproduction (V3) via l'héritage partiel de table Q.

**Risques / coûts :**
- Complexité additionnelle du Decision Record.
- Risque de comportements dégénérés si `reward` est mal défini (ex. sur-optimisation d'un seul besoin au détriment des autres).
- Charge mémoire par entité à borner (cf. point 2).

## 7. Statut de la décision

Cette ADR reste **[OUVERTE]** — elle documente une piste architecturalement compatible avec la doctrine V0.1/V1/V2, prévue pour intégration en V3 aux côtés de la reproduction et de l'adaptateur LLM conversationnel (§9.4.1). Aucune implémentation n'est engagée à ce stade.
