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

	ObstacleCentersUnreal.Empty();
	ObstacleRadiiUnreal.Empty();
	ObstacleCentersUnreal.Reserve(Obstacles.Num());
	ObstacleRadiiUnreal.Reserve(Obstacles.Num());

	const bool bCanProject = (K > 0.0 && CellSize > 0.0);
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
		if (Res.TypeEnum == EPrismSyneResourceType::Water && Res.bHasPosition)
		{
			FIntPoint CellPt(Res.Position.X, Res.Position.Y);
			WaterResourceCells.Add(CellPt);
			FVector CenterCell = FVector::ZeroVector;
			if (bCanProject && CellSize > 0.0)
			{
				CenterCell = UPrismLdkProjection::CellToTileCenter(CellPt, CellSize, K);
			}
			WaterResourceCentersUnreal.Add(CenterCell);
		}
	}
}
