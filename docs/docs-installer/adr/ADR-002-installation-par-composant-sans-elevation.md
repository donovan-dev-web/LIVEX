# ADR-002 : Installation par composant, sans élévation, workspace séparé

**Composant** : LIVEX (Installateur)
**Statut** : [Proposed]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `ADR-001-installateur-avalonia-coeur-sans-interface.md`
**Source Monographie** : —

---

## Contexte

`PACKAGING.md` §2 stipule « Installation par composant : Non » et « un seul
paquet installe la pile orchestration ». L'objectif de l'installateur est
l'inverse : l'utilisateur choisit ses composants (par exemple installer ou non le
mock, la documentation, PRISM-LDK) et voit leur taille. Les points ouverts de
`PACKAGING.md` §12 demandent la même décision : installation par composant,
format de livraison Linux, emplacement des données, seuil d'espace, mise à jour.

Contraintes existantes :

- l'installation doit se faire **sans privilèges élevés** (`PACKAGING.md` §2) ;
- le workspace est **séparé** et ne doit jamais être supprimé par la mise à jour
  ou la désinstallation (`PACKAGING.md` §2.2, §10) ;
- le Launcher détecte déjà les composants par `components/`, `LIVEX_HOME`,
  `~/.livex/components/` et `~/.livex/components.registry`, et le workspace par
  `LIVEX_DATA` (défaut `~/.livex-data`) ;
- chaque composant a son propre cycle de version (`VERSIONING.md` §2).

## Décision

1. **L'installation est par composant.** Le Launcher est obligatoire ; SYNE, ECHOS,
   le mock, PRISM-LDK et la documentation sont des options, avec leurs
   dépendances déclarées dans le catalogue.
2. **Installation par utilisateur uniquement en V0.1** : aucune élévation ; refus
   explicite si l'installateur est lancé en administrateur ou `root` sans l'option
   dédiée.
3. **Programme et workspace sont deux répertoires distincts**, choisis à l'étape 4.
   Le workspace est communiqué au Launcher par `LIVEX_DATA` quand il diffère du
   défaut ; un mécanisme de persistance côté Launcher est requis (OI-05).
4. **Le registre d'installations** (`~/.livex/components.registry`) est le
   mécanisme principal de détection ; `LIVEX_HOME` est secondaire.
5. **La désinstallation retire uniquement ce qui est inscrit au reçu** et ne
   supprime jamais le workspace sans `--purge-workspace` et confirmation.
6. `PACKAGING.md` §2 et §12 sont mis à jour pour refléter cette décision.

## Conséquences

### Positives
- Conforme aux principes déjà posés (aucune élévation, données séparées).
- L'utilisateur n'installe que ce dont il a besoin ; tailles visibles.
- Le détecteur existant fonctionne sans modification pour le chemin nominal.

### Négatives
- Le chemin par défaut du workspace diffère selon OI-02 ; un workspace hors du
  défaut nécessite une variable ou un fichier de configuration.
- Pas d'installation multi-utilisateurs ni de service système en V0.1.
- Chaque combinaison d'options est une surface de test supplémentaire.

### Risques
- Une variable `LIVEX_DATA` absente d'un contexte de lancement (raccourci, session)
  fait pointer le Launcher vers le défaut : workspace apparemment vide (OI-05).
- Les dossiers synchronisés (OneDrive) peuvent alourdir fortement la
  synchronisation de paquets volumineux.

## Alternatives considérées

- **Un seul paquet monolithique** (position antérieure de `PACKAGING.md`) : plus
  simple, mais impose mock, documentation et PRISM à tous → refusé.
- **Installation système (`/opt`, Program Files)** : conforme aux usages, mais exige
  une élévation, contraire au principe → reporté après V0.1.
- **Workspace à l'intérieur du programme** : simplifie la désinstallation mais
  expose les données aux mises à jour → refusé.
- **Workspace dans Documents (Windows)** : visible, mais souvent redirigé vers un
  service de synchronisation → avertissement plutôt que défaut (OI-02).

## Validation / rejet

- Validation : TS-03, TS-19, TS-20, TS-21 passent ; le Launcher détecte les
  composants sans variable d'environnement (TS-22).
- Réouverture : si la persistance du workspace ne peut être résolue côté Launcher
  (OI-05), ou si une demande d'installation multi-utilisateurs apparaît.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création | Résolution des points ouverts de `PACKAGING.md` §12 |
