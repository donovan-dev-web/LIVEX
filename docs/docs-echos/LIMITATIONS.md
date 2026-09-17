# LIMITATIONS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `VISION.md`, `METRICS_SPEC.md`
**Source Monographie** : §4.10

---

## 1. Les limites méthodologiques

- Un indice d'émergence est une **mesure particulière**, **pas une preuve**.
- L'observation peut introduire un **biais de conception** : les mesures reflètent les choix du développeur.
- Un **phénomène rare mais intéressant peut passer inaperçu** si la métrique correspondante n'existe pas.

## 2. Les limites techniques

- La **volumétrie des événements** peut dépasser la capacité d'analyse (besoin de sous-échantillonnage — cf. `API_REST.md` §4).
- Les calculs **O(n²)** (co-localisation, centralité) doivent être parallélisés aux hautes échelles.
- **L'analyse causale précise devient difficile** avec les boucles de rétroaction multiples (cf. `CAUSAL_ANALYSIS.md` §3).

## 3. La règle d'or

> **ECHOS ne doit jamais transformer une métrique en vérité scientifique.** Un score d'émergence ou une valeur de centralité reste une mesure particulière d'un phénomène, jamais une preuve de l'existence d'une intelligence ou d'une société.

---

## Points restés ouverts dans ce document
- Aucun — les limites sont assumées comme contraintes méthodologiques permanentes.