# ADR-004 : Le paquet `.livexp`

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : —
**Source Monographie** : —

---

## Contexte

Le Launcher orchestre des campagnes : un ensemble de runs reproductibles, dont les
artefacts doivent survivre à la session, être partageables, et pouvoir être
rejoués. Il lui faut donc un format de données unique, qui serve à la fois d'espace
de travail et d'archive.

Cinq besoins se font face, et aucun format ne les sert également.

1. **Reprise après incident.** Une campagne peut durer des heures ou des jours. Un
   plantage ne doit pas coûter la campagne entière, ni obliger à la rejouer.
2. **Partage.** Un paquet doit pouvoir quitter le poste de travail et être ouvert
   ailleurs, par quelqu'un d'autre, éventuellement sans le Launcher.
3. **Autonomie.** Un paquet doit porter tout ce qui est nécessaire à la
   compréhension et à la rejouabilité de la campagne.
4. **Reproductibilité.** Deux campagnes de contenu identique doivent produire deux
   paquets identiques, pour que la comparaison soit un test et non une intuition.
5. **Intégrité.** Une altération doit être détectée, et un paquet interrompu ne doit
   jamais être confondu avec un paquet valide.

Le besoin de reproductibilité est celui qui contraint le plus le format. Il impose
un ordre d'écriture déterministe, des horodatages d'entrées normalisés, des noms
dérivés et non horodatés. Or un format qui autorise l'écriture libre ne garantit
rien de tout cela.

## Décision

**Le Launcher adopte un format unique, le paquet `.livexp`, qui est un conteneur ZIP
vivant pendant la campagne puis scellé à son terme.**

Les éléments structurants :

- **Conteneur ZIP, ZIP64 obligatoire**, sous l'extension `.livexp`. Le conteneur est
  un choix d'implémentation : la structure logique est définie indépendamment, et un
  lecteur n'a pas à dépendre de ZIP.
- **Double nature.** Le paquet est `live` pendant la campagne, écrit en continu au
  fil des runs, puis passe à `sealed` et devient immuable. Cette double nature
  évite toute conversion entre un format de travail et un format d'archive.
- **Le manifeste est le point de commit.** `manifest.json` est écrit en dernier. Un
  paquet dont manifeste n'est pas à jour est `recoverable`, jamais `valid`.
- **Reprise par l'index.** `runs/index.json` fait foi. La reprise ne relit jamais le
  contenu des répertoires, et un répertoire sans entrée d'index est ignoré.
- **Écriture sérialisée et ordonnée.** L'ordre d'écriture de chaque run est normatif
  (`PACKAGE_FORMAT.md` §5.2), afin qu'une écriture interrompue laisse un état
  interprétable.
- **Déterminisme du paquet scellé.** Horodatages d'entrées fixés, permissions
  normalisées, ordre imposé, chemins absolus interdits, compression à niveau fixé.
  Deux campagnes identiques donnent deux paquets identiques octet pour octet.
- **Périmètres d'écriture disjoints.** Le Launcher écrit le manifeste, l'expérience,
  les métadonnées de run, les journaux et les empreintes. ECHOS écrit `data/` et
  `analysis/`. Aucun producteur n'écrit dans le périmètre d'un autre.
- **Versionnage par `schema`.** Champ obligatoire partout, évolution additive, refus
  explicite d'une version inconnue, jamais de lecture approximative.
- **Contre-mesures de sécurité.** Chemins relatifs uniquement, refus de `..`, de
  lien symbolique et d'entrée exécutable, plafond de décompression, écriture
  exclusive.

## Conséquences

### Positives
- Un seul format pour le travail et pour l'archive : pas d'export, pas de
  conversion, donc pas de perte entre l'exécution et le partage.
- La reprise est simple et fiable, car elle repose sur un index unique et non sur
  l'exploration d'une arborescence.
- Le déterminisme transforme la reproductibilité en **test** : deux paquets se
  comparent directement, ce qui est un contrôle bien plus fort qu'une relecture.
- Le ZIP est un format courant, donc lisible sans outil dédié, ce qui sert
  l'exigence d'autonomie.
- Le versionnement par `schema` permet d'évoluer sans casser les paquets existants,
  avec une règle de refus explicite plutôt qu'un comportement implicite.

### Négatives
- Le ZIP n'offre pas de transactions. L'atomicité est reconstruite par l'ordre
  d'écriture normatif et par le manifeste, ce qui laisse une fenêtre de
  récupérabilité, mais pas d'annulation d'écriture.
- Écrire en incrément dans un ZIP impose de maintenir un index central d'entrées en
  mémoire, ce qui consomme de la mémoire à proportion du paquet.
- La normalisation des horodatages d'entrées est un travail de fond, souvent
  négligé, et qui conditionne le déterminisme.
- Le double nature rend la gestion des cas d'erreur plus riche : un paquet peut
  être valide, vivant, scellé ou récupérable, et chaque cas a un comportement
  distinct.

### Risques
- **Le format devient un point de blocage.** Un paquet écrit en V0.1 doit rester
  lisible bien plus tard. Mitigation : le `schema` n'évolue que par ajout de champs
  optionnels, et toute rupture majeure est refusée à la lecture plutôt que
  interprétée.
- **Le déterminisme est perdu par une optimisation.** Un ajout de compression
  parallèle, ou un ordre d'écriture libre, suffit à produire des paquets différents.
  Mitigation : la comparaison octet pour octet est un test de propriété permanent,
  dans `TESTING.md` §5.
- **Un paquet volumineux devient un piège.** Un paquet très volumineux, non
  particionnable, est peu partageable. Mitigation : le sujet est explicitement ouvert
  dans `PACKAGE_FORMAT.md` §9, avec la stratégie d'index comme piste.
- **Sécurité différée.** Ni chiffrement ni signature ne sont prévus, alors qu'un
  paquet destiné au partage peut contenir des données sensibles. Mitigation : le
  sujet est tracé et non oublié, et le refus d'écoute hors boucle locale évite la
  fuite réseau.

## Alternatives considérées

- **Un répertoire ordinaire, pas un conteneur** : refusé. Un répertoire ne peut pas
  être partagé ni scellé comme un artefact unique, et son déplacement à l'ouverture
  est une opération destructive.
- **Deux formats, un vivant et un scellé** : refusé. Cela impose une conversion en
  fin de campagne, donc un moment de perte, un second code et une divergence possible
  entre l'exécution et l'archive.
- **Une base de données, par exemple SQLite** : refusé pour le paquet. SQLite gère
  bien la transposition atomique, mais un fichier de base de données ne peut pas être
  transporté par un tiers ni ouvert par un outil standard, et son format n'est pas
  lisible par un humain. SQLite reste un choix interne possible pour le cache local.
- **JSON ou NDJSON seuls** : refusé. Un paquet de campagne contient des artefacts
  binaires, et un format texte n'en rend pas compte.
- **Un format propriétaire sans base ZIP** : refusé. Le bénéfice propre est faible,
  et le coût est élevé : il faut écrire un lecteur complet, et l'autonomie de
  lecture disparaît.
- **Le format de données d'ECHOS comme format de campagne** : refusé. Le Launcher
  doit être indépendant d'ECHOS, et un format de données n'est pas un format
  d'orchestration : il ne porte ni le plan de campagne, ni les décisions du
  Launcher, ni le journal de session.

## Validation / rejet

- **Déterminisme** : la propriété « deux campagnes identiques donnent deux paquets
  identiques octet pour octet » est un test permanent de `TESTING.md` §5, exécuté
  sur chaque campagne de test du format.
- **Reprise** : la propriété « reprendre un paquet ne rejoue aucun run réussi » est
  vérifiée par génération de cas, y compris par interruption simulée en chaque
  point de la séquence d'écriture.
- **Autonomie** : la relecture d'un paquet sans le Launcher, et dans une locale et
  un fuseau différents, est un test de bout en bout de `TESTING.md` §7.
- **Intégrité et sécurité** : les propriétés de `PACKAGE_FORMAT.md` §5 et §8 sont
  vérifiées par des tests de Zip-bomb, de traversal de chemin, de corruption et
  d'écriture concurrente.
- **Réouverture** : la décision est réexaminée si le coût mémoire de l'écriture en
  incrément devient prohibitif, si un paquet typique dépasse la limite pratique de
  partage, ou si l'autonomie devient nécessaire. Le conteneur pourrait alors changer
  sans que la structure logique change.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création | Retenir un format unique, vivant puis scellé, déterministe |
