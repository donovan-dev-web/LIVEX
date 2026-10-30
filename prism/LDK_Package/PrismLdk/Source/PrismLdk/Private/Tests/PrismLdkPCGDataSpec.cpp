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
	Res.bPositionIsCellIndex = true;
	Res.Position.X = 10; Res.Position.Y = 10;
	World.Resources.Add(Res);

	Data->BuildFromDescription(World, Stats);

	TestEqual(TEXT("CellCountX"), Data->CellCountX, 70);
	TestEqual(TEXT("CellCountY"), Data->CellCountY, 70);
	TestEqual(TEXT("K"), Data->K, 100.0);
	TestEqual(TEXT("Obstacle count"), Data->Obstacles.Num(), 1);
	TestEqual(TEXT("Obstacle centers projected"), Data->ObstacleCentersUnreal.Num(), 1);
	TestEqual(TEXT("Obstacle radii projected"), Data->ObstacleRadiiUnreal.Num(), 1);
	TestEqual(TEXT("Water cells"), Data->WaterResourceCells.Num(), 1);
	TestEqual(TEXT("Water cell X"), Data->WaterResourceCells[0].X, 10);
	TestEqual(TEXT("Water cell Y"), Data->WaterResourceCells[0].Y, 10);
	TestEqual(TEXT("Water centers"), Data->WaterResourceCentersUnreal.Num(), 1);

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

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkPCGData_BuildFromDescription_CellMaskAndWorldPosition, "PrismLdk.PCG.Data.BuildFromDescription.CellMaskAndWorldPosition", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkPCGData_BuildFromDescription_CellMaskAndWorldPosition::RunTest(const FString& Parameters)
{
	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	TestNotNull(TEXT("PCGData instance created"), Data);
	if (!Data) return false;

	FPrismSyneWorldDescription World;
	World.Version = TEXT("1.1");
	World.Seed = 7;
	World.CellCountX = 2;
	World.CellCountY = 2;
	World.CellSize = 32.0;

	for (int32 Y = 0; Y < 2; ++Y)
	{
		for (int32 X = 0; X < 2; ++X)
		{
			FPrismSyneWorldCell Cell;
			Cell.X = X;
			Cell.Y = Y;
			Cell.bWalkable = !(X == 1 && Y == 0);
			World.Cells.Add(Cell);
		}
	}

	FPrismSyneResource Water;
	Water.Id = TEXT("w-world");
	Water.Type = TEXT("water");
	Water.TypeEnum = EPrismSyneResourceType::Water;
	Water.bHasPosition = true;
	Water.bPositionIsCellIndex = false;
	Water.Position.X = 48.0;
	Water.Position.Y = 48.0;
	World.Resources.Add(Water);

	FPrismSyneResource FractionalIndex;
	FractionalIndex.Id = TEXT("w-fractional-index");
	FractionalIndex.Type = TEXT("water");
	FractionalIndex.TypeEnum = EPrismSyneResourceType::Water;
	FractionalIndex.bHasPosition = true;
	FractionalIndex.bPositionIsCellIndex = true;
	FractionalIndex.Position.X = 1.5;
	FractionalIndex.Position.Y = 1.0;
	World.Resources.Add(FractionalIndex);

	FPrismSyneResource NegativeIndex;
	NegativeIndex.Id = TEXT("w-negative-index");
	NegativeIndex.Type = TEXT("water");
	NegativeIndex.TypeEnum = EPrismSyneResourceType::Water;
	NegativeIndex.bHasPosition = true;
	NegativeIndex.bPositionIsCellIndex = true;
	NegativeIndex.Position.X = -1.0;
	NegativeIndex.Position.Y = 0.0;
	World.Resources.Add(NegativeIndex);

	FPrismSyneResource Outside;
	Outside.Id = TEXT("w-outside");
	Outside.Type = TEXT("water");
	Outside.TypeEnum = EPrismSyneResourceType::Water;
	Outside.bHasPosition = true;
	Outside.bPositionIsCellIndex = false;
	Outside.Position.X = 160.0;
	Outside.Position.Y = 160.0;
	World.Resources.Add(Outside);

	FPrismLdkWorldStats Stats;
	Stats.CellSize = 32.0;
	Stats.TileSizeUu = 3200.0;
	Stats.K = 100.0;
	Stats.CellCountX = 2;
	Stats.CellCountY = 2;
	Stats.bScaleValid = true;

	Data->BuildFromDescription(World, Stats);

	TestEqual(TEXT("Cell walkable count"), Data->CellWalkable.Num(), 4);
	if (Data->CellWalkable.Num() == 4)
	{
		TestEqual(TEXT("Cell (0,0) walkable"), Data->CellWalkable[0], 1);
		TestEqual(TEXT("Cell (1,0) blocked"), Data->CellWalkable[1], 0);
		TestEqual(TEXT("Cell (0,1) walkable"), Data->CellWalkable[2], 1);
		TestEqual(TEXT("Cell (1,1) walkable"), Data->CellWalkable[3], 1);
	}

	// Une description sans grille complète ne doit pas exposer un faux masque
	// « tout marchable » de même taille, car le renderer doit dériver les
	// bloqueurs à partir des obstacles SYNE.
	FPrismSyneWorldDescription IncompleteWorld;
	IncompleteWorld.CellCountX = 2;
	IncompleteWorld.CellCountY = 2;
	IncompleteWorld.CellSize = 32.0;
	UPrismLdkPCGData* IncompleteData = NewObject<UPrismLdkPCGData>();
	IncompleteData->BuildFromDescription(IncompleteWorld, Stats);
	TestEqual(TEXT("Cell walkable mask absent for incomplete grid"), IncompleteData->CellWalkable.Num(), 0);

	FPrismSyneWorldDescription MalformedWorld;
	MalformedWorld.CellCountX = 2;
	MalformedWorld.CellCountY = 2;
	MalformedWorld.CellSize = 32.0;
	for (int32 Index = 0; Index < 4; ++Index)
	{
		FPrismSyneWorldCell Cell;
		Cell.X = Index % 2;
		Cell.Y = Index / 2;
		Cell.bWalkable = true;
		MalformedWorld.Cells.Add(Cell);
	}
	MalformedWorld.Cells[3].X = 0; // Doublon de (0, 1), et (1, 1) absent.
	FPrismSyneWorldObstacle MalformedObstacle;
	MalformedObstacle.Id = TEXT("fallback-obstacle");
	MalformedObstacle.Position.X = 16.0;
	MalformedObstacle.Position.Y = 16.0;
	MalformedObstacle.Radius = 12.0;
	MalformedWorld.Obstacles.Add(MalformedObstacle);
	UPrismLdkPCGData* MalformedData = NewObject<UPrismLdkPCGData>();
	MalformedData->BuildFromDescription(MalformedWorld, Stats);
	TestEqual(TEXT("Cell mask absent for duplicate/out-of-grid coordinate grid"), MalformedData->CellWalkable.Num(), 0);

	FPrismSyneWorldDescription OversizedWorld;
	OversizedWorld.CellCountX = TNumericLimits<int32>::Max();
	OversizedWorld.CellCountY = 2;
	UPrismLdkPCGData* OversizedData = NewObject<UPrismLdkPCGData>();
	OversizedData->BuildFromDescription(OversizedWorld, Stats);
	TestEqual(TEXT("Cell mask absent for overflowing grid dimensions"), OversizedData->CellWalkable.Num(), 0);

	TestEqual(TEXT("Only in-grid integral water positions"), Data->WaterResourceCells.Num(), 1);
	if (Data->WaterResourceCells.Num() == 1)
	{
		TestEqual(TEXT("World position to cell X"), Data->WaterResourceCells[0].X, 1);
		TestEqual(TEXT("World position to cell Y"), Data->WaterResourceCells[0].Y, 1);
	}
	if (Data->WaterResourceCentersUnreal.Num() == 1)
	{
		TestEqual(TEXT("Water center X"), Data->WaterResourceCentersUnreal[0].X, 4800.0);
		TestEqual(TEXT("Water center Y"), Data->WaterResourceCentersUnreal[0].Y, 4800.0);
	}

	return true;
}
