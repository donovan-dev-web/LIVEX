# Conventions de lecture

Ce livre décrit un système en activité. Pour qu'il reste honnête et vérifiable, quelques conventions sont fixes dans tout l'ouvrage. Elles concernent les statuts, le vocabulaire, la typographie, la présentation des formules et des schémas, et la manière dont chaque chapitre est construit.

## 1. Les cinq statuts d'une capacité

Une même phrase peut dire la vérité ou mentir selon qu'elle décrit une fonction livrée ou une intention. Pour éviter cette confusion, chaque capacité mentionnée relève de l'un des cinq statuts suivants, employés de manière stable dans tout le livre.

- **Disponible** : la capacité est livrée et vérifiée dans la version annoncée. Un test, un contrat ou une campagne rejouable l'atteste.
- **Expérimental** : la capacité est utilisable, mais avec des limites connues et documentées. Elle peut changer.
- **En développement** : le travail est engagé, mais rien n'est garanti pour une version donnée.
- **Envisagé** : piste considérée, sans engagement de livraison. Aucune promesse n'est attachée.
- **Hors périmètre** : la capacité est volontairement non prise en charge dans l'état décrit. Elle est exclue, non oubliée.

Lorsqu'un chapitre décrit un mécanisme, il précise son statut, sa version et, quand c'est utile, la preuve qui l'accompagne. Une capture d'écran, un schéma ou un chiffre n'établissent jamais à eux seuls qu'une fonction est disponible. Seuls le code exécuté, le contrat tenu et la preuve rejouable font foi.

Ces cinq statuts ne sont pas une échelle de qualité. Une capacité **expérimentale** n'est pas « moins bien » qu'une capacité **disponible** : elle est simplement dans un état où l'on ne peut pas encore l'affirmer. Une capacité **hors périmètre** n'est pas un défaut : c'est un choix. Le lecteur doit donc lire ces mots comme des informations, non comme des jugements.

## 2. Le vocabulaire

Quelques distinctions reviennent sans cesse et sont utilisées avec un sens précis.

- **Entité**, **agent simulé** : un être simulé autonome doté d'un état, de besoins, d'une perception, d'une mémoire, de croyances, de capacités et d'actions. Ce n'est ni un personnage scénarisé, ni un modèle d'apprentissage.
- **Autonomie fonctionnelle** : la capacité de sélectionner ses actions sans commande externe directe, à partir de son état interne et de son modèle. Elle ne suppose ni conscience, ni volonté, ni libre arbitre au sens philosophique.
- **Émergence faible** : l'apparition, au niveau du système, de propriétés qui ne sont pas spécifiées comme objectifs par les règles locales, mais qui restent analysables et retraçables à ces règles.
- **Observabilité partielle** : le fait, pour une entité, de ne percevoir et ne connaître qu'une partie de l'état du monde.
- **Déterminisme** : la propriété, pour un run donné (même graine, même configuration, même version), de produire exactement la même trajectoire.
- **Tick** : l'unité de temps discrète de la simulation. Par défaut, un tick vaut une minute simulée.
- **Run** : une exécution complète et reproductible, définie par une graine, une configuration, une version de moteur et un état initial.

Le glossaire (annexe E) développe ces définitions et les complète. Les acronymes sont explicités à leur première occurrence dans chaque chapitre, afin que le livre reste lisible même ouvert au milieu.

Une convention de vocabulaire mérite d'être signalée d'emblée : le mot **société** n'est employé, dans ce livre, que pour désigner un objet d'interprétation, jamais un résultat du moteur. Le moteur produit des groupes, des confiances, des messages : des faits mesurables. Parler de « société » est une lecture humaine de ces faits, et le livre la marque comme telle. De même, le mot **culture** n'apparaît que pour décrire une frontière : la transmission culturelle n'est pas encore cognitive dans l'état décrit.

## 3. Typographie et mise en page

Quelques règles rendent le texte homogène et imprimable.

- Le tiret cadratin, le tiret demi-cadratin et le tiret insécable ne sont pas employés comme ponctuation. Les incises utilisent la virgule, les parenthèses ou les deux-points. Le trait d'union reste réservé à la formation des mots (par exemple « c'est-à-dire », « peut-être »).
- Les acronymes désignent les composants : **LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*), **SYNE** (*Systems & Emergent Network Engine*), **ECHOS** (*Emergent Cognition & Holistic Observation System*), **PRISM** (*Perceptual Rendering & Interactive Simulation Module*), **PRISM-LDK** (*LIVEX Development Kit*, module technique `PrismLdk`).
- Les formules sont présentées en ligne ou dans des blocs encadrés, avec les symboles explicités au moment de leur introduction.
- Les valeurs numériques citées sont les valeurs par défaut du moteur à la version indiquée, sauf mention contraire. Elles proviennent du fichier de configuration, du contrat ou du code.
- Les schémas portent un titre, une légende et, si nécessaire, une version. Un trait plein désigne un élément disponible, un trait pointillé un outil ou un élément en développement, ou hors périmètre. Aucun schéma n'est décoratif.

La règle typographique sur les tirets mérite une explication, car elle est inhabituelle. Beaucoup de textes français emploient le tiret cadratin pour les incises. Ce livre l'évite pour trois raisons : il se veut imprimable sans surprise selon les moteurs de composition, il privilégie une ponctuation qui se lit à voix haute, et il réserve la ponctuation forte aux incises réellement importantes. Le lecteur ne perd rien : la virgule, la parenthèse et le deux-points couvrent tous les cas nécessaires.

## 4. Présentation des formules

Les formules apparaissent de deux manières. En ligne, lorsqu'elles sont courtes et qu'elles s'insèrent dans une phrase. En bloc, lorsqu'elles structurent un raisonnement. Un bloc de formule est toujours accompagné, juste avant ou juste après, de la signification de chaque symbole et de l'unité employée.

Une formule, dans ce livre, n'est jamais un ornement. Chacune décrit un mécanisme réel du système, avec des valeurs par défaut citées. Lorsqu'une constante est signalée comme « héritée et non encore calibrée », le livre le dit explicitement, afin qu'aucune valeur provisoire ne soit prise pour une référence.

## 5. Présentation des schémas

Les schémas sont des diagrammes, reproductibles depuis le texte source. Chaque schéma est accompagné d'une légende qui indique ce qu'il montre et, lorsque c'est utile, ce qu'il ne montre pas. Un schéma d'architecture ne prouve pas qu'une fonction est livrée : il montre un flux ou une structure. La règle générale s'applique : seule une preuve rejouable établit la disponibilité d'une capacité.

## 6. Structure d'un chapitre

Chaque chapitre suit la même charpente.

1. Il ouvre sur la question qu'il traite.
2. Il progresse sans rupture : une explication accessible d'abord, puis le détail technique (structures, algorithmes, contrats, valeurs), puis l'approfondissement rigoureux (précision, limites, références).
3. Il distingue en permanence l'observation, l'interprétation et l'hypothèse.
4. Il rappelle ce qui est disponible, expérimental, en développement, envisagé ou hors périmètre.
5. Il se termine par ce qu'il apporte à la question directrice et par ce qu'il ne permet pas encore de conclure, avec une transition vers le chapitre suivant.

Cette charpente explique pourquoi chaque chapitre se termine par deux paragraphes symétriques : ce qu'il apporte, et ce qu'il ne permet pas encore de conclure. Le premier est une synthèse, le second une limite. Les deux sont nécessaires : un chapitre qui ne dirait que son apport serait une plaidoirie, un chapitre qui ne dirait que sa limite serait un aveu stérile. Le livre veut les deux.

## 7. Ce que ce livre ne fait pas

Ce livre ne remplace pas la documentation opératoire du projet, qui décrit les procédures d'installation, de construction et de test. Il ne proclame ni supériorité ni validation scientifique. Il ne présente aucune fonction hypothétique comme acquise. Lorsque le texte doit citer un chiffre, un contrat ou un comportement, il le rapporte à sa version et à sa source. En cas de doute entre une description et le code, le code prime.

Enfin, une règle de lecture pour les lecteurs pressés. Les tableaux, les formules et les schémas ne sont jamais indispensables à la compréhension du fil principal. Un lecteur qui les sauterait comprendrait l'essentiel du livre. Un lecteur qui les lit trouverait la précision. C'est un choix d'écriture : le livre doit pouvoir se lire à deux vitesses, sans que la rapidité trahisse le propos.
