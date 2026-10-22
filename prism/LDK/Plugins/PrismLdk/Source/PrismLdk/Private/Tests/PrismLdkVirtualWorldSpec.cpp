// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Role : Tests du generateur de "Monde Virtuel" (ile, obstacles, lacs).

#include "Misc/AutomationTest.h"

#include "PrismLdkPCGData.h"
#include "PrismLdkVirtualWorld.h"
#include "PrismLdkWorldRegistry.h"
#include "PrismLdkTypes.h"
#include "Engine/World.h"

namespace
{
	/** Description SYNE minimale mais realiste (profil `prism`). */
	static FPrismSyneWorldDescription MakeTestWorld()
	{
		FPrismSyneWorldDescription World;
		World.Version = TEXT("1.1");
		World.Seed = 42;
		World.CellCountX = 70;
		World.CellCountY = 70;
		World.CellSize = 32.0;

		// Les trois disques de la "cote sud" et la crete centrale du profil prism.
		const struct { const TCHAR* Id; double X; double Y; double R; } Layout[] = {
			{ TEXT("cote-sud-1"),  400.0, 2180.0, 150.0 },
			{ TEXT("cote-sud-2"), 1120.0, 2180.0, 150.0 },
			{ TEXT("cote-sud-3"), 1840.0, 2180.0, 150.0 },
			{ TEXT("crete-centre"),1120.0, 1120.0,  60.0 },
			{ TEXT("amer-1"),      300.0,  300.0,   8.0 },
		};

		for (const auto& Entry : Layout)
		{
			FPrismSyneWorldObstacle Obstacle;
			Obstacle.Id = Entry.Id;
			Obstacle.Position.X = Entry.X;
			Obstacle.Position.Y = Entry.Y;
			Obstacle.Radius = Entry.R;
			World.Obstacles.Add(Obstacle);
		}

		// Une ressource water positionnee (indice de cellule).
		FPrismSyneResource Water;
		Water.Id = TEXT("resource-water-1");
		Water.Type = TEXT("water");
		Water.TypeEnum = EPrismSyneResourceType::Water;
		Water.bHasPosition = true;
		Water.bPositionIsCellIndex = true;
		Water.Position.X = 20;
		Water.Position.Y = 30;
		World.Resources.Add(Water);

		return World;
	}

	static FPrismLdkWorldStats MakeTestStats()
	{
		FPrismLdkWorldStats Stats;
		Stats.CellSize = 32.0;
		Stats.TileSizeUu = 3200.0;
		Stats.K = 100.0;
		Stats.CellCountX = 70;
		Stats.CellCountY = 70;
		Stats.TicksPerSecond = 6;
		Stats.bScaleValid = true;
		return Stats;
	}
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_BuildsIsland, "PrismLdk.VirtualWorld.BuildsIsland", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_BuildsIsland::RunTest(const FString& Parameters)
{
	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	TestNotNull(TEXT("PCGData cree"), Data);
	if (!Data)
	{
		return false;
	}

	Data->BuildFromDescription(MakeTestWorld(), MakeTestStats());

	// Le monde mesure 70 * 3200 = 224 000 uu de cote.
	TestEqual(TEXT("Obstacles projetes"), Data->ObstacleCentersUnreal.Num(), 5);
	TestEqual(TEXT("Rayons projetes"), Data->ObstacleRadiiUnreal.Num(), 5);
	TestEqual(TEXT("Cellules water"), Data->WaterResourceCells.Num(), 1);
	TestEqual(TEXT("Centres water"), Data->WaterResourceCentersUnreal.Num(), 1);

	if (Data->ObstacleRadiiUnreal.Num() == 5)
	{
		// r = 150 SYNE, k = 100 -> 15 000 uu.
		TestEqual(TEXT("Rayon cote sud"), Data->ObstacleRadiiUnreal[0], 15000.0);
		// r = 8 SYNE -> 800 uu.
		TestEqual(TEXT("Rayon amer"), Data->ObstacleRadiiUnreal[4], 800.0);
	}

	// Generation effective de l'ile.
	UWorld* World = GWorld;
	if (!World)
	{
		AddError(TEXT("Pas de UWorld : impossible de instancier l'acteur."));
		return false;
	}

	APrismLdkVirtualWorldActor* Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	TestNotNull(TEXT("Acteur du monde virtuel cree"), Actor);
	if (!Actor)
	{
		return false;
	}

	FPrismLdkVirtualWorldSettings Settings;
	Settings.GridResolution = 64;   // resolution reduite pour la spec
	Settings.bCreateCollision = false;

	const bool bBuilt = Actor->BuildWorld(Data, Settings);
	TestTrue(TEXT("Monde virtuel construit"), bBuilt);
	TestTrue(TEXT("IsBuilt"), Actor->IsBuilt());

	// --- Geometrie ---
	// Centre de l'ile : doit etre haut (plateau).
	const float HeightCenter = Actor->SampleHeightAt(112000.0, 112000.0);
	// Coin nord-ouest : doit etre sous le niveau de la mer.
	const float HeightCorner = Actor->SampleHeightAt(100.0, 100.0);

	TestTrue(TEXT("Le centre est plus haut que le bord"), HeightCenter > HeightCorner);
	TestTrue(TEXT("Le centre est au-dessus du niveau de la mer"), HeightCenter > 0.0f);
	TestTrue(TEXT("Le coin est sous le niveau de la mer"), HeightCorner < 0.0f);

	// --- Obstacle = montagne ---
	// La crete centrale est a (1120, 1120) SYNE -> (112 000, 112 000) uu.
	// Elle doit rester au-dessus du plateau lisse qui l'entoure.
	const float HeightOnCrest = Actor->SampleHeightAt(112000.0, 112000.0);
	const float HeightNearCrest = Actor->SampleHeightAt(112000.0 + 12000.0, 112000.0);
	TestTrue(TEXT("L'obstacle forme un relief dominant"), HeightOnCrest >= HeightNearCrest);

	// --- Lac ---
	// La ressource water est en cellule (20, 30) -> centre de tuile
	// ((20 + 0.5) * 3200, (30 + 0.5) * 3200) = (65 600, 97 600).
	const float HeightInLake = Actor->SampleHeightAt(65600.0, 97600.0);
	TestTrue(TEXT("Le lac est creuse sous le niveau de l'eau"), HeightInLake < 400.0f);

	// La normale doit etre un vecteur unitaire.
	const FVector Normal = Actor->SampleNormalAt(112000.0, 112000.0);
	TestTrue(TEXT("Normale unitaire"), FMath::IsNearlyEqual(Normal.Size(), 1.0f, 0.01f));

	Actor->Destroy();
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_RejectsInvalidData, "PrismLdk.VirtualWorld.RejectsInvalidData", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_RejectsInvalidData::RunTest(const FString& Parameters)
{
	UWorld* World = GWorld;
	if (!World)
	{
		AddError(TEXT("Pas de UWorld : impossible de instancier l'acteur."));
		return false;
	}

	APrismLdkVirtualWorldActor* Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	TestNotNull(TEXT("Acteur cree"), Actor);
	if (!Actor)
	{
		return false;
	}

	FPrismLdkVirtualWorldSettings Settings;

	// Donnees nulles : la generation doit echouer proprement (pas de crash).
	const bool bBuilt = Actor->BuildWorld(nullptr, Settings);
	TestFalse(TEXT("Pas de generation sans donnees"), bBuilt);
	TestFalse(TEXT("IsBuilt reste faux"), Actor->IsBuilt());

	// Des donnees sans echelle exploitable : echec attendu.
	UPrismLdkPCGData* Empty = NewObject<UPrismLdkPCGData>();
	Empty->BuildFromDescription(FPrismSyneWorldDescription(), FPrismLdkWorldStats());
	TestFalse(TEXT("Pas de generation sans echelle"), Actor->BuildWorld(Empty, Settings));

	Actor->Destroy();
	return true;
}
