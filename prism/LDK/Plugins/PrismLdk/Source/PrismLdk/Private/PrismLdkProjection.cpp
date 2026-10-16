// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Implémentation des projections SYNE ↔ Unreal (math pure, sans état).

#include "PrismLdkProjection.h"

namespace
{
	/**
	 * Facteur d'échelle (`K`, `CellSize`, `TicksPerSecond`) ramené à zéro
	 * lorsqu'il est invalide, pour ne jamais propager d'infini ou de NaN dans
	 * un graphe de rendu.
	 */
	static double PositiveOrZero(double Value)
	{
		return Value > 0.0 ? Value : 0.0;
	}
}

FVector UPrismLdkProjection::SyneToUnreal(const FPrismSyneVector2D& Position, double K)
{
	const double Scale = PositiveOrZero(K);
	return FVector(Position.X * Scale, Position.Y * Scale, 0.0);
}

FPrismSyneVector2D UPrismLdkProjection::UnrealToSyne(const FVector& Location, double K)
{
	FPrismSyneVector2D Out;
	const double Scale = PositiveOrZero(K);
	if (Scale > 0.0)
	{
		Out.X = Location.X / Scale;
		Out.Y = Location.Y / Scale;
	}
	return Out;
}

FIntPoint UPrismLdkProjection::SyneToCell(const FPrismSyneVector2D& Position, double CellSize, int32 CellCountX, int32 CellCountY)
{
	if (CellSize <= 0.0 || CellCountX <= 0 || CellCountY <= 0)
	{
		return FIntPoint(0, 0);
	}

	const int32 CellX = FMath::Clamp(FMath::FloorToInt32(Position.X / CellSize), 0, CellCountX - 1);
	const int32 CellY = FMath::Clamp(FMath::FloorToInt32(Position.Y / CellSize), 0, CellCountY - 1);
	return FIntPoint(CellX, CellY);
}

FVector UPrismLdkProjection::CellToTileOrigin(const FIntPoint& Cell, double CellSize, double K)
{
	const double TileSize = PositiveOrZero(CellSize) * PositiveOrZero(K);
	return FVector(Cell.X * TileSize, Cell.Y * TileSize, 0.0);
}

FVector UPrismLdkProjection::CellToTileCenter(const FIntPoint& Cell, double CellSize, double K)
{
	const double TileSize = PositiveOrZero(CellSize) * PositiveOrZero(K);
	const double Half = TileSize * 0.5;
	return FVector(Cell.X * TileSize + Half, Cell.Y * TileSize + Half, 0.0);
}

double UPrismLdkProjection::SyneRadiusToUnreal(double Radius, double K)
{
	return Radius * PositiveOrZero(K);
}

double UPrismLdkProjection::MaxWalkSpeedForSpeed(double Speed, double K, int32 TicksPerSecond)
{
	const double Ticks = PositiveOrZero(static_cast<double>(TicksPerSecond));
	return Speed * PositiveOrZero(K) * Ticks;
}

double UPrismLdkProjection::TileSizeFromCellSize(double CellSize, double K)
{
	return PositiveOrZero(CellSize) * PositiveOrZero(K);
}
