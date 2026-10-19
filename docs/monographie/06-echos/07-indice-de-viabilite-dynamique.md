# ECHOS VII. L'indice de viabilité dynamique

Un monde peut être mesuré de mille façons et rester sans verdict. Ce chapitre décrit l'**indice de viabilité dynamique** (DVI), le cadre mathématique qui répond à une question unique : à partir de l'état actuel et de l'histoire récente du monde, celui-ci possède-t-il les propriétés nécessaires pour rester durablement actif, diversifié, organisé, capable de se renouveler et de résorber ses crises ?

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande si un monde peut produire des comportements non programmés. Elle ne demande pas si ce monde est **viable**. Or ces deux questions sont liées : un monde qui s'éteint au tick 3 000 ne permet d'observer presque rien ; un monde figé produit des comportements triviaux ; un monde chaotique produit du bruit, pas des structures. L'indice de viabilité est donc la condition de possibilité de l'observation : il ne prouve aucune émergence, il établit que le monde se trouvait dans un régime où une observation a un sens. C'est un instrument de contexte, pas de verdict.

## 1. Le régime recherché : une zone, pas un maximum

La formulation de l'indice contient une décision de fond. Un monde vivant n'est ni **figé** ni **chaotique** : il oscille dans une zone d'activité soutenue mais bornée. L'indice ne cherche donc pas à maximiser l'activité, mais à mesurer la **distance à une zone optimale**.

Cette distinction a une conséquence immédiate : un monde immobile et un monde survolté doivent tous deux obtenir un score faible. Un indice bâti sur un volume d'activité aurait récompensé le chaos ; un indice bâti sur la stabilité seule aurait récompensé la mort lente. C'est la raison de la forme en cloche décrite ci-dessous.

**Rapport à l'indice d'émergence.** L'indice de viabilité ne remplace pas le score d'émergence composite (ECHOS VIII) : les deux cohabitent, et le second reste le composite implémenté et publié. Leurs questions diffèrent.

| | Score d'émergence | Indice de viabilité |
| :-- | :-- | :-- |
| Question posée | Quelles structures observe-t-on à ce tick ? | Le monde est-il dans un régime dynamique viable ? |
| Agrégation | Somme pondérée arithmétique | Moyenne géométrique pondérée |
| Compensation | Totale (une composante haute compense une basse) | Interdite (une composante à zéro annule le noyau) |
| Activité | Non modélisée | Composante centrale, avec zone optimale |
| Régénération, résilience, persistance | Absentes | Composantes dédiées |

Le fait que l'indice ne consomme pas le score d'émergence est délibéré : les deux composites lisent les mêmes métriques primitives, mais les agrègent avec des sémantiques différentes.

## 2. Sept composantes

Chaque composante est bornée sur `[0, 1]` et peut être **non mesurée** à un tick donné. Une composante non mesurée n'est jamais un zéro.

### 2.1 𝓐 : activité optimale

**Explication globale** : Mesure si le niveau d'activité est proche d'une zone cible, ni figé ni chaotique. C'est la composante qui pénalise explicitement les mondes immobiles **et** les mondes hyperactifs.

```math
a_{\mathrm{act}} = \frac{1}{T} \sum_{t=1}^{T} \frac{|\{a : \mathrm{act}_t(a) \neq \mathrm{Idle}\}|}{\max(|\mathcal{A}_t|, 1)} \qquad
a_{\mathrm{dec}} = \frac{D}{N \cdot T} \qquad
a_{\mathrm{evt}} = \frac{E}{N \cdot T}
```

```math
\hat{A} = \frac{w_a \min(1, a_{\mathrm{act}}/r^*_{\mathrm{act}}) + w_d \min(1, a_{\mathrm{dec}}/r^*_{\mathrm{dec}}) + w_e \min(1, a_{\mathrm{evt}}/r^*_{\mathrm{evt}})}{w_a + w_d + w_e}
```

```math
\mathcal{A} = \exp\!\left(-\frac{(\hat{A} - A^*)^2}{2\,\sigma_A^2}\right)
```

**Pourquoi cette forme** : La cloche exponentielle traduit la contrainte centrale : le régime viable est une **zone**, pas un maximum. `Â` tendant vers zéro (figé) et `Â` très supérieur à `A*` (débordement) produisent tous deux `𝓐 → 0`. La normalisation par des repères fixes, et non par le maximum observé dans la fenêtre, évite de masquer les dérives lentes : un monde qui perd peu à peu son activité voit `Â` décroître, au lieu d'être re-normalisé à 1.

### 2.2 𝓓 : diversité

Mesure la diversité à trois niveaux : croyances, objectifs, actions.

```math
\mathcal{D} = \frac{w_b \mathcal{D}_{c} + w_g \mathcal{D}_{g} + w_a \mathcal{D}_{a}}{w_b + w_g + w_a}
```

Les trois sous-mesures sont les entropies normalisées décrites au chapitre précédent. Le choix de trois niveaux se justifie par leur orthogonalité : un monde peut avoir des croyances identiques et des objectifs divers, ou l'inverse. La diversité est une **condition nécessaire** et non suffisante : c'est pourquoi elle entre dans un noyau géométrique plutôt que de faire score à elle seule.

### 2.3 𝓒 : structure

Mesure l'organisation du réseau d'interactions : clustering, densité, couverture communautaire.

```math
\mathcal{C} = \frac{w_c \mathcal{C}_{\mathrm{clustering}} + w_d \mathcal{C}_{\mathrm{densité}} + w_k \mathcal{C}_{\mathrm{communautés}}}{w_c + w_d + w_k}
```

Les trois se lisent ensemble : un réseau dense mais sans clustering est chaotique ; un clustering élevé sur un sous-ensemble minuscule est une structure partielle.

### 2.4 𝓖 : renouvellement

Mesure la capacité à produire de la nouveauté.

```math
r_G = \frac{E^*}{N \cdot T} \qquad
\mathcal{G} = \min(1,\; r_G / r^*_G) \qquad
\mathrm{balance} = \frac{\nu + 1}{\delta + 1}
```

Le ratio par `N · T` rend le taux comparable entre tailles de population et durées de fenêtre. Le `min(1, ·)` borne sans pénaliser l'excès : un renouvellement au-delà du repère n'est pas un défaut, contrairement à l'activité. La **balance** entre créations (`ν`) et destructions (`δ`) est volontairement **hors du noyau** : elle informe l'interprétation (« crée-t-on autant qu'on détruit ? ») sans contaminer l'indice par une grandeur redondante.

### 2.5 𝓡 : résilience

Part des crises observées qui ont été résorbées.

```math
\mathcal{R} = \frac{R_{\mathrm{ep}}}{R_{\mathrm{ep}} + R_{\mathrm{un}}} \quad \text{si } R_{\mathrm{ep}} + R_{\mathrm{un}} > 0
```

**Pourquoi l'absence de crise est un cas non mesuré** : si aucune crise n'est détectée dans la fenêtre, le ratio n'est pas défini. Lui attribuer `0` (résilience nulle) ou `1` (résilience parfaite) induirait en erreur : un monde sans crise observée peut être robuste, ou simplement n'avoir jamais été éprouvé. La règle est donc « non mesuré », et l'affichage doit le dire. Un point de méthode accompagne cette composante : l'observatoire **n'injecte aucune perturbation**. La résilience est mesurée sur les crises **spontanées**, uniquement.

### 2.6 𝓟 : persistance

Stabilité des structures dans le temps : alignement des tailles de communautés et âge des objectifs.

```math
\mathrm{alignement}_{\mathrm{tailles}} = \exp\!\left(-\frac{\mathrm{Var}(\{s_j\})}{\bar{s}^{\,2}}\right) \qquad
\mathrm{persistance}_{\mathrm{obj}} = \min(1,\; G_{\mathrm{âge}} / T)
```

**Pourquoi la dispersion relative** : l'exponentielle de `−CV²` (le carré du coefficient de variation) mesure l'homogénéité des communautés **indépendamment de l'échelle** : des groupes de 5, 5, 5 et 5 persistent aussi bien que des groupes de 500. Une variance brute aurait favorisé mécaniquement les petites communautés.

### 2.7 𝓢 : stabilité

Régularité de l'activité au fil de la fenêtre.

```math
a_t = \frac{|\{a : \mathrm{act}_t(a) \neq \mathrm{Idle}\}|}{\max(|\mathcal{A}_t|, 1)} \qquad
\sigma_a^2 = \frac{1}{T} \sum_{t=1}^{T} (a_t - \mu_a)^2 \qquad
\mathcal{S} = \exp\!\left(-\frac{\sigma_a^2}{\sigma_V^2}\right)
```

Attention à ne pas confondre cette composante avec la stabilité statique : un monde figé a une variance d'activité nulle, donc `𝓢 = 1`. Ce n'est pas une récompense, c'est une propriété distincte, le figé est pénalisé par `𝓐`, pas ici. `𝓢` mesure la régularité d'une activité **qui existe**.

## 3. Le noyau géométrique, et pourquoi pas une somme

```math
W_M = \sum_{i \in M} w_i \qquad
V = \exp\!\left(\frac{1}{W_M} \sum_{i \in M} w_i \ln(X_i)\right)
```

avec `M` l'ensemble des composantes **mesurées** et `X_i` leurs valeurs. Si une composante mesurée vaut zéro, le score vaut zéro.

**Pourquoi une moyenne géométrique** : la moyenne géométrique est strictement concave sur l'orthant positif, et **aucune compensation n'y est possible**. Une composante à zéro annule le noyau, quelles que soient les autres. C'est voulu : un monde qui ne renouvelle rien ou qui n'a aucune activité n'est pas « partiellement viable », il manque une propriété **nécessaire**. Une somme arithmétique aurait permis à une composante haute de masquer une composante nulle, et le score aurait affiché une viabilité là où une propriété manquait. La forme exponentielle évite par ailleurs les indéfinitions (`ln 0`) : un seul zéro rend le score nul sans erreur.

**Couverture du noyau.** Plusieurs composantes peuvent être non mesurées au même tick. Le noyau n'est alors calculé que sur les composantes mesurées, avec renormalisation des poids.

```math
\kappa = \frac{W_M}{\sum_{i=1}^{7} w_i}
```

`κ = 1` signifie un score complet ; `0 < κ < 1`, un score **partiel** qui doit être affiché avec sa couverture ; `κ = 0`, aucun score, et l'on n'affiche alors jamais `0`, mais « non mesuré ». Un score partiel ne doit pas être comparé à un score complet sans mentionner `κ`.

## 4. Les régimes

Le régime est une catégorie discrète dérivée du score **et** de la stabilité de l'activité.

```math
\mathrm{ViabilityRegime} =
\begin{cases}
\text{UNKNOWN} & \text{si } V = \mathrm{null} \\
\text{CRITICAL} & \text{si } V < s_1 \\
\text{WEAK} & \text{si } s_1 \le V < s_2 \\
\text{MODERATE} & \text{si } s_2 \le V < s_3 \\
\text{STRONG} & \text{si } s_3 \le V < s_4 \\
\text{OPTIMAL} & \text{si } V \ge s_4 \\
\text{STATIC} & \text{si } \mathcal{A} \text{ mesuré et } \mathcal{A} < a_{\mathrm{static}} \\
\text{CHAOTIC} & \text{si } \mathcal{S} \text{ mesuré et } \mathcal{S} < s_{\mathrm{chaotic}}
\end{cases}
```

Le régime condense la lecture pour une interface sans la remplacer : le **profil complet** des composantes reste la source. Deux règles de non-double-comptage s'appliquent : la pénalisation du monde figé vient **uniquement** de `𝓐`, et celle du monde chaotique **uniquement** de `𝓢`. Aucune composante n'est multipliée deux fois, et le noyau géométrique agrège chaque défaut une seule fois. `UNKNOWN` n'est jamais assimilé à `CRITICAL` : l'absence de mesure n'est pas une mauvaise nouvelle, c'est une absence.

## 5. La couche post-run : trajectoire et marges

Le moteur fournit un indice en direct, borné à une fenêtre courte. La couche post-run répond à deux questions que le direct ne peut pas trancher : le monde s'améliore-t-il, et à quelle distance se trouve-t-il des seuils de crise ?

```math
\beta(S) = \frac{\sum_t (t - \bar{t})(S(t) - \bar{S})}{\sum_t (t - \bar{t})^2} \qquad
\mathrm{Trajectoire} =
\begin{cases}
\text{IMPROVING} & \Delta_{\mathrm{pop}} > +\varepsilon_p \text{ et } \Delta_{\mathrm{act}} > +\varepsilon_a \\
\text{DEGRADING} & \Delta_{\mathrm{pop}} < -\varepsilon_p \text{ ou } \Delta_{\mathrm{act}} < -\varepsilon_a \\
\text{STABLE} & \text{sinon} \\
\text{NOT\_MEASURED} & \text{séries indisponibles}
\end{cases}
```

La pente est une régression des moindres carrés ordinaires, le même outil que celui de la calibration : déterministe, sans paramètre appris. La trajectoire est calculée par comparaison de tiers du run (début et fin), pour éviter de surinterpréter le bruit tick par tick.

Une règle de composition est explicite : **la trajectoire conditionne l'interprétation du score, elle ne le modifie jamais.** Le multiplicateur du type « score × (1 + λ·pente) » est rejeté : il masquerait le score mesuré derrière une correction opaque. Le moteur publie son score tel quel ; le post-run publie des séries et des diagnostics séparés.

Les **marges aux seuils de crise** complètent la résilience : celle-ci dit **si** les crises passées ont été résorbées, celles-là disent **à quelle distance** on se trouve d'une nouvelle crise, en ticks, sur les seuils de `_CRITICAL_RATIO = 0,2`.

## 6. Statut, phases d'implémentation et publication

L'indice est **spécifié**, mais son implémentation n'est pas attestée. La date envisagée auparavant — avant la publication de ce livre — n'a pas valeur de preuve : le relevé du code du 18 octobre 2026 ne trouve ni moteur `ViabilityIndicators` ni métriques DVI publiées. L'endpoint de viabilité existant expose les séries sources et le rapport de calibration, sans score dynamique synthétique. Le présent chapitre est donc une référence de spécification et non la description d'un mécanisme en service; les phases ci-dessous sont un plan de travail, pas des livraisons acquises.

L'implémentation est découpée en trois phases, chacune produisant une valeur publiable et testable, sans casser les contrats existants.

### Phase 1. Le moteur de fenêtre courte

Un moteur `ViabilityIndicators` s'ajoute au registre, après les indicateurs d'émergence. Il calcule les sept composantes sur l'instantané, dans une fenêtre d'au plus cent ticks, en consommant les résultats des moteurs déjà en place et les replis de l'instantané. Il publie le score, la couverture, le régime, les sept composantes, la balance de renouvellement et l'avertissement. Les tests attendus portent sur des valeurs de référence par composante, sur les cas limites (`κ = 0` donnant un score nul non mesuré, une composante à zéro annulant le noyau), sur l'absence de crise pour la résilience et sur le déterminisme strict. Contrainte explicite : ni le moteur d'émergence ni l'endpoint de viabilité existant ne sont modifiés.

### Phase 2. Les séries et la couche post-run

Les séries du score et des composantes sont écrites tick par tick dans les tables de métriques, puis la couche post-run fournit la trajectoire, les pentes par moindres carrés, les marges aux seuils de crise et les avertissements. Les tests portent sur des séries synthétiques de forme connue (croissante, décroissante, plate), sur le cas « non mesuré » d'une série trop courte et sur l'émission des avertissements selon les seuils.

### Phase 3. Le contrat et la calibration

Les sorties sont exposées par une interface conforme aux règles de lecture du paragraphe suivant, avec couverture et avertissement systématiques et sans synthèse opaque. La campagne de calibration ajuste les constantes sur un jeu de scénarios de référence et publie les résultats avec leurs intervalles. L'analyse de sensibilité fait varier chaque poids et mesure l'impact sur le score et le régime. Une décision reste ouverte jusqu'à cette phase : le score d'émergence reste le composite publié **tant que** la calibration ne démontre pas un gain de stabilité ou d'interprétabilité. Il n'est pas remplacé par principe.

| Aspect | Statut |
| :-- | :-- |
| Spécification mathématique | complète |
| Composantes | spécifiées, implémentation en phase 1 |
| Constantes | proposées et **héritées**, non calibrées |
| Séquences et trajectoire | implémentation en phase 2 |
| Interface et calibration | phase 3 |
| Score d'émergence | inchangé, composite publié jusqu'à preuve contraire |

Cette honnêteté n'est pas un détail administratif : elle détermine la façon de lire le reste du livre. Le DVI n'étant pas implémenté dans le code vérifié, il ne peut actuellement ni **juger** un run ni **décrire** son régime. Les séries sources de l'endpoint existant restent consultables, mais ne sont pas l'indice spécifié ici.

## 7. Sorties publiées et règles de lecture

### 7.1 Ce que le moteur publie à chaque tick d'analyse

| Sortie | Type | Mesuré | Rôle |
| :-- | :-- | :-- | :-- |
| `ViabilityScore` | fraction `[0,1]` ou absent | oui | Noyau géométrique |
| `ViabilityCoverage` | fraction `[0,1]` | oui | Couverture `κ` |
| `ViabilityRegime` | catégorie | oui | Régime observé |
| `ViabilityActivity` | fraction `[0,1]` | si 𝓐 mesuré | Composante 𝓐 |
| `ViabilityDiversity` | fraction `[0,1]` | si 𝓓 mesuré | Composante 𝓓 |
| `ViabilityStructure` | fraction `[0,1]` | si 𝓒 mesuré | Composante 𝓒 |
| `ViabilityRenewal` | fraction `[0,1]` | si 𝓖 mesuré | Composante 𝓖 |
| `ViabilityResilience` | fraction `[0,1]` ou repli | si crise observée | Composante 𝓡 |
| `ViabilityPersistence` | fraction `[0,1]` | si 𝓟 mesuré | Composante 𝓟 |
| `ViabilityStability` | fraction `[0,1]` | si 𝓢 mesuré | Composante 𝓢 |
| `ViabilityTurnoverBalance` | ratio sans unité | si fenêtre mesurable | Balance créations/destructions |
| `ViabilityTrajectory` | catégorie | post-run | Trajectoire |
| `ViabilityDisclaimer` | texte invariant | toujours | Règle d'or |

### 7.2 Six règles de lecture obligatoires

1. Toujours afficher `κ` à côté du score.
2. Toujours afficher le profil des sept composantes : le score seul ne suffit jamais.
3. Distinguer un zéro **mesuré** d'un zéro de repli ; afficher « non mesuré, aucune crise observée » plutôt que `0,0` pour la résilience sans crise.
4. Afficher l'avertissement tel quel.
5. Ne jamais afficher un score sans ses constantes versionnées ni sa date de génération.
6. Ne jamais traiter la trajectoire comme un multiplicateur du score.

### 7.3 Contrat d'interface

L'endpoint de viabilité existant reste **inchangé** : il publie les séries sources, sans agrégat synthétique. L'indice s'ajoute comme **moteur de métriques** (ses sorties sont publiées par tick dans les tables de métriques) et comme **méthode post-run**. Toute interface future qui l'exposerait devra respecter les six règles ci-dessus. Les métriques de sortie de l'indice seront ajoutées au catalogue au moment de l'implémentation, avec un statut exploratoire tant que la calibration n'a pas eu lieu.

Un point de conception mérite d'être souligné : **aucune métrique primitive nouvelle n'est inventée**. Sept composantes et un score sont nouveaux ; tout le reste est emprunté aux moteurs déjà en place (diversité cognitive, complexité sociale, dynamique des groupes, durabilité des ressources) et aux replis de l'instantané. Une composante de l'indice se lit donc toujours comme une agrégation de métriques déjà publiées, jamais comme une boîte noire.

## 8. Constantes héritées, poids et calibration

### 8.1 Constantes proposées

| Constante | Symbole | Défaut | Rôle |
| :-- | :-- | --: | :-- |
| Activité cible | `A*` | 0,5 | Centre de la zone optimale |
| Demi-largeur de zone | `σ_A` | 0,25 | Largeur de la cloche 𝓐 |
| Repère d'activité | `r_act*` | 0,8 | Normalisation de la part d'entités actives |
| Repère de décisions | `r_dec*` | 2,0 | Normalisation des décisions par entité et par tick |
| Repère d'événements | `r_evt*` | 0,5 | Normalisation des événements par entité et par tick |
| Repère de renouvellement | `r_G*` | 0,1 | Normalisation de 𝓖 |
| Variance de référence | `σ_V` | 0,05 | Pénalité de variabilité 𝓢 |
| Seuil figé | `a_static` | 0,1 | Drapeau STATIC |
| Seuil chaotique | `s_chaotic` | 0,05 | Drapeau CHAOTIC |
| Seuils de régime | `s₁..s₄` | 0,15 / 0,30 / 0,50 / 0,70 | Graduation |
| Seuil optimal | `s₄` (second) | 0,85 | OPTIMAL |
| Bruit de pente | `ε` | 0,001 | STABLE contre croissant ou décroissant |
| Marge fine | `w_rel` | 0,2 | Avertissement de marge mince |

Les seuils de régime apparaissent deux fois dans la spécification (0,70 puis 0,85) : c'est un point de revue signalé par le document lui-même, et il est cité ici pour que le lecteur ne le découvre pas comme une incohérence cachée.

### 8.2 Poids des composantes

| Composante | Poids | Justification |
| :-- | --: | :-- |
| 𝓐 Activité optimale | 0,20 | propriété fondatrice du régime dynamique |
| 𝓓 Diversité | 0,15 | condition de robustesse cognitive |
| 𝓒 Structure | 0,15 | organisation nécessaire à la diffusion |
| 𝓖 Renouvellement | 0,15 | capacité de production de nouveauté |
| 𝓢 Stabilité | 0,15 | régularité du régime |
| 𝓡 Résilience | 0,10 | diagnostic de sortie de crise, souvent non mesuré |
| 𝓟 Persistance | 0,10 | durée des structures |

### 8.3 La calibration, sans apprentissage

L'indice ne contient **aucun modèle appris**. La calibration consiste à choisir des constantes déterministes sur un jeu de scénarios de référence (monde stable, chaotique, figé, en crise, en récupération, en dérive lente), puis à les publier et les versionner.

Le signal de vérité observée n'est ni un étiquetage humain arbitraire ni un modèle : c'est l'issue réellement constatée du run (extinction, survie, dérive documentée). La procédure est un balayage de paramètres rejoué de façon identique sur tout le jeu, et ses critères sont publiés : séparation des régimes sur les scénarios de référence, stabilité entre exécutions, et sensibilité bornée. Une calibration réussie **remplace** les défauts par des valeurs mesurées, publiées avec la version du moteur ; à défaut, les défauts restent utilisables et l'indice reste exploratoire.

L'analyse de sensibilité fait varier chaque poids de ±20 % et mesure l'impact sur le score et sur le régime. Un poids dont la variation provoque souvent un basculement de régime est signalé comme **critique** et discuté dans les limites du composant. Aucune pondération n'est optimisée par gradient : la sensibilité est un diagnostic d'interprétabilité, pas une boucle d'apprentissage.

## 9. Limites connues et risques d'interprétation

| Limite | Impact | Atténuation |
| :-- | :-- | :-- |
| Constantes non calibrées | seuils et poids indicatifs | publier `κ` et le profil, statut exploratoire, calibrer en phase 3 |
| Résilience souvent non mesurée | son poids quitte le noyau, `κ` varie d'un tick à l'autre | afficher `κ`, croiser avec les marges |
| Aucune perturbation injectée | un monde jamais éprouvé n'a pas de résilience | afficher « aucune crise observée », jamais un score |
| Fenêtre courte du moteur | les tendances lentes sont invisibles | consulter la couche post-run pour toute décision |
| Aucune preuve scientifique | un indice élevé ne prouve ni vie ni société | avertissement invariant |
| Compensation interdite | un score nul ne dit pas **quelle** propriété manque | profil obligatoire |
| Échantillonnage des agents | diversité et activité estimées sur échantillon | mentionner l'échantillon dans les interfaces |
| Plafond d'événements | la fenêtre peut tronquer les événements | la métadonnée publie le plafond |
| Trajectoire non agrégée | un score élevé avec trajectoire dégradante surprend | afficher la trajectoire à côté du score |

## 10. Ce que ce chapitre apporte à la question directrice


Ce chapitre a posé la condition de possibilité de toute observation : un monde qui ne s'éteint pas, qui ne se fige pas et qui ne part pas en chaos. Il l'a fait avec une forme mathématique qui porte une thèse : la viabilité n'est pas un maximum, c'est une **zone**, et elle exige **toutes** ses propriétés à la fois, sans compensation. C'est ce que dit la moyenne géométrique. Un monde qui renouvelle mais ne diversifie rien n'est pas viable à moitié : il lui manque une propriété nécessaire.

En liant la mesure à des propriétés nécessaires plutôt qu'à un volume, l'indice oblige aussi à distinguer ce qui est **observé** de ce qui est **mesuré**. La couverture `κ` et la règle « zéro observé n'est pas non mesuré » sont les garde-fous qui empêchent un instrument incomplet de produire une conclusion complète.

**Ce que ce chapitre ne permet pas de conclure.**

- Le DVI n'est **pas implémenté** dans le code vérifié le 18 octobre 2026 : les sorties de viabilité à sept composantes ne sont pas publiées. L'endpoint existant expose des séries sources et diagnostics observés; ce chapitre décrit la spécification et un plan d'implémentation, pas une fonctionnalité livrée.
- Les constantes sont héritées et non calibrées : les seuils de régime et la zone optimale ne sont pas des normes.
- L'indice ne prouve rien sur l'émergence : un monde viable n'est pas un monde qui produit des structures nouvelles, seulement un monde où l'on peut espérer en observer.
- L'observatoire n'injecte aucune perturbation : la résilience n'est mesurée que sur les crises spontanées, ce qui rend la composante dépendante de ce que le monde a bien voulu produire.
