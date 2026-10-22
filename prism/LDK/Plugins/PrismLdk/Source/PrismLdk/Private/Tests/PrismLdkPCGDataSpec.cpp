// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests unitaires PrismLdkPCGData / helpers PCG

#include "Misc/AutomationTest.h"
#include "PrismLdkPCGData.h"
#include "PrismLdkWorldRegistry.h"
#include "PrismLdkTypes.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkPCGData_BuildFromDescription_Basic, "PrismLdk.PCG.Data.BuildFromDescription.Basic", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkPCGData_BuildFromDescription_Basic::RunTest(const FString& Parameters)
{
	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	TestNotNull(TEXT("PCGData instance created"), Data);
	if (!Data) return false;

	FPrismSyneWorldDescription World;
	World.Version = TEXT("1.1");
	World.Seed = 12345;
	World.CellCountX = 70;
	World.CellCountY = 70;
	World.CellSize = 32.0;

	FPrismLdkWorldStats Stats;
	Stats.CellSize = 32.0;
	Stats.TileSizeUu = 3200.0;
	Stats.K = 100.0;
	Stats.CellCountX = 70;
	Stats.CellCountY = 70;
	Stats.bScaleValid = true;

	FPrismSyneWorldObstacle Obs;
	Obs.Id = TEXT("test-obs");
	Obs.Position.X = 150.0; Obs.Position.Y = 120.0;
	Obs.Radius = 8.0;
	World.Obstacles.Add(Obs);

	FPrismSyneResource Res;
	Res.Id = TEXT("w1");
	Res.Type = TEXT("water");
	Res.TypeEnum = EPrismSyneResourceType::Water;
	Res.bHasPosition = true;
	Res.Position.X = 10; Res.Position.Y = 10;
	World.Resources.Add(Res);

	Data->BuildFromDescription(World, Stats);

	TestEqual(TEXT("CellCountX"), Data->CellCountX, 70);
	TestEqual(TEXT("CellCountY"), Data->CellCountY, 70);
	TestEqual(TEXT("K"), Data->K, 100.0);
	TestEqual(TEXT("Obstacle count"), Data->Obstacles.Num(), 1);
	TestEqual(TEXT("Obstacle centers projected"), Data->ObstacleCentersUnreal.Num(), 1);
	TestEqual(TEXT("Obstacle radii projected"), Data->ObstacleRadiiUnreal.Num(), 1);
	TestTrue(TEXT("Water cells"), Data->WaterResourceCells.Num() >= 1);
	TestTrue(TEXT("Water centers"), Data->WaterResourceCentersUnreal.Num() >= 1);

	if (Data->ObstacleCentersUnreal.Num() > 0)
	{
		TestEqual(TEXT("Obs X"), Data->ObstacleCentersUnreal[0].X, 15000.0);
		TestEqual(TEXT("Obs Y"), Data->ObstacleCentersUnreal[0].Y, 12000.0);
	}
	if (Data->ObstacleRadiiUnreal.Num() > 0)
	{
		TestEqual(TEXT("Obs R"), Data->ObstacleRadiiUnreal[0], 800.0);
	}

	return true;
}
