# ADR-007 : Mémoire oubliante

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.8 (ADR-007), §3.10

---

## Contexte

Une entité qui a tout acquis est triviale et n'a plus besoin d'explorer. Une mémoire parfaite élimine la variance comportementale.

## Décision

Implémenter un modèle de **mémoire oubliante** avec :

- **Décroissance exponentielle** de la salience (défaut `decayRate` 0.01, par type 0.01/0.005/0.002) ;
- **Capacité maximale** (500–1000 souvenirs, défaut 1000) ;
- **Priorisation par pertinence** ;
- **Propagation du savoir par communication** (avec dégradation).

## Conséquences

### Positives
- Les entités peuvent oublier des événements importants.
- La mémoire crée de la variance comportementale.
- La transmission de savoir est dégradée (plus réaliste, empêche la connaissance parfaite).

### Négatives
- Paramètres de décroissance sensibles à calibrer.

### Risques
- Oubli trop rapide → comportements répétitifs ; trop lent → entités "omniscientes" (à suivre par métriques).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |