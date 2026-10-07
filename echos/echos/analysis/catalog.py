"""Catalogue versionné des métriques ECHOS (P1 — RAPPORT §8, §6.3).

Source unique de vérité **documentaire** : ECHOS reste propriétaire des
définitions et du calcul ; le Launcher lit ce catalogue et se contente de le
restaurer (formatage, axes, filtrage). Aucun score scientifique n'est recalculé
depuis ce module — il ne contient que des chaînes et des métadonnées.

Contrat (``/api/metrics/catalog``) :

============  =========================================================
Champ         Signification
============  =========================================================
``id``        identifiant stable (clé de contrat public)
``engine``    moteur producteur
``label``     libellé français affiché à l'utilisateur
``unit``      unité (``fraction``, ``bits``, ``ticks``, ``count``, …)
``domain``    plage mathématique théorique
``definition`` ce qui est compté/observé — et ce qui ne l'est pas
``calculation`` formule, dénominateurs et hypothèses
``population`` dénominateur (ce qui est divisé par quoi)
``window``    fenêtre d'observation
``direction`` lecture d'une hausse, sans en faire une cause
``status``    ``measured`` | ``exploratory`` | ``suspended``
``states``    états de données possibles (provenance par point)
``warning``   principal biais ou limite
``visual``    forme de visualisation recommandée / à éviter
``renamedFrom`` anciens identifiants (migration des séries historiques)
============  =========================================================

États de données (vocabulaire commun, P1) :

- ``observed_zero`` — zéro observé dans une fenêtre publiée ;
- ``window_empty`` — fenêtre publiée mais sans occurrence ;
- ``insufficient_coverage`` — couverture partielle (ex. 18/24 émetteurs) ;
- ``absent`` — donnée non produite par SYNE pour ce run ;
- ``unmeasured`` — repli neutre servi faute de fenêtre (``measured=false``) ;
- ``censored`` — observation interrompue avant la fin d'un épisode.

``CATALOG_VERSION`` suit le schéma semver : toute modification de formule ou
d'unité **augmente** la version et est consignée dans ``HISTORY`` — deux
versions incompatibles ne doivent pas être comparées silencieusement.
"""

from __future__ import annotations

CATALOG_VERSION = "2.0.0"
"""Version du catalogue. 2.0.0 : renommages P0/P1 + nouvelles mesures."""

HISTORY = (
    {
        "version": "2.0.0",
        "changes": (
            "NetworkCentrality → SenderConcentration (formule corrigée : part du "
            "principal émetteur, et non émetteurs distincts / messages) ; "
            "InformationDiffusionSpeed → EmitterCoverageDelay ; "
            "RumorAccuracyDegradation → TheoreticalHopDecay ; "
            "CooperationPotential → GoalCategoryConcordance ; "
            "DecisionDiversity → ActionDiversity (entropie normalisée des "
            "décisions, repli sur les buts supprimé) + DecisionCount ; "
            "IntentionStability → AverageGoalAge ; "
            "IdentifiedLoops/LoopStrength/CriticalLoops/SystemStability/LoopTypes → "
            "RepeatedActionPairs/RepeatedActionShare/AmplifiedRepetitions/"
            "ActionDistributionBalance/RepeatedActionCounts ; "
            "ActiveGroups/AverageGroupSize → InferredCommunities/"
            "AverageCommunitySize ; GroupObjectiveSuccessRate → "
            "DissolvedGroupSuccessShare ; MemberTurnoverRate → "
            "MemberExitsPerDissolution ; ResourceToConsumptionRatio → "
            "ResourceFillRatio ; CriticalityPoints → CriticalResourceCount ; "
            "AverageCentrality → AverageOutDegree ; CommunityStability → "
            "CommunitySizeMatch ; UnpredictabilityIndex retiré ; "
            "NetworkDensity : dénominateur corrigé en n(n-1)/2 ; "
            "EmergenceScore : composantes normalisées + contributions publiées."
        ),
    },
    {
        "version": "1.0.0",
        "changes": "Catalogue initial (7 moteurs + composite, EMERGENCE_INDICATORS).",
    },
)

_STATES = {
    "measured": ["observed_zero", "unmeasured"],
    "windowed": ["observed_zero", "window_empty", "unmeasured"],
    "coverage": ["observed_zero", "window_empty", "insufficient_coverage", "unmeasured"],
    "episodes": ["observed_zero", "window_empty", "censored", "unmeasured"],
    "instant": ["observed_zero", "absent", "unmeasured"],
}

_EXPLORATORY = "exploratory"
_MEASURED = "measured"


def _metric(
    metric_id: str,
    engine: str,
    label: str,
    unit: str,
    domain: str,
    definition: str,
    calculation: str,
    population: str,
    window: str,
    direction: str,
    *,
    status: str = _MEASURED,
    states: str = "measured",
    warning: str = "",
    visual: str = "courbe par tick, axe nommé avec l'unité",
    renamed_from: tuple[str, ...] = (),
) -> dict:
    entry = {
        "id": metric_id,
        "engine": engine,
        "label": label,
        "unit": unit,
        "domain": domain,
        "definition": definition,
        "calculation": calculation,
        "population": population,
        "window": window,
        "direction": direction,
        "status": status,
        "states": list(_STATES[states]),
        "warning": warning,
        "visual": visual,
    }
    if renamed_from:
        entry["renamedFrom"] = list(renamed_from)
    return entry


def _build() -> tuple[dict, ...]:
    cognitive = "CognitiveDiversityMetrics"
    information = "InformationPropagationMetrics"
    social = "SocialComplexityMetrics"
    goals = "GoalConvergenceMetrics"
    loops = "FeedbackLoopDetector"
    resources = "ResourceSustainabilityMetrics"
    groups = "GroupDynamicsMetrics"
    emergence = "EmergenceIndicators"

    entropy_visual = "une seule série par unité (bits), jamais mélangée à des fractions"
    return (
        # ----- Moteur 1 : diversité cognitive -------------------------------
        _metric(
            "BeliefDiversity", cognitive, "Diversité des croyances (bits)", "bits", "≥ 0",
            "Entropie de Shannon des triplets (sujet, prédicat, valeur) observés. "
            "Mesure la dispersion des énoncés, pas une « diversité cognitive » normalisée.",
            "H = -Σ p·log₂(p) sur les triplets comptés",
            "croyances déclarées par la population",
            "instantané du tick",
            "plus haut = énoncés plus dispersés, sans jugement de qualité",
            states="instant",
            warning="Croît avec le nombre d'énoncés et de catégories : non comparable "
                    "entre populations de tailles différentes — comparer des "
                    "distributions, pas les valeurs brutes.",
            visual=entropy_visual,
        ),
        _metric(
            "BeliefDiversityNorm", cognitive, "Diversité des croyances normalisée",
            "fraction", "[0, 1]",
            "Entropie des croyances ramenée à son maximum pour le nombre de catégories observées.",
            "H / log₂(k), k = énoncés distincts",
            "croyances déclarées par la population", "instantané du tick",
            "1 = répartition parfaitement uniforme sur les énoncés observés",
            states="instant",
            warning="Normalise sur les catégories observées, pas sur celles possibles.",
            visual="courbe ou carte numérique",
            renamed_from=(),
        ),
        _metric(
            "BeliefDisagreement", cognitive, "Désaccord sur les faits comparables",
            "fraction", "[0, 1]",
            "Part des croyances hors de la valeur majoritaire, moyennée sur les faits "
            "qui admettent plusieurs valeurs. Les faits unanimes sont exclus.",
            "moyenne non pondérée des parts de divergence, par fait divergent",
            "croyances portant sur des faits comparables (≥ 2 valeurs)",
            "instantané du tick",
            "1 = tous les faits comparables sont disputés",
            states="windowed",
            warning="Même poids pour un fait rare et un fait fréquent ; le dénominateur "
                    "n'inclut pas les faits sans divergence — distinguer « pas de "
                    "désaccord » et « aucun fait comparable ».",
            visual="carte numérique + nombre de faits comparables",
        ),
        _metric(
            "BeliefConfidenceVariance", cognitive, "Dispersion des confiances",
            "unité² (confiance)", "≥ 0",
            "Variance population des confiances déclarées — hétérogénéité, pas exactitude.",
            "variance (ddof=0) des confiances", "croyances avec confiance publiée",
            "instantané du tick",
            "plus haut = confiances moins homogènes",
            states="instant",
            warning="Ne dit rien de la justesse des croyances ; dépend de l'échelle de "
                    "confiance publiée par SYNE.",
            visual="carte numérique (moyenne à afficher à côté)",
        ),
        _metric(
            "GoalDiversity", cognitive, "Diversité des objectifs (bits)", "bits", "≥ 0",
            "Entropie de Shannon de la distribution des catégories d'objectifs actifs.",
            "H = -Σ p·log₂(p) sur les catégories de buts",
            "entités vivantes avec objectif déclaré", "instantané du tick",
            "plus haut = objectifs plus dispersés",
            states="instant",
            warning="Non mesurée si aucune entité ne déclare d'objectif : la distribution "
                    "se construirait alors sur l'action courante (repli supprimé).",
            visual=entropy_visual,
        ),
        _metric(
            "GoalDiversityNorm", cognitive, "Diversité des objectifs normalisée",
            "fraction", "[0, 1]",
            "Entropie des objectifs ramenée à son maximum pour le nombre de catégories observées.",
            "H / log₂(k)", "entités vivantes avec objectif déclaré", "instantané du tick",
            "1 = répartition uniforme sur les catégories observées",
            states="instant",
            warning="Mesure la répartition, pas la qualité des objectifs.",
            visual="courbe ou carte numérique",
        ),
        _metric(
            "GoalConvergence", cognitive, "Objectif majoritaire (part)",
            "fraction", "[0, 1]",
            "Part de la population dans la catégorie d'objectif la plus fréquente.",
            "max des comptages / entités vivantes", "entités vivantes", "instantané du tick",
            "1 = toutes les entités visent la même catégorie",
            states="instant",
            warning="Concordance de catégories : ni coordination ni accord observés. "
                    "Doublon du même calcul que GlobalGoalAlignment (moteur 4).",
            visual="barre 0–100 % avec dénominateur",
        ),
        _metric(
            "GoalCoverage", cognitive, "Couverture des objectifs déclarés",
            "fraction", "[0, 1]",
            "Part d'entités vivantes qui déclarent au moins un objectif.",
            "entités avec goals[] non vide / entités vivantes",
            "entités vivantes", "instantané du tick",
            "bas = la distribution de buts décrit peu d'entités",
            states="instant",
            warning="Sans couverture, GoalDiversity/GoalConvergence décrivent un repli "
                    "sur l'action courante.",
            visual="à afficher à côté de toute mesure de buts",
        ),
        _metric(
            "ActionDiversity", cognitive, "Diversité des décisions (normalisée)",
            "fraction", "[0, 1]",
            "Entropie normalisée de la distribution des actions décidées. Ne se "
            "mélangent plus aux objectifs (l'ancien DecisionDiversity le faisait).",
            "H / log₂(k) sur les actions observées",
            "décisions (event.action) de la fenêtre d'événements",
            "fenêtre d'événements du pipeline",
            "1 = répartition uniforme des actions décidées",
            states="coverage",
            warning="0 avec une seule action **et** 0 sans décision : lire DecisionCount "
                    "à côté. Les décisions de la fenêtre, pas du seul tick.",
            visual="courbe + effectif (DecisionCount)",
            renamed_from=("DecisionDiversity",),
        ),
        _metric(
            "DecisionCount", cognitive, "Décisions observées", "count", "≥ 0",
            "Nombre de décisions (event decision_made) dans la fenêtre d'événements.",
            "comptage des événements decision_made",
            "décisions de la fenêtre", "fenêtre d'événements du pipeline",
            "contexte de couverture pour ActionDiversity",
            states="coverage",
            warning="Dénominateur à afficher systématiquement : un ratio sans effectif "
                    "n'est pas interprétable.",
            visual="courbe ou valeur en contexte",
        ),
        _metric(
            "AverageGoalAge", cognitive, "Ancienneté moyenne des objectifs", "ticks", "≥ 0",
            "Âge moyen des objectifs actifs — une durée d'engagement, pas une stabilité.",
            "moyenne de goals[].age", "objectifs déclarés", "instantané du tick",
            "plus haut = buts plus anciens en moyenne",
            states="instant",
            warning="Une durée moyenne ne dit rien des transitions : une stabilité "
                    "supposerait un suivi d'identité d'intention dans le temps.",
            visual="courbe avec unité ticks",
            renamed_from=("IntentionStability",),
        ),
        _metric(
            "TraitExpressionDiversity", cognitive, "Dispersion des traits",
            "unité² (traits)", "≥ 0",
            "Moyenne, par trait, de la variance de ses valeurs sur la population.",
            "moyenne des variances par trait", "entités publiant des traits",
            "instantané du tick",
            "plus haut = valeurs de trait plus dispersées",
            states="instant",
            warning="Variance non normalisée : dépend de l'échelle de chaque trait. Ne "
                    "pas agréger les traits sans standardisation documentée.",
            visual="distribution par trait plutôt qu'un score global",
        ),
        # ----- Moteur 2 : information --------------------------------------
        _metric(
            "MessageVolume", information, "Volume de messages", "messages/entité/tick",
            "≥ 0",
            "Messages envoyés au tick courant rapportés à la population vivante.",
            "messages du tick / entités vivantes", "entités vivantes", "tick courant",
            "contexte : activité de communication",
            warning="Rapporté par entité : une hausse peut venir d'une population plus "
                    "petite — toujours afficher le volume brut et la taille de "
                    "population en contexte.",
            visual="courbe avec volume brut et population en contexte",
        ),
        _metric(
            "EmitterCoverageDelay", information, "Délai de couverture des émetteurs",
            "ticks", "≥ 0",
            "Ticks entre le premier message de la fenêtre et le moment où 80 % des "
            "entités vivantes ont émis. **Ne mesure pas la réception.**",
            "amplitude entre le 1er message et l'atteinte du seuil de couverture",
            "entités vivantes", "fenêtre d'événements du pipeline",
            "plus court = émissions plus vite réparties",
            states="coverage",
            warning="Ne prouve ni réception ni propagation d'un contenu ; 0 est ambigu "
                    "sans SenderCoverage à côté.",
            visual="courbe (ticks) avec SenderCoverage affiché à côté",
            renamed_from=("InformationDiffusionSpeed",),
        ),
        _metric(
            "TheoreticalHopDecay", information, "DÉGRADATION THÉORIQUE par saut",
            "fraction", "[0, 1]",
            "1 − 0,9^hops : **transformation théorique** du nombre de sauts sous "
            "l'hypothèse de 10 % de perte par saut. Aucune précision n'est observée.",
            "moyenne de (1 − 0,9^hops) sur les messages à hops connus",
            "messages avec compteur de sauts", "fenêtre d'événements du pipeline",
            "plus haut = hypothèse de perte plus accumulée",
            status=_EXPLORATORY,
            states="windowed",
            warning="Hypothèse non calibrée (10 %/saut), aucun contenu vérifié à la "
                    "réception. À ne jamais présenter comme une exactitude de rumeur.",
            visual="au besoin, avec l'hypothèse écrite sur le graphique",
            renamed_from=("RumorAccuracyDegradation",),
        ),
        _metric(
            "MaxMessageHops", information, "Plus longue chaîne de sauts", "sauts", "≥ 0",
            "Maximum des compteurs de sauts observés dans la fenêtre.",
            "max(hops)", "messages avec compteur de sauts",
            "fenêtre d'événements du pipeline",
            "valeur extrême : plus haut = chaîne la plus longue",
            states="windowed",
            warning="Maximum sensible au volume et à la durée de fenêtre : fragile "
                    "comme tendance, utile comme diagnostic.",
            visual="distribution/quantiles plutôt qu'une courbe seule",
        ),
        _metric(
            "SenderConcentration", information, "Concentration des émetteurs",
            "fraction", "[0, 1]",
            "Part du message le plus productif dans le volume total. 1 = un seul "
            "émetteur concentre tout le volume.",
            "max(messages par émetteur) / messages totaux",
            "messages de la fenêtre", "fenêtre d'événements du pipeline",
            "plus haut = communication plus concentrée sur peu d'émetteurs",
            states="coverage",
            warning="Porte sur les **émissions** uniquement ; un seuil de goulot "
                    "reste non calibré.",
            visual="courbe + part des principaux émetteurs",
            renamed_from=("NetworkCentrality",),
        ),
        _metric(
            "SenderCoverage", information, "Couverture des émetteurs", "fraction",
            "[0, 1]",
            "Part d'entités vivantes ayant émis au moins un message dans la fenêtre "
            "(« 18/24 émetteurs »).",
            "émetteurs distincts / entités vivantes", "entités vivantes",
            "fenêtre d'événements du pipeline",
            "bas = peu d'entités ont pris la parole",
            states="coverage",
            warning="Couverture des émetteurs, pas des destinataires : dit ce qui a été "
                    "émis, jamais ce qui a été reçu.",
            visual="à afficher avec le délai de couverture",
        ),
        # ----- Moteur 3 : complexité sociale --------------------------------
        _metric(
            "AverageTrustLevel", social, "Confiance moyenne déclarée", "confiance", "—",
            "Moyenne des poids de confiance déclarés (relations orientées).",
            "moyenne des trust[] > 0", "relations de confiance publiées",
            "instantané du tick",
            "plus haut = confiances déclarées plus fortes",
            states="instant",
            warning="Relations absentes exclues et valeurs potentiellement comptées "
                    "deux fois (a→b et b→a) : afficher le nombre de relations et la "
                    "convention directionnelle.",
            visual="courbe + nombre de relations",
        ),
        _metric(
            "TrustVariance", social, "Dispersion des confiances", "unité²", "≥ 0",
            "Variance population des poids de confiance déclarés.",
            "variance (ddof=0)", "relations de confiance publiées", "instantané du tick",
            "plus haut = confiances moins homogènes",
            states="instant",
            warning="Une moyenne seule masque les désaccords réciproques : afficher "
                    "les deux.",
            visual="carte numérique avec la moyenne",
        ),
        _metric(
            "NetworkDensity", social, "Densité du réseau de confiance", "fraction",
            "[0, 1]",
            "Arêtes non orientées uniques / paires d'entités possibles.",
            "arêtes / (n(n−1)/2)", "entités vivantes (n)", "instantané du tick",
            "1 = graphe complet",
            states="instant",
            warning="Graphe rendu non orienté : la direction des déclarations est perdue "
                    "ici (voir AverageOutDegree).",
            visual="courbe + n et nombre d'arêtes",
        ),
        _metric(
            "ClusteringCoefficient", social, "Clustering (triangles)", "fraction",
            "[0, 1]",
            "Coefficient local moyen sur voisinage symétrisé : triangles fermés / possibles.",
            "moyenne des coefficients locaux", "entités du graphe", "instantané du tick",
            "plus haut = voisinages plus reliés entre eux",
            states="instant",
            warning="Inclut les nœuds isolés ou de degré 1 avec coefficient 0 ; graphe "
                    "initiallement directionnel rendu non orienté.",
            visual="courbe avec la variante et la population précisées",
        ),
        _metric(
            "AverageOutDegree", social, "Degré sortant moyen normalisé", "fraction",
            "[0, 1]",
            "Degré sortant moyen rapporté à n−1. Ce n'est **pas** une centralité "
            "intermédiaire (betweenness).",
            "moyenne(degré sortant / (n−1))", "entités vivantes", "instantané du tick",
            "1 = chaque entité déclare une confiance vers toutes les autres",
            states="instant",
            warning="Sur graphe non orienté, redondant avec NetworkDensity à un "
                    "facteur de normalisation près ; la spec ancienne annonçait à tort "
                    "une betweenness.",
            visual="courbe, ou ne pas afficher à côté de NetworkDensity",
            renamed_from=("AverageCentrality",),
        ),
        _metric(
            "NumberOfCommunities", social, "Communautés détectées", "count", "≥ 0",
            "Nombre de communautés (taille ≥ 2) issues de la propagation d'étiquettes "
            "déterministe ; les singletons sont exclus.",
            "comptage des communautés de taille ≥ 2", "entités du graphe",
            "instantané du tick",
            "contexte structurel, pas une preuve de formation",
            states="instant",
            warning="Sensible aux seuils de confiance, aux liens isolés et aux "
                    "singletons ; un comptage > 2 n'observe aucune formation.",
            visual="courbe + tailles et couverture",
        ),
        _metric(
            "CommunitySizeMatch", social, "Correspondance des tailles de communautés",
            "fraction", "[0, 1]",
            "Part des communautés de l'historique dont la **taille** est présente dans "
            "le partition courant.",
            "tailles réapparues / tailles de l'historique",
            "communautés du dernier historique", "fenêtre de communityHistory",
            "1 = mêmes tailles qu'à l'instant précédent",
            states="windowed",
            warning="Compare des tailles, pas des membres : deux communautés disjointes "
                    "de même taille comptent comme stables. Une stabilité d'identité "
                    "exigerait la publication des membres (lacune tracée).",
            visual="courbe, libellée « correspondance des tailles »",
            renamed_from=("CommunityStability",),
        ),
        # ----- Moteur 4 : objectifs -----------------------------------------
        _metric(
            "GlobalGoalAlignment", goals, "Alignement sur l'objectif majoritaire",
            "fraction", "[0, 1]",
            "Part de la population dans la catégorie d'objectif la plus fréquente.",
            "max des comptages / entités vivantes", "entités vivantes", "instantané du tick",
            "1 = objectifs uniformes",
            states="instant",
            warning="Même calcul que GoalConvergence (moteur 1) : doublon de définition "
                    "à ne pas afficher deux fois ni additionner dans un composite.",
            visual="barre 0–100 % avec dénominateur",
        ),
        _metric(
            "GoalDiversity", goals, "Diversité des objectifs (bits)", "bits", "≥ 0",
            "Entropie de Shannon de la distribution des catégories d'objectifs.",
            "H = -Σ p·log₂(p)", "entités vivantes", "instantané du tick",
            "plus haut = objectifs plus dispersés",
            states="instant",
            warning="Doublon du moteur 1 (mêmes valeurs) : n'afficher qu'une série.",
            visual=entropy_visual,
        ),
        _metric(
            "GoalCategoryConcordance", goals, "Concordance attendue des catégories",
            "fraction", "[0, 1]",
            "Probabilité que deux tirages indépendants d'entités tombent sur la même "
            "catégorie de buts (Σ p²). **Aucune compatibilité par paire n'est testée.**",
            "Σ pᵢ² sur les catégories", "entités vivantes", "instantané du tick",
            "1 = toutes les entités dans une catégorie",
            states="instant",
            warning="Ce n'est ni un potentiel de coopération ni une mesure de "
                    "compatibilité d'objectifs — l'ancien nom CooperatiónPotential "
                    "l'affirmait à tort.",
            visual="carte numérique avec la formule",
            renamed_from=("CooperationPotential",),
        ),
        # ----- Moteur 5 : répétitions d'action ------------------------------
        _metric(
            "RepeatedActionPairs", loops, "Paires (agent, action) répétées", "count",
            "≥ 0",
            "Nombre de paires (agent, action) répétées plus de 2 fois dans la fenêtre "
            "de décisions. **Pas des boucles causales.**",
            "comptage des paires fréquence > 2", "décisions de la fenêtre",
            "fenêtre de 100 ticks glissants",
            "plus haut = répétitions plus nombreuses",
            status=_EXPLORATORY,
            states="windowed",
            warning="N'observe ni conséquence, ni relation action → conséquence ; une "
                    "routine normale peut satisfaire le critère, une vraie boucle "
                    "alternant plusieurs actions peut le manquer.",
            visual="courbe avec fenêtre et compteurs affichés",
            renamed_from=("IdentifiedLoops",),
        ),
        _metric(
            "RepeatedActionShare", loops, "Part moyenne des répétitions", "fraction",
            "[0, 1]",
            "Fréquence moyenne des paires répétées rapportée à la taille de la fenêtre.",
            "moyenne(freq / fenêtre) sur les paires retenues",
            "décisions de la fenêtre", "fenêtre de 100 ticks glissants",
            "plus haut = une part plus grande des décisions se répète",
            status=_EXPLORATORY,
            states="windowed",
            warning="Ce n'est pas un facteur d'amplification mesuré : aucune "
                    "conséquence n'est observée.",
            visual="courbe, jamais présentée comme une amplification",
            renamed_from=("LoopStrength",),
        ),
        _metric(
            "ActionDistributionBalance", loops, "Équilibre de la distribution des actions",
            "fraction", "[0, 1]",
            "1 − divergence (écart absolu total) à la distribution uniforme des "
            "actions observées.",
            "clamp(1 − Σ|pᵢ − 1/k|)", "actions observées dans la fenêtre",
            "fenêtre de 100 ticks glissants",
            "1 = distribution strictement uniforme",
            status=_EXPLORATORY,
            states="windowed",
            warning="Ce n'est **pas** une stabilité temporelle : la distribution "
                    "uniforme n'est pas un état d'équilibre établi du système.",
            visual="courbe, libellée « équilibre de la distribution »",
            renamed_from=("SystemStability",),
        ),
        _metric(
            "AmplifiedRepetitions", loops, "Répétitions très au-dessus du attendu",
            "count", "≥ 0",
            "Paires dont la fréquence dépasse de 1,5× la fréquence uniforme théorique.",
            "comptage des paires(freq / attendue > 1,5)",
            "paires répétées de la fenêtre", "fenêtre de 100 ticks glissants",
            "plus haut = écarts plus marqués à la référence uniforme",
            status=_EXPLORATORY,
            states="windowed",
            warning="Référence théorique, pas un danger observé : le seuil 1,5 est "
                    "hérité et non calibré.",
            visual="comptage avec la référence écrite à côté",
            renamed_from=("CriticalLoops",),
        ),
        # ----- Moteur 6 : ressources ----------------------------------------
        _metric(
            "ResourceFillRatio", resources, "Remplissage moyen des réserves",
            "fraction", "[0, 1]",
            "Part de capacité restante, moyenne des réserves dont la capacité est "
            "publiée. Une réserve au-delà de sa capacité est comptée saturée (1,0).",
            "moyenne(min(quantity/capacity, 1))",
            "réserves avec capacité publiée", "instantané du tick",
            "bas = réserves peu remplies",
            states="instant",
            warning="N'inclut pas les réserves sans capacité publiée : lire "
                    "ResourceCoverage à côté. Ne mélange plus deux dénominateurs.",
            visual="une courbe par type de ressource, même échelle 0–1",
            renamed_from=("ResourceToConsumptionRatio",),
        ),
        _metric(
            "CriticalResourceCount", resources, "Réserves sous 20 % de capacité",
            "count", "≥ 0",
            "Nombre de réserves sous 20 % de leur capacité publiée.",
            "comptage(quantity/capacity < 0,2)", "réserves avec capacité publiée",
            "instantané du tick",
            "plus haut = plus de réserves en zone critique",
            states="instant",
            warning="Seuil opérationnel (20 %) issu de la spec héritée, non calibré ; "
                    "ne pas réduire plusieurs ressources à un entier sans détail.",
            visual="comptage + liste des ressources concernées",
            renamed_from=("CriticalityPoints",),
        ),
        _metric(
            "ResourceCoverage", resources, "Couverture des réserves", "fraction",
            "[0, 1]",
            "Part de réserves dont la capacité est publiée (dénominateur exploitable).",
            "réserves avec capacité / réserves totales", "réserves du tick",
            "instantané du tick",
            "bas = mesures de remplissage fondées sur peu de réserves",
            states="instant",
            warning="Sans couverture, une moyenne de remplissage est partiellement "
                    "vide.",
            visual="à afficher avec ResourceFillRatio",
        ),
        _metric(
            "ConsumptionPerTick", resources, "Consommation par tick",
            "unité/tick", "≥ 0",
            "Volume total consommé dans la fenêtre rapporté à la durée observée de "
            "cette fenêtre.",
            "Σ amount / fenêtre (ticks)", "fenêtre d'événements",
            "fenêtre d'événements du pipeline",
            "contexte : flux de consommation observé",
            states="coverage",
            warning="Somme sur tous les types de ressources : ventiler par type avant "
                    "toute projection d'autonomie.",
            visual="courbe par type de ressource",
        ),
        _metric(
            "RecoveryTime", resources, "Durée moyenne de récupération", "ticks", "≥ 0",
            "Durée moyenne entre un passage sous 20 % de capacité et un retour à ≥ 80 %.",
            "moyenne des durées d'épisodes complets",
            "épisodes critiques complets observés", "fenêtre history",
            "plus haut = crises plus longues à résorber",
            states="episodes",
            warning="0 signifie « aucun épisode complet observé » : lire "
                    "RecoveryEpisodes et UnresolvedCrisisCount à côté (censure).",
            visual="courbe avec le nombre d'épisodes affiché",
        ),
        _metric(
            "RecoveryEpisodes", resources, "Épisodes critiques complets", "count",
            "≥ 0",
            "Nombre de cycles sous 20 % puis retour ≥ 80 % observés dans l'historique.",
            "comptage des cycles complets", "fenêtre history", "fenêtre history",
            "dénominateur de RecoveryTime",
            states="episodes",
            warning="0 peut vouloir dire « aucune crise » ou « crise non résolue » : "
                    "croiser avec UnresolvedCrisisCount.",
            visual="comptage à afficher avec RecoveryTime",
        ),
        _metric(
            "UnresolvedCrisisCount", resources, "Crises non résolues", "count", "≥ 0",
            "Crises ouvertes (sous 20 %) encore ouvertes au dernier tick de "
            "l'historique — observation censurée, jamais un zéro rassurant.",
            "comptage des crises ouvertes en fin de fenêtre", "fenêtre history",
            "fenêtre history",
            "plus haut = plus de réserves en crise non résorbée",
            states="episodes",
            warning="Le run peut se terminer pendant une crise : ce n'est ni une durée "
                    "mesurée ni une absence de crise.",
            visual="comptage avec la liste des ressources concernées",
        ),
        # ----- Moteur 7 : dynamique des groupes -----------------------------
        _metric(
            "InferredCommunities", groups, "Communautés inférées", "count", "≥ 0",
            "Communautés (taille ≥ 2) du graphe de confiance. **Ce ne sont pas les "
            "groupes déclarés par SYNE.**",
            "comptage des communautés de taille ≥ 2", "entités du graphe",
            "instantané du tick",
            "contexte : structure inférée",
            states="instant",
            warning="Ne pas confondre avec group_formed/group_dissolved (événements "
                    "natifs) : deux origines distinctes.",
            visual="courbe séparée des événements de groupes",
            renamed_from=("ActiveGroups",),
        ),
        _metric(
            "AverageCommunitySize", groups, "Taille moyenne des communautés",
            "entités", "≥ 2",
            "Moyenne des tailles des communautés inférées.",
            "moyenne des tailles", "communautés inférées", "instantané du tick",
            "contexte : compacité de la structure inférée",
            states="instant",
            warning="Origine inférée, pas un groupe déclaré.",
            visual="courbe avec n de communautés à côté",
            renamed_from=("AverageGroupSize",),
        ),
        _metric(
            "CommunityCoverage", groups, "Couverture des communautés", "fraction",
            "[0, 1]",
            "Part d'entités vivantes rattachées à une communauté de taille ≥ 2.",
            "membres des communautés / entités vivantes", "entités vivantes",
            "instantané du tick",
            "bas = population majoritairement isolée",
            states="instant",
            warning="Base de l'ancien terme de composite « groupes/100 » : bornée et "
                    "documentée, elle ne mesure toujours pas une émergence.",
            visual="courbe ou barre 0–100 %",
        ),
        _metric(
            "AverageGroupLifetime", groups, "Durée de vie moyenne des groupes",
            "ticks", "≥ 0",
            "Moyenne des durées de vie déclarées sur les dissolutions observées.",
            "moyenne des lifetime des group_dissolved", "dissolutions observées",
            "fenêtre d'événements",
            "plus haut = groupes natifs plus durables",
            states="windowed",
            warning="Ne porte que sur les groupes déjà dissous (biais de sélection).",
            visual="courbe avec le nombre de dissolutions affiché",
        ),
        _metric(
            "GroupFormationRate", groups, "Formations de groupes (par 1000 ticks)",
            "groupes/1000 ticks", "≥ 0",
            "Événements group_formed normalisés sur la fenêtre **réellement observée**.",
            "formations × 1000 / ticks observés", "fenêtre d'événements",
            "fenêtre d'événements du pipeline",
            "contexte : rythme de formation déclaré",
            states="coverage",
            warning="Une fenêtre courte amplifie fortement la valeur : afficher aussi "
                    "FormationCount et la durée observée.",
            visual="courbe avec le comptage brut en sous-titre",
        ),
        _metric(
            "GroupDissolutionRate", groups, "Dissolutions de groupes (par 1000 ticks)",
            "groupes/1000 ticks", "≥ 0",
            "Événements group_dissolved normalisés sur la fenêtre observée.",
            "dissolutions × 1000 / ticks observés", "fenêtre d'événements",
            "fenêtre d'événements du pipeline",
            "contexte : rythme de dissolution déclaré",
            states="coverage",
            warning="Même amplification de fenêtre courte que GroupFormationRate.",
            visual="courbe avec le comptage brut en sous-titre",
        ),
        _metric(
            "FormationCount", groups, "Formations observées", "count", "≥ 0",
            "Nombre brut d'événements group_formed dans la fenêtre.",
            "comptage", "fenêtre d'événements", "fenêtre d'événements du pipeline",
            "dénominateur de contexte des taux",
            states="coverage",
            warning="Comptage brut : à afficher systématiquement à côté du taux.",
            visual="valeur de contexte",
        ),
        _metric(
            "DissolutionCount", groups, "Dissolutions observées", "count", "≥ 0",
            "Nombre brut d'événements group_dissolved dans la fenêtre.",
            "comptage", "fenêtre d'événements", "fenêtre d'événements du pipeline",
            "dénominateur de DissolvedGroupSuccessShare",
            states="coverage",
            warning="Dénominateur obligatoire : sans lui, un taux de succès est "
                    "invérifiable.",
            visual="valeur de contexte",
        ),
        _metric(
            "DissolvedGroupSuccessShare", groups, "Part de dissolutions « réussies »",
            "fraction", "[0, 1]",
            "Moyenne du booléen ``success`` sur les dissolutions observées **qui le "
            "publient** — pas sur tous les groupes.",
            "Σ success / dissolutions avec succès publié", "dissolutions observées",
            "fenêtre d'événements",
            "1 = toutes les dissolutions observées marquées réussies",
            status=_EXPLORATORY,
            states="windowed",
            warning="Biais de sélection : concerne les seuls groupes dissous observés ;"
                    " aucun déni d'objectif n'est mesuré. Sans dissolution → non mesuré.",
            visual="barre avec DissolutionCount affiché",
            renamed_from=("GroupObjectiveSuccessRate",),
        ),
        _metric(
            "MemberExitsPerDissolution", groups, "Sorties de membres par dissolution",
            "membres", "≥ 0",
            "Nombre moyen de ``membersOut`` déclarés par dissolution observée.",
            "moyenne(membersOut)", "dissolutions observées", "fenêtre d'événements",
            "plus haut = renouvellement plus marqué à chaque dissolution",
            status=_EXPLORATORY,
            states="windowed",
            warning="Ce n'est **pas** un taux annualisé : le contrat ne publie pas "
                    "l'effectif exposé en membres-temps. Sans dissolution → non mesuré.",
            visual="courbe avec DissolutionCount à côté",
            renamed_from=("MemberTurnoverRate",),
        ),
        # ----- Sorties non numériques (contrat public, non persistées) ------
        _metric(
            "GoalTypeCounts", goals, "Distribution des catégories d'objectifs",
            "non numérique", "comptages",
            "Comptage par catégorie d'objectif actif — sortie composite, jamais "
            "persistée comme série numérique.",
            "{catégorie: comptage}", "entités vivantes", "instantané du tick",
            "contexte : lecture directe de la distribution",
            states="instant",
            warning="Sans effectif total affiché, un comptage n'est pas une part : "
                    "afficher aussi GoalCoverage.",
            visual="tableau de proportions avec dénominateur",
        ),
        _metric(
            "RepeatedActionCounts", loops,
            "Actions répétées (par nom)", "non numérique", "comptages",
            "Nombre de paires (agent, action) répétées, classées par nom d'action. "
            "Aucune catégorie normative positive/négative (retirée en P2).",
            "{action: nombre de paires répétées}", "paires répétées de la fenêtre",
            "fenêtre de 100 ticks glissants",
            "la somme des valeurs vaut RepeatedActionPairs",
            status=_EXPLORATORY,
            states="windowed",
            warning="Aucun effet mesuré sur ces actions : un nom répété n'est ni "
                    "bénéfique ni nuisible.",
            visual="tableau trié par fréquence",
            renamed_from=("LoopTypes",),
        ),
        _metric(
            "DetectedPhenomena", emergence, "Signaux au-dessus de leurs seuils",
            "non numérique", "liste",
            "Signaux de la règle active dont la condition est satisfaite, avec la "
            "valeur observée et le seuil pour chaque signal.",
            "liste de {identifier, label, description, signals[]}",
            "métriques des règles actives", "instantané du tick",
            "observation d'un seuil franchi, jamais une conclusion causale",
            status=_EXPLORATORY,
            states="measured",
            warning="Seuils non calibrés et absence de détection ≠ absence du "
                    "phénomène.",
            visual="tableau « signal selon la règle X » avec seuil, valeur et tick",
        ),
        _metric(
            "Disclaimer", emergence, "Avertissement méthodologique",
            "non numérique", "texte",
            "Texte invariant publié par ECHOS, affiché tel quel et jamais reformulé.",
            "constante textuelle", "—", "—",
            "rappelle qu'une métrique n'est jamais une preuve",
            states="measured",
            warning="—",
            visual="à afficher dans toute vue présentant un score",
        ),
        # ----- Moteur 8 : composite -----------------------------------------
        _metric(
            "EmergenceScore", emergence, "Score d'émergence (EXPLORATOIRE)",
            "fraction", "[0, 1]",
            "Somme pondérée de six composantes normalisées. **Indicateur "
            "exploratoire** : ni mesure établie, ni preuve d'émergence.",
            "Σ poids × composante (poids Σ = 1.0, [HÉRITÉ], non calibrés)",
            "composantes publiées en ``Contribution*``", "instantané du tick",
            "1 = toutes les composantes à leur maximum théorique",
            status=_EXPLORATORY,
            states="measured",
            warning="Poids non validés par campagne de référence ni analyse de "
                    "sensibilité ; toujours afficher les contributions et le disclaimer.",
            visual="barre de contributions décomposées, jamais une jauge « santé »",
        ),
        _metric(
            "ContributionBeliefDiversity", emergence,
            "Contribution — diversité des croyances", "fraction", "[0, 0.15]",
            "Part du score apportée par la diversité des croyances normalisée.",
            "0.15 × BeliefDiversityNorm", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Poids hérité non calibré.",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "ContributionGoalDiversity", emergence,
            "Contribution — diversité des objectifs", "fraction", "[0, 0.15]",
            "Part du score apportée par la diversité des objectifs normalisée.",
            "0.15 × GoalDiversityNorm", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Poids hérité non calibré.",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "ContributionEmitterCoverage", emergence,
            "Contribution — couverture des émetteurs", "fraction", "[0, 0.10]",
            "Part du score apportée par la couverture des émetteurs (délai normalisé).",
            "0.10 × clamp(1 − délai/100)", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Horizon de 100 ticks hérité, non calibré.",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "ContributionClustering", emergence, "Contribution — clustering",
            "fraction", "[0, 0.15]",
            "Part du score apportée par le coefficient de clustering.",
            "0.15 × ClusteringCoefficient", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Poids hérité non calibré.",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "ContributionRepeatedActions", emergence,
            "Contribution — répétitions d'action", "fraction", "[0, 0.20]",
            "Part du score apportée à la part moyenne des répétitions d'action.",
            "0.20 × RepeatedActionShare", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Poids hérité non calibré ; la composante source est elle-même "
                    "exploratoire.",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "ContributionCommunityCoverage", emergence,
            "Contribution — couverture communautaire", "fraction", "[0, 0.25]",
            "Part du score apportée par la part de population en communauté.",
            "0.25 × CommunityCoverage", "composante du score", "instantané du tick",
            "composante du score", status=_EXPLORATORY, states="measured",
            warning="Remplace l'ancien terme arbitraire « groupes/100 ».",
            visual="segment d'une barre empilée",
        ),
        _metric(
            "SystemComplexity", emergence, "Complexité du système (EXPLORATOIRE)",
            "fraction", "[0, 1]",
            "Moyenne de trois grandeurs normalisées : croyances, objectifs et "
            "couverture des émetteurs. **Pas** une approximation de la complexité de "
            "Kolmogorov.",
            "(BeliefDiversityNorm + GoalDiversityNorm + CoverageDelay_Norm) / 3",
            "composantes du score", "instantané du tick",
            "1 = trois composantes à leur maximum",
            status=_EXPLORATORY,
            states="measured",
            warning="Formule canonique versionnée (2.0.0) : l'ancienne version "
                    "mêlait entropies en bits et ticks bruts, non bornée. Toujours "
                    "accompagnée du disclaimer.",
            visual="carte numérique avec la formule et la version",
        ),
    )


METRICS_CATALOG: tuple[dict, ...] = _build()
"""Entrées du catalogue, ordre stable (moteur puis nomenclature publiée)."""

_BY_ID: dict[tuple[str, str], dict] = {
    (entry["engine"], entry["id"]): entry for entry in METRICS_CATALOG
}


def catalog() -> dict:
    """Payload ``/api/metrics/catalog`` (déterministe, sans horodatage)."""
    return {
        "version": CATALOG_VERSION,
        "history": list(HISTORY),
        "metricCount": len(METRICS_CATALOG),
        "metrics": list(METRICS_CATALOG),
    }


def lookup(engine: str, metric_id: str) -> dict | None:
    """Fiche d'une métrique, ou ``None`` si elle n'est pas au catalogue."""
    return _BY_ID.get((engine, metric_id))


__all__ = [
    "CATALOG_VERSION",
    "HISTORY",
    "METRICS_CATALOG",
    "catalog",
    "lookup",
]
