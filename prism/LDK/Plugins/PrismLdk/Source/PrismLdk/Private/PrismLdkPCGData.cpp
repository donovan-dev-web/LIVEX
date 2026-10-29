// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Implémentation UPrismLdkPCGData

#include "PrismLdkPCGData.h"
#include "PrismLdkProjection.h"

void UPrismLdkPCGData::BuildFromDescription(const FPrismSyneWorldDescription& World, const FPrismLdkWorldStats& Stats)
{
	RunId.Empty();
	WorldVersion = World.Version;
	Seed = World.Seed;

	CellCountX = World.CellCountX;
	CellCountY = World.CellCountY;
	CellSize = World.CellSize;
	TileSizeUu = Stats.TileSizeUu;
	K = Stats.K;

	Obstacles = World.Obstacles;
	Cells = World.Cells;
	Resources = World.Resources;
	Regions = World.Regions;

	// Reject invalid/overflowing dimensions before allocating the flattened mask.
	const int64 CellTotal64 = static_cast<int64>(CellCountX) * static_cast<int64>(CellCountY);
	const bool bValidGridDimensions = CellCountX > 0 && CellCountY > 0
		&& CellTotal64 > 0 && CellTotal64 <= TNumericLimits<int32>::Max();
	const int32 CellTotal = bValidGridDimensions ? static_cast<int32>(CellTotal64) : 0;
	CellWalkable.Reset();
	if (CellTotal > 0 && Cells.Num() == CellTotal)
	{
		// Le masque n'est fiable que si SYNE a fourni une grille complète, sans
		// coordonnées hors limites ni doublon. Sinon le renderer doit garder
		// son fallback à partir des obstacles.
		TArray<uint8> CandidateMask;
		CandidateMask.Init(1, CellTotal);
		TArray<uint8> Seen;
		Seen.Init(0, CellTotal);
		bool bCompleteGrid = true;
		for (const FPrismSyneWorldCell& Cell : Cells)
		{
			if (Cell.X < 0 || Cell.Y < 0 || Cell.X >= CellCountX || Cell.Y >= CellCountY)
			{
				bCompleteGrid = false;
				break;
			}
			const int32 Index = Cell.Y * CellCountX + Cell.X;
			if (Seen[Index])
			{
				bCompleteGrid = false;
				break;
			}
			Seen[Index] = 1;
			CandidateMask[Index] = Cell.bWalkable ? 1 : 0;
		}
		if (bCompleteGrid)
		{
			CellWalkable = MoveTemp(CandidateMask);
		}
	}

	ObstacleCentersUnreal.Empty();
	ObstacleRadiiUnreal.Empty();
	ObstacleCentersUnreal.Reserve(Obstacles.Num());
	ObstacleRadiiUnreal.Reserve(Obstacles.Num());

	const bool bCanProject = (K > 0.0 && CellSize > 0.0);
	const int64 GridTotal64 = static_cast<int64>(CellCountX) * static_cast<int64>(CellCountY);
	const bool bGridBoundsValid = CellCountX > 0 && CellCountY > 0
		&& GridTotal64 > 0 && GridTotal64 <= TNumericLimits<int32>::Max();
	for (const auto& Obs : Obstacles)
	{
		FVector Center = FVector::ZeroVector;
		if (bCanProject)
		{
			Center = UPrismLdkProjection::SyneToUnreal(Obs.Position, K);
		}
		ObstacleCentersUnreal.Add(Center);
		double RUnreal = 0.0;
		if (bCanProject)
		{
			RUnreal = UPrismLdkProjection::SyneRadiusToUnreal(Obs.Radius, K);
		}
		else
		{
			RUnreal = Obs.Radius;
		}
		ObstacleRadiiUnreal.Add(RUnreal);
	}

	WaterResourceCells.Empty();
	WaterResourceCentersUnreal.Empty();
	for (const auto& Res : Resources)
	{
		if (Res.TypeEnum == EPrismSyneResourceType::Water && Res.bHasPosition && bGridBoundsValid)
		{
			FIntPoint CellPt;
			if (Res.bPositionIsCellIndex)
			{
				// La position grille est un indice entier contractuel. Ne pas arrondir
				// les valeurs invalides : elles pourraient masquer une coordonnée
				// fractionnaire ou déborder la plage d'int32.
				if (!FMath::IsFinite(Res.Position.X) || !FMath::IsFinite(Res.Position.Y)
					|| Res.Position.X < 0.0 || Res.Position.Y < 0.0
					|| Res.Position.X >= CellCountX || Res.Position.Y >= CellCountY)
				{
					continue;
				}
				const int32 X = static_cast<int32>(Res.Position.X);
				const int32 Y = static_cast<int32>(Res.Position.Y);
				if (static_cast<double>(X) != Res.Position.X || static_cast<double>(Y) != Res.Position.Y)
				{
					continue;
				}
				CellPt = FIntPoint(X, Y);
			}
			else if (CellSize > 0.0 && FMath::IsFinite(Res.Position.X) && FMath::IsFinite(Res.Position.Y))
			{
				const double CellX = Res.Position.X / CellSize;
				const double CellY = Res.Position.Y / CellSize;
				if (!FMath::IsFinite(CellX) || !FMath::IsFinite(CellY)
					|| CellX < 0.0 || CellY < 0.0 || CellX >= CellCountX || CellY >= CellCountY)
				{
					continue;
				}
				CellPt = FIntPoint(FMath::FloorToInt32(CellX), FMath::FloorToInt32(CellY));
			}
			else
			{
				continue;
			}

			WaterResourceCells.Add(CellPt);
			FVector CenterCell = FVector::ZeroVector;
			if (bCanProject)
			{
				CenterCell = UPrismLdkProjection::CellToTileCenter(CellPt, CellSize, K);
			}
			WaterResourceCentersUnreal.Add(CenterCell);
		}
	}
}
