ADR-XXX — Système d'inventaire (capacité poids/slots, ressources stockables)
Statut : Proposé

Contexte
--------
La structure d'entité V1 (§3.7.2) définit un inventaire réduit à deux compteurs
bruts (`Nourriture: int`, `Eau: int`), sans capacité maximale. La structure
d'entité V2 (§3.7.3) ne le redétaille pas dans le texte disponible. Le système de
ressources (§3.20.1) prévoit pourtant 4 types en V2 (Food, Water, Wood, Mineral),
et l'action `Trade` (ainsi que les futures primitives `Prendre`/`Donner`/`Échanger`,
voir ADR « Primitives d'actions ») suppose qu'un agent connaisse précisément ce
qu'il possède, en quelle quantité, et dans quelle limite il peut continuer à en
accumuler. Sans cette structure, `Trade` reste une action non exécutable de façon
cohérente, et le phénomène-exemple cité par la doc elle-même comme cas
d'émergence typique — « un marché informel qui apparaît à un carrefour » (§6.4.2)
— est structurellement impossible à produire : un marché suppose des agents
inégalement dotés à un instant t, donc une possibilité de stockage différencié.

Décision
--------
Ajouter à la structure de l'entité un composant `Inventaire` avec :
- `capacitePoids` : poids total maximum que l'entité peut transporter (paramètre
  de configuration, valeur par défaut à fixer — voir point ouvert ci-dessous).
- `slots` (optionnel V0.1, activable en V2) : nombre maximum de types de
  ressources distincts transportés simultanément, en complément de la contrainte
  de poids.
- Une entrée par ressource détenue : `{ typeRessource, quantité }`.

Étendre la définition de chaque type de ressource (§3.20.1) avec deux propriétés
statiques, données de configuration et non logique de moteur :
- `poidsUnitaire` : poids d'une unité de cette ressource.
- `stockable` (bool) : si `false`, la ressource ne peut pas être transférée en
  inventaire — elle ne peut être affectée que par `Consommer` directement sur
  place (ex. un feu, une carcasse fraîche). Si `true`, elle peut être manipulée
  par `Prendre`, `Donner`, `Échanger`.

Règle d'exécution de `Prendre` : l'action échoue (statut `Failed`, §3.15.3) si
`poidsActuel + poidsUnitaire × quantité > capacitePoids`, ou si la ressource
ciblée a `stockable = false`. Aucune règle globale de priorité n'est imposée sur
ce qu'un agent choisit de porter — ce choix reste un résultat du système de
décision (§3.14), pas une contrainte câblée dans le moteur.

Alternatives envisagées
------------------------
1. **Inventaire illimité (statu quo)** — rejeté : bloque toute émergence liée à
   l'accumulation, au don et à l'échange ; contredit le principe d'un monde
   physiquement contraint déjà appliqué à l'énergie et à la perception.
2. **Inventaire à slots uniquement (grille façon RPG), sans poids** — rejeté pour
   la V0.1 : ajoute une granularité (emplacement, empilement) que rien dans le
   modèle actuel de ressources ne justifie encore ; le poids seul suffit à faire
   apparaître un arbitrage de portage, avec moins de paramètres à figer.
3. **Poids ET slots dès la V0.1** — retenu en option : les slots restent une
   contrainte secondaire activable si le poids seul se révèle insuffisant pour
   produire une limite perceptible (ex. objets encombrants mais légers).

Conséquences
------------
- `Prendre`, `Donner`, `Échanger` (voir ADR « Primitives d'actions ») deviennent
  des opérations bien définies sur une structure de données explicite, au lieu de
  s'appuyer sur les deux compteurs ad hoc actuels.
- Le trait `Greed` (déjà présent, §3.14.8, actuellement limité au bonus sur
  `Gather`) peut désormais moduler un comportement d'accumulation réel : un agent
  avare continue `Prendre` au-delà de son besoin immédiat tant que
  `poidsActuel < capacitePoids`, produisant un surplus — condition nécessaire à
  l'apparition spontanée d'échanges (§6.4.2).
- ECHOS (§4.3) gagne une source de métriques nouvelle : distribution de la
  richesse entre entités (à la manière des métriques de complexité sociale déjà
  prévues en §4.3.3), utile pour objectiver le phénomène de marché informel plutôt
  que de le constater seulement de manière anecdotique.
- Le calcul de coût (§3.14.4) de `SeDéplacer` peut être étendu pour tenir compte
  du poids porté (option, à trancher séparément — non requise pour la V0.1
  minimale).
- La section 3.7.2/3.7.3 (structure de l'entité) et la section 3.20.1 (types de
  ressources) doivent être mises à jour en conséquence.

Points ouverts à trancher séparément
--------------------------------------
- [OUVERT] Valeur par défaut de `capacitePoids` pour la V0.1.
- [OUVERT] Wood et Mineral font-ils partie du périmètre minimal de la V0.1, ou
  restent-ils réservés à Food/Water tant que le système d'échange n'est pas
  validé sur un cas simple à deux ressources ?
- [OUVERT] Les `slots` sont-ils activés dès la V0.1 ou différés à la V2 (cf.
  alternative 3 ci-dessus) ?
