# Launcher — acceptation de la phase 5 et intégration (octobre 2026)

**Date du relevé :** 17–18 octobre 2026 · **Volet initial :** BL-27 · **Portée :** flux direct SYNE→consommateurs, paquet `.livexp`, restitution des données ECHOS.

## Acceptation du flux et du paquet

SYNE a été publié au chemin déclaré par son manifeste. Le relevé du 17 octobre donne `Launcher.Tests.Integration` **20/20** et `Launcher.Tests.EndToEnd` **33/33** sur SYNE publié, exit 0. Les bancs exécutés comprenaient SYNE réel avec ECHOS réel et une campagne dont le paquet `.livexp` a été scellé (`PackageStates.Sealed`) puis relu par le lecteur de paquets. L’archive de campagne est distincte du snapshot SQLite interne de SYNE.

Une porte environnementale demeure signalée : sans `LIVEX_SYNE_PUBLISHED_ROOT`, quatre scénarios gatés sont comptabilisés comme échecs plutôt que comme tests ignorés avec la combinaison xUnit/runner alors utilisée. C’est un signal de test trompeur en absence de dépendance publiée, pas un échec produit; la correction du runner n’était pas dans le périmètre de la phase 5.

## Alignement de la fenêtre d’analyse sur les contrats

Le travail d’intégration Launcher a aussi détecté et corrigé les pertes ou inventions de données suivantes :

- **Réserves sans position publiée :** une coordonnée absente était lue comme `(0,0)` et dessinait une réserve agrégée au coin de la carte. Les coordonnées sont maintenant nullable; une réserve sans position reste listée avec sa quantité et n’est pas projetée sur le monde.
- **Chaîne causale :** l’API ECHOS de chaîne causale est rendue consultable par couche, sans compléter, réordonner ou inférer des nœuds dans le Launcher.
- **Communauté et groupes natifs :** les communautés inférées d’ECHOS ne sont pas confondues avec les groupes natifs SYNE. Depuis le relevé ultérieur (18/10), ECHOS publie l’état natif via `GET /api/runs/{id}/native-groups`; le Launcher affiche ces groupes sans calcul local.
- **Absence d’utilité :** l’absence et le report ne sont plus affichés comme un score nul. ECHOS conserve `utility` nullable, `held_utility`, `decision_state` et `decision_tick` dans le schéma v7; l’interface affiche l’absence comme « non mesurée » et le score reporté sous son état d’origine.

La campagne d’intégration du 18 octobre rapporte, après les changements cités, ECHOS **376 tests verts**, Launcher **172 unitaires**, **20 intégration**, **33 E2E** sur composants réels; les preuves couvrent migration, ingestion, rendu et parcours bout-en-bout. Les chiffres sont ceux du relevé, pas une mesure exécutée dans la présente migration documentaire.

## Limites

Un passage E2E sous publication et tests concurrents a produit 32/33, puis le run isolé et la relance au repos 33/33; l’hypothèse rapportée est la contention machine, sans changement de code entre runs. Une exécution chargée peut donc être instable. Le Launcher rend les valeurs et la chaîne publiées; il ne calcule pas de métriques et ne complète pas les données absentes. Une porte de test reste non franchie lorsque les quatre tests E2E conditionnés par `LIVEX_SYNE_PUBLISHED_ROOT` sont exécutés sans composant publié : la combinaison xUnit/runner les compte comme échecs au lieu de tests ignorés. Les contrats d’acceptation opérationnels restent dans la documentation et les suites de tests maintenues de `docs/launcher/`, `docs/echos/` et `docs/prism/`.
