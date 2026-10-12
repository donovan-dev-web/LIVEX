# ADR-004 : Catalogue signé et mise à jour manuelle atomique par composant

**Composant** : LIVEX (Installateur)
**Statut** : [Proposed]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `ADR-002-installation-par-composant-sans-elevation.md`, `ADR-003-dependances-embarquees.md`
**Source Monographie** : —

---

## Contexte

`VERSIONING.md` impose un versionnement indépendant par composant et quatre
familles de tags. `PACKAGING.md` §8 prévoit : application remplacée sans perte de
données, **pas de mise à jour automatique** en V0.1, reprise après échec
(installation précédente utilisable), et indique que la mise à jour du Launcher
n'implique pas celle des composants. Il laisse ouvertes la mise à jour
automatique, la signature des livraisons et le format de livraison Linux.

L'installateur doit décider comment il connaît les versions disponibles, comment
il vérifie ce qu'il télécharge et comment il remplace un composant sans risquer de
laisser l'utilisateur sans installation fonctionnelle.

Contraintes :

- un exécutable ne peut pas être remplacé pendant qu'il s'exécute (Windows) ;
- un run actif ne doit pas être interrompu par une mise à jour ;
- le déterminisme scientifique lie un run à la version du moteur
  (`VERSIONING.md` §3) : un composant remplacé ne doit pas être remplacé par un
  moteur dont la version est inconnue de l'utilisateur.

## Décision

1. **Un catalogue** (`catalog.json`, `schema` versionné) publié avec chaque
   release `livex-v*` décrit les composants, versions, OS, tailles, empreintes
   SHA-256, dépendances et acceptation par OS. L'installateur n'embarque aucune URL
   d'artefact.
2. **Intégrité** : SHA-256 obligatoire avant extraction ; **signature détachée du
   catalogue** par une clé dont la partie publique est embarquée (OI-04).
3. **Mise à jour manuelle** en V0.1 : lancée par l'utilisateur, jamais en
   arrière-plan ; **par composant** ; refusée si un run est actif.
4. **Remplacement atomique** : préparation dans `<id>.new`, validation (manifeste,
   démarrage, santé), renommages, conservation de `<id>.prev` jusqu'à validation
   par `--check` ; retour arrière automatique en cas d'échec.
5. **Rétrogradation refusée** sans option explicite.
6. **Mise à jour du Launcher** : mécanisme à trancher (OI-06) entre processus
   assistant et bibliothèque tierce à évaluer ; la politique reste manuelle, par
   composant et sans perte de données.
7. **Tag de l'installateur** : `setup-v*` dédié ou rattachement à `livex-v*`, à
   trancher en I0 et à reporter dans `VERSIONING.md`.

## Conséquences

### Positives
- Mise à jour sûre : en cas d'échec, l'ancienne version reste utilisable
  (exigence de `PACKAGING.md` §8).
- Cycles de version indépendants par composant, conformes à `VERSIONING.md`.
- L'utilisateur contrôle les changements de moteur, ce qui préserve la
  reproductibilité des runs.
- Un seul point de vérité (catalogue) pour tailles, compatibilités et acceptation.

### Négatives
- Un artefact supplémentaire à produire, valider et signer à chaque release.
- La gestion d'une clé de signature (stockage, rotation) devient une charge.
- Pas de mise à jour silencieuse : les utilisateurs peuvent rester sur d'anciennes
  versions.

### Risques
- Compromission ou perte de la clé de signature : procédure de rotation à
  documenter avant I4.
- Disponibilité de l'hébergement du catalogue à long terme (OI-07).
- Rupture de compatibilité entre un composant plus récent et le Launcher installé :
  traitée par `protocolVersion`, `minInstaller` et les règles de
  `PACKAGING.md` §9.

## Alternatives considérées

- **Mise à jour automatique silencieuse** : confortable, mais contraire à
  `PACKAGING.md` §8 et dangereuse pour la reproductibilité → refusée en V0.1.
- **Remplacement en place sans répertoire de préparation** : plus simple, mais une
  interruption laisse un composant à moitié écrit → refusé.
- **Mise à jour globale unique de la pile** : simple, mais contredit le
  versionnement indépendant et force à tout retester → refusée.
- **Dépôt de paquets système (`apt`, `winget`)** : mécanisme standard, mais
  élévation, délais de publication et gestion de deux circuits → reporté, non exclu
  pour une phase ultérieure.
- **Bibliothèque de mise à jour tierce pour toute la pile** (par exemple Velopack) :
  attrayante pour le Launcher seul ; non retenue pour les composants, qui ont des
  cycles et des runtimes propres → évaluation limitée au Launcher (OI-06).

## Validation / rejet

- Validation : TS-08, TS-09, TS-15, TS-16, TS-17, TS-24 passent ; une interruption à
  chaque point du flux de `RELEASE_AND_UPDATE.md` §8.2 laisse une installation
  utilisable.
- Réouverture : si l'hébergement impose un autre format de distribution, si un
  besoin de mise à jour de sécurité urgente rend la politique manuelle
  insuffisante, ou si la gestion de clé s'avère disproportionnée pour le projet.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création | Résolution des points ouverts de `PACKAGING.md` §12 sur la mise à jour et la signature |
