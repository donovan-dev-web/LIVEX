# ADR-006 : PRNG reproductible

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.7 (ADR-006), §3.6.2–3.6.4

---

## Contexte

Le déterminisme est une contrainte fondamentale. Deux exécutions identiques doivent produire exactement la même trajectoire.

## Décision

Utiliser **xoshiro256\*\*** (`prng_engine = xoshiro256**`) avec initialisation **splitmix64(seed)**. La seed est stockée dans la configuration. L'état du PRNG (4 × ulong) est sérialisé dans la persistance pour reprise exacte.

**Règle** : `System.Random` est **interdit** (non déterministe entre runtimes, état non sérialisable).

## Conséquences

### Positives
- Reproductibilité bit-à-bit garantie (si la seed est la même).
- Testable via `BitIdenticalPersistenceTest` et checksums de runs.
- xoshiro256\*\* : qualité (BigCrush) et vitesse.

### Négatives
- Le parallélisme doit préserver l'ordre causal (un PRNG par thread partagé avec agrégation d'ordre fixe).

### Risques
- Mauvaise utilisation du PRNG dans le code parallèle → non-déterminisme silencieux (à vérifier par tests).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |