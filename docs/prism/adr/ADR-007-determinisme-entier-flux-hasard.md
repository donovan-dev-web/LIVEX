# ADR-007 : Déterminisme du monde — arithmétique entière, hasard à compteur

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004, `../../syne/adr/ADR-006-prng-reproductible.md`, `../../syne/DETERMINISM.md`
**Source Monographie** : §2.3.4

---

## Contexte

LIVEX s'engage sur la reproductibilité : même seed, même configuration, même version → même trajectoire. SYNE garantit le niveau *exact* sur une même plateforme et statistique entre plateformes, car les calculs flottants .NET peuvent différer. Le monde voxel peut aller plus loin, car rien n'y oblige à utiliser le flottant.

## Décision

1. **Tout calcul qui influence l'état est entier ou en virgule fixe** (génération, mises à jour, navigation, durées, conflits). Le flottant n'existe que dans `Prism.Viewer`.
2. **Bruit de génération** : bruit de valeurs à treillis entier, hachage d'entiers, interpolation en virgule fixe 16.16.
3. **Hasard** : deux familles.
   - *Séquentiel* (`xoshiro256**`, amorcé par SplitMix64 à partir de `hash(runSeed, nom du flux)`) pour les processus sans parallélisme (génération).
   - *À compteur* (sans état : `Mix64(seed ⊕ flux ⊕ tick ⊕ clé)`) pour tout ce qui doit être indépendant de l'ordre d'itération ou parallélisable : mises à jour aléatoires par chunk, priorité des conflits, gigue de croissance.
4. **Flux nommés et isolés** : `terrain`, `ores`, `forest`, `randomTicks`, `conflicts`, `growthJitter`, `spawn`. Ajouter un flux ne décale jamais les autres.
5. **Empreintes** : FNV-1a 64 bits par chunk modifié, combinées dans l'ordre canonique des chunks ; empreinte globale à chaque point de reprise et tous les `checksumIntervalTicks`.
6. **Ordre canonique** de traitement : agents par identifiant croissant pour l'exécution ; par clé de priorité `Mix64(…)` pour l'arbitrage.

## Conséquences

### Positives
- Niveau d'équivalence **exact entre plateformes** pour l'état du monde (à confirmer par test Windows/Linux).
- Parallélisation par chunk sans perte de déterminisme.

### Négatives
- Bruit entier moins « organique » qu'un bruit flottant ; compensé par octaves et domaine déformé.
- Discipline : bibliothèques tierces interdites dans le noyau pour les maths.

### Risques
- Un flottant introduit par inadvertance. Mitigation : analyseur CI interdisant `float`/`double` dans `Prism.Core`, tests d'empreinte Windows/Linux.

## Alternatives considérées

- **Bruit flottant seedé** : refusé, divergences entre plateformes.
- **Un seul flux PRNG partagé** : refusé, tout changement de cardinalité décale tout le reste.

## Validation / rejet

Validé au jalon G3 (empreintes identiques Windows/Linux sur le monde généré et sur 5 000 ticks de bots). Réouvert au premier écart non expliqué.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
