# TESTING.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `PACKAGE_FORMAT.md`, `COMPONENTS.md`
**Source Monographie** : —

---

## 1. Objet

Ce document spécifie **comment le Launcher doit être validé**. Il décrit les niveaux
de test, la matrice de couverture et les propriétés qui doivent être vérifiées.

Les tests du Launcher sont d'un type particulier : la majorité des composants
existent déjà et ne sont pas son objet. Le Launcher est donc testé **par
observation de composants simulés**, et par ses **propriétés structurelles**.

## 2. Niveaux de test

| Niveau | Cible | Dépendances | Vitesse |
| :-- | :-- | :-- | :-- |
| **Unitaire** | Domaine, paquet, protocole | Aucune | Très rapide |
| **Propriétés** | Invariants du paquet et de l'orchestration | Aucune | Rapide |
| **Intégration** | Infrastructure et composants simulés | Processus simulés | Moyenne |
| **Bout en bout** | Scénarios d'orchestration complets | Composants simulés ou réels | Lente |
| **Acceptation** | Conformité au contrat d'intégration | Composants réels | Lente |

Le niveau **propriétés** est propre à ce composant. Le format `.livexp` et le cycle
d'orchestration sont des **invariants**, pas des scénarios : ils se vérifient par
génération de cas, pas par énumération.

## 3. Ce qui doit être testé sans composant

Une part majeure de la logique du Launcher est testable **sans démarrer SYNE, ECHOS
ni PRISM**. C'est un choix de conception, rendu possible par les frontières de
`ARCHITECTURE.md`.

| Élément | Testé sans composant |
| :-- | :-- |
| Lecture et écriture de paquet | Oui |
| Cycle de vie du paquet | Oui |
| Reprise d'un paquet interrompu | Oui |
| Déterminisme du paquet | Oui |
| Sécurité du paquet | Oui |
| Dérivation des graines | Oui |
| Planification d'une campagne | Oui |
| Machine à états des composants | Oui |
| Résolution de profil | Oui |
| Agenda global de santé | Oui |
| Rendu de l'interface | Oui, sur un modèle de données |

## 4. Le banc de composants simulés

Pour les tests d'intégration, le Launcher s'exerce contre des **composants
simulés** qui respectent le contrat d'intégration sans être les vrais composants.

| Simulé | Comportement simulé |
| :-- | :-- |
| **Moteur conforme** | Cycle `Prepare` → `Ready` → `Start`, snapshots, fin d'horizon |
| **Moteur lent** | Dépassement du délai de démarrage |
| **Moteur muet** | Port ouvert, aucune réponse |
| **Moteur instable** | Basculements répétés entre disponible et indisponible |
| **Moteur qui plante** | Disparition du processus en cours de run |
| **Analyste conforme** | Restitution d'artefacts par run et agrégats de campagne |
| **Analyste lent** | Analyse qui dépasse le délai d'attente |
| **Analyste fautif** | Artefact manquant, ou empreinte incohérente |
| **Composant à port fixe** | Refus de réattribution, conflit de port |
| **Composant hors boucle locale** | Refus de démarrage |

Ce banc est **obligatoire** : sans lui, l'orchestration ne peut pas être testée de
façon reproductible et rapide.

## 5. Propriétés du format `.livexp`

Ces propriétés sont vérifiées par génération de cas.

| Propriété | Énoncé |
| :-- | :-- |
| **Écriture Sanitaire** | N'écrire jamais dans un paquet scellé |
| **Atomicité** | Une écriture interrompue laisse le paquet récupérable, jamais invalide |
| **Reprise exacte** | Reprendre un paquet ne rejoue aucun run réussi |
| **Déterminisme** | Deux campagnes de contenu identique produisent deux paquets identiques octet pour octet |
| **Intégrité** | Toute altération d'une entrée est détectée à la lecture |
| **Auto-description** | Un paquet se relit sans le Launcher |
| **Traçabilité** | Chaque donnée est rattachable à un run et à une campagne |
| **Confinement** | Aucun chemin absolu, aucun `..`, aucun lien symbolique |
| **Atomicité des artefacts** | Un artefact n'est jamais visible à moitié écrit |
| **Conformité de schéma** | Un `schema` inconnu est refusé, jamais interprété |

## 6. Propriétés de l'orchestration

| Propriété | Énoncé |
| :-- | :-- |
| **Ordre de démarrage** | SYNE est prêt avant tout composant qui en dépend |
| **Ordre d'arrêt** | Inverse strict de l'ordre de démarrage |
| **Priorité au manifeste** | Un port déclaré n'est jamais réattribué s'il est libre |
| **Dégradation ciblée** | Un composant défaillant n'affecte que son mode |
| **Aucune science** | Aucun test ne dépend d'un résultat scientifique |
| **Pas de reprise automatique** | Le Launcher ne redémarre aucun composant seul |
| **Reprise idempotente** | Ouvrir deux fois le même paquet ne duplique aucun run |
| **Absence de fuite** | Fermer le Launcher ne laisse aucun processus fils actif |

## 7. Matrice de couverture

La couverture minimale exigée, par niveau.

| Niveau | Couverture | Portée |
| :-- | :-- | :-- |
| **Unitaire** | ≥ 85 % des lignes du domaine | `Launcher.Domain`, `Launcher.Package`, `Launcher.Protocol` |
| **Intégration** | ≥ 75 % des lignes de l'infrastructure | `Launcher.Infrastructure` |
| **Bout en bout** | Tous les scénarios du §8 | Scénarios complets |
| **Propriétés** | 100 % des propriétés du §5 et du §6 | Génération de cas |

La couverture du domaine est la plus exigeante, parce que c'est là que se trouvent
les invariants du format et de l'orchestration.

## 8. Scénarios de bout en bout

| Scénario | Vérifie |
| :-- | :-- |
| **Session minimale** | Détection, choix de profil, démarrage du moteur, arrêt propre |
| **Session complète** | Les modes sont activables ensemble, sans conflit de port |
| **Campagne nominale** | Création, exécution de tous les runs, scellement, relecture |
| **Pause et reprise** | Reprise sans rejouer un run réussi |
| **Panne en cours de run** | Détection, abandon du run, état du paquet |
| **Échec de l'analyste** | Run marqué en échec, campagne poursuivie selon la politique |
| **Paquet corrompu** | Détection de l'entrée fautive, refus explicite |
| **Port occupé** | Refus explicite, aucun écrasement |
| **Composant lancé à la main** | Détection, proposition d'adoption, pas de doublon |
| **Composant hors boucle locale** | Refus de démarrage avec motif |
| **Mode verrouillé** | Entrée visible, sélection refusée, raison affichée |
| **Panne disque** | Écriture de paquet refusée, alerte, campagne interruptible |

## 9. Tests d'acceptation des composants

Un composant est accepté comme orchestrable si les exigences de
`INTEGRATION_CONTRACT.md` sont vérifiées. Ces tests sont **écrits du point de vue du
Launcher**, et échouent si un composant ne tient pas ses engagements.

| Exigence | Test d'acceptation |
| :-- | :-- |
| **Manifeste** | Le manifeste est lisible et complet |
| **Démarrage** | Le composant atteint l'état prêt dans le délai déclaré |
| **Arrêt** | L'arrêt est propre et sans perte d'état annoncé |
| **Suspension** | La suspension et la reprise sont réversibles |
| **Sonde de santé** | La sonde répond, y compris pendant le démarrage |
| **Commandes** | Toute commande a un résultat, un code, un message |
| **Écriture d'artefacts** | Les artefacts sont écrits atomiquement et nommés de façon prévisible |
| **Écriture hors périmètre** | L'écriture hors du périmètre du producteur est refusée |

## 10. Tests d'interface

| Aspect | Vérification |
| :-- | :-- |
| **États visibles** | Chaque état a une pastille et un libellé textuel |
| **Cause affichée** | Aucun état défectueux sans cause lisible |
| **Accessibilité** | Navigation complète au clavier, contrastes, libellés de lecture d'écran |
| **Mise en page** | Rendu correct à différentes tailles de fenêtre et jusqu'à 200 % de zoom |
| **États vides** | Chaque vue vide a un contenu explicite |
| **Mode verrouillé** | La raison du verrouillage est visible et exacte |
| **Absence de mouvement** | Aucune animation permanente, aucun rendu temps réel parasite |
| **Consoles et fenêtre d'analyse** | Ouverture en fenêtre native du Launcher, sans navigateur, sans webview, sans secret en URL (`USER_INTERFACE.md` §9, ADR-007) |

## 11. Tests de performance

Le Launcher n'exécute pas de calcul scientifique. Ses exigences de performance
portent sur le **temps de réaction de l'interface** et sur la **robustesse de
l'archivage**, non sur le débit de simulation.

| Mesure | Cible | Condition |
| :-- | :-- | :-- |
| **Ouverture de fenêtre** | < 2 s | Poste de référence, sans composant |
| **Détection des composants** | < 1 s | Tous composants présents |
| **Réaction de l'interface** | < 100 ms | Toute action utilisateur, hors attente réseau |
| **Reprise de paquet** | < 5 s | Paquet de 1 000 runs, sans ECHOS |
| **Écriture de paquet** | Sans impact notable | Campagne en cours, interface active |
| **Mémoire** | < 300 Mio | Session avec campagne en cours |
| **Stabilité** | Aucun incident sur 72 h | Campagne continue, supervision active |

## 12. Tests de sécurité

| Vecteur | Test |
| :-- | :-- |
| **Traversal de chemin** | Un paquet contenant `../` est refusé à la lecture |
| **Chemin absolu** | Un paquet contenant un chemin absolu est refusé |
| **Lien symbolique** | Un paquet contenant un lien symbolique est refusé |
| **Zip-bomb** | Un paquet au-delà du ratio de décompression est refusé |
| **Exécutable** | Une entrée exécutable est refusée à l'écriture et à la extraction |
| **Corruption** | Une altération d'octet est détectée par l'empreinte |
| **Schéma en avance** | Un `schema` supérieur est refusé |
| **Écriture concurrente** | Un second processus ne peut pas écrire dans un paquet ouvert |
| **Secret dans la configuration** | La configuration résolue ne contient aucun secret |
| **Exposition réseau** | Un composant écoutant hors boucle locale est refusé au démarrage |

## 13. Tests de robustesse

Ces scénarios ne se déduisent pas du code : ils consistent à **casser** la pile et
à vérifier qu'elle tient.

| Scénario | Vérification attendue |
| :-- | :-- |
| Arrêt brutal du Launcher en plein run | Aucun processus orphelin, sous Windows et sous Linux |
| Disque plein pendant un run | Échec propre, run marqué, campagne suspendue |
| `experiment.json` tronqué | Reprise sans corruption, ou refus explicite |
| Composant gelé, tick figé | État Injoignable, alerte émise, politique de run appliquée |
| Composant tué en cours de campagne | Politique `OnRunFailure` respectée |
| Port déjà occupé | Échec au pré-vol, avant tout démarrage, avec le nom de l'occupant si identifiable |
| Jeton invalide ou absent | Commande de contrôle refusée |
| Origine non autorisée sur le WebSocket | Connexion refusée |
| Nœud distant perdu pendant un run | Nœud Injoignable, run traité par la politique |
| Gateway arrêtée en cours de simulation *(forme de sortie, non planifiée — test conditionné à une future répartition, sans objet en V0.1)* | Simulation poursuivie, visualisation interrompue |
| Campagne longue, plusieurs heures | Aucune fuite mémoire du tampon de métriques |

## 14. Tests de reproductibilité

| Scénario | Vérification attendue |
| :-- | :-- |
| Deux runs identiques | Même empreinte de résultat |
| Monitoring activé et désactivé | Résultats **identiques** |
| Écriture atomique interrompue | Aucun artefact visible à moitié écrit |
| Configuration résolue enregistrée | Elle suffit à rejouer le run |
| Reprise après incident | Les runs terminés ne sont pas refaits |

## 15. Tests multi-plateformes et d'installation

La suite complète est exécutée sur **Windows et Linux** à chaque version, dans la
matrice d'intégration continue. Les tests d'installation se font sur une machine
vierge, Windows et Linux, et couvrent l'installation, le premier démarrage et la
désinstallation.

## 16. Références

- `ARCHITECTURE.md` §5 — découpage des projets testables
- `PACKAGE_FORMAT.md` — format, propriétés, cycle de vie
- `COMPONENTS.md` — machine à états, profils
- `INTEGRATION_CONTRACT.md` §5, §8 — arrêt propre et effets du refus ou du silence d'un composant

---

## Points restés ouverts dans ce document

- **Protocole de validation d'un composant.** Le §9 énumère les tests, mais la
  procédure qui atteste qu'un composant est accepté n'est pas définie : qui
  l'exécute, à quelle fréquence, et où le résultat est consigné.
- **Seuils de performance.** Les cibles du §11 supposent un poste de référence
  qui n'est pas défini. Sans lui, ces cibles ne sont pas mesurables.
- **Poste instrumenté.** La campagne de 72 h suppose un poste instrumenté, dont la
  disponibilité n'est pas acquise.
- **Outil de génération de cas.** Le choix de l'outil pour les tests de propriétés
  n'est pas arrêté.
- **Tests de charge.** Aucun scénario de charge multi-campagnes n'est prévu, alors
  que le multi-espace est différé. Il faut confirmer que ce cas ne se présente pas
  en V0.1.
- **Accessibilité.** La conformité visée n'est pas nommée. Un niveau de référence
  explicite est nécessaire pour que les tests du §10 soient arbitrables.
