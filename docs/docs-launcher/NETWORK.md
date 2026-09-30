# NETWORK.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `COMPONENTS.md`, `OBSERVABILITY.md`, `INTEGRATION_CONTRACT.md`
**Source Monographie** : —

---

## 1. Objet et périmètre

Ce document spécifie les communications entre le Launcher, SYNE, ECHOS, PRISM et
les éventuels composants réseau — sur une machine, sur un réseau local, ou
répartis sur **plusieurs sous-réseaux locaux**.

> **Interprétation retenue de « sous-réseaux locaux ».** Plusieurs machines ou
> segments réseau distincts — par exemple une machine de calcul pour SYNE, un poste
> de visualisation pour PRISM, un poste d'analyse pour ECHOS, éventuellement
> séparés en VLAN. **[Si une autre lectures était visée, le §3 est à adapter ; le
> reste du document tient.]**

### 1.1 Objectifs

1. Faire fonctionner LIVEX **sans configuration réseau** sur une seule machine — le
   cas par défaut.
2. Permettre **plusieurs instances de SYNE**, pour des campagnes parallèles ou la
   comparaison de versions, sans collision de ports.
3. Permettre de **répartir** les composants sur plusieurs machines d'un réseau
   local, y compris sur des sous-réseaux distincts.
4. Offrir un **point d'entrée unique optionnel**, la Gateway : routage,
   authentification, agrégation de la santé.
5. **Ne jamais** rendre un composant dépendant de la Gateway. Elle reste
   optionnelle.
6. Sécuriser par défaut : rien n'est exposé au-delà de la boucle locale sans
   action explicite.

**Non-objectifs** : exposition Internet, multi-utilisateur avec droits, cloud.

## 2. Plan de contrôle et plan de données

C'est la distinction qui permet d'avoir une Gateway sans que le Launcher devienne
un proxy de tout.

```text
PLAN DE CONTRÔLE   (léger, fiable, authentifié)
  Launcher ⇄ composants : démarrage/arrêt, santé, info, métriques, registre

PLAN DE DONNÉES    (volumineux, temps réel)
  SYNE ── WebSocket snapshot/event ──► PRISM, ECHOS
  PRISM, ECHOS ── HTTP de contrôle ──► SYNE
```

Règles :

- Le **plan de données reste direct**, de composant à composant, par défaut. Un
  instantané d'un monde de plusieurs milliers d'agents ne doit pas transiter par un
  intermédiaire sans nécessité.
- Le **plan de contrôle** passe par le Launcher en local, ou par la Gateway en
  distant.
- La Gateway peut **aussi** relayer le plan de données, mais seulement quand le
  lien direct est impossible — sous-réseaux isolés, pare-feu — ou pour centraliser
  l'authentification.

### 2.1 L'adaptateur de protocole

Le protocole de données — le format des messages `snapshot` et `event` — est une
décision encore ouverte. Le Launcher ne doit pas en dépendre pour fonctionner.

Un **adaptateur de protocole** est donc la couche qui isole cette décision. Il
traduit le protocole courant vers les types internes du Launcher, et rien d'autre.

| Règle | Comportement |
| :-- | :-- |
| **Isolation** | `Launcher.Domain` ne référence aucun type du protocole. Seul `Launcher.Integration` le connaît. |
| **Remplacement** | Changer de protocole revient à remplacer l'adaptateur, sans toucher au domaine ni à l'interface. |
| **Indépendance du contrôle** | Le plan de contrôle — santé, infos, métriques, registre, arrêt — ne dépend **jamais** de l'adaptateur. C'est ce qui permet de développer le Launcher avant la décision. |
| **Version déclarée** | Chaque instance publie son `protocolVersion` dans le registre. Une incompatibilité est détectée à l'enregistrement. |
| **Adaptateur minimal** | Si aucun protocole n'est résolu, l'adaptateur minimal suffit : le Launcher pilote le plan de contrôle et ne consomme aucun flux de données. |

Cette séparation a une conséquence concrète : **le Launcher peut être développé,
testé et packagé avant que le protocole soit tranché**. Les stubs n'ont qu'à produire
des messages au format courant ; si le protocole change, seul l'adaptateur change.

## 3. Topologies

| ID | Topologie | Réseau | Gateway | Cible |
| :-- | :-- | :-- | :-- | :-- |
| **T0** | Tout sur un poste | 127.0.0.1 | Non | **V0.1** |
| **T1** | Plusieurs postes, même sous-réseau | LAN | Optionnelle | V1.x |
| **T2** | Plusieurs sous-réseaux — calcul, affichage, administration | LAN routé ou VLAN | **Recommandée** | V1.x |
| **T3** | Poste distant ou laboratoire | VPN | Requise, avec TLS | Long terme |

```text
T2 — exemple

  Sous-réseau CALCUL          Sous-réseau ADMIN           Sous-réseau AFFICHAGE
  ┌───────────────┐          ┌──────────────────┐         ┌──────────────────┐
  │ Node Agent    │          │ Launcher         │         │ Node Agent       │
  │ SYNE #1..#n   │◄────────►│ Registre         │◄────────►│ PRISM            │
  │               │          │ Gateway          │         │ ECHOS            │
  └───────┬───────┘          └──────────────────┘         └────────▲─────────┘
          └──────────────── WebSocket snapshot/event ──────────────┘
                 (direct, ou relayé par la Gateway)
```

## 4. Composants réseau

### 4.1 Registre de services — V0.1

Le registre est la table des instances vivantes, tenue dans le cœur du Launcher.

```json
{
  "instances": [
    {
      "instanceId": "syne-0001",
      "component": "syne",
      "node": "local",
      "state": "Running",
      "endpoints": {
        "ws":      "ws://127.0.0.1:5180/",
        "control": "http://127.0.0.1:5181/",
        "health":  "http://127.0.0.1:5181/health/ready"
      },
      "experimentId": "EXP-2026-001",
      "runId": "RUN-0042",
      "protocolVersion": 1,
      "startedAt": "2026-09-30T10:12:00Z"
    }
  ]
}
```

- Alimenté par le gestionnaire de services au démarrage et à l'arrêt.
- Consulté via l'abstraction **`IEndpointResolver`**, dont la signature est
  `Resolve(component, instanceId?) → Endpoint`. C'est le **seul** endroit où le
  Launcher connaît une adresse. En T0 elle renvoie `127.0.0.1:port` ; plus tard,
  l'adresse de la Gateway.
- Exposé aux autres composants, en lecture seule, via `GET /registry`.

Cette indirection est ce qui permet d'ajouter la Gateway sans refonte : le
Launcher ne connaît pas l'adresse, il connaît une résolution.

### 4.2 Node Agent (`livex-agent`) — V1.x

Petit service installé sur chaque machine distante. Il :

- démarre et arrête les processus **locaux** à la demande du Launcher, qui ne peut
  pas lancer un processus sur une autre machine ;
- relaie la santé, les informations et les métriques de son nœud ;
- applique les mêmes règles de validation des arguments et de chemins que le
  gestionnaire de processus local.

### 4.3 Gateway (`LIVEX.Gateway`) — V1.x, processus séparé

Proxy inverse .NET, fondé sur **YARP**, le proxy inverse de Microsoft qui gère HTTP
et WebSocket. **[À valider par spike.]**

Fonctions :

1. **Routage** par composant et par instance.
2. **Authentification** par jeton et contrôle d'origine.
3. **Agrégation** de la santé globale et par composant.
4. **Relais WebSocket** de SYNE vers les clients quand le lien direct est impossible.
5. **Journalisation** des accès aux commandes de contrôle.

| Route Gateway | Cible |
| :-- | :-- |
| `/syne/{instanceId}/ws` | WebSocket de l'instance SYNE |
| `/syne/{instanceId}/control/*` | HTTP de contrôle SYNE |
| `/echos/{instanceId}/*` | REST et interface ECHOS |
| `/registry` | Registre, en lecture |
| `/gateway/status` | Santé agrégée, globale et par composant |
| `/gateway/metrics` | Métriques de la Gateway |

**Pourquoi un processus séparé et non une fonction du Launcher** : le Launcher doit
rester mince, indépendant et remplaçable. La Gateway a un cycle de vie et des
exigences de sécurité propres, et doit pouvoir tourner sans interface graphique,
sur une machine dédiée.

## 5. Options et recommandation

| Option | Description | Avantages | Inconvénients |
| :-- | :-- | :-- | :-- |
| **A** | Pas de Gateway ; registre et `IEndpointResolver` ; ports statiques | Simple | Pas de multi-sous-réseaux, sécurité limitée à la boucle locale |
| **B** | A + Node Agent + Gateway YARP séparée | Couvre T1 et T2, point d'entrée unique | Plus de composants à maintenir |
| **C** | Maillage complet de type service mesh | — | Disproportionné |

**Recommandation** : **A en V0.1**, en concevant dès maintenant `IEndpointResolver`
et le registre pour que **B** s'ajoute sans refonte. **C est écartée.**

## 6. Adressage, identité et ports

### 6.1 Identifiants

- `nodeId` : `local` par défaut, sinon le nom de la machine.
- `instanceId` : `<composant>-<nnnn>`, par exemple `syne-0003`.
- Toute requête inter-composants porte un `X-Livex-Correlation-Id`.

### 6.2 Ports

- **V0.1** : ports par défaut du prototype — SYNE 5180 et 5181, ECHOS 5000.
  **[À CONFIRMER, décision n°28.]** Noter que 5000 est le port historique des
  modèles ASP.NET de développement et entre facilement en conflit ; envisager de le
  changer.
- **Multi-instance** : le Launcher alloue un port dans une **plage réservée
  configurable** et le transmet à l'instance par argument ou variable
  d'environnement ; l'instance le publie dans le registre.
- **Pré-vol** : tester la disponibilité du port **avant** le démarrage. En cas
  d'échec, produire une erreur de configuration explicite, avec le processus
  occupant s'il est identifiable.
- Une instance ne doit **jamais** choisir seule un port sans le déclarer.

### 6.3 Configuration

`Config/network.json` :

```json
{
  "version": 1,
  "bind": "127.0.0.1",
  "portRange": { "from": 5200, "to": 5399 },
  "nodes": [
    { "nodeId": "local", "address": "127.0.0.1", "agent": null }
  ],
  "gateway": { "enabled": false, "listen": "127.0.0.1:5100" },
  "security": {
    "requireToken": true,
    "allowedOrigins": ["http://127.0.0.1:5000"],
    "tls": { "enabled": false }
  }
}
```

*Les valeurs de plage et de port Gateway sont des exemples à arrêter.*

## 7. Sécurité réseau

| Règle | Détail |
| :-- | :-- |
| **Bind local par défaut** | Tous les composants écoutent sur `127.0.0.1`. L'écoute sur le réseau local est une option explicite, jamais implicite. |
| **Jeton de session** | Généré par le Launcher à chaque démarrage, transmis par variable d'environnement — jamais en argument de ligne de commande, visible dans la liste des processus. Exigé sur toutes les commandes de contrôle. |
| **Lecture et écriture** | `/health` et `/info` peuvent rester en lecture sans jeton en local ; toute commande exige le jeton. |
| **Origine navigateur** | L'interface ECHOS appelle SYNE et l'API ECHOS depuis un navigateur : liste blanche d'origines et vérification de l'en-tête `Origin` à l'ouverture du WebSocket. |
| **TLS** | Non requis en T0. Obligatoire dès qu'on quitte la boucle locale. Gestion des certificats à spécifier. |
| **Pare-feu** | L'installeur ne modifie pas le pare-feu sans confirmation ; les ports à ouvrir sont documentés. |
| **Validation** | Les arguments envoyés à un Node Agent respectent les règles de chemins connus et de paramètres validés. |
| **Journal d'audit** | Toute commande de contrôle reçue est journalisée : origine, instance, commande, résultat. |

## 8. Résilience réseau

- **Reconnexion WebSocket** : temporisation exponentielle avec plafond. À la
  reconnexion, le client reçoit d'abord un `snapshot` complet — ce type de message
  existe déjà — puis les `event`. Aucun rejeu d'historique n'est nécessaire.
- **Partition réseau** : un nœud injoignable passe à **Injoignable** dans le
  registre après N battements de cœur manqués. **[N à calibrer.]** Les runs qu'il
  porte suivent la politique `OnRunFailure`.
- **Gateway indisponible** : en T0 et T1 les composants continuent en direct. En T2
  la visualisation est interrompue, mais **la simulation continue**.
- **Charge** : le volume des instantanés croît avec le nombre d'agents. Mesurer
  taille et fréquence avant de décider d'une compression ou d'un mode différentiel.
  **[À mesurer, pas à supposer.]**
- **Horloges** : ne jamais corréler des événements de machines différentes par heure
  murale. Utiliser le **tick** comme temps de référence, l'heure UTC pour les
  journaux.

## 9. Réseau requis par mode

| Mode | Réseau requis |
| :-- | :-- |
| Contrôle | Launcher → SYNE |
| Immersif | + WebSocket SYNE → PRISM, HTTP PRISM → SYNE |
| Analyse pur, ou analyse avec télémétrie | + WebSocket SYNE → ECHOS, HTTP ECHOS → SYNE |
| **Analyse hors ligne** | Aucun lien avec SYNE ; lecture de fichiers |
| Expérience | Tous |
| Campagne multi-run parallèle | N instances SYNE, registre et allocation de ports |

## 10. Plan de mise en œuvre

| Étape | Contenu | Jalon |
| :-- | :-- | :-- |
| **R0** | `IEndpointResolver`, registre en mémoire, `network.json`, pré-vol de ports | Launcher V0.1 |
| **R1** | Bind local, jeton de session, contrôle d'origine | Launcher V0.1 |
| **R2** | Allocation dynamique de ports, pour le parallèle | Après validation de l'exécution parallèle |
| **R3** | Node Agent | V1.x |
| **R4** | Gateway YARP : routage et santé agrégée | V1.x |
| **R5** | TLS et audit | Avant tout usage hors boucle locale |
| **R6** | Découverte automatique en mDNS | Optionnel |

**Spikes à réaliser** : (1) YARP relaie-t-il correctement le flux `snapshot` au débit
réel de SYNE ? (2) Coût CPU et latence du relais ? (3) Comportement des
navigateurs avec le contrôle d'origine sur WebSocket ?

**Tests** : conflits de ports ; reconnexion WebSocket après coupure ; jeton invalide
ou absent, refus ; origine non autorisée, refus ; perte de nœud pendant un run ;
Gateway arrêtée en cours de simulation.

## 11. Risques

| Risque | Impact | Mitigation |
| :-- | :-- | :-- |
| La Gateway devient un point unique de défaillance | Perte de visualisation | Mode direct de repli ; simulation indépendante de la Gateway |
| Latence ajoutée sur le flux d'instantanés | Rendu saccadé | Relais seulement si nécessaire ; mesurer avant de généraliser |
| Commandes de contrôle non authentifiées | Prise de contrôle locale ou réseau | Jeton et bind local |
| Dérive de configuration entre nœuds | Runs non reproductibles | Versions et `protocolVersion` vérifiés à l'enregistrement dans le registre |
## Points restés ouverts dans ce document

- Les ports par défaut ne sont pas arbitrés, et le port 5000 d'ECHOS entre facilement
  en conflit avec les valeurs par défaut des modèles ASP.NET de développement.
- Le relais WebSocket par YARP n'est pas validé au débit réel de SYNE.
- Le mode d'arrêt propre sous Windows sans console n'est pas tranché.
- Le mécanisme de gestion des certificats TLS reste à spécifier.
