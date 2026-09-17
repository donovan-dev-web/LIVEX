# Glossaire V2 — Termes et concepts clés

## A

**Agent**
Entité autonome dotée d'une cognition BDI, capable de percevoir, mémoriser, former des croyances, générer des objectifs et prendre des décisions. Dans V2, les agents peuvent avoir jusqu'à 1000 pour une simulation.

**Acquisition (de croyance)**
Processus par lequel un agent crée une nouvelle croyance à partir d'une observation ou d'une communication.

**Action**
Comportement qu'un agent peut exécuter (MoveTo, Eat, Communicate, etc.). Les actions peuvent être instantanées ou multi-tick.

**Analyse (Analyzer)**
Service .NET qui consomme les métriques de simulation et calcule les indicateurs d'émergence. Produit des rapports sur la complexité cognitive, la propagation de l'information, etc.

**Annonce (Announcement)**
Type de message où un agent diffuse une information à tous les agents dans son rayon de communication.

---

## B

**Belief (Croyance)**
Modèle mental d'un agent sur un fait du monde. Contrairement à la réalité du monde, une croyance peut être fausse. Chaque croyance a une confiance (0-1).

**BDI (Belief-Desire-Intention)**
Architecture cognitive pour les agents. Pipeline séquentiel : Perception → Mémoire → Croyances → Besoins → Objectifs → Utilité → Délibération → Intention → Action.

**Belief Store**
Système de stockage local d'un agent qui maintient ses croyances actuelles avec versioning et révision.

**Bootstrapping**
Phase d'initialisation d'une simulation V2 : création des agents, configuration des ressources, déploiement des obstacles.

---

## C

**Coalitions**
Groupes explicites d'agents qui se sont volontairement associés pour atteindre un objectif commun.

**Cognitive Diversity (Diversité cognitive)**
Métrique d'émergence mesurant la variation des croyances, objectifs et décisions entre agents. Calculée via entropie de Shannon.

**Communication (système)**
Mécanisme permettant aux agents d'échanger des messages structurés. Basé sur rayon local (e.g., 50 unités).

**Communication Protocol (Protocole de communication)**
Spécification formelle des types de messages (Information, Request, Response, Announcement, Warning, Trading, Acknowledgement), de leur contenu, et des règles de traitement.

**Confiance (Trust)**
Valeur numérique (0-1) représentant le niveau de confiance qu'un agent a envers un autre agent. Affecte la crédibilité des messages reçus.

**Confiance révisée (Trust update)**
Processus d'ajustement de la confiance après une interaction. Si un agent X envoie une information vérifiable et correcte, trust(X) augmente.

**Confiance-médiatisée (Trust-mediated)**
Processus par lequel la confiance envers un messager affecte la confiance dans son message. Confiance du message = Confiance du contenu × Confiance de l'expéditeur.

**Confidence (Confiance - dans une croyance)**
Degré de certitude d'une croyance, de 0 (certain que c'est faux) à 1 (certain que c'est vrai).

**Couche d'abstraction**
Chaque système (Perception, Memory, Beliefs, etc.) est une couche indépendante testable.

---

## D

**Decay (Décroissance)**
Processus exponentiel où les souvenirs et croyances perdent en saillance/confiance au fil du temps. Formule : `salience = exp(-decay_rate * age)`.

**Décision**
Résultat du pipeline BDI : choix d'une action parmi les candidats basé sur l'utilité.

**Décision Record**
Log structuré d'une décision : quelles croyances, objectifs, actions candidates, scores d'utilité, action choisie, raison.

**Délibération**
Phase du BDI où l'agent évalue et sélectionne l'action avec l'utilité la plus élevée.

**Desire (Désir)**
En BDI classique, ce qu'un agent souhaite. Dans V2, représenté par les Objectifs générés des Besoins non satisfaits.

---

## E

**Émergence**
Phénomènes complexes qui surgissent des interactions locales d'agents simples (exemple : formation de communautés de confiance).

**Emergence Score**
Métrique composite (0-1) combinant tous les indicateurs d'émergence. Indique le niveau de complexité global.

**Énergie**
Ressource d'un agent (0-100). Les actions consomment de l'énergie. Augmente avec le repos, diminue avec l'activité.

**Entités**
Au sens large : agents, ressources, obstacles. Chaque entité a une position dans le monde.

**Environment System (Système d'environnement)**
Gère les cycles saisonniers, jour/nuit, événements (sécheresse, épidémie).

**Événement (Event)**
Log structuré d'un fait qui s'est produit (PerceptionEvent, DecisionEvent, ActionEvent, CommunicationEvent, etc.).

**Expiration (Expiry)**
Date limite après laquelle une croyance est marquée comme "suspecte" et sa confiance plafonnée.

---

## F

**Fait (Fact)**
Unité d'information : sujet + prédicat + valeur (exemple : "Agent Alice est à position (50,75)").

**Faim (Hunger)**
Besoin physiologique. Augmente chaque tick, diminué par action Eat.

**Faisabilité (Feasibility)**
Évaluation si un agent peut physiquement/mentalement atteindre un objectif.

**Feedback Loop (Boucle de rétroaction)**
Cycle action→conséquence→décision. Positive = auto-amplifiante, Négative = stabilisante.

**Flooding (Inondation)**
Cas pathologique où trop de messages saturent la communication locale.

---

## G

**Godot**
Moteur de jeu utilisé pour le rendu 3D et l'interface de visualisation.

**Goal (Objectif)**
État désirée qu'un agent poursuit. Généré des besoins non satisfaits. Exemple : "Atteindre la nourriture".

**Goal System (Système d'objectifs)**
Composant qui génère, filtre et priorise les objectifs basés sur les besoins.

**Gossip (Rumeur)**
Message relayé d'agent en agent, perdant de la confiance à chaque hop.

**Graphe de confiance (Trust graph)**
Structure de données : nœuds = agents, arêtes = relations de confiance pondérées.

**Greed (Avidité)**
Trait de personnalité augmentant le poids accordé à l'acquisition de ressources.

**Groupe**
Coalition explicite d'agents avec leader, rôles assignés et objectif commun.

---

## H

**Heading (Direction)**
Vecteur unitaire indiquant l'orientation courante d'un agent.

**Heatmap (Carte thermique)**
Visualisation 2D où chaque cellule a une couleur basée sur une métrique (confiance, diversité, etc.).

---

## I

**Information Propagation (Propagation d'information)**
Métrique d'émergence mesurant la vitesse et la qualité de diffusion d'information via communication.

**Intention**
En BDI, décision d'exécuter une action spécifique après délibération.

**Interruption**
Capacité d'un agent à arrêter son action courante si un besoin plus urgent surgit (ex: faim critique).

**Inventaire (Inventory)**
Ressources possédées par un agent.

---

## L

**Level of Detail (LOD) (Niveau de détail)**
Technique d'optimisation : agents loin décident moins souvent, agents près décident à chaque tick.

**Ligne de vue (Line-of-Sight)**
Vérification si deux entités peuvent se percevoir (pas de mur bloquant).

**Logging (Journalisation)**
Système structuré de capture des événements dans SQLite pour analyse post-simulation.

**Louvain (Algorithme)**
Détection de communautés dans graphes. Utilisé pour identifier groupes naturels.

---

## M

**Mémoire (Memory)**
Historique local des observations et événements. Stockée avec salience décroissante.

**Memory System (Système de mémoire)**
Composant gérant le stockage, décroissance et rappel des souvenirs.

**Message**
Unité de communication entre agents. Contient : sender, receiver(s), type, payload, confiance, coûts.

**Message Cost (Coût du message)**
Énergie consommée pour envoyer (5 points) et recevoir (2 points) un message.

**Métrique (Metric)**
Mesure quantifiable d'un aspect de la simulation (diversité cognitive, complexité sociale, etc.).

**Misinformation (Désinformation)**
Croyance fausse propagée parmi agents. Peut être corrigée si la vérité est découverte.

**Monde partiellement observable (Partial Observability)**
État du monde que les agents ne peuvent observer que localement, créant potentiellement des croyances divergentes.

**Monde persistant (Persistent World)**
Simulation continue pouvant être sauvegardée/chargée, contrairement à des runs indépendantes.

---

## N

**Navigation**
Système de déplacement utilisant pathfinding Godot pour contourner obstacles.

**Needs (Besoins)**
Motivations intrinsèques (Hunger, Thirst, Fatigue, Safety, Social, Curiosity) générant les objectifs.

**Needs System (Système de besoins)**
Composant calculant le niveau de chaque besoin chaque tick.

**Nœud (Node)**
Dans graph, représentant un agent ou une entité.

---

## O

**Objectif (Goal)**
État qu'un agent souhaite atteindre. Voir Goal.

**Observabilité partielle**
Voir Monde partiellement observable.

**Observation**
Donnée sensorielle retournée par Perception System. Inclut : entity, position, distance, confiance, attributs.

**Obstacle**
Entité statique (mur, rocher) bloquant la perception et le mouvement. V2 = statiques uniquement.

**Obstacle System (Système d'obstacles)**
Gère les obstacles statiques, les collisions, les lignes de vue.

---

## P

**Partial Observability**
Voir Monde partiellement observable.

**Pathfinding (Recherche de chemin)**
Algorithme (A*, Dijkstra intégré à Godot) trouvant le chemin optimal autour des obstacles.

**Payload (Contenu)**
Données transportées par un message (JSON structuré).

**Perception**
Acte de sensorialiser l'environnement local. Retourne observations.

**Perception System (Système de perception)**
Composant gérant les capteurs, requêtes spatiales, calcul de confiance.

**Personnalité (Personality)**
Traits d'un agent (Bravery, Curiosity, Sociability, etc.) affectant les décisions.

**Persistent (Persistance)**
Voir Monde persistant.

**Phase (Étape)**
Une des 12 phases du roadmap (0-12) représentant une étape majeure du développement.

**Phénomène émergent (Emergent phenomenon)**
Comportement complexe surgissant des interactions (ex : formation de hub d'information).

**Population (Population)**
Ensemble d'agents dans une simulation.

---

## R

**Radius (Rayon)**
Distance maximale pour la perception ou communication. Par défaut 50 unités.

**Receive (Recevoir)**
Agent traite un message entrant. Ajuste croyances et confiance envers l'expéditeur.

**Rédaction (Draft)**
Phase préalable avant implémentation complète.

**Relation (Relationship)**
Lien de confiance entre deux agents.

**Renderer (Rendu)**
Application Godot affichant visuellement la simulation.

**Resource (Ressource)**
Entité consommable (nourriture, eau) présente en quantités limitées dans le monde.

**Resource System (Système de ressources)**
Gère la régénération, consommation et dégradation des ressources.

**Révision (Revision)**
Processus d'ajustement de croyances existantes à la lumière de nouvelles preuves.

**Rumeur (Gossip)**
Information propagée d'agent en agent, perdant confiance à chaque hop.

---

## S

**Salience (Saillance)**
Poids/importance d'une mémoire. Décroît exponentiellement avec l'âge.

**Save/Load (Sauvegarde/Chargement)**
Capacité à enregistrer l'état complet du monde en SQLite et le recharger plus tard.

**Scenario**
Configuration initiale d'une simulation (nombre d'agents, ressources, obstacles).

**Schema (Schéma)**
Structure de données SQLite définissant tables et colonnes.

**Seed (Graine)**
Valeur initiale du générateur de nombres aléatoires pour reproducibilité.

**Send (Envoyer)**
Agent émet un message. Coûte de l'énergie.

**Social Complexity (Complexité sociale)**
Métrique d'émergence mesurant structure du réseau de confiance, clustering, communautés.

**Source (Source)**
Origine d'une croyance : "observation", "hearsay", "inference".

**Spatial Grid (Grille spatiale)**
Index 2D d'optimisation permettant queries O(1) des entités proches.

**Specification (Spécification)**
Document formel décrivant comportements, algorithmes, interfaces.

**SQLite**
Moteur de base de données pour la persistance V2.

**State (État)**
Configuration complète d'une simulation à un tick donné.

**Strength (Force)**
Trait de capacité physique affectant succès des combats.

**Sub-issue (Sous-tâche)**
Tâche enfant d'une tâche plus grande dans le roadmap.

---

## T

**Tension (Tension)**
Conflit entre croyances contradictoires chez un même agent.

**Tick (Itération)**
Une unité de temps de simulation. Chaque tick, tous les agents exécutent leur pipeline BDI.

**Trait (Trait)**
Caractéristique de personnalité ou capacité d'un agent (Bravery, Speed, etc.).

**Transmission (Transmission)**
Relayage d'un message par un agent à d'autres.

**Trading (Commerce)**
Échange de ressources entre agents. Type de message spécialisé.

**Trust (Confiance)**
Voir Confiance.

**Truth (Vérité)**
État réel du monde. Peut diverger de ce qu'un agent croit.

---

## U

**Utility (Utilité)**
Score numérique d'une action candidate : `(benefit - cost - risk) * confidence * personality + urgency`.

**Utility Evaluator (Évaluateur d'utilité)**
Composant calculant les scores d'utilité pour toutes les actions.

---

## V

**Vector2**
Structure 2D (X, Y) pour positions et directions.

**Versioning (Gestion des versions)**
Système de migration schéma SQLite pour évolutions futures.

**Visualization (Visualisation)**
Rendu graphique de la simulation (Godot, Web UI).

---

## W

**World (Monde)**
Environnement global contenant tous les agents, ressources, obstacles. Entité centrale de la simulation.

**World State (État du monde)**
Snapshot complet : tous les agents + ressources + obstacles + tick courant.

**WebSocket**
Protocole de communication bidirectionnelle entre moteur simulation et Web UI pour metrics temps-réel.

---

## Z

**Zone (Zone)**
Région du monde utilisée dans LOD pour décider de la fréquence de décision d'un agent.

---

## Acronymes courants

| Acronyme | Signification |
|----------|---------------|
| ADR | Architecture Decision Record |
| API | Application Programming Interface |
| ACID | Atomicity, Consistency, Isolation, Durability |
| ASP.NET | Active Server Pages .NET |
| BDI | Belief-Desire-Intention |
| CSV | Comma-Separated Values |
| FIPA | Foundation for Intelligent Physical Agents |
| GC | Garbage Collection |
| JSON | JavaScript Object Notation |
| LOD | Level of Detail |
| PRNG | Pseudo-Random Number Generator |
| RDBMS | Relational Database Management System |
| REST | Representational State Transfer |
| SQL | Structured Query Language |
| UI | User Interface |
| V1 | Version 1 |
| V2 | Version 2 |
| V3 | Version 3 (futur) |
| WebSocket | Protocole de communication bidirectionnelle |
| XP | Extreme Programming |

---

## Concepts connexes

**Agent-based modeling (ABM)**
Simulation de système complexe via agents autonomes interagissant localement.

**Causal graph**
Représentation des relations cause-effet dans la simulation.

**Emergent behavior**
Comportement global surgissant de règles locales simples.

**Game theory**
Étude des interactions stratégiques entre agents.

**Information theory**
Théorie mathématique de l'information et son entropie.

**Markov Decision Process (MDP)**
Formalisme pour prise de décision sous incertitude.

**Social network analysis**
Étude de structures de relations sociales.

---

## Conventions de notation

- `CamelCase` : Classes C#, noms de composants
- `snake_case` : Variables, paramètres locaux
- `SCREAMING_SNAKE_CASE` : Constantes
- `0.0` à `1.0` : Valeurs normalisées (confiance, utilité)
- `{...}` : Objet/dictionnaire JSON
- `[...]` : Tableau/liste
- Entités : Agent, Resource, Obstacle (capitalisées)

