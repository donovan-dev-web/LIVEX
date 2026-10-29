// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Registre de monde PRISM (identifiant SYNE → acteur Unreal) et échelle de projection.
//
// Pourquoi une classe C++ **concrète** : le moteur instancie tous les
// dérivés non abstraits de `UGameInstanceSubsystem` **chargés**
// (`FSubsystemCollectionBase::Initialize` → `AddAndInitializeSubsystems`,
// `SubsystemCollection.cpp`, moteur 5.8), et une classe native est chargée avec
// son module, tandis qu'un asset Blueprint ne l'est que si quelque chose le
// référence. Un registre porté par un Blueprint enfant — l'implémentation
// précédente, `BP_PRISM_WorldSubsystem` — disparaissait donc dès que personne
// ne le référençait : `Get Prism Ldk World Registry` renvoyait `nullptr`, la
// variable `Registry` du directeur de monde restait nulle et le monde n'était
// plus construit, sans qu'aucune ligne de code n'ait changé. La classe est donc
// désormais concrète et instanciée par le moteur lui-même ; le Blueprint enfant
// n'a plus lieu
// d'être, et s'il en subsiste un il est **masqué** — la résolution demande la
// classe exacte avant tout dérivé (`GetSubsystemInternal` :
// `SubsystemMap.FindRef(SubsystemClass)` d'abord, tableau des dérivés ensuite),
// si bien que `GetSubsystem<UPrismLdkWorldRegistryBase>()` renvoie toujours
// l'instance C++.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "PrismLdkTypes.h"
#include "PrismLdkWorldRegistry.generated.h"

/**
 * Échelle de projection et dimensions de grille, dérivées **une seule fois** de
 * `world_initialized` (TRANSPORT_API §2 : jamais de constante en dur).
 *
 * Aucun champ d'exécution (`runId`, `tick`, `aliveCount`, `season`) n'est
 * dupliqué ici : ils se lisent sur `UPrismLdkSubsystem` (`GetStatus`,
 * `GetLatestSnapshot`). Deux copies d'une même donnée divergent toujours.
 */
USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismLdkWorldStats
{
	GENERATED_BODY()

	/** `world.version` : version du contrat reçue dans `world_initialized`. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") FString WorldVersion;

	/** Taille de cellule en unités Unreal, fournie par la configuration produit (`DA_PRISM_SessionConfig.TileSizeUu`). */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") double TileSizeUu = 0.0;

	/** `world.cellSize` : taille d'une cellule dans l'espace logique SYNE. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") double CellSize = 0.0;

	/** `K = TileSizeUu / CellSize` : à passer à `Syne To Unreal`, `Cell To Tile Center`, … */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") double K = 0.0;

	/** `world.metersPerUnit` : conversion logique → mètres du contrat. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") double MetersPerUnit = 1.0;

	/** `world.cellCountX` : nombre de cellules de la grille. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") int32 CellCountX = 0;

	/** `world.cellCountY`. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") int32 CellCountY = 0;

	/** `world.ticksPerSecond` : cadence de simulation du run. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") int32 TicksPerSecond = 10;

	/** `world.simulatedSecondsPerTick` : `simulatedTimeSeconds = Tick × SimulatedSecondsPerTick`. */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") int32 SimulatedSecondsPerTick = 60;

	/**
	 * Vrai quand `CellSize > 0` **et** `TileSizeUu > 0` : `K` et les dimensions
	 * sont alors exploitables. Faux, les fonctions de projection renvoient des
	 * sorties nulles ; c'est le cas à tester au diagnostic, pas la valeur de `K`.
	 */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Registry") bool bScaleValid = false;
};

/**
 * Cœur du registre : état de correspondance `id SYNE → acteur Unreal` et
 * dérivation de l'échelle.
 *
 * Ce type ne dépend d'**aucun** `UObject` : il est instanciable et testable
 * sans monde (Automation Spec `PrismLdk.WorldRegistry`), et il ne contient
 * aucune formule de projection — les conversions vivent dans
 * `UPrismLdkProjection` et reçoivent `K` / `CellSize` en paramètre.
 *
 * Les acteurs sont mémorisés en `TWeakObjectPtr` : le registre ne retient pas
 * les acteurs en vie (le niveau en est propriétaire) et un acteur détruit est
 * signalé absent au lieu de laisser un pointeur pendant.
 */
struct PRISMLDK_API FPrismLdkWorldRegistry
{
	/** Échelle courante. */
	const FPrismLdkWorldStats& GetStats() const { return Stats; }

	/**
	 * Configure la taille de cellule en unités Unreal (config produit).
	 * Refusée si elle n'est pas finie et strictement positive. Recalcule `K`
	 * immédiatement si une description est déjà connue : l'ordre des deux
	 * appels (`SetTileSizeUu` / `SetStatsFromDescription`) est indifférent.
	 */
	bool SetTileSizeUu(double InTileSizeUu);

	/**
	 * Mémorise la description reçue dans `world_initialized` et dérive `K`.
	 * Unique point de dérivation de l'échelle, à appeler à chaque description
	 * (y compris au second run).
	 */
	void SetStatsFromDescription(const FPrismSyneWorldDescription& World);

	/** Enregistre (ou remplace) l'acteur d'un agent. Faux si l'acteur est invalide. */
	bool RegisterAgent(int64 Id, AActor* Actor);

	/** Retire un agent. Vrai si une entrée a été retirée. */
	bool UnregisterAgent(int64 Id);

	/** Acteur d'un agent, `nullptr` si l'agent est absent ou son acteur détruit. */
	AActor* GetAgentActor(int64 Id) const;

	/** Vrai si l'agent a un acteur vivant. */
	bool HasAgent(int64 Id) const;

	/** Nombre d'agents dont l'acteur est vivant. */
	int32 GetAgentCount() const;

	/** Identifiants des agents vivants, triés croissant (parcours reproductible). */
	TArray<int64> GetAgentIds() const;

	/** Enregistre (ou remplace) l'acteur d'un obstacle. Faux si l'identifiant est vide ou l'acteur invalide. */
	bool RegisterObstacle(const FString& Id, AActor* Actor);

	/** Retire un obstacle. Vrai si une entrée a été retirée. */
	bool UnregisterObstacle(const FString& Id);

	/** Acteur d'un obstacle, `nullptr` si l'obstacle est absent ou son acteur détruit. */
	AActor* GetObstacleActor(const FString& Id) const;

	/** Vrai si l'obstacle a un acteur vivant. */
	bool HasObstacle(const FString& Id) const;

	/** Nombre d'obstacles dont l'acteur est vivant. */
	int32 GetObstacleCount() const;

	/** Identifiants des obstacles vivants, triés (poids des chaînes, ordre stable). */
	TArray<FString> GetObstacleIds() const;

	/**
	 * Ajoute un marqueur de ressource. Faux si l'acteur est invalide ou déjà
	 * enregistré. Les références mortes sont compactées au passage.
	 */
	bool AddResourceMarker(AActor* Actor);

	/** Retire un marqueur de ressource. Vrai si le marqueur était enregistré. */
	bool RemoveResourceMarker(AActor* Actor);

	/** Marqueurs vivants, dans leur ordre d'ajout. */
	TArray<AActor*> GetResourceMarkers() const;

	/** Nombre de marqueurs vivants. */
	int32 GetResourceMarkerCount() const;

	/**
	 * Nouveau run ou fin de niveau : vide les registres et remet l'échelle
	 * dérivée à zéro. La taille de cellule **configurée** (`TileSizeUu`) est
	 * conservée : c'est un paramètre produit, pas un état de run.
	 */
	void ClearAll();

private:
	/** Seule expression qui produit `K`. */
	void RecomputeScale();

	FPrismLdkWorldStats Stats;
	TMap<int64, TWeakObjectPtr<AActor>> AgentsById;
	TMap<FString, TWeakObjectPtr<AActor>> ObstaclesById;
	TArray<TWeakObjectPtr<AActor>> ResourceMarkers;
};

/**
 * Sous-système de registre de PRISM : **concret**, instancié par le moteur.
 *
 * Aucun Blueprint enfant n'est nécessaire (et aucun n'est attendu) : la classe
 * est native, donc sa présence ne dépend pas du chargement d'un asset. Elle
 * reste `Blueprintable` pour être utilisable depuis un graphe, mais elle est
 * l'unique porteuse de l'instance — un Blueprint dérivé vivrait à côté, sans
 * jamais être celui que `Get Prism Ldk World Registry` renvoie.
 *
 * Elle porte le registre (appelé par le `BP_PRISM_WorldDirector` lors de la
 * construction du monde) et **aucune formule** : les conversions de position
 * viennent de `UPrismLdkProjection`, alimentées par `GetStats().K` /
 * `GetStats().CellSize`. Elle évite le `Get All Actors Of Class` par tick.
 *
 * Cycle de vie : celui de la GameInstance (`UGameInstanceSubsystem`), c'est-à-
 * dire le run complet. Les fonctions de mutation sont donc appelées au
 * `Reset` au moyen de `ClearAll`.
 */
UCLASS(Blueprintable, BlueprintType)
class PRISMLDK_API UPrismLdkWorldRegistryBase : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	/**
	 * Journalise l'instanciation (`registre de monde instancié`) : la ligne doit
	 * apparaître au démarrage de **chaque** run, sans dépendre d'un asset chargé.
	 */
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	/** Vide le registre : ses acteurs appartiennent au niveau qui disparaît. */
	virtual void Deinitialize() override;

	/** Taille de cellule en unités Unreal, à appeler avec `DA_PRISM_SessionConfig.TileSizeUu`. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool SetTileSizeUu(double TileSizeUu);

	/** Dérive l'échelle de `world_initialized` : à appeler sur `OnWorldInitialized`, avant toute construction. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	void SetStatsFromDescription(const FPrismSyneWorldDescription& World);

	/** Échelle et dimensions courantes (`K`, `CellSize`, …). */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	FPrismLdkWorldStats GetStats() const { return Registry.GetStats(); }

	/** Enregistre l'acteur d'un agent (`world_initialized.agents[]` ou apparition en cours de run). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool RegisterAgent(int64 Id, AActor* Actor);

	/** Retire un agent (mort, sortie de zone, destruction d'acteur). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool UnregisterAgent(int64 Id);

	/** Acteur d'un agent. `nullptr` si l'agent est absent ou son acteur détruit. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	AActor* GetAgentActor(int64 Id) const;

	/** Vrai si l'agent a un acteur vivant. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	bool HasAgent(int64 Id) const;

	/** Nombre d'agents vivants dans le registre. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	int32 GetAgentCount() const;

	/** Identifiants des agents vivants, triés croissant. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	TArray<int64> GetAgentIds() const;

	/** Enregistre l'acteur d'un obstacle (`world_initialized.obstacles[]`). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool RegisterObstacle(const FString& Id, AActor* Actor);

	/** Retire un obstacle. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool UnregisterObstacle(const FString& Id);

	/** Acteur d'un obstacle. `nullptr` si l'obstacle est absent ou son acteur détruit. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	AActor* GetObstacleActor(const FString& Id) const;

	/** Vrai si l'obstacle a un acteur vivant. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	bool HasObstacle(const FString& Id) const;

	/** Nombre d'obstacles vivants dans le registre. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	int32 GetObstacleCount() const;

	/** Identifiants des obstacles vivants, triés. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	TArray<FString> GetObstacleIds() const;

	/** Enregistre un marqueur de ressource (position issue d'un indice de cellule). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool AddResourceMarker(AActor* Actor);

	/** Retire un marqueur de ressource. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	bool RemoveResourceMarker(AActor* Actor);

	/** Marqueurs de ressource vivants, dans leur ordre d'ajout. */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry")
	TArray<AActor*> GetResourceMarkers() const;

	/** Nouveau run (`Reset`) : vide les registres et conserve la taille de cellule configurée. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Registry")
	void ClearAll();

	/** Accès C++ au cœur testable (Automation Spec `PrismLdk.WorldRegistry`). */
	const FPrismLdkWorldRegistry& GetRegistry() const { return Registry; }
	FPrismLdkWorldRegistry& GetRegistry() { return Registry; }

private:
	FPrismLdkWorldRegistry Registry;
};
