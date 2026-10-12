# Annexe J. Du prototype à la version décrite

Ce livre décrit un état, mais le projet a une histoire : un prototype a précédé la version actuelle, et celle-ci n'en est pas une simple continuation. Cette annexe compare les deux, en séparant ce qui a été **conservé**, ce qui a été **changé** et ce qui a été **abandonné**. Elle a une fonction précise : un lecteur qui trouve une valeur, une formule ou un nom dans un document ancien doit pouvoir savoir s'il est encore valable.

**Pourquoi cette annexe importe.** Un projet de recherche qui réécrit son propre passé sans le dire perd la possibilité de comprendre ses choix. Les décisions du prototype ne sont pas des erreurs : ce sont les hypothèses que l'implémentation a permis d'éprouver. Les exposer est une condition de sérieux.

## 1. Deux phases, deux natures

| | Prototype (mars à août 2026) | Version décrite (septembre à octobre 2026) |
| :-- | :-- | :-- |
| Nature | un artefact qui fonctionne | un système à composants, versionné et contractuel |
| Validation | tests fonctionnels (quatre-vingt-dix-huit) | plusieurs centaines de tests, campagnes de calibration, déterminisme vérifié |
| Documentation | un document de spécification unique | documents par composant, décisions d'architecture numérotées, monographie |
| Composants | un moteur avec rendu intégré | SYNE, ECHOS, PRISM, Launcher, installateur, séparés par contrats |
| Persistance | sérialisation simple | snapshot bit-à-bit et schéma relationnel versionné |
| Rendu | Godot 4.7.2 (.NET) | Unreal Engine, par un plugin de transport |

Le passage d'un artefact à un système est le mouvement le plus structurant de cette histoire. Il explique la plupart des différences qui suivent.

Sur le rendu, le prototype utilisait **Godot 4.7.2 en édition .NET**, cohérent avec un dépôt majoritairement .NET, tandis que le choix du moteur définitif restait ouvert. La version décrite retient **Unreal Engine**, avec un plugin d'intégration en couche mince. La conséquence est la même dans les deux cas : le moteur de simulation ne dépend d'aucun moteur graphique, et les documents historiques qui citent Godot décrivent une couche de représentation, non l'état actuel.

## 2. Ce qui a été conservé

Cinq choix du prototype ont traversé la réécriture intacte, ce qui leur donne une valeur particulière : ils ont survécu à l'épreuve du code.

- **Le déterminisme comme contrat** (ADR-006). Le générateur est reproductible, l'usage de `System.Random` est interdit, et l'état du générateur est sauvegardé.
- **La fonction d'utilité.** Sa forme, un solde bénéfice moins coût moins risque, pondéré par la confiance et la personnalité, puis majoré d'une urgence, est celle du prototype. Seules les constantes ont bougé.
- **La mémoire oubliante** (ADR-007). La décroissance exponentielle de la saillance par catégorie, avec un seuil d'oubli et une capacité bornée, vient du prototype.
- **La communication non confidentielle** (ADR-008). Le signal public, local et dégradable était déjà le principe retenu.
- **L'énergie comme monnaie d'action** (ADR-009). Une entité paie ses actions en énergie, et une énergie nulle est fatale.

Chacun des cinq choix ci-dessus a sa décision d'architecture : ce qui a été conservé ne l'a pas été par inertie, mais réaffirmé après avoir été éprouvé par le code.

## 3. Ce qui a été changé, et pourquoi

| Élément | Prototype | Version décrite | Motif du changement |
| :-- | :-- | :-- | :-- |
| Rendu (décision ADR-002) | Godot 4.7.2 (.NET) | Unreal Engine, par un plugin | changer de rendu sans toucher au moteur |
| Persistance (ADR-011) | JSON simple | JSON bit-à-bit **et** schéma SQLite versionné | transactionnalité, requêtes, migration |
| Observatoire (ADR-007 du Launcher) | interface intégrée | service sans interface | mesurer sans risquer d'influencer |
| Échelle temporelle (ADR-017) | un tick fixe | échelle configurable | découpler le rythme de la physique simulée |
| Morphologie (ADR-010) | entités avec forme physique | morphologie abandonnée, champs logiques conservés | la forme physique ajoutait une complexité massive sans bénéfice pour l'émergence de haut niveau |
| Mort | une santé à zéro tue, et l'entité morte reste dans le monde comme objet observable | une énergie à zéro tue, avec dissolution complète et un simple événement de trace | supprimer un état intermédiaire redondant et éviter une population de cadavres inertes |
| Complexité du système | formules non bornées selon les ticks | moyenne de trois dispersions normalisées | une grandeur qui croît avec la durée du run n'est pas une complexité |
| Indice d'imprévisibilité | présent | retiré | mesurer l'imprévisibilité exigerait une base prédictive hors échantillon |

**Trois renommages** méritent d'être cités séparément, parce qu'ils relèvent de l'honnêteté et non de l'implémentation. Une « centralité de réseau » mesurait la concentration des émissions ; une « vitesse de diffusion » mesurait un délai d'émission ; un « détecteur de boucles » comptait des répétitions. Les trois ont été renommés pour dire ce qu'ils mesurent réellement. Le récit de ces corrections est un des fils du chapitre ECHOS II.

## 4. Ce que la calibration a corrigé

La différence la plus intéressante entre le prototype et la version décrite n'est ni technique ni architecturale : elle est numérique. Plusieurs valeurs plausibles rendaient le scénario de référence **non viable**, et seule une campagne a permis de le constater. Les deux corrections sont documentées comme décisions : ADR-015 pour l'arbitrage alimentaire (calibration D1), ADR-016 pour la stabilité du profil par défaut (calibration B1).

| Paramètre | Avant | Après | Effet constaté |
| :-- | --: | --: | :-- |
| Coût énergétique d'un déplacement | 0,5 puis 0,05 | 0,03 | l'énergie décroissait sans borne, jusqu'à l'extinction lente |
| Bénéfice de manger et de boire | plafonné à 30 | `min(besoin, 100) × 0,6` | à faim saturée, manger perdait contre socialiser : utilité moyenne 13,9 contre 83,7 |
| Dérive du besoin social | 0,001 | 0,0002 | sans mécanisme de satisfaction, la socialisation occupait jusqu'à 96 % des décisions |
| Dérive de la curiosité | 0,002 | 0,0005 | même cause : un besoin sans exutoire devient une tyrannie |

La leçon transversale est reprise à plusieurs endroits du livre : un mécanisme peut être juste localement et toxique globalement, et seule une mesure sur une campagne révèle l'écart. Un paramètre n'est pas un réglage cosmétique, c'est une loi du monde.

## 5. Ce que la comparaison ne permet pas de conclure

Cette annexe est un constat de trajectoire, pas une évaluation comparative.

- Elle ne démontre **pas** que la version décrite produise des comportements plus intéressants que le prototype : aucune campagne comparable n'a été menée sur les deux.
- Elle ne prouve pas que les changements soient tous des progrès : l'abandon de la morphologie et du rendu intégré a un coût, notamment pour l'expérience d'immersion, qui reste hors périmètre.
- Elle s'appuie sur des documents de dates différentes ; en cas d'écart entre cette annexe et le code, **le code fait foi**.
- Les valeurs « avant » sont celles documentées par le prototype et ses successeurs immédiats ; elles ne constituent pas une base de mesure indépendante.
