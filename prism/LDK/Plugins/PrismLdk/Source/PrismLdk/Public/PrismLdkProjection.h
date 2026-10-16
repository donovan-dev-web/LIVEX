// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Projection déterministe entre l'espace logique SYNE et l'espace Unreal.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "PrismLdkTypes.h"
#include "PrismLdkProjection.generated.h"

/**
 * Conversions entre les coordonnées logiques SYNE et l'espace du monde Unreal.
 *
 * Aucune de ces fonctions ne conserve d'état et aucune ne contient de constante
 * de produit : le facteur d'échelle `K` et la taille de cellule sont toujours
 * fournis par l'appelant. `K` est calculé côté produit par
 * `K = TileSizeUu / World.CellSize` (profil PRISM : 3200 / 32 = 100).
 *
 * Contrat d'entrée : `K`, `CellSize` et `TicksPerSecond` doivent être
 * strictement positifs. Si ce n'est pas le cas, la fonction renvoie une sortie
 * nulle plutôt qu'une valeur non finie ; ce comportement est verrouillé par
 * `PrismLdkProjectionSpec`.
 *
 * Deux repères distincts cohabitent :
 * - `SyneToUnreal` / `UnrealToSyne` traitent les positions **continues**
 *   (agents, obstacles, territoires) ;
 * - `SyneToCell` / `CellToTileOrigin` / `CellToTileCenter` traitent les
 *   **indices de cellule** (`Cells[]`, ressources initiales).
 */
UCLASS()
class PRISMLDK_API UPrismLdkProjection : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Position continue SYNE vers position Unreal. L'altitude reste nulle :
	 * la projection 3D est un choix de présentation du produit.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static FVector SyneToUnreal(const FPrismSyneVector2D& Position, double K);

	/**
	 * Position Unreal vers position continue SYNE (opération inverse de
	 * `SyneToUnreal`, utilisée pour le picking et les outils d'édition).
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static FPrismSyneVector2D UnrealToSyne(const FVector& Location, double K);

	/**
	 * Indice de cellule contenant une position continue.
	 * Une position hors du monde est projetée sur la cellule de bord : le
	 * résultat est toujours dans `[0, CellCount - 1]`. Une position hors-bornes
	 * est une anomalie de données ; la conserver brute est du ressort de
	 * l'appelant qui dispose de `Width` / `Height`.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static FIntPoint SyneToCell(const FPrismSyneVector2D& Position, double CellSize, int32 CellCountX, int32 CellCountY);

	/**
	 * Coin bas de la tuile d'une cellule, exprimé en unités Unreal.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static FVector CellToTileOrigin(const FIntPoint& Cell, double CellSize, double K);

	/**
	 * Centre de la tuile d'une cellule, exprimé en unités Unreal.
	 * C'est la position à utiliser pour placer un marqueur dont la donnée
	 * source est un indice de cellule.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static FVector CellToTileCenter(const FIntPoint& Cell, double CellSize, double K);

	/**
	 * Rayon continu SYNE (obstacle, territoire) vers rayon Unreal.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static double SyneRadiusToUnreal(double Radius, double K);

	/**
	 * Vitesse de déplacement en coordonnées SYNE vers vitesse Unreal.
	 * `Speed` est la valeur de déplacement par tick ; `TicksPerSecond` est
	 * celui du run (`world_initialized.ticksPerSecond`).
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static double MaxWalkSpeedForSpeed(double Speed, double K, int32 TicksPerSecond);

	/**
	 * Taille d'une cellule en unités Unreal (`CellSize * K`) : dimension de la
	 * tuile et de la cellule de World Partition.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Projection")
	static double TileSizeFromCellSize(double CellSize, double K);
};
