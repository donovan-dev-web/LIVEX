# ADR — Institutionnalisation de l'Émergence (Boucle de Second Ordre)

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Extension d'ECHOS (Partie 4) et du Système de Croyances (§3.11), avec un point d'entrée dans SYNE
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 4, Partie 6)

---

## 1. Contexte

La définition de l'émergence de LIVEX (§6.4.1) pose 5 critères, dont le 5ᵉ : *« Le phénomène modifie les perceptions, les croyances et les décisions ultérieures »*. Ce critère est aujourd'hui satisfait **uniquement à l'échelle individuelle** : une entité qui vit un événement le mémorise (§3.10) et ajuste ses croyances (§3.11). Rien ne le satisfait à l'échelle **collective**.

Exemple directement tiré de la monographie (§6.4.2) : un marché informel apparaît à un carrefour. Aujourd'hui, ce phénomène n'existe que de deux façons :
1. Comme la somme de comportements individuels co-localisés (des entités qui, indépendamment, `Trade` régulièrement au même endroit) ;
2. Comme une mesure externe, produite par ECHOS (§4.3, moteurs de métriques ; §4.4, indicateurs d'émergence) — un observateur *hors du monde simulé*.

Aucune entité qui n'a pas personnellement participé aux échanges à ce carrefour n'a de moyen de savoir que ce lieu est devenu un marché, sauf transmission fortuite par un livre (§3.18) ou par communication directe (§3.16) — deux vecteurs qui transmettent des faits ponctuels, pas des structures reconnues comme telles. Le marché n'est perceptible ni comme objet ni comme catégorie du monde.

Conséquence : LIVEX peut produire de l'émergence *observable par nous*, mais les structures émergentes ne se sédimentent pas en histoire pour le monde lui-même. Chaque phénomène reste plat dans le temps — il n'existe pas de mécanisme équivalent à ce que l'anthropologie computationnelle appelle l'**effet de cliquet culturel** (cumulative culture) : la capacité d'une génération à partir des structures laissées par la précédente plutôt qu'à les redécouvrir ou les ignorer.

## 2. Décision

Introduire un mécanisme à deux étages, sans faire d'ECHOS un acteur causal du monde simulé (violation sinon du principe d'indépendance des modules, §2.2.2 — ECHOS doit rester indépendant de la logique comportementale, doctrine §9.6.3 point 8) :

**Étage 1 — Détection (reste dans ECHOS, inchangé dans son principe)**
ECHOS continue de détecter les phénomènes émergents via ses moteurs de métriques existants (§4.3, §4.4.2 « phénomènes auto-détectés »).

**Étage 2 — Publication comme fait perceptible dans SYNE (nouveau)**
Quand un phénomène franchit un seuil de robustesse (cohérent avec le problème de la démonstration, §6.4.5 — robuste, non artefact statistique), ECHOS **publie un objet `WorldLandmark`** dans le monde simulé — pas en modifiant directement les croyances des entités (ce qui violerait l'indépendance du module), mais en l'ajoutant comme un objet perceptible au même titre qu'une Ressource ou un Obstacle (§3.9.5) :

```
WorldLandmark:
  id
  type              // ex. TradeHub, GatheringPlace, ConflictZone
  position / zone
  detectedAtTick
  confidence         // robustesse de la détection, pas une vérité absolue
  status: Emerging | Established | Declined | Vanished
```

Une entité qui perçoit un `WorldLandmark` dans son rayon de perception (§3.9.2) en forme une croyance comme pour toute autre observation (§3.11.2) — avec sa propre confiance, sa propre dégradation. Cette croyance peut ensuite être communiquée (§3.16), transmise par un livre (§3.18), et surtout **influencer la génération d'objectifs** (§3.13.1) : une entité affamée qui croit qu'un `TradeHub` existe à proximité pourrait générer un objectif `SeekTrade` plutôt que `Gather`, alimentant directement le Means-End Reasoning (ADR liée).

## 3. Pourquoi ça ne casse pas l'indépendance des modules

Le point est subtil et mérite d'être explicite : ECHOS ne *décide* rien pour une entité — il ne fait qu'ajouter un objet au monde, exactement comme un `EnvironmentEvent` (§3.21) est ajouté par SYNE lui-même. La différence avec une violation du principe d'indépendance (§2.2.2, §9.6.3 point 8) est que :
- ECHOS ne lit ni ne modifie directement les croyances/décisions d'une entité (ça reste interdit) ;
- Le `WorldLandmark` est un fait du monde comme un autre, que les entités perçoivent, interprètent et croient (ou non) selon leur propre système de perception/croyance — l'agentivité individuelle reste intacte.

C'est l'équivalent d'un panneau planté dans le monde plutôt que d'un message injecté directement dans la tête d'une entité.

## 4. Points à trancher

1. **Seuil de publication** — à quel niveau de robustesse (§6.4.5, critère 3 : apparaît dans plusieurs runs ou sur une plage de paramètres) un phénomène détecté par ECHOS mérite-t-il de devenir un `WorldLandmark` ? Un seuil trop bas produit du bruit halluciné ; trop haut, la boucle n'a jamais d'effet observable.
2. **Fréquence de vérification** — ECHOS scanne-t-il en continu ou à intervalle (cohérent avec les fréquences adaptatives du scheduler, §3.4.2, catégorie « Faible : analyse, agrégations ») ?
3. **Cycle de vie d'un landmark** — un `TradeHub` qui n'est plus fréquenté doit pouvoir passer en `Declined` puis `Vanished`, avec une décroissance de croyance similaire à la mémoire (§3.10.3) plutôt qu'une suppression brutale.
4. **Granularité des types** — catalogue fermé de types de landmarks (cohérent avec la doctrine de primitives simples, §9.6.3 point 15) plutôt qu'une taxonomie ouverte, au moins pour V3.
5. **Risque de circularité de mesure** — si les entités commencent à agir *à cause* d'un landmark publié, ECHOS mesure-t-il encore un phénomène « émergent » (§6.4.1, critère 1 : non imposé) ou en partie un artefact de sa propre publication ? Ce point touche directement à la rigueur scientifique du projet (§6.4.5) et mérite d'être documenté comme limite méthodologique plutôt que résolu — la distinction entre émergence de premier ordre (non assistée) et de second ordre (assistée par la mémoire collective publiée) devrait être explicitement tracée dans les Decision Records et les métriques ECHOS, pour que les deux ne soient jamais confondues dans une même mesure.

## 5. Dépendances et synergies

- Consomme directement l'**ADR — Perception des Événements** : un `WorldLandmark` est structurellement un cas particulier d'objet perceptible, au même titre que le type `Event` déjà proposé.
- Alimente le **Means-End Reasoning** (ADR liée) : un landmark connu devient une source de plans candidats supplémentaires pour un objectif donné.
- Renforce indirectement les **Intentions Partagées** : un `GatheringPlace` reconnu pourrait devenir un point de ralliement naturel pour la coordination de groupe.

## 6. Conséquences

**Positives :**
- Ferme la boucle du critère 5 de la définition de l'émergence (§6.4.1) à l'échelle collective, pas seulement individuelle.
- Ouvre la possibilité d'une histoire qui se sédimente : les structures passées deviennent des conditions initiales pour les générations suivantes, condition nécessaire à une culture cumulative plutôt qu'à des émergences isolées et amnésiques.
- Donne à ECHOS un rôle actif dans la boucle scientifique du projet sans compromettre son indépendance (§2.2.2).

**Risques / coûts :**
- Risque méthodologique réel (cf. point 5) : brouiller la distinction entre émergence spontanée et émergence assistée par la publication. À documenter avec la même rigueur que les limites déjà assumées d'ECHOS (§4.10).
- Complexité d'implémentation non négligeable — probablement le plus ambitieux des ADR proposés à ce jour.
- Nécessite une classification a priori des types de landmarks pertinents, contraire à l'esprit « rien n'est prévu à l'avance » si mal calibrée — à documenter comme tension assumée plutôt qu'à nier.

## 7. Statut de la décision

[OUVERTE] — la plus structurante et la plus tardive des pistes V3 proposées à ce jour ; recommandé de l'introduire après stabilisation des ADR Perception des Événements et Means-End Reasoning, dont elle dépend directement.
