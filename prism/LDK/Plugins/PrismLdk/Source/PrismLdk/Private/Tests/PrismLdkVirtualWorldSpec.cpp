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
	Settings.TerrainChunkResolution = 32;
	Settings.bCreateCollision = false;

	const bool bBuilt = Actor->BuildWorld(Data, Settings);
	TestTrue(TEXT("Monde virtuel construit"), bBuilt);
	TestTrue(TEXT("IsBuilt"), Actor->IsBuilt());
	TestEqual(TEXT("Terrain decoupe en 4 chunks"), Actor->GetTerrainChunkCount(), 4);
	TestTrue(TEXT("Les obstacles SYNE alimentent le masque de navigation quand la description ne contient pas Cells"),
		Actor->GetBlockedCellCount() > 0);
	TestTrue(TEXT("Les cellules bloquees produisent des volumes NavArea_Null"),
		Actor->GetNavBlockerCount() > 0);

	// Une grille de la bonne taille mais invalide (coordonnées dupliquées)
	// ne doit pas être prise pour une grille complète « tout marchable ».
	FPrismSyneWorldDescription MalformedDescription = MakeTestWorld();
	MalformedDescription.Cells.Reset();
	for (int32 Index = 0; Index < MalformedDescription.CellCountX * MalformedDescription.CellCountY; ++Index)
	{
		FPrismSyneWorldCell Cell;
		Cell.X = Index % MalformedDescription.CellCountX;
		Cell.Y = Index / MalformedDescription.CellCountX;
		Cell.bWalkable = true;
		MalformedDescription.Cells.Add(Cell);
	}
	MalformedDescription.Cells.Last().X = 0;
	MalformedDescription.Cells.Last().Y = 0;
	UPrismLdkPCGData* MalformedData = NewObject<UPrismLdkPCGData>();
	MalformedData->BuildFromDescription(MalformedDescription, MakeTestStats());
	TestEqual(TEXT("Le masque invalide n'est pas publié"), MalformedData->CellWalkable.Num(), 0);
	FPrismLdkVirtualWorldSettings MalformedSettings = Settings;
	MalformedSettings.bRebuildNavigation = false;
	TestTrue(TEXT("Le fallback obstacles restaure les bloqueurs NavArea_Null"), Actor->BuildWorld(MalformedData, MalformedSettings));
	TestTrue(TEXT("Une grille malformée ne supprime pas les blockers"), Actor->GetBlockedCellCount() > 0);
	Actor->Destroy();
	Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	TestNotNull(TEXT("Nouvel acteur pour les tests de géométrie"), Actor);
	if (!Actor)
	{
		return false;
	}
	TestTrue(TEXT("Le build de référence pour la géométrie réussit"), Actor->BuildWorld(Data, MalformedSettings));

	// --- Geometrie : la grille logique EST l'ile ---
	const double GridSize = 70.0 * 3200.0; // 224 000 uu
	const double ShoreMargin = 0.12 * GridSize; // bordure de decoration

	// Centre de la grille : plateau, plus haut que le bord.
	const float HeightCenter = Actor->SampleHeightAt(GridSize * 0.5, GridSize * 0.5);
	// Coin nord-ouest de la grille : toujours de la terre ferme.
	const float HeightCorner = Actor->SampleHeightAt(100.0, 100.0);

	TestTrue(TEXT("Le centre est au-dessus du niveau de la mer"), HeightCenter > 0.0f);
	TestTrue(TEXT("Le coin de la grille est au-dessus de la mer"), HeightCorner > 0.0f);
	TestTrue(TEXT("Le centre est plus haut que le coin"), HeightCenter > HeightCorner);

	// Contrat de projection : aucune cellule de la simulation ne tombe dans la
	// mer ou sur la plage. On balaye les centres de cellules.
	bool bAllCellsAreLand = true;
	double LowestCellHeight = TNumericLimits<double>::Max();
	for (int32 CellY = 0; CellY < 70 && bAllCellsAreLand; CellY += 7)
	{
		for (int32 CellX = 0; CellX < 70 && bAllCellsAreLand; ++CellX)
		{
			const double X = (CellX + 0.5) * 3200.0;
			const double Y = (CellY + 0.5) * 3200.0;
			const float Height = Actor->SampleHeightAt(X, Y);
			LowestCellHeight = FMath::Min(LowestCellHeight, static_cast<double>(Height));
			// 350 uu = BeachHeight : au-dessus, c'est la terre, pas la plage.
			if (Height <= 350.0f)
			{
				bAllCellsAreLand = false;
			}
		}
	}
	TestTrue(TEXT("Toute la grille logique est sur la terre ferme"), bAllCellsAreLand);

	// La mer vit **hors** de la grille : au-dela de la bordure de decoration.
	const float HeightOutside = Actor->SampleHeightAt(-ShoreMargin * 0.8, -ShoreMargin * 0.8);
	TestTrue(TEXT("La zone hors grille est sous la mer"), HeightOutside < 0.0f);

	// --- Obstacle = montagne ---
	// La crete centrale est a (1120, 1120) SYNE -> (112 000, 112 000) uu.
	// Elle doit rester au-dessus du plateau lisse qui l'entoure.
	const float HeightOnCrest = Actor->SampleHeightAt(112000.0, 112000.0);
	const float HeightNearCrest = Actor->SampleHeightAt(112000.0 + 12000.0, 112000.0);
	TestTrue(TEXT("L'obstacle forme un relief dominant"), HeightOnCrest >= HeightNearCrest);

	// bObstaclesCreateLand ne doit désactiver que la terre créée hors grille :
	// les obstacles de la simulation gardent leur relief SYNE.
	FPrismLdkVirtualWorldSettings NoOffGridLandSettings = Settings;
	NoOffGridLandSettings.bObstaclesCreateLand = false;
	TestTrue(TEXT("Monde reconstruit sans terre hors grille"), Actor->BuildWorld(Data, NoOffGridLandSettings));
	const float HeightOnCrestWithoutOffGridLand = Actor->SampleHeightAt(112000.0, 112000.0);
	const float HeightNearCrestWithoutOffGridLand = Actor->SampleHeightAt(112000.0 + 12000.0, 112000.0);
	TestTrue(TEXT("Obstacle interne conserve son relief si les ilots hors grille sont desactives"),
		HeightOnCrestWithoutOffGridLand > HeightNearCrestWithoutOffGridLand);
	// La position de test doit réellement se trouver dans l'influence de
	// l'obstacle, au-delà de la côte de base. Agrandir temporairement un rayon
	// synthétique évite de tester par erreur la terre côtière indépendante des
	// obstacles (qui s'étend normalement hors de la grille).
	const double OriginalAmerRadius = Data->ObstacleRadiiUnreal[4];
	Data->ObstacleRadiiUnreal[4] = 100000.0;
	const double OffGridProbeX = -26800.0; // dans la marge terrain, hors grille
	const double OffGridProbeY = 30000.0;
	TestTrue(TEXT("Le build autorise le cap synthétique hors grille"), Actor->BuildWorld(Data, Settings));
	const float HeightWithOffGridObstacle = Actor->SampleHeightAt(OffGridProbeX, OffGridProbeY);
	TestTrue(TEXT("Obstacle activé crée du relief au point hors grille"), HeightWithOffGridObstacle > 0.0f);
	TestTrue(TEXT("Rebuild sans création de terre hors grille"), Actor->BuildWorld(Data, NoOffGridLandSettings));
	const float HeightWithoutOffGridObstacle = Actor->SampleHeightAt(OffGridProbeX, OffGridProbeY);
	TestTrue(TEXT("Obstacle hors grille ne crée pas de terre si désactivé"), HeightWithoutOffGridObstacle < 0.0f);
	Data->ObstacleRadiiUnreal[4] = OriginalAmerRadius;

	// --- Lac ---
	// La ressource water est en cellule (20, 30) -> centre de tuile
	// ((20 + 0.5) * 3200, (30 + 0.5) * 3200) = (65 600, 97 600).
	TestEqual(TEXT("Un lac genere"), Actor->GetLakeCount(), 1);

	const float LakeWaterLevel = Actor->SampleWaterLevelAt(65600.0, 97600.0);
	TestTrue(TEXT("Le lac a un niveau d'eau au-dessus de la mer"), LakeWaterLevel > 0.0f);

	const float HeightInLake = Actor->SampleHeightAt(65600.0, 97600.0);
	TestTrue(TEXT("Le lac est creuse sous sa surface"), HeightInLake < LakeWaterLevel - 1.0f);
	// Au centre, la cuvette atteint exactement `LakeDepth` sous la surface
	// (largeur du monde = reference -> pas d'echelle verticale).
	TestTrue(TEXT("Fond du lac a LakeDepth sous la surface"),
		FMath::IsNearlyEqual(HeightInLake, LakeWaterLevel - 700.0f, 2.0f));

	TestTrue(TEXT("Hors lac, le niveau d'eau est celui de la mer"),
		FMath::IsNearlyEqual(Actor->SampleWaterLevelAt(1000.0, 1000.0), 0.0f));
	// La normale doit etre un vecteur unitaire.
	const FVector Normal = Actor->SampleNormalAt(112000.0, 112000.0);
	TestTrue(TEXT("Normale unitaire"), FMath::IsNearlyEqual(Normal.Size(), 1.0f, 0.01f));

	Actor->Destroy();
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_ScalesWithTileSize, "PrismLdk.VirtualWorld.ScalesWithTileSize", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_ScalesWithTileSize::RunTest(const FString& Parameters)
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
	Settings.GridResolution = 48;
	Settings.bCreateCollision = false;

	// Monde de reference : 70 * 3200 = 224 000 uu.
	UPrismLdkPCGData* Reference = NewObject<UPrismLdkPCGData>();
	Reference->BuildFromDescription(MakeTestWorld(), MakeTestStats());
	TestTrue(TEXT("Monde de reference construit"), Actor->BuildWorld(Reference, Settings));
	const float ReferenceCenter = Actor->SampleHeightAt(112000.0, 112000.0);
	// Hors grille : -0.096 x 224 000 = -21 504 uu (au-dela de la bordure).
	const float ReferenceSea = Actor->SampleHeightAt(-21504.0, -21504.0);

	// Meme monde, 10 fois plus petit : 70 * 320 = 22 400 uu. Le registre derive
	// `K = TileSizeUu / CellSize`, donc K vaut 10 ici (32 * 10 = 320).
	FPrismLdkWorldStats SmallStats = MakeTestStats();
	SmallStats.TileSizeUu = 320.0;
	SmallStats.K = 10.0;
	UPrismLdkPCGData* Small = NewObject<UPrismLdkPCGData>();
	Small->BuildFromDescription(MakeTestWorld(), SmallStats);
	TestTrue(TEXT("Petit monde construit"), Actor->BuildWorld(Small, Settings));
	const float SmallCenter = Actor->SampleHeightAt(11200.0, 11200.0);
	const float SmallSea = Actor->SampleHeightAt(-2150.4, -2150.4);

	TestTrue(TEXT("Le centre reste au-dessus de la mer"), SmallCenter > 0.0f);
	TestTrue(TEXT("La mer reste hors grille"), SmallSea < 0.0f);
	// Toutes les hauteurs suivent `TileSizeUu` : le rapport doit valoir 0.1.
	TestTrue(TEXT("Hauteur du plateau proportionnelle a TileSizeUu"),
		FMath::IsNearlyEqual(SmallCenter / ReferenceCenter, 0.1f, 0.005f));
	TestTrue(TEXT("Profondeur de mer proportionnelle a TileSizeUu"),
		FMath::IsNearlyEqual(SmallSea / ReferenceSea, 0.1f, 0.02f));

	// Meme proportion cote lacs : la profondeur de cuvette suit l'echelle.
	TestEqual(TEXT("Un lac dans le petit monde"), Actor->GetLakeCount(), 1);
	const float SmallLakeLevel = Actor->SampleWaterLevelAt(6560.0, 9760.0);
	TestTrue(TEXT("Le lac du petit monde est moins profond en valeur absolue"),
		SmallLakeLevel > 0.0f && SmallLakeLevel < 2000.0f);

	Actor->Destroy();
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_LakeStaysOnGrid, "PrismLdk.VirtualWorld.LakeStaysOnGrid", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_LakeStaysOnGrid::RunTest(const FString& Parameters)
{
	UWorld* World = GWorld;
	if (!World)
	{
		AddError(TEXT("Pas de UWorld : impossible de instancier l'acteur."));
		return false;
	}

	// Une ressource Water sur la cellule la plus extreme (0, 0) : sa position
	// projetee est le centre de tuile (1 600, 1 600) uu, donc dans la grille.
	// Contrat : le lac y reste, sans etre deplace ni ignore, et il est sur la
	// terre ferme (la plage et la mer sont hors grille).
	FPrismSyneWorldDescription Description = MakeTestWorld();
	TestTrue(TEXT("Une ressource water"), Description.Resources.Num() >= 1);
	Description.Resources[0].Position.X = 0;
	Description.Resources[0].Position.Y = 0;

	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	Data->BuildFromDescription(Description, MakeTestStats());
	TestTrue(TEXT("Centre water projete dans la grille"),
		Data->WaterResourceCentersUnreal.Num() == 1
			&& Data->WaterResourceCentersUnreal[0].X == 1600.0
			&& Data->WaterResourceCentersUnreal[0].Y == 1600.0);

	APrismLdkVirtualWorldActor* Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	TestNotNull(TEXT("Acteur cree"), Actor);
	if (!Actor)
	{
		return false;
	}

	FPrismLdkVirtualWorldSettings Settings;
	Settings.GridResolution = 48;
	Settings.bCreateCollision = false;

	TestTrue(TEXT("Monde construit"), Actor->BuildWorld(Data, Settings));
	TestEqual(TEXT("La ressource produit un lac"), Actor->GetLakeCount(), 1);

	const float WaterLevel = Actor->SampleWaterLevelAt(1600.0, 1600.0);
	const float Ground = Actor->SampleHeightAt(1600.0, 1600.0);
	TestTrue(TEXT("Le lac a une surface au-dessus de la mer"), WaterLevel > 0.0f);
	TestTrue(TEXT("Le lac est creuse sous sa surface"), Ground < WaterLevel - 1.0f);
	// Meme le fond de la cuvette reste au-dessus du niveau de la mer.
	TestTrue(TEXT("Le fond du lac reste sur la terre ferme"), Ground > 0.0f);
	// Hors de l'influence du lac, la cellule voisine est sur le haut de la falaise.
	const float Undisturbed = Actor->SampleHeightAt(12000.0, 1600.0);
	TestTrue(TEXT("La cellule voisine reste sur la terre ferme"), Undisturbed > 1000.0f);

	Actor->Destroy();
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_AsyncBuildLifecycle, "PrismLdk.VirtualWorld.AsyncBuildLifecycle", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_AsyncBuildLifecycle::RunTest(const FString& Parameters)
{
	UWorld* World = GWorld;
	if (!World)
	{
		AddError(TEXT("Pas de UWorld pour tester le build progressif."));
		return false;
	}

	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	Data->BuildFromDescription(MakeTestWorld(), MakeTestStats());
	FPrismLdkVirtualWorldSettings Settings;
	Settings.GridResolution = 16;
	Settings.TerrainChunkResolution = 8;
	Settings.bCreateCollision = false;
	Settings.bGenerateSea = false;
	Settings.bGenerateResourceScatter = false;
	Settings.bGenerateNavBlockers = false;
	Settings.bRebuildNavigation = false;

	APrismLdkVirtualWorldActor* Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	TestNotNull(TEXT("Acteur créé"), Actor);
	if (!Actor)
	{
		return false;
	}

	float PreviousProgress = Actor->GetBuildProgress();
	int32 ProgressUpdates = 0;
	TestTrue(TEXT("Le démarrage asynchrone accepte les données"), Actor->StartBuildWorldAsync(Data, Settings, 17));
	TestFalse(TEXT("Le monde n'est pas immédiatement considéré prêt"), Actor->IsBuilt());
	TestTrue(TEXT("La génération est signalée active"), Actor->IsBuildInProgress());
	TestTrue(TEXT("L'acteur active son tick pour la progression"), Actor->IsActorTickEnabled());

	for (int32 Frame = 0; Frame < 100 && Actor->IsBuildInProgress(); ++Frame)
	{
		Actor->Tick(1.0f / 60.0f);
		const float CurrentProgress = Actor->GetBuildProgress();
		if (CurrentProgress > PreviousProgress)
		{
			++ProgressUpdates;
		}
		PreviousProgress = CurrentProgress;
	}

	TestFalse(TEXT("La génération progressive est terminée"), Actor->IsBuildInProgress());
	TestTrue(TEXT("La génération se termine avec succès"), Actor->IsBuilt());
	TestTrue(TEXT("La progression avance sur plusieurs frames"), ProgressUpdates > 3);
	TestTrue(TEXT("La progression terminale vaut 100 %"), FMath::IsNearlyEqual(Actor->GetBuildProgress(), 1.0f));
	TestFalse(TEXT("Le tick est désactivé après complétion"), Actor->IsActorTickEnabled());

	Actor->Destroy();
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPrismLdkVirtualWorld_AsyncBuildCancellation, "PrismLdk.VirtualWorld.AsyncBuildCancellation", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPrismLdkVirtualWorld_AsyncBuildCancellation::RunTest(const FString& Parameters)
{
	UWorld* World = GWorld;
	if (!World)
	{
		AddError(TEXT("Pas de UWorld pour tester l'annulation du build."));
		return false;
	}
	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	Data->BuildFromDescription(MakeTestWorld(), MakeTestStats());
	FPrismLdkVirtualWorldSettings Settings;
	Settings.GridResolution = 16;
	Settings.bCreateCollision = false;

	APrismLdkVirtualWorldActor* Actor = World->SpawnActor<APrismLdkVirtualWorldActor>();
	if (!Actor)
	{
		AddError(TEXT("Échec de création de l'acteur."));
		return false;
	}
	TestTrue(TEXT("Le build démarre"), Actor->StartBuildWorldAsync(Data, Settings, 23));
	TestTrue(TEXT("Le build démarre avec une progression nulle"), FMath::IsNearlyZero(Actor->GetBuildProgress()));
	Actor->CancelBuildWorldAsync(22);
	TestTrue(TEXT("Un autre identifiant n'annule pas le build"), Actor->IsBuildInProgress());
	Actor->CancelBuildWorldAsync(23);
	TestFalse(TEXT("L'annulation termine le build"), Actor->IsBuildInProgress());
	TestFalse(TEXT("Le monde annulé reste non construit"), Actor->IsBuilt());
	TestFalse(TEXT("Le tick s'arrête après annulation"), Actor->IsActorTickEnabled());
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
