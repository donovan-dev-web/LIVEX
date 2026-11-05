# LIVEX — état transversal actuel

**Composant** : LIVEX (général)
**Statut** : [V1 LIVRÉE ; PRISM V2 EN COURS D'INTÉGRATION]
**Date du relevé** : 4 novembre 2026
**Sources** : mémoire projet, documentation des composants et résultats de validation datés ci-dessous.

> Cette page décrit l'état du travail V2 connu au 4 novembre 2026. Elle ne
> remplace pas le relevé détaillé de la release V1 du 31 octobre, conservé dans
> [`LIVEX-STATUS.md`](LIVEX-STATUS.md). Les résultats de suites datés différemment
> ne sont pas additionnés en un total global.

## Synthèse

LIVEX 1.0.0 reste la release publique fondée sur SYNE 0.17.0, ECHOS 0.1.0,
Launcher 0.1.0 et PRISM V1/PRISM-LDK sous Unreal. Le travail actuel porte sur
PRISM V2 : une simulation de monde .NET headless, distincte de la présentation
Unreal V1. Le contrat SYNE↔PRISM `1.0.0` est gelé et la boucle couplée est
implémentée, mais le transfert complet de l'autorité physique, les validations
longues, l'intégration ECHOS en production et la release V2 restent incomplets.

## État par composant

| Composant | État connu | Limites et travaux ouverts |
| :-- | :-- | :-- |
| **SYNE** | Moteur `0.17.0`, contrat d'observabilité `0.5.0`. Au relevé du 4 novembre : 708/708 tests (604 Core, 104 Console). Adaptateur PRISM, `ApplyExternalObservations`, boucle couplée et CLI `--prism-url` livrés ; transport exercé sur 1 000 ticks. | Extraction du monde physique en plusieurs étapes, mémoire spatiale/croyances adaptées à PRISM, persistance couplée et run R2 répétable restent à faire. La suite V2 ne remplace pas le mode SYNE autonome V1. |
| **PRISM V2** | Contrat `1.0.0` gelé en documentation et `ProtocolVersion.Current`. Au relevé du 4 novembre : 403/403 tests et build sans avertissement. Host headless, monde 2,5D, contenu, actions, persistance, bots déterministes et chaînes de base implémentés par étapes. Campagne réduite 3 graines × 200 ticks × 3 bots épinglée ; campagne complète 3 × 5 000 ticks exécutée, neuf empreintes identiques publiées. | R2 couplé de 5 000 ticks et répétitions, reprise partagée, preuves interplateformes, calibrage/portes d'acceptation, ingestion ECHOS en production, viewer live et release autonome restent ouverts. |
| **ECHOS** | API et analyse Python demeurent le service de mesure de la pile V1. Le socle ECHOS-100 valide et persiste les enveloppes de lecture PRISM avec séquence et `contentHash`, et les expose par API. Dernier relevé de suite trouvé : 383 réussis, 14 ignorés, couverture 91,32 % (19 octobre). | Cette suite n'a pas de relevé plus récent dans les sources consultées. L'ingestion PRISM n'est pas encore raccordée au cycle de run, aux métriques ni à la comparaison de runs V2. |
| **Launcher** | Orchestrateur de la release V1, avec paquets `.livexp` et composants publiés. Le manifeste du host PRISM a un raccordement au Launcher générique. | Les lanes LAUNCHER-200/201/202 pour PRISM V2 et l'orchestration complète des ports, du cycle de vie et de l'archivage restent à réaliser. |
| **PRISM V1 / PRISM-LDK** | Projet Unreal et plugin historiques livrés dans LIVEX 1.0.0. | Cette architecture n'est pas le runtime de PRISM V2. Les documents V1 restent des références historiques. |
| **`syne-mock`** | Émulateur Node.js des contrats V1, utile aux clients historiques. | Ce n'est pas le serveur de campagne PRISM V2 ni un moteur scientifiquement équivalent ; PRISM utilise `Prism.Bots` pour ses campagnes. |
| **Installateur** | Installateur de la release V1 publié pour Windows et Linux. | La distribution des composants V2 et leur acquisition par l'installateur attendent la lane INSTALLER-200 et les artefacts définitifs. |

## Portes V2 encore ouvertes

- **R2 / G3** : le transport couplé a été exercé sur 1 000 ticks. Restent un run de
  5 000 ticks, au moins trois répétitions aux empreintes identiques et la reprise
  commune SYNE/PRISM.
- **G1 et performance** : les mesures initiales existent, mais les budgets et
  validations Windows/Linux ne sont pas déclarés franchis.
- **G4–G6** : chaînes de contenu et structures partiellement acceptées ; ingestion
  ECHOS et orchestration Launcher V2 restent à raccorder au cycle de production.
- **G7–G8** : calibrage, ablations, validation expérimentale et préparation de
  livraison restent ouverts. Les résultats d'octobre n'établissent pas une
  émergence générale : la quête spatiale est confirmée dans le protocole publié,
  le cycle social n'est pas confirmé.

## Règle de lecture des chiffres

Les compteurs indiquent la dernière exécution datée, pas une validation globale
du dépôt au même instant. PRISM et SYNE ont des relevés du 4 novembre ; le dernier
relevé ECHOS trouvé date du 19 octobre ; les chiffres détaillés de release V1
restent ceux du 31 octobre. Aucun total combiné actuel n'est calculé à partir de
ces dates différentes.
