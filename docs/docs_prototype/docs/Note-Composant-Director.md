# Note de Réflexion — Composant "Director" (Entité Supérieure Narrative)

**Statut :** [RÉFLEXION] — idée pour une phase future du projet, volontairement non formalisée en ADR à ce stade
**Portée envisagée :** Nouveau composant, potentiellement au même niveau que SYNE/ECHOS/PRISM
**Prérequis explicite :** Simulation de base stable, émergence caractérisée et mesurable (ECHOS mature), avant tout entraînement
**Document parent :** LIVEX — Monographie Générale
**Auteur :** Donovan Chartrain — notes de conversation à conserver pour reprise ultérieure

---

## 1. Idée de départ

Une fois LIVEX stable et son émergence bien caractérisée, ajouter un quatrième composant — une IA entraînée par deep learning / RL — dont le rôle serait de **piloter la simulation** en créant des événements globaux ou locaux (catastrophes naturelles, épidémies, apparition d'agents prédateurs, etc.) pour maintenir la simulation intéressante et faire émerger des comportements originaux.

Principe fondateur, formulé dès le départ et jamais remis en cause dans la discussion : ce composant **ne doit jamais contrôler directement une entité ni forcer un scénario** — il crée des conditions qui *pourraient* faire émerger une direction particulière, sans jamais garantir ni scripter l'issue. Ce sont toujours les entités elles-mêmes qui décident de réagir ou non, via leur propre boucle BDI.

## 2. Cadrage théorique (pour ne pas repartir de zéro le moment venu)

Ce concept correspond à des notions déjà étudiées ailleurs, ce qui donne des pistes de lecture utiles avant implémentation :

- **AI Director** (Left 4 Dead) — module qui gère rythme et intensité en modifiant les conditions (apparitions, ressources), sans jamais contrôler un personnage individuellement. Référence la plus proche du principe de contrôle indirect visé ici.
- **Drama Manager / Experience Manager** — terme académique (Façade, Mateas & Stern ; travaux de Riedl & Bulitko) pour un système qui pilote une expérience narrative sans scripter les acteurs.
- **God games** (Populous, From Dust, et surtout **Black & White**) — précédent le plus proche de l'axe "moralité perçue" évoqué plus bas ; Black & White avait un "belief meter" villageois directement piloté par les actions du joueur-dieu. Vaut le coup de regarder des post-mortems de ce jeu avant de concevoir l'axe moralité.
- **Design d'environnement non supervisé** (unsupervised environment design — POET, PAIRED) — cadre RL le plus proche de l'architecture à deux niveaux envisagée ici : un système génère des défis, un autre (la population, via l'ExperienceMod) s'y adapte, les deux évoluant en tension sans scénario prédéfini.
- **Leverage points** (Donella Meadows) — cadre pour penser les "causes faibles à grandes conséquences" : chercher les points du système où une petite perturbation produit un effet disproportionné, plutôt qu'agir avec force directement à l'endroit voulu.
- **Goodhart's Law / reward hacking** — risque central identifié : toute métrique unique utilisée comme cible finit par être exploitée sans produire ce qu'elle était censée mesurer. Discuté en détail en §4 ci-dessous.

## 3. Position architecturale

- Le composant s'insérerait dans la formule de transition d'état déjà existante (§6.2.1) : `S(t+1) = F(S(t), événements, actions, environnement, hasard contrôlé)`. Le terme "événements" est aujourd'hui rempli par un générateur scripté/aléatoire (§3.21) ; ce composant en serait une **source supplémentaire**, plus intelligente, pas un nouveau concept dans la formule.
- Canal d'injection réutilisé : le Système d'Environnement (§3.21, `EnvironmentEvent`) et, si implémenté, le mécanisme de publication `WorldLandmark` (ADR Institutionnalisation de l'Émergence).
- **Rupture à documenter explicitement** : SYNE/ECHOS/PRISM suivent aujourd'hui un flux à sens unique (SYNE produit, ECHOS/PRISM observent, jamais l'inverse — §2.2.2). Ce composant serait le premier à écrire dans SYNE depuis l'extérieur. À ne pas traiter comme une entorse silencieuse — prévoir des garde-fous explicites : fréquence d'intervention plafonnée, catalogue fermé de types d'événements injectables, jamais d'accès direct aux croyances/décisions d'une entité (même principe que pour l'ADR Institutionnalisation).
- ECHOS deviendrait, de facto, la **fonction de récompense** de ce composant — pas seulement un outil d'observation scientifique pour l'auteur.

## 4. Le problème central : définir "intéressant"

Identifié comme *le* point dur, comparable au "reward specification problem" de la recherche en Drama Management — aucune définition formelle de "intéressant" n'existe qui résiste à l'optimisation directe (elle serait exploitée/gamée).

**Piège identifié :** entraîner directement sur le score d'émergence composite (§4.4.1) pousserait le système vers un chaos permanent qui fait grimper les métriques de variance sans produire de cohérence narrative — Goodhart's law appliqué à la narration.

**Critère négatif utile, déjà présent dans la philosophie du projet (§1) :** un événement spectaculaire mais causalement intraçable (dans les Decision Records / moteur d'analyse causale d'ECHOS, §4.3) ne devrait pas compter comme "intéressant", même avec un bon score de variance brut.

### Pistes de composantes de récompense à combiner (aucune seule ne suffit)

1. **Impact contrefactuel** — ratio effet produit / ampleur de l'intervention (comparaison à une trajectoire de référence non perturbée), plutôt qu'un effet brut.
2. **Traçabilité causale** — l'effet doit rester reconstructible via les Decision Records et le moteur d'analyse causale (§4.3, §3.14.12) ; un effet massif mais opaque est pénalisé.
3. **Propagation inter-systèmes** — un événement intéressant touche plusieurs sous-systèmes à la fois (besoins ET relations ET groupes), pas un seul indicateur isolé.
4. **Persistance au-delà de l'absorption immédiate** — durée pendant laquelle l'écart à la baseline reste mesurable, plutôt qu'un pic digéré en quelques ticks.
5. **Nouveauté par rapport aux interventions passées** — pénaliser la répétition du même type d'événement dans des contextes similaires (réutilisation possible de `TraitExpressionDiversity`, §4, appliquée cette fois à l'historique des interventions).
6. **Contrainte de viabilité (borne dure, pas un terme de récompense)** — empêcher les optima dégénérés du type extermination totale d'une population pour le seul spectacle.

**Position réaliste retenue :** ces signaux servent d'amorçage pour l'entraînement initial ; une calibration par retour humain périodique (l'auteur note "intéressant / pas intéressant" sur des runs observés) reste probablement nécessaire pour ajuster les poids — une forme de RLHF appliquée à ce composant plutôt qu'à un modèle de langage.

## 5. Les trois axes de mesure/entraînement proposés par l'auteur

En plus des critères ci-dessus, trois facteurs structurants ont été proposés pour cadrer l'entraînement :

### 5.1 Économie de ressources liée à l'impact

Chaque action du composant est rare ; plus l'événement créé a de conséquence/impact mesurable, plus le composant "gagne" de ressource lui permettant d'agir à nouveau. Effet recherché : forcer le composant à arbitrer s'il doit agir ou non, plutôt que d'agir en continu.

**Analyse :** c'est la pièce la plus solide de la proposition — elle transforme la récompense en contrainte structurelle plutôt qu'en simple signal d'entraînement (un gaspillage sur une intervention insignifiante s'auto-punit mécaniquement, sans pénalité artificielle à coder). Elle rend aussi le composant lui-même *tension-driven*, dans la même logique conceptuelle que les besoins des entités (§3.12.2, "les besoins sont des tensions") — cohérence avec le reste de la doctrine plutôt qu'un système greffé à part.

### 5.2 Facteur de favoritisme envers des groupes/idéologies

Un facteur par groupe mesurant si le composant favorise statistiquement un groupe plutôt qu'un autre.

**Point tranché dans la discussion :** distinguer deux échelles de temps plutôt qu'un facteur unique —
- **Favoritisme instantané/local (par événement)** : peut être fortement asymétrique, c'est ce qui crée le drame (ex. une catastrophe avantage un groupe et désavantage l'autre).
- **Favoritisme cumulé sur fenêtre longue** : doit rester borné — contrainte d'entraînement garantissant qu'aucun groupe n'est structurellement toujours celui qui subit, sans empêcher l'asymétrie ponctuelle qui fait l'intérêt narratif.

### 5.3 Moralité perçue

Mesurer si les entités considèrent ce composant comme bénéfique ou maléfique.

**Décision de conception actée dans la discussion :** ne PAS donner aux entités de croyance directe envers une "entité supérieure" nommée (option jugée trop scriptée, en rupture avec l'architecture bottom-up du reste du projet). La moralité perçue doit rester un **phénomène émergent**, construit uniquement via les systèmes déjà existants :
- Une catastrophe frappe → les entités en forment des croyances individuelles (§3.11).
- Elles en discutent (§3.16) et la transmettent/déforment via les Livres (§3.18, dégradation type "téléphone arabe" déjà modélisée, §3.16.6).
- Un mythe collectif ("le dieu maléfique de l'est") se construit progressivement, sans variable cachée injectée — mesurable après coup par ECHOS, au même titre qu'un `WorldLandmark` (ADR Institutionnalisation de l'Émergence).

**Risque explicitement identifié :** entraîner *directement* sur la maximisation de la moralité perçue pousserait le composant à ne plus jamais prendre de risque narratif (distribuer uniquement de l'abondance) — retour au problème de reward hacking de §4. Conclusion actée : le favoritisme et la moralité perçue doivent rester des **variables observées et affichées** (utiles pour l'auteur/le joueur/la narration), pas des composantes de récompense à maximiser directement — tout au plus une contrainte de viabilité (pas d'extermination totale pour le spectacle).

## 6. Complémentarité avec le RL individuel (ExperienceMod)

Décision actée : ce composant ne remplace ni ne concurrence l'apprentissage individuel des entités (ExperienceMod, ADR déjà formalisée) — les deux fonctionnent en complémentarité, formant un système à deux niveaux d'apprentissage couplés, proche du design d'environnement non supervisé (POET/PAIRED, cf. §2).

**Effet secondaire positif identifié, obtenu sans code supplémentaire :** si l'ExperienceMod est actif, le critère de "nouveauté" (§4, point 5) s'auto-applique en partie — une intervention répétée dans un contexte similaire produit un impact mesuré plus faible, non pas parce qu'une pénalité de répétition est codée, mais parce que la population a réellement appris entre-temps (table Q mise à jour) à mieux répondre à ce type d'événement. Complexité émergente de deux systèmes simples en interaction, cohérent avec la doctrine générale du projet.

**Point de vigilance identifié pour plus tard (à observer empiriquement, pas à anticiper en théorie) :** dynamique de "course aux armements" implicite entre les deux niveaux —
- Si les entités apprennent vite (taux α de l'ExperienceMod élevé), le composant devra être plus inventif avec le temps pour continuer à produire de l'impact — dynamique narrativement souhaitable.
- Si l'ExperienceMod converge trop bien et trop vite (population qui "résout" toutes les situations connues), le composant risque de manquer de leviers efficaces et d'être poussé vers des extrêmes juste pour maintenir un signal d'impact — à surveiller une fois les deux systèmes opérationnels ensemble.

## 7. Ce qui reste explicitement ouvert (non tranché, pour la reprise future)

1. Catalogue fermé de types d'interventions disponibles pour le composant.
2. Fréquence maximale d'intervention (probablement très faible, cohérent avec la fréquence "analyse" de §3.4.2).
3. Poids relatifs exacts des composantes de récompense de §4, et méthode de calibration humaine périodique.
4. Comment un opérateur humain donnerait un "scénario cible" au composant sans que ça devienne du contrôle direct déguisé.
5. Mécanisme concret de mesure du favoritisme cumulé (fenêtre temporelle, seuil de déséquilibre acceptable).
6. Granularité de la mesure de "moralité perçue" une fois émergente — comment ECHOS la détecterait et la quantifierait sans l'imposer.

## 8. Prérequis avant toute implémentation

Rappel du point de départ de cette réflexion, jugé non négociable dans la discussion : la simulation de base doit être stable et son émergence caractérisée et mesurable (ECHOS mature, score de robustesse fiable, §6.4.5) **avant** tout entraînement de ce composant — sans quoi il est impossible de distinguer "le composant a produit un effet intéressant" de "le système de base produit déjà ça tout seul" ou "le système de base ne produit jamais rien d'exploitable". Même risque méthodologique de circularité de mesure identifié pour l'ADR Institutionnalisation de l'Émergence.

## 9. Dépendances avec les ADR déjà formalisées

- **ExperienceMod** — complémentarité directe (§6 ci-dessus).
- **Perception des Événements** — canal de perception que les entités utiliseraient pour réagir aux interventions du composant.
- **Institutionnalisation de l'Émergence** — mécanisme de publication (`WorldLandmark`) potentiellement réutilisable pour l'injection d'événements, et cadre méthodologique déjà posé pour le risque de circularité de mesure.
- **Livres / Engagements Communicationnels** — vecteurs par lesquels la moralité perçue se construirait et se transmettrait culturellement.

---

*Document de réflexion, non formalisé en ADR à la demande explicite de l'auteur — à reprendre et détailler lorsque le projet atteindra la phase de stabilisation complète mentionnée en §8.*
