# Installation & Démarrage

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : —

---

> Les implémentations V0.1 sont en cours de conception (documentation d'abord).
> Les commandes ci-dessous reflètent la cible ; l'état courant du dépôt contient la
> documentation et le prototype historique ([`docs/docs_prototype/`](docs/docs_prototype/)).

## Prérequis

- [.NET SDK](https://dotnet.microsoft.com/) (moteur SYNE / prototype `Simulation.Console`)
- Docker & Docker Compose (orchestration complète, cible V0.1)

## Lancer le prototype historique (SYNE, tête-à-tête)

```bash
dotnet run --project simulation-core/Simulation.Console -- --seed 12345 --max-ticks 1000
```

- `--seed` : graine du PRNG (xoshiro256\*\*), garantit la reproductibilité bit-à-bit
  pour une même config et une même version du moteur.
- `--max-ticks` : nombre de ticks simulés avant arrêt.

## Orchestration complète (cible V0.1)

```bash
docker compose up --build
```

Cette commande est la cible d'orchestration des trois modules (SYNE, ECHOS, PRISM)
une fois leur implémentation V0.1 disponible. Elle n'est pas encore fonctionnelle
en l'état actuel du dépôt — voir [`ROADMAP.md`](ROADMAP.md) pour l'avancement.

## État technique actuel

- Prototype historique fonctionnel, validé par 98 tests (`[HÉRITÉ]`).
- Documentation V0.1 (phases 0 à 5) consolidée — voir la
  [monographie complète](docs/LIVEX-Monographie.pdf).
- Implémentation des composants V0.1 : à venir.

## Points restés ouverts

- Commandes cibles (`docker compose`, V0.1) à revalider lors de l'implémentation réelle.
- Les références au prototype restent marquées `[HÉRITÉ]` jusqu'à la refonte V0.1.

## Aller plus loin

- Architecture détaillée : [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Documentation par module : [`docs/docs-syne/`](docs/docs-syne/) ·
  [`docs/docs-echos/`](docs/docs-echos/) · [`docs/docs-prism/`](docs/docs-prism/)
- Contribuer : [`CONTRIBUTING.md`](CONTRIBUTING.md)
