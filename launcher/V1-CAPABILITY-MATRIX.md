# Matrice de capacités observées — Launcher V1

**Référence :** branche `develop`, commit de base `90f06463` (2 octobre 2026),
plus le code et les documents de suivi en cours dans la worktree  
**Nature :** état observé et cible d’acceptation proposée ; ce document ne
déclare pas conformes les capacités non testées avec le composant réel.

Cette matrice est le livrable de cadrage du jalon 0 dans
[`ROADMAP-V1.md`](ROADMAP-V1.md). Elle distingue :

- **Implémenté** : le code existe dans le dépôt ;
- **Stub testé** : validé avec un composant factice, pas avec le runtime réel ;
- **Partiel** : le service peut fonctionner, mais il manque une capacité ou une
  intégration au parcours attendu ;
- **Absent** : aucun manifeste ou support exécutable n’a été détecté ;
- **Conditionnel** : disponible seulement sur les plateformes/capacités
  expressément indiquées.

## 1. Capacités par composant et variante

| Rôle / variante | Détection dans le Launcher | Démarrage supervisé constaté | Run / analyse réel | OS déclaré | Limite décisive |
| :-- | :-- | :-- | :-- | :-- | :-- |
| **SYNE réel** | `syne/component.json` présent, exécutable Linux publié déclaré | Service supervisé avec readiness, `/info`, commandes mutantes protégées et arrêt authentifié ; campagne réelle exécutée par le Launcher depuis une copie isolée du publish Linux | Batch autonome `reference`, seed/ticks et `result.json` déterministe avec checksum ; en mode supervisé, `--run-id` et `--export-stream` produisent `data/stream.jsonl`, artefact rejouable archivé dans le paquet ; test E2E vérifie que le Launcher archive résultat, flux et journaux, conserve la version SYNE et scelle un `.livexp` lisible | Linux seulement | J2A accepté sur Linux ; aucun support Windows/macOS déclaré |
| **SYNE émulé (`syne-mock`)** | `component.json` présent | Oui, service Linux avec ports HTTP/WebSocket, sonde et arrêt authentifié | Émule le protocole et un scénario ; pas un exécuteur de campagne compatible ni une simulation scientifique équivalente | Linux seulement | Doit rester exclusif avec SYNE réel et clairement étiqueté comme émulation |
| **ECHOS headless** | `component.json` présent | Oui, adaptateur Linux ; `/health/ready` vérifie la disponibilité et l'interrogeabilité de la base | Les trois routes `/analysis/run`, `/analysis/experiment`, `/analysis/report` sont implémentées, déterministes et headless ; `POST /ingest/run` enregistre un run archivé à partir de son `stream.jsonl`, et le Launcher l'appelle avant chaque analyse | Linux seulement dans le manifeste du Launcher | La validation de l'installation ECHOS Linux (versions et OS supportés, ressources runtime) n'est pas faite |
| **ECHOS UI / Electron** | **Supprimé le 5 octobre 2026** (`ADR-007`) : `echos-ui/` et `echos-desktop/` retirés du dépôt, montage statique et CI associés supprimés | Sans objet : plus de variante UI à sélectionner | L’observation se fait par les **fenêtres natives du Launcher** (consoles de logs, fenêtre d’analyse sondant l’API REST) | Sans objet | Aucune : la frontière est tranchée — ECHOS est une API seule, le Launcher présente |
| **PRISM / PRISM-LDK** | Aucun `component.json` PRISM trouvé | Aucun démarrage Launcher accepté | Plugin Unreal avec contrats/transport et types d’intégration ; expérience de rendu/interaction complète non validée dans le Launcher | Non déclaré | Mode Immersion doit rester verrouillé jusqu’à manifeste et tests d’acceptation réels |
| **Launcher + stubs** | Stubs SYNE/ECHOS/PRISM inclus dans la solution | Oui pour tests automatisés | Campagnes et orchestration vérifiées par suites unitaires/intégration/E2E contre stubs | Selon environnement de test .NET | Ne prouve pas la conformité des composants réels |

### Contrats observés pour les adaptations Linux

- `syne/component.json` déclare le binaire publié Linux, le contrôle HTTP et le
  WebSocket d'observabilité ; le scénario batch réellement supporté est
  `reference`, avec arrêt authentifié et readiness distinguée de la vivacité.
- `echos/component.json` déclare l’exécutable `echos-launcher`, le contrôle HTTP
  sur le port 5000 et la capacité `headless`/`analytics`. Le service utilise
  `/health/ready` et `/control/shutdown`; ses rapports headless dépendent de la
  base ECHOS peuplée en amont.
- `syne-mock/component.json` déclare le contrôle HTTP (5181), le flux WebSocket
  (5180, argument `--data-port`), `headless`, `control` et `observability`.
  Son endpoint de statut sert de sonde ; l’arrêt est authentifié.
- Ces ports sont les valeurs déclarées par défaut dans les manifestes, pas une
  garantie qu’ils sont libres. Le Launcher doit les réserver et transmettre les
  valeurs résolues selon le contrat.

## 2. Matrice des modes V1

| Mode | Intention | Ce qui est réellement disponible à la référence | Critère d’acceptation V1 |
| :-- | :-- | :-- | :-- |
| **Console** | Exécuter une campagne headless avec SYNE + ECHOS, sans PRISM | Scénario par défaut du Launcher aligné sur le flux des données : export du flux par SYNE, ingestion ECHOS, analyse et rapport archivés | Campagne réelle complète vérifiée de bout en bout sur SYNE et ECHOS réels, y compris la réanalyse à l'identique depuis le paquet ; arrêt/reprise sans rejouer les runs terminés |
| **Standard** | Profil par défaut, rôle SYNE choisi explicitement, autres composants démarrables manuellement | Les manifestes Linux et services SYNE/ECHOS/mock existent ; le lancement SYNE via une installation publiée n’a pas encore passé l’acceptation Launcher | Démarrage manuel supervisé des seuls composants disponibles, variante et état affichés, aucune capacité absente proposée comme disponible |
| **Développement** | Remplacer SYNE réel par son émulateur et permettre le travail sur les clients | Profil d’émulation + ECHOS supervisable sous Linux ; l’exclusivité réel/mock est appliquée ; campagnes `.livexp` avec le mock non prises en charge | Mock et ECHOS démarrent/arrêtent proprement, SYNE réel exclu ; toute exécution de démonstration est marquée émulée et n’est pas présentée comme résultat scientifique |
| **Personnaliser** | Choisir les rôles et variantes disponibles | Sélection par rôle et installations gérées ; manifests existants limités à Linux et pas de sélection déclarative UI/headless | Afficher seulement les installations/capacités compatibles avec l’OS et la sélection ; refuser avant lancement une combinaison insatisfaisable avec cause exacte |
| **Immersion / PRISM** | Rendu Unreal temps réel avec SYNE | PRISM est détecté uniquement s’il est ajouté comme installation, mais aucun manifeste PRISM accepté n’est présent ; profil verrouillé | Déverrouiller uniquement après conformité manifeste, flux, readiness/arrêt, métriques de rendu et test que la panne PRISM n’interrompt pas SYNE |

## 3. Capacités transverses et critères de validation

| Capacité | État observé | Propriétaire principal | Preuve requise pour clore |
| :-- | :-- | :-- | :-- |
| `.livexp` : lecture, écriture, scellement, intégrité | Implémenté et testé | Launcher | Tests de propriétés et acceptation des paquets produits par runs réels |
| Détection, profils, exclusivité du rôle SYNE | Implémenté ; SYNE réel reste non compatible | Launcher | Tests de résolution par manifeste, variante, OS et sélection impossible |
| Gestion du processus et sondes | Implémentée au niveau Launcher ; ECHOS/mock adaptés sur Linux | Launcher + chaque composant | Test d’acceptation par composant : démarrage, readiness, arrêt et absence d’orphelin |
| Campagne séquentielle / reprise | Implémentée contre stubs | Launcher + SYNE réel | Campagne réelle interrompue puis reprise sans rejouer run réussi |
| Analyse et rapports ECHOS | API réelle headless implémentée sur runs en base ; le Launcher ingère le flux archivé de chaque run avant d'analyser, sous l'identité `{campagne}-{run}` | ECHOS + Launcher | Run SYNE ingéré, trois opérations réelles appelées par le Launcher, artefacts archivés et relus à l’identique |
| Monitoring système | Échantillonnage système présent | Launcher | Tests de disponibilité et d’affichage des données manquantes |
| Monitoring de flux | Non fourni par endpoints/collecteurs actuels | SYNE/ECHOS/mock + Launcher | Mesures issues de compteurs fiables, avec unité, période, erreurs et provenance ; sinon affichage explicite « non fourni » |
| Logs session et logs de run | Consultation/export implémentés | Launcher | Tests des bornes, refus d’entrées invalides, export exact et ouverture du chemin |
| UI/headless configurable par composant | Pas décrit comme variante dans les manifestes actuels | Launcher + composants | Capacité/variant explicite, contrôle des prérequis, test par mode et OS |
| Déterminisme scientifique | Tests SYNE présents ; conformité end-to-end Launcher à établir | SYNE + Launcher | Deux runs identiques (même version/configuration/OS/seed) produisent la même empreinte d’artefacts |
| Installation Launcher Windows/Linux | Builds de CI pour composants présents ; installateur Launcher non livré | Launcher | Installation propre, `--check`, mise à jour et conservation des données sur chaque OS annoncé |

## 4. Décisions de cadrage retenues pour la V1

Ces choix reprennent les contrats et décisions déjà présents dans les
documents du dépôt ; toute évolution doit être enregistrée comme décision,
pas glissée dans le code sans mise à jour documentaire.

1. SYNE réel reste l’unique autorité de simulation ; le mock est une variante de
   développement du même rôle, jamais un quatrième composant ou un moteur
   concurrent.
2. Console, Standard, Développement et Personnaliser sont les quatre modes
   utilisateur. Les combinaisons impossibles restent visibles avec une cause,
   mais ne sont pas démarrables.
3. Les campagnes V1 sont séquentielles, mono-moteur et reprenables sans rejouer
   les runs terminés.
4. Le Launcher transporte et archive les résultats ; ECHOS calcule les
   analyses et produit les rapports.
5. Une erreur d’analyse est consignée et ne produit pas de rapport de
   substitution ; les données de simulation déjà obtenues restent préservées.
6. Aucun redémarrage automatique n’est introduit sans politique décidée et
   testée.
7. PRISM reste verrouillé comme capacité optionnelle distincte des parcours
   headless ; il n’est pas une condition pour déclarer opérationnels Console,
   Développement ou Standard sans immersion.
8. La V1 supporte uniquement les OS explicitement construits et acceptés par
   manifeste et tests. Un build d’un composant n’implique pas le support de
   toute la pile sur cet OS.

## 5. Questions explicitement hors du cadrage automatique

La roadmap peut avancer sans trancher ces sujets parce que la V1 peut garder
les comportements les plus restrictifs :

- **IPv6 / écoute réseau distante :** rester en loopback IPv4 tant que la
  stratégie de sécurité n’est pas modifiée.
- **Redémarrage automatique :** rester désactivé.
- **Parallélisme et multi-espace :** rester exclus de V1.
- **Immersion PRISM :** rester verrouillée jusqu’à validation réelle.
- **Mesures de flux :** signaler l’absence au lieu de calculer une approximation.
- **Campagnes avec le mock :** option de démonstration séparée, non requise pour
  l’acceptation scientifique de V1.
