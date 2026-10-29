// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Role : Generation runtime du "Monde Virtuel" (ile stylisee hand-painted).

#include "PrismLdkVirtualWorld.h"

#include "PrismLdkPCGData.h"
#include "PrismLdkProjection.h"
#include "PrismLdkSubsystem.h"
#include "ProceduralMeshComponent.h"
#include "Components/HierarchicalInstancedStaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Materials/MaterialInterface.h"
#include "NavAreas/NavArea_Null.h"
#include "NavModifierComponent.h"
#include "NavigationSystem.h"
#include "NavigationData.h"
#include "Math/RandomStream.h"

namespace
{
	/** Lisseur de bord : t -> 3t^2 - 2t^3, borne a [0,1]. */
	inline double SmoothEdge(double T)
	{
		T = FMath::Clamp(T, 0.0, 1.0);
		return T * T * (3.0 - 2.0 * T);
	}

	/** Hash deterministe dans [0,1], sans etat global. */
	inline double Hash01(int32 Index, int32 Salt)
	{
		uint32 H = static_cast<uint32>(Index) * 374761393u + static_cast<uint32>(Salt) * 668265263u;
		H = (H ^ (H >> 13)) * 1274126177u;
		H ^= H >> 16;
		return static_cast<double>(H & 0x00FFFFFFu) / 16777215.0;
	}
}

APrismLdkVirtualWorldActor::APrismLdkVirtualWorldActor()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;

	TerrainMesh = CreateDefaultSubobject<UProceduralMeshComponent>(TEXT("TerrainMesh"));
	SetRootComponent(TerrainMesh);
	TerrainMesh->SetCollisionProfileName(TEXT("BlockAll"));
	TerrainMesh->bUseAsyncCooking = false;

	WaterMesh = CreateDefaultSubobject<UProceduralMeshComponent>(TEXT("WaterMesh"));
	WaterMesh->SetupAttachment(TerrainMesh);
	WaterMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	WaterMesh->bUseAsyncCooking = false;

	auto SetupInstances = [this](UHierarchicalInstancedStaticMeshComponent* Component)
	{
		Component->SetupAttachment(TerrainMesh);
		Component->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Component->SetGenerateOverlapEvents(false);
		Component->SetCastShadow(true);
		Component->SetMobility(EComponentMobility::Movable);
	};

	TreeInstances = CreateDefaultSubobject<UHierarchicalInstancedStaticMeshComponent>(TEXT("TreeInstances"));
	SetupInstances(TreeInstances);

	RockInstances = CreateDefaultSubobject<UHierarchicalInstancedStaticMeshComponent>(TEXT("RockInstances"));
	SetupInstances(RockInstances);

	CropInstances = CreateDefaultSubobject<UHierarchicalInstancedStaticMeshComponent>(TEXT("CropInstances"));
	SetupInstances(CropInstances);

	ReedInstances = CreateDefaultSubobject<UHierarchicalInstancedStaticMeshComponent>(TEXT("ReedInstances"));
	SetupInstances(ReedInstances);
}

void APrismLdkVirtualWorldActor::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	SetActorTickEnabled(false);
	AsyncBuildStage = EAsyncBuildStage::None;
	AsyncHeightGrid.Reset();
	AsyncNormalGrid.Reset();
	UnbindNavigationBuild();
	CleanupNavBlockers();
	Super::EndPlay(EndPlayReason);
}

void APrismLdkVirtualWorldActor::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	TickAsyncBuild();
}

bool APrismLdkVirtualWorldActor::StartBuildWorldAsync(
	UPrismLdkPCGData* Data,
	const FPrismLdkVirtualWorldSettings& InSettings,
	int32 BuildId)
{
	if (AsyncBuildStage != EAsyncBuildStage::None)
	{
		return false;
	}

	AsyncBuildId = BuildId;
	AsyncBuildProgress = 0.0f;
	AsyncBuildPhase = NAME_None;
	// Le même nettoyage et la même validation que BuildWorld, sans effectuer
	// de travail coûteux dans le callback du bouton Blueprint.
	LoadContext(Data, InSettings);
	bBuilt = false;
	if (!Data || WorldSizeX <= 1.0 || WorldSizeY <= 1.0 || CellCountX <= 0 || CellCountY <= 0 || !TerrainMesh)
	{
		CleanupNavBlockers();
		ResetTerrainChunks();
		if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
		if (TreeInstances) { TreeInstances->ClearInstances(); }
		if (RockInstances) { RockInstances->ClearInstances(); }
		if (CropInstances) { CropInstances->ClearInstances(); }
		if (ReedInstances) { ReedInstances->ClearInstances(); }
		AsyncBuildId = INDEX_NONE;
		AsyncBuildPhase = TEXT("Failed");
		// Le build n'a pas été accepté : pas de callback terminal, pour que le
		// Blueprint ne traite pas deux fois la voie de retour false.
		return false;
	}

	CleanupNavBlockers();

	ResetTerrainChunks();
	if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
	if (TreeInstances) { TreeInstances->ClearInstances(); }
	if (RockInstances) { RockInstances->ClearInstances(); }
	if (CropInstances) { CropInstances->ClearInstances(); }
	if (ReedInstances) { ReedInstances->ClearInstances(); }
	if (WaterMesh && WaterMaterialOverride) { WaterMesh->SetMaterial(0, WaterMaterialOverride); }

	AsyncBuildResolution = FMath::Clamp(InSettings.GridResolution, 16, 512);
	AsyncBuildVertexCount = AsyncBuildResolution + 1;
	AsyncBuildRow = 0;
	AsyncBuildChunk = 0;
	AsyncNavigationWaitSeconds = 0.0;
	bNavigationGenerationFinished = false;
	bNavigationBuildRequested = false;
	ExpectedNavData = nullptr;
	PendingNavigationSystem.Reset();
	bHasNavigationProbe = false;
	AsyncHeightGrid.SetNumUninitialized(AsyncBuildVertexCount * AsyncBuildVertexCount);
	AsyncNormalGrid.SetNumUninitialized(AsyncBuildVertexCount * AsyncBuildVertexCount);

	const int32 RequestedChunk = FMath::Clamp(InSettings.TerrainChunkResolution, 0, AsyncBuildResolution);
	// La voie progressive borne le coût de chaque CreateMeshSection, même si
	// l'utilisateur a demandé un seul chunk très grand ou désactivé le découpage.
	AsyncBuildChunkResolution = FMath::Min(RequestedChunk > 0 ? RequestedChunk : 64, 64);
	AsyncBuildChunksX = (AsyncBuildResolution + AsyncBuildChunkResolution - 1) / AsyncBuildChunkResolution;
	AsyncBuildChunksY = AsyncBuildChunksX;
	AsyncBuildStage = EAsyncBuildStage::Heights;
	AsyncBuildProgress = 0.0f;
	AsyncBuildPhase = TEXT("Heights");
	SetActorTickEnabled(true);
	OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
	return true;
}

void APrismLdkVirtualWorldActor::CancelBuildWorldAsync(int32 BuildId)
{
	if (AsyncBuildStage == EAsyncBuildStage::None || BuildId != AsyncBuildId)
	{
		return;
	}
	FinishAsyncBuild(false, TEXT("Génération annulée."));
}

void APrismLdkVirtualWorldActor::FinishAsyncBuild(bool bSuccess, const FString& Error)
{
	const int32 CompletedBuildId = AsyncBuildId;
	const bool bHadActiveBuild = AsyncBuildId != INDEX_NONE;
	AsyncBuildStage = EAsyncBuildStage::None;
	UnbindNavigationBuild();

	bBuilt = bSuccess;
	SetActorTickEnabled(false);
	AsyncHeightGrid.Reset();
	AsyncNormalGrid.Reset();
	if (!bSuccess)
	{
		CleanupNavBlockers();
		ResetTerrainChunks();
		if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
		if (TreeInstances) { TreeInstances->ClearInstances(); }
		if (RockInstances) { RockInstances->ClearInstances(); }
		if (CropInstances) { CropInstances->ClearInstances(); }
		if (ReedInstances) { ReedInstances->ClearInstances(); }
	}
	AsyncBuildId = INDEX_NONE;
	if (bHadActiveBuild)
	{
		if (bSuccess)
		{
			AsyncBuildProgress = 1.0f;
			AsyncBuildPhase = TEXT("Complete");
			OnBuildProgress.Broadcast(CompletedBuildId, AsyncBuildPhase, AsyncBuildProgress);
		}
		else
		{
			AsyncBuildPhase = TEXT("Failed");
		}
		OnBuildCompleted.Broadcast(CompletedBuildId, bSuccess, Error);
	}
}

void APrismLdkVirtualWorldActor::TickAsyncBuild()
{
	if (AsyncBuildStage == EAsyncBuildStage::None)
	{
		return;
	}

	// Limite de travail par frame. Toute création/modification d'UObject reste
	// sur le Game Thread. Le budget borne les boucles CPU; CreateMeshSection
	// reste un appel Engine pouvant prendre du temps pour un chunk.
	constexpr int32 RowsPerFrame = 8;
	constexpr int32 NormalsPerFrame = 12;
	constexpr double NavigationTimeoutSeconds = 30.0;

	if (AsyncBuildStage == EAsyncBuildStage::Heights)
	{
		const int32 EndRow = FMath::Min(AsyncBuildRow + RowsPerFrame, AsyncBuildVertexCount);
		for (; AsyncBuildRow < EndRow; ++AsyncBuildRow)
		{
			const double Y = TerrainMinY + (static_cast<double>(AsyncBuildRow) / AsyncBuildResolution) * TerrainSizeY;
			for (int32 XIndex = 0; XIndex < AsyncBuildVertexCount; ++XIndex)
			{
				const double X = TerrainMinX + (static_cast<double>(XIndex) / AsyncBuildResolution) * TerrainSizeX;
				AsyncHeightGrid[AsyncBuildRow * AsyncBuildVertexCount + XIndex] = ComputeHeight(X, Y);
			}
		}
		const float Progress = 0.25f * AsyncBuildRow / AsyncBuildVertexCount;
		if (AsyncBuildRow >= AsyncBuildVertexCount)
		{
			AsyncBuildRow = 0;
			AsyncBuildStage = EAsyncBuildStage::Normals;
		}
		AsyncBuildProgress = Progress;
		AsyncBuildPhase = TEXT("Heights");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}

	if (AsyncBuildStage == EAsyncBuildStage::Normals)
	{
		const int32 EndRow = FMath::Min(AsyncBuildRow + NormalsPerFrame, AsyncBuildVertexCount);
		for (; AsyncBuildRow < EndRow; ++AsyncBuildRow)
		{
			const int32 YPrev = FMath::Max(0, AsyncBuildRow - 1);
			const int32 YNext = FMath::Min(AsyncBuildResolution, AsyncBuildRow + 1);
			const double DeltaY = FMath::Max(1.0, static_cast<double>(YNext - YPrev) * (TerrainSizeY / AsyncBuildResolution));
			for (int32 XIndex = 0; XIndex < AsyncBuildVertexCount; ++XIndex)
			{
				const int32 XPrev = FMath::Max(0, XIndex - 1);
				const int32 XNext = FMath::Min(AsyncBuildResolution, XIndex + 1);
				const double DeltaX = FMath::Max(1.0, static_cast<double>(XNext - XPrev) * (TerrainSizeX / AsyncBuildResolution));
				const double HLeft = AsyncHeightGrid[AsyncBuildRow * AsyncBuildVertexCount + XPrev];
				const double HRight = AsyncHeightGrid[AsyncBuildRow * AsyncBuildVertexCount + XNext];
				const double HDown = AsyncHeightGrid[YPrev * AsyncBuildVertexCount + XIndex];
				const double HUp = AsyncHeightGrid[YNext * AsyncBuildVertexCount + XIndex];
				AsyncNormalGrid[AsyncBuildRow * AsyncBuildVertexCount + XIndex] = FVector(
					static_cast<float>(-(HRight - HLeft) / DeltaX),
					static_cast<float>(-(HUp - HDown) / DeltaY), 1.0f).GetSafeNormal();
			}
		}
		const float Progress = 0.25f + 0.10f * AsyncBuildRow / AsyncBuildVertexCount;
		if (AsyncBuildRow >= AsyncBuildVertexCount)
		{
			AsyncBuildChunk = 0;
			AsyncBuildStage = EAsyncBuildStage::TerrainChunks;
		}
		AsyncBuildProgress = Progress;
		AsyncBuildPhase = TEXT("Normals");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}

	if (AsyncBuildStage == EAsyncBuildStage::TerrainChunks)
	{
		const int32 ChunkCount = AsyncBuildChunksX * AsyncBuildChunksY;
		if (!BuildTerrainChunkFromGrid(AsyncBuildChunk))
		{
			FinishAsyncBuild(false, FString::Printf(TEXT("Échec de création du chunk terrain %d."), AsyncBuildChunk));
			return;
		}
		++AsyncBuildChunk;
		const float Progress = 0.35f + 0.45f * AsyncBuildChunk / FMath::Max(1, ChunkCount);
		if (AsyncBuildChunk >= ChunkCount)
		{
			AsyncBuildStage = EAsyncBuildStage::Water;
		}
		AsyncBuildProgress = Progress;
		AsyncBuildPhase = TEXT("TerrainChunks");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}

	if (AsyncBuildStage == EAsyncBuildStage::Water)
	{
		BuildWaterMesh(Settings);
		AsyncBuildStage = EAsyncBuildStage::Scatter;
		AsyncBuildProgress = 0.82f;
		AsyncBuildPhase = TEXT("Water");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}
	if (AsyncBuildStage == EAsyncBuildStage::Scatter)
	{
		BuildResourceScatter(Settings);
		AsyncBuildStage = EAsyncBuildStage::NavBlockers;
		AsyncBuildProgress = 0.87f;
		AsyncBuildPhase = TEXT("Scatter");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}
	if (AsyncBuildStage == EAsyncBuildStage::NavBlockers)
	{
		if (!BuildNavBlockers(Settings))
		{
			FinishAsyncBuild(false, TEXT("Échec de création d'un blocker de navigation."));
			return;
		}

		if (Settings.bRebuildNavigation)
		{
			if (!GetWorld())
			{
				FinishAsyncBuild(false, TEXT("Monde Unreal indisponible avant la génération du NavMesh."));
				return;
			}
			UNavigationSystemV1* Nav = UNavigationSystemV1::GetCurrent(GetWorld());
			if (!Nav)
			{
				FinishAsyncBuild(false, TEXT("NavigationSystem indisponible avant la génération des blockers."));
				return;
			}
			if (!BeginNavigationBuild())
			{
				FinishAsyncBuild(false, TEXT("NavData absent ou delegate de fin de génération indisponible."));
				return;
			}
			AsyncNavigationWaitSeconds = 0.0;
			AsyncBuildStage = EAsyncBuildStage::Navigation;
		}
		else
		{
			AsyncBuildStage = EAsyncBuildStage::Complete;
		}
		AsyncBuildProgress = 0.92f;
		AsyncBuildPhase = TEXT("NavBlockers");
		OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		return;
	}
	if (AsyncBuildStage == EAsyncBuildStage::Navigation)
	{
		const double DeltaSeconds = GetWorld() ? GetWorld()->GetDeltaSeconds() : 0.0;
		AsyncNavigationWaitSeconds += DeltaSeconds;
		if (AsyncNavigationWaitSeconds < 0.1)
		{
			return;
		}
		if (!GetWorld())
		{
			FinishAsyncBuild(false, TEXT("Monde Unreal indisponible pendant l'attente du NavMesh."));
			return;
		}
		UNavigationSystemV1* Nav = UNavigationSystemV1::GetCurrent(GetWorld());
		if (!Nav || Nav != PendingNavigationSystem.Get())
		{
			FinishAsyncBuild(false, TEXT("NavigationSystem indisponible ou remplacé pendant la génération du NavMesh."));
			return;
		}
		AsyncBuildProgress = 0.92f + 0.07f * (1.0f - FMath::Exp(-AsyncNavigationWaitSeconds));
		AsyncBuildPhase = TEXT("Navigation");
		if (bNavigationGenerationFinished && Nav->GetNumRemainingBuildTasks() == 0 && !Nav->IsNavigationBuildInProgress())
		{
			if (ValidateNavigationBuild())
			{
				AsyncBuildStage = EAsyncBuildStage::Complete;
			}
			else if (AsyncNavigationWaitSeconds >= NavigationTimeoutSeconds)
			{
				FinishAsyncBuild(false, TEXT("Le NavMesh généré ne couvre pas le point de contrôle jouable."));
				return;
			}
		}
		if (AsyncBuildStage == EAsyncBuildStage::Navigation && AsyncNavigationWaitSeconds >= NavigationTimeoutSeconds)
		{
			FinishAsyncBuild(false, TEXT("Délai dépassé pendant la génération du NavMesh."));
			return;
		}

		if (AsyncBuildStage == EAsyncBuildStage::Navigation)
		{
			OnBuildProgress.Broadcast(AsyncBuildId, AsyncBuildPhase, AsyncBuildProgress);
		}
		return;
	}
	if (AsyncBuildStage == EAsyncBuildStage::Complete)
	{
		FinishAsyncBuild(true, FString());
	}
}

bool APrismLdkVirtualWorldActor::BuildTerrainChunkFromGrid(int32 ChunkIndex)
{
	if (!TerrainMesh || ChunkIndex < 0 || ChunkIndex >= AsyncBuildChunksX * AsyncBuildChunksY)
	{
		return false;
	}
	const int32 ChunkX = ChunkIndex % AsyncBuildChunksX;
	const int32 ChunkY = ChunkIndex / AsyncBuildChunksX;
	const int32 StartX = ChunkX * AsyncBuildChunkResolution;
	const int32 StartY = ChunkY * AsyncBuildChunkResolution;
	const int32 EndX = FMath::Min(StartX + AsyncBuildChunkResolution, AsyncBuildResolution);
	const int32 EndY = FMath::Min(StartY + AsyncBuildChunkResolution, AsyncBuildResolution);
	const int32 LocalResX = EndX - StartX;
	const int32 LocalResY = EndY - StartY;
	const int32 LocalVertexCount = LocalResX + 1;
	const int32 LocalVertexTotal = LocalVertexCount * (LocalResY + 1);
	TArray<FVector> Vertices; Vertices.Reserve(LocalVertexTotal);
	TArray<FVector> Normals; Normals.Reserve(LocalVertexTotal);
	TArray<FVector2D> UV0; UV0.Reserve(LocalVertexTotal);
	TArray<FVector2D> UV1; UV1.Reserve(LocalVertexTotal);
	TArray<FLinearColor> Colors; Colors.Reserve(LocalVertexTotal);
	TArray<FProcMeshTangent> Tangents; Tangents.Reserve(LocalVertexTotal);
	TArray<int32> Triangles; Triangles.Reserve(LocalResX * LocalResY * 6);
	const double UvScale = FMath::Max(1.0, static_cast<double>(Settings.TerrainUvScale));
	for (int32 LocalY = 0; LocalY <= LocalResY; ++LocalY)
	{
		const int32 GlobalY = StartY + LocalY;
		const double Y = TerrainMinY + (static_cast<double>(GlobalY) / AsyncBuildResolution) * TerrainSizeY;
		for (int32 LocalX = 0; LocalX <= LocalResX; ++LocalX)
		{
			const int32 GlobalX = StartX + LocalX;
			const double X = TerrainMinX + (static_cast<double>(GlobalX) / AsyncBuildResolution) * TerrainSizeX;
			const int32 GridIndex = GlobalY * AsyncBuildVertexCount + GlobalX;
			const FVector Normal = AsyncNormalGrid[GridIndex];
			const double Z = AsyncHeightGrid[GridIndex];
			Vertices.Add(FVector(X, Y, Z)); Normals.Add(Normal);
			UV0.Add(FVector2D(X / UvScale, Y / UvScale));
			UV1.Add(FVector2D(static_cast<double>(GlobalX) / AsyncBuildResolution, static_cast<double>(GlobalY) / AsyncBuildResolution));
			const double Food = GetResourceMask(X, Y, EPrismSyneResourceType::Food);
			const double Wood = GetResourceMask(X, Y, EPrismSyneResourceType::Wood);
			const double Mineral = GetResourceMask(X, Y, EPrismSyneResourceType::Mineral);
			const double Slope = FMath::Clamp((1.0 - Normal.Z) * 2.0, 0.0, 1.0);
			const double Height01 = FMath::Clamp((Z - Settings.BeachHeight) / FMath::Max(1.0, static_cast<double>(Settings.IslandHeight) - Settings.BeachHeight), 0.0, 1.0);
			const double Moisture = FMath::Clamp(0.45 + 0.25 * SampleFbm(X * Settings.NoiseScale * 1.7 + DetailOffset.X, Y * Settings.NoiseScale * 1.7 + DetailOffset.Y) + 0.45 * Wood + 0.30 * Food - 0.20 * Mineral, 0.0, 1.0);
			const double Rock = FMath::Clamp(0.8 * Slope + 0.15 * Height01 + 0.55 * Mineral, 0.0, 1.0);
			Colors.Add(FLinearColor(static_cast<float>(Height01), static_cast<float>(Slope), static_cast<float>(Moisture), static_cast<float>(Rock)));
			FVector TangentX(1.0f, 0.0f, 0.0f);
			TangentX = (TangentX - Normal * FVector::DotProduct(TangentX, Normal)).GetSafeNormal();
			if (TangentX.IsNearlyZero()) { TangentX = FVector(0.0f, 1.0f, 0.0f); }
			Tangents.Add(FProcMeshTangent(TangentX, false));
		}
	}
	for (int32 LocalY = 0; LocalY < LocalResY; ++LocalY)
	{
		for (int32 LocalX = 0; LocalX < LocalResX; ++LocalX)
		{
			const int32 I0 = LocalY * LocalVertexCount + LocalX, I1 = I0 + 1;
			const int32 I2 = I0 + LocalVertexCount, I3 = I2 + 1;
			Triangles.Add(I0); Triangles.Add(I2); Triangles.Add(I1);
			Triangles.Add(I1); Triangles.Add(I2); Triangles.Add(I3);
		}
	}
	UProceduralMeshComponent* Chunk = nullptr;
	if (ChunkIndex == 0)
	{
		Chunk = TerrainMesh;
	}
	else
	{
		Chunk = CreateTerrainChunk();
		if (Chunk)
		{
			TerrainChunks.Add(Chunk);
		}
	}
	if (!Chunk)
	{
		return false;
	}

	// Les sections doivent exister avant l'enregistrement : l'octree de navigation
	// ignore un composant enregistré avec des bounds vides.
	if (Chunk->IsRegistered())
	{
		Chunk->UnregisterComponent();
	}
	Chunk->ClearAllMeshSections();
	Chunk->CreateMeshSection_LinearColor(
		0, Vertices, Triangles, Normals, UV0, UV1,
		TArray<FVector2D>(), TArray<FVector2D>(), Colors, Tangents,
		Settings.bCreateCollision, false);
	if (!Chunk->IsRegistered())
	{
		Chunk->RegisterComponent();
	}
	if (!Chunk->GetProcMeshSection(0))
	{
		UE_LOG(LogPrismLdk, Error, TEXT("Échec de création du chunk terrain %d."), ChunkIndex);
		return false;
	}

	if (TerrainMaterialOverride)
	{
		Chunk->SetMaterial(0, TerrainMaterialOverride);
	}
	return true;
}

void APrismLdkVirtualWorldActor::LoadContext(const UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& InSettings)
{
	Settings = InSettings;

	WorldSizeX = 1.0;
	WorldSizeY = 1.0;
	VerticalScale = 1.0;
	CellSizeUu = 1.0;
	CellCountX = 0;
	CellCountY = 0;
	CellBlocked.Reset();
	ResourcePoints.Reset();
	ObstacleInfluences.Reset();
	Lakes.Reset();
	LakeWaterLevels.Reset();

	if (!Data)
	{
		return;
	}

	const int64 CellTotal64 = static_cast<int64>(Data->CellCountX) * static_cast<int64>(Data->CellCountY);
	if (Data->CellCountX <= 0 || Data->CellCountY <= 0
		|| CellTotal64 <= 0 || CellTotal64 > TNumericLimits<int32>::Max())
	{
		return;
	}

	const double TileSize = Data->TileSizeUu > 0.0
		? Data->TileSizeUu
		: (Data->CellSize > 0.0 && Data->K > 0.0 ? Data->CellSize * Data->K : 0.0);

	WorldSizeX = FMath::Max(1.0, Data->CellCountX * TileSize);
	WorldSizeY = FMath::Max(1.0, Data->CellCountY * TileSize);
	CellSizeUu = FMath::Max(1.0, TileSize);
	CellCountX = FMath::Max(0, Data->CellCountX);
	CellCountY = FMath::Max(0, Data->CellCountY);

	// Echelle verticale : le monde de reference mesure 224 000 uu (70 cellules
	// de 3 200). Les hauteurs absolues suivent cette echelle ; les frequences
	// de bruit font l'inverse (coordonnees plus petites -> frequence plus
	// grande) pour garder le meme nombre de reliefs par ecran.
	VerticalScale = FMath::Clamp(
		FMath::Max(WorldSizeX, WorldSizeY) / 224000.0, 0.01, 100.0);
	const double HeightScale = VerticalScale;
	const double FrequencyScale = 1.0 / HeightScale;

	Settings.IslandHeight *= HeightScale;
	Settings.CliffHeight *= HeightScale;
	Settings.BeachHeight *= HeightScale;
	Settings.SeaDepth *= HeightScale;
	Settings.NoiseAmplitude *= HeightScale;
	Settings.RidgeNoiseAmplitude *= HeightScale;
	Settings.TerrainDetailAmplitude *= HeightScale;
	Settings.LakeLevel *= HeightScale;
	Settings.LakeDepth *= HeightScale;
	Settings.ResourceHeightInfluence *= HeightScale;
	Settings.TerrainUvScale = FMath::Max(100.0, Settings.TerrainUvScale * HeightScale);
	Settings.NoiseScale *= FrequencyScale;
	Settings.CoastNoiseScale *= FrequencyScale;
	Settings.CoastWarpScale *= FrequencyScale;
	Settings.NoiseWarpScale *= FrequencyScale;
	Settings.RidgeNoiseScale *= FrequencyScale;
	Settings.TerrainDetailScale *= FrequencyScale;

	// Geometrie du terrain : la grille logique (la simulation) est entouree d'une
	// bordure de decoration ou vivent la falaise, la plage et la mer. Le
	// rectangle de la grille reste donc entierement terre ferme : les agents et
	// les ressources ne peuvent pas tomber a l'eau par construction.
	HalfMinUu = FMath::Max(1.0, FMath::Min(WorldSizeX, WorldSizeY) * 0.5);
	ShoreMarginUu = FMath::Max(0.04, static_cast<double>(Settings.ShoreMargin))
		* FMath::Max(WorldSizeX, WorldSizeY);
	LandBandUu = (static_cast<double>(Settings.BeachWidth) + static_cast<double>(Settings.CliffWidth)) * HalfMinUu;
	WobbleUu = FMath::Max(0.0, ShoreMarginUu - LandBandUu)
		* FMath::Clamp(static_cast<double>(Settings.CoastNoiseAmplitude), 0.0, 1.0);

	TerrainMinX = -ShoreMarginUu;
	TerrainMinY = -ShoreMarginUu;
	TerrainSizeX = WorldSizeX + 2.0 * ShoreMarginUu;
	TerrainSizeY = WorldSizeY + 2.0 * ShoreMarginUu;

	// Obstacles : le rayon SYNE projete donne l'etendue de l'influence, la
	// hauteur du relief en decoule (un gros obstacle = une montagne, un petit
	// obstacle = un amer). C'est la seule donnee SYNE qui pilote le relief.
	const double InfluenceScale = FMath::Max(0.1, static_cast<double>(Settings.ObstacleInfluenceScale));
	for (int32 Index = 0; Index < Data->Obstacles.Num(); ++Index)
	{
		if (!Data->ObstacleCentersUnreal.IsValidIndex(Index) || !Data->ObstacleRadiiUnreal.IsValidIndex(Index))
		{
			continue;
		}
		const FVector& Center = Data->ObstacleCentersUnreal[Index];
		const double Radius = Data->ObstacleRadiiUnreal[Index];
		const double Influence = FMath::Max(Radius * InfluenceScale, 1.0);
		ObstacleInfluences.Add(FVector3d(Center.X, Center.Y, Influence));
	}

	// Bruits : graine stable (graine SYNE si fournie, sinon 0). On decale les
	// coordonnees echantillonnees plutot que d'embarquer une table de bruit :
	// le resultat reste deterministe, sans dependance a une version d'engine.
	// Doit preceder les lacs, dont le niveau depend du relief local.
	const int32 Seed = Settings.NoiseSeed != 0 ? Settings.NoiseSeed : static_cast<int32>(Data->Seed);
	const double SeedA = static_cast<double>(Seed % 10007) * 13.37;
	const double SeedB = static_cast<double>(Seed % 9973) * 7.91;

	NoiseOffset = FVector2D(SeedA, SeedB);
	CoastNoiseOffset = FVector2D(SeedA + 7919.0, SeedB - 4231.0);
	WarpOffset = FVector2D(SeedA + 1301.0, SeedB + 1777.0);
	RidgeOffset = FVector2D(SeedA - 2411.0, SeedB + 5501.0);
	DetailOffset = FVector2D(SeedA + 6007.0, SeedB - 8009.0);

	// Points d'influence des ressources. La position SYNE est un index de
	// cellule ; on la projette au centre de la tuile Unreal. Le rayon influence
	// les couleurs, le relief et le scatter.
	if (Data->K > 0.0 && Data->CellSize > 0.0 && CellCountX > 0 && CellCountY > 0)
	{
		for (const FPrismSyneResource& Resource : Data->Resources)
		{
			if (!Resource.bHasPosition)
			{
				continue;
			}

			FIntPoint Cell;
			if (!FMath::IsFinite(Resource.Position.X) || !FMath::IsFinite(Resource.Position.Y))
			{
				continue;
			}

			if (Resource.bPositionIsCellIndex)
			{
				if (Resource.Position.X < 0.0 || Resource.Position.Y < 0.0
					|| Resource.Position.X >= CellCountX || Resource.Position.Y >= CellCountY)
				{
					continue;
				}
				const int32 X = static_cast<int32>(Resource.Position.X);
				const int32 Y = static_cast<int32>(Resource.Position.Y);
				if (static_cast<double>(X) != Resource.Position.X || static_cast<double>(Y) != Resource.Position.Y)
				{
					continue;
				}
				Cell = FIntPoint(X, Y);
			}
			else
			{
				// Les positions continues sont des coordonnées monde SYNE : convertir
				// en cellule avec floor, comme UPrismLdkPCGData. Round décale une
				// position juste après une frontière vers la cellule suivante.
				const double CellX = Resource.Position.X / Data->CellSize;
				const double CellY = Resource.Position.Y / Data->CellSize;
				if (!FMath::IsFinite(CellX) || !FMath::IsFinite(CellY)
					|| CellX < 0.0 || CellY < 0.0 || CellX >= CellCountX || CellY >= CellCountY)
				{
					continue;
				}
				Cell = FIntPoint(FMath::FloorToInt32(CellX), FMath::FloorToInt32(CellY));
			}

			if (Cell.X < 0 || Cell.Y < 0 || Cell.X >= CellCountX || Cell.Y >= CellCountY)
			{
				continue;
			}

			const FVector Center = UPrismLdkProjection::CellToTileCenter(Cell, Data->CellSize, Data->K);
			if (Center.X < 0.0 || Center.Y < 0.0 || Center.X > WorldSizeX || Center.Y > WorldSizeY)
			{
				continue;
			}

			const double QuantityScale = FMath::Sqrt(FMath::Clamp(Resource.Quantity / 100.0, 0.5, 2.0));
			const double Radius = FMath::Max(1.0, static_cast<double>(Settings.ResourceInfluenceRadiusCells) * TileSize)
				* QuantityScale;

			FPrismLdkResourcePoint Point;
			Point.Center = FVector2D(Center.X, Center.Y);
			Point.Type = Resource.TypeEnum;
			Point.Radius = static_cast<float>(Radius);
			Point.Quantity = static_cast<float>(Resource.Quantity);
			ResourcePoints.Add(Point);
		}
	}

	// Lacs : un par ressource Water positionnee. La position est **celle de la
	// grille**, sans reinterpretation : la projection place toute cellule dans
	// `[0, WorldSize]`, et cette zone est entierement terre ferme (voir plus
	// haut). Le rayon suit `TileSizeUu` avec un plancher a 3 % de l'etendue du
	// monde, sinon un petit monde n'aurait que des flaques invisibles. Le niveau
	// est relatif au relief local : une ressource Water sur un plateau doit
	// donner une cuvette sur ce plateau, pas un puits de 9 000 uu.
	const double MinLakeRadius = FMath::Max(WorldSizeX, WorldSizeY) * 0.03;
	const double Jitter = FMath::Clamp(static_cast<double>(Settings.LakeRadiusJitter), 0.0, 0.8);

	for (int32 Index = 0; Index < Data->WaterResourceCentersUnreal.Num(); ++Index)
	{
		const FVector& Center = Data->WaterResourceCentersUnreal[Index];

		// Filet de securite : une position hors grille signalerait une erreur de
		// projection en amont, pas une donnee normale.
		if (Center.X < 0.0 || Center.Y < 0.0 || Center.X > WorldSizeX || Center.Y > WorldSizeY)
		{
			UE_LOG(LogPrismLdk, Warning,
				TEXT("Lac %d ignore : position (%.0f, %.0f) hors de la grille [0..%.0f, 0..%.0f] (projection invalide)."),
				Index, Center.X, Center.Y, WorldSizeX, WorldSizeY);
			continue;
		}

		const double LakeJitter = 1.0 + Jitter * (2.0 * Hash01(Index, 17) - 1.0);
		const double LakeRadius = FMath::Max(
			MinLakeRadius,
			static_cast<double>(Settings.LakeRadiusFraction) * TileSize * LakeJitter);

		const double BaseHeight = ComputeBaseHeight(Center.X, Center.Y);
		Lakes.Add(FVector3d(Center.X, Center.Y, LakeRadius));
		LakeWaterLevels.Add(BaseHeight - static_cast<double>(Settings.LakeLevel));
		UE_LOG(LogPrismLdk, Log,
			TEXT("Lac %d : cellule projetee (%.0f, %.0f), rayon %.0f uu, terrain %.0f uu, surface %.0f uu."),
			Index, Center.X, Center.Y, LakeRadius, BaseHeight, BaseHeight - static_cast<double>(Settings.LakeLevel));
	}

	// Cellules logiques non marchables. PCGData ne publie CellWalkable que si
	// les coordonnées forment une grille complète sans doublon/hors limites.
	// Une simple égalité de nombre d'éléments ne suffit pas : une grille
	// malformée de la bonne taille doit garder le fallback par obstacles.
	// Les dimensions ont déjà été validées sans overflow dans LoadContext.
	const int64 BlockedCellTotal64 = static_cast<int64>(CellCountX) * static_cast<int64>(CellCountY);
	if (BlockedCellTotal64 > 0 && BlockedCellTotal64 <= TNumericLimits<int32>::Max())
	{
		const int32 CellTotal = static_cast<int32>(BlockedCellTotal64);
		CellBlocked.Init(0, CellTotal);

		if (Data->CellWalkable.Num() == CellTotal)
		{
			for (int32 Index = 0; Index < CellTotal; ++Index)
			{
				CellBlocked[Index] = Data->CellWalkable[Index] ? 0 : 1;
			}
		}
		else if (Data->K > 0.0 && Data->CellSize > 0.0)
		{
			for (int32 ObstacleIndex = 0; ObstacleIndex < Data->ObstacleCentersUnreal.Num(); ++ObstacleIndex)
			{
				if (!Data->ObstacleRadiiUnreal.IsValidIndex(ObstacleIndex))
				{
					continue;
				}

				const FVector& Center = Data->ObstacleCentersUnreal[ObstacleIndex];
				const double Radius = Data->ObstacleRadiiUnreal[ObstacleIndex] + TileSize * 0.5;
				const double RadiusSq = Radius * Radius;

				for (int32 Y = 0; Y < CellCountY; ++Y)
				{
					for (int32 X = 0; X < CellCountX; ++X)
					{
						const FVector CellCenter = UPrismLdkProjection::CellToTileCenter(
							FIntPoint(X, Y), Data->CellSize, Data->K);
						const double Dx = CellCenter.X - Center.X;
						const double Dy = CellCenter.Y - Center.Y;
						if (Dx * Dx + Dy * Dy <= RadiusSq)
						{
							CellBlocked[Y * CellCountX + X] = 1;
						}
					}
				}
			}
		}
	}

	UE_LOG(LogPrismLdk, Log,
		TEXT("Contexte virtuel : grille %dx%d, cellule %.0f uu, %d obstacles, %d ressources, %d lacs, %d cellules bloquees."),
		CellCountX, CellCountY, CellSizeUu, ObstacleInfluences.Num(), ResourcePoints.Num(),
		Lakes.Num(), GetBlockedCellCount());
}

double APrismLdkVirtualWorldActor::SampleNoise(double X, double Y) const
{
	// `FMath::PerlinNoise2D` renvoie [-1, 1] et est deterministe.
	const double Value = FMath::PerlinNoise2D(FVector2D(
		static_cast<float>(X + NoiseOffset.X),
		static_cast<float>(Y + NoiseOffset.Y)));
	return FMath::Clamp(static_cast<double>(Value), -1.0, 1.0);
}

double APrismLdkVirtualWorldActor::SampleFbm(double X, double Y) const
{
	// Somme d'octaves : la premiere octave donne la forme large, les suivantes
	// ajoutent le detail. Le gain de 0.5 garde le total dans [-1, 1].
	const int32 Octaves = FMath::Clamp(Settings.NoiseOctaves, 1, 8);
	double Amplitude = 1.0;
	double Frequency = 1.0;
	double Sum = 0.0;
	double Norm = 0.0;

	for (int32 Octave = 0; Octave < Octaves; ++Octave)
	{
		Sum += Amplitude * SampleNoise(X * Frequency, Y * Frequency);
		Norm += Amplitude;
		Amplitude *= 0.5;
		Frequency *= 2.0;
	}

	if (Norm <= 0.0)
	{
		return 0.0;
	}
	return FMath::Clamp(Sum / Norm, -1.0, 1.0);
}

double APrismLdkVirtualWorldActor::SampleRidged(double X, double Y) const
{
	// `1 - |noise|` cree des cretes ; la somme ponderee reste dans [0, 1].
	const int32 Octaves = FMath::Clamp(Settings.NoiseOctaves, 1, 8);
	double Amplitude = 1.0;
	double Frequency = 1.0;
	double Sum = 0.0;
	double Norm = 0.0;

	for (int32 Octave = 0; Octave < Octaves; ++Octave)
	{
		Sum += Amplitude * (1.0 - FMath::Abs(SampleNoise(X * Frequency, Y * Frequency)));
		Norm += Amplitude;
		Amplitude *= 0.5;
		Frequency *= 2.0;
	}

	if (Norm <= 0.0)
	{
		return 0.0;
	}
	return FMath::Clamp(Sum / Norm, 0.0, 1.0);
}

double APrismLdkVirtualWorldActor::GridInlandDistance(double X, double Y) const
{
	// Distance signee (uu) au rectangle de la grille : > 0 dedans, < 0 dehors.
	// C'est la seule reference de forme : la simulation EST ce rectangle.
	const double Dx = FMath::Abs(X - WorldSizeX * 0.5) - WorldSizeX * 0.5;
	const double Dy = FMath::Abs(Y - WorldSizeY * 0.5) - WorldSizeY * 0.5;
	const double OutsideX = FMath::Max(Dx, 0.0);
	const double OutsideY = FMath::Max(Dy, 0.0);
	const double Outside = FMath::Sqrt(OutsideX * OutsideX + OutsideY * OutsideY);
	return -FMath::Min(FMath::Max(Dx, Dy), 0.0) - Outside;
}

double APrismLdkVirtualWorldActor::GridExitDistance(double X, double Y, double DirX, double DirY) const
{
	// Distance jusqu'au bord de la grille dans la direction donnee : sert a
	// rogner les nappes d'eau (un lac ne deborde pas de la zone simulee).
	double Distance = TNumericLimits<double>::Max();
	if (DirX > 1e-6)
	{
		Distance = FMath::Min(Distance, (WorldSizeX - X) / DirX);
	}
	else if (DirX < -1e-6)
	{
		Distance = FMath::Min(Distance, -X / DirX);
	}
	if (DirY > 1e-6)
	{
		Distance = FMath::Min(Distance, (WorldSizeY - Y) / DirY);
	}
	else if (DirY < -1e-6)
	{
		Distance = FMath::Min(Distance, -Y / DirY);
	}
	return FMath::Max(0.0, Distance);
}

double APrismLdkVirtualWorldActor::CoastDistanceAt(double X, double Y) const
{
	// Le trait de cote est **hors** de la grille : il se trouve a
	// `LandBandUu` (plage + falaise) du bord, plus un mamelonnement qui ne se
	// deploie que vers le large. Une cellule de la grille (Inland >= 0) a donc
	// toujours une distance >= LandBandUu / HalfMinUu : elle est sur le haut de
	// la falaise, jamais sur la plage.
	const double Inland = GridInlandDistance(X, Y);

	const double WobbleA = 0.5 * (1.0 + SampleNoise(
		X * Settings.CoastNoiseScale + CoastNoiseOffset.X,
		Y * Settings.CoastNoiseScale + CoastNoiseOffset.Y));
	const double WobbleB = 0.5 * (1.0 + SampleNoise(
		X * Settings.CoastWarpScale + CoastNoiseOffset.X + 3313.0,
		Y * Settings.CoastWarpScale + CoastNoiseOffset.Y - 1913.0));

	const double Weight = FMath::Clamp(static_cast<double>(Settings.CoastWarpWeight), 0.0, 1.0);
	const double Wobble = FMath::Clamp(WobbleA * (1.0 - Weight) + WobbleB * Weight, 0.0, 1.0);
	const double Waterline = LandBandUu + Wobble * WobbleUu;

	return (Inland + Waterline) / HalfMinUu;
}

double APrismLdkVirtualWorldActor::GetResourceMask(double X, double Y, EPrismSyneResourceType Type) const
{
	double Mask = 0.0;
	for (const FPrismLdkResourcePoint& Point : ResourcePoints)
	{
		if (Point.Type != Type || Point.Radius <= 0.0f)
		{
			continue;
		}

		const double Dx = X - Point.Center.X;
		const double Dy = Y - Point.Center.Y;
		const double T = FMath::Sqrt(Dx * Dx + Dy * Dy) / static_cast<double>(Point.Radius);
		if (T < 1.0)
		{
			Mask = FMath::Max(Mask, 1.0 - SmoothEdge(T));
		}
	}
	return Mask;
}

double APrismLdkVirtualWorldActor::ComputeBaseHeight(double X, double Y) const
{
	// --- 1-2. distance a la cote (blob + bruit) ---
	const double CoastDistance = CoastDistanceAt(X, Y);

	// --- 3. profil altimetrique en fonction de la distance a la cote ---
	const double BeachWidth = FMath::Max(0.0, static_cast<double>(Settings.BeachWidth));
	const double CliffWidth = FMath::Max(0.001, static_cast<double>(Settings.CliffWidth));
	const double SeaWidth = FMath::Max(0.001, static_cast<double>(Settings.SeaWidth));

	double Height = 0.0;
	if (CoastDistance <= 0.0)
	{
		// Mer : on descend sous zero vers le large.
		const double SeaT = SmoothEdge(-CoastDistance / SeaWidth);
		Height = -static_cast<double>(Settings.SeaDepth) * SeaT;
	}
	else if (CoastDistance < BeachWidth)
	{
		// Plage : risee douce depuis le niveau de la mer.
		Height = static_cast<double>(Settings.BeachHeight) * (CoastDistance / BeachWidth);
	}
	else
	{
		// Falaise : la ligne ondule legerement, pour eviter un mur parfait.
		const double CliffNoise = static_cast<double>(Settings.CliffNoiseAmplitude)
			* SampleNoise(
				X * Settings.CoastNoiseScale * 2.7 + CoastNoiseOffset.X + 1301.0,
				Y * Settings.CoastNoiseScale * 2.7 + CoastNoiseOffset.Y - 913.0)
			* CliffWidth;
		const double CliffT = SmoothEdge((CoastDistance - BeachWidth - CliffNoise) / CliffWidth);
		const double Cliff = FMath::Lerp(static_cast<double>(Settings.BeachHeight), static_cast<double>(Settings.CliffHeight), CliffT);

		// Plateau : le reste de l'ile monte lentement vers `IslandHeight`.
		const double Remaining = FMath::Max(0.001, 1.0 - BeachWidth - CliffWidth);
		const double CliffEnd = BeachWidth + CliffWidth + CliffNoise;
		const double PlateauT = SmoothEdge((CoastDistance - CliffEnd) / Remaining);
		Height = FMath::Lerp(Cliff, static_cast<double>(Settings.IslandHeight), PlateauT);
	}

	// --- 4. domain-warp + bruit organique + cretes ---
	const double WarpX = static_cast<double>(Settings.NoiseWarpAmplitude) * SampleNoise(
		X * Settings.NoiseWarpScale + WarpOffset.X,
		Y * Settings.NoiseWarpScale + WarpOffset.Y);
	const double WarpY = static_cast<double>(Settings.NoiseWarpAmplitude) * SampleNoise(
		X * Settings.NoiseWarpScale + WarpOffset.X + 5171.0,
		Y * Settings.NoiseWarpScale + WarpOffset.Y - 2903.0);

	const double NX = X + WarpX;
	const double NY = Y + WarpY;

	const double HighlandT = SmoothEdge(
		(CoastDistance - (BeachWidth + CliffWidth)) / FMath::Max(0.001, 1.0 - BeachWidth - CliffWidth));

	Height += SampleFbm(NX * Settings.NoiseScale, NY * Settings.NoiseScale)
		* static_cast<double>(Settings.NoiseAmplitude);

	if (Settings.bUseRidgedMountains)
	{
		const double Ridge = SampleRidged(
			NX * Settings.RidgeNoiseScale + RidgeOffset.X,
			NY * Settings.RidgeNoiseScale + RidgeOffset.Y);
		Height += (Ridge - 0.5) * 2.0 * static_cast<double>(Settings.RidgeNoiseAmplitude) * HighlandT;
	}

	Height += SampleFbm(
		NX * Settings.TerrainDetailScale + DetailOffset.X,
		NY * Settings.TerrainDetailScale + DetailOffset.Y)
		* static_cast<double>(Settings.TerrainDetailAmplitude)
		* (0.35 + 0.65 * HighlandT);

	// --- 5. obstacles SYNE = montagnes / cretes / amers ---
	// Le profil est **additif** : base locale + plateau + falaise.
	// `bObstaclesCreateLand` ne contrôle que la création de terre hors grille ;
	// les obstacles restent des reliefs dans la zone simulée dans les deux cas.
	const bool bCanCreateOffGridLand = Settings.bObstaclesCreateLand || GridInlandDistance(X, Y) >= 0.0;
	if (bCanCreateOffGridLand)
	{
		const double CliffStart = FMath::Clamp(static_cast<double>(Settings.ObstacleCliffStart), 0.0, 0.95);
		const double CliffEnd = FMath::Max(CliffStart + 0.01, static_cast<double>(Settings.ObstacleCliffEnd));

		for (const FVector3d& Obstacle : ObstacleInfluences)
		{
			const double Dx = X - Obstacle.X;
			const double Dy = Y - Obstacle.Y;
			const double Distance = FMath::Sqrt(Dx * Dx + Dy * Dy);

			const double Outline = 1.0 + static_cast<double>(Settings.ObstacleOutlineNoise)
				* SampleNoise(
					X * Settings.NoiseScale * 3.1 + NoiseOffset.X + 811.0,
					Y * Settings.NoiseScale * 3.1 + NoiseOffset.Y - 1721.0);
			const double T = Distance / FMath::Max(1.0, Obstacle.Z * FMath::Max(0.1, Outline));
			if (T >= 1.0)
			{
				continue;
			}

			const double Profile = 1.0 - SmoothEdge((T - CliffStart) / (CliffEnd - CliffStart));
			const double Peak = Obstacle.Z * static_cast<double>(Settings.ObstacleHeightFactor);
			Height += Peak * Profile;
		}
	}

	// --- 6. influence des ressources sur le relief (subtile) ---
	const double ResourceStrength = static_cast<double>(Settings.ResourceHeightInfluence);
	if (ResourceStrength > 0.0)
	{
		const double Food = GetResourceMask(X, Y, EPrismSyneResourceType::Food);
		const double Wood = GetResourceMask(X, Y, EPrismSyneResourceType::Wood);
		const double Mineral = GetResourceMask(X, Y, EPrismSyneResourceType::Mineral);
		Height += ResourceStrength * (0.15 * Food + 0.35 * Wood + 0.8 * Mineral);
	}

	return Height;
}

double APrismLdkVirtualWorldActor::ComputeHeight(double X, double Y) const
{
	double Height = ComputeBaseHeight(X, Y);

	// --- 7. lacs et etangs (ressources Water) ---
	// Cuvette locale : le fond descend sous la surface de l'eau au centre et le
	// terrain rejoint le relief existant sur la berge. Sans cette separation,
	// un lac sur un plateau de 9 000 uu devient un puits vertical.
	for (int32 Index = 0; Index < Lakes.Num(); ++Index)
	{
		const FVector3d& Lake = Lakes[Index];
		const double Dx = X - Lake.X;
		const double Dy = Y - Lake.Y;
		const double T = FMath::Sqrt(Dx * Dx + Dy * Dy) / Lake.Z;
		if (T >= 1.0)
		{
			continue;
		}

		const double WaterLevel = LakeWaterLevels[Index];
		const double Base = WaterLevel + static_cast<double>(Settings.LakeLevel);
		const double Bottom = WaterLevel - static_cast<double>(Settings.LakeDepth);

		// Profil en cuvette : fond au centre, niveau du terrain local au bord.
		const double Profile = SmoothEdge(T);
		const double Bowl = Bottom + (Base - Bottom) * Profile;

		// La berge est la bande exterieure ou la cuvette rejoint le terrain.
		const double ShoreFraction = FMath::Clamp(static_cast<double>(Settings.LakeShoreFraction), 0.05, 0.9);
		const double ShoreT = FMath::Clamp((T - (1.0 - ShoreFraction)) / ShoreFraction, 0.0, 1.0);
		const double Mask = 1.0 - SmoothEdge(ShoreT);

		// Le terrain de la berge est remonte au-dessus de la surface s'il est
		// plus bas (pente) : sans ce seuil, l'eau deborde du disque et laisse un
		// anneau sec sous le niveau du lac.
		const double Bank = WaterLevel + FMath::Max(1.0, static_cast<double>(Settings.LakeLevel) * 0.5);
		const double Rim = FMath::Max(Height, Bank);
		const double Local = FMath::Lerp(Rim, Bowl, Mask);

		// La cuvette reste dans la grille : hors de la zone simulee le relief
		// d'origine (falaise, plage) reprend la main.
		const double OutsideUu = FMath::Max(0.0, -GridInlandDistance(X, Y));
		const double EdgeFade = 1.0 - SmoothEdge(OutsideUu / FMath::Max(1.0, Lake.Z * 0.5));

		Height = FMath::Lerp(Height, Local, EdgeFade);
	}

	return Height;
}

float APrismLdkVirtualWorldActor::SampleWaterLevelAt(double X, double Y) const
{
	// Hors lac, le niveau de l'eau est celui de la mer.
	float WaterLevel = 0.0f;
	double BestDistance = 1.0;
	for (int32 Index = 0; Index < Lakes.Num() && LakeWaterLevels.IsValidIndex(Index); ++Index)
	{
		const FVector3d& Lake = Lakes[Index];
		const double Dx = X - Lake.X;
		const double Dy = Y - Lake.Y;
		const double T = FMath::Sqrt(Dx * Dx + Dy * Dy) / Lake.Z;
		if (T < BestDistance)
		{
			BestDistance = T;
			WaterLevel = static_cast<float>(LakeWaterLevels[Index]);
		}
	}
	return WaterLevel;
}

float APrismLdkVirtualWorldActor::SampleHeightAt(double X, double Y) const
{
	return static_cast<float>(ComputeHeight(X, Y));
}

float APrismLdkVirtualWorldActor::GetGroundHeightAt(double X, double Y) const
{
	return SampleHeightAt(X, Y);
}

FVector APrismLdkVirtualWorldActor::SampleNormalAt(double X, double Y) const
{
	// Differences centrees : la normale suit le relief, sans reconstruire les
	// triangles.
	const double Epsilon = FMath::Max(1.0, WorldSizeX / 512.0);
	const double HLeft = ComputeHeight(X - Epsilon, Y);
	const double HRight = ComputeHeight(X + Epsilon, Y);
	const double HDown = ComputeHeight(X, Y - Epsilon);
	const double HUp = ComputeHeight(X, Y + Epsilon);

	const FVector Normal(
		static_cast<float>(HLeft - HRight),
		static_cast<float>(HDown - HUp),
		static_cast<float>(2.0 * Epsilon));
	return Normal.GetSafeNormal();
}

int32 APrismLdkVirtualWorldActor::GetBlockedCellCount() const
{
	int32 Count = 0;
	for (const uint8 Blocked : CellBlocked)
	{
		if (Blocked)
		{
			++Count;
		}
	}
	return Count;
}

int32 APrismLdkVirtualWorldActor::GetTreeInstanceCount() const
{
	return TreeInstances ? TreeInstances->GetInstanceCount() : 0;
}

int32 APrismLdkVirtualWorldActor::GetRockInstanceCount() const
{
	return RockInstances ? RockInstances->GetInstanceCount() : 0;
}

int32 APrismLdkVirtualWorldActor::GetCropInstanceCount() const
{
	return CropInstances ? CropInstances->GetInstanceCount() : 0;
}

int32 APrismLdkVirtualWorldActor::GetReedInstanceCount() const
{
	return ReedInstances ? ReedInstances->GetInstanceCount() : 0;
}

bool APrismLdkVirtualWorldActor::IsCellBlocked(int32 X, int32 Y) const
{
	if (X < 0 || Y < 0 || X >= CellCountX || Y >= CellCountY)
	{
		return true;
	}
	const int32 Index = Y * CellCountX + X;
	return CellBlocked.IsValidIndex(Index) && CellBlocked[Index] != 0;
}

void APrismLdkVirtualWorldActor::ResetTerrainChunks()
{
	if (!TerrainMesh)
	{
		TerrainChunks.Reset();
		return;
	}

	for (int32 Index = 1; Index < TerrainChunks.Num(); ++Index)
	{
		if (TerrainChunks[Index])
		{
			TerrainChunks[Index]->DestroyComponent();
		}
	}

	TerrainChunks.Reset();
	TerrainMesh->ClearAllMeshSections();
	TerrainMesh->SetVisibility(true);
	TerrainChunks.Add(TerrainMesh);
}

UProceduralMeshComponent* APrismLdkVirtualWorldActor::CreateTerrainChunk()
{
	if (!TerrainMesh)
	{
		return nullptr;
	}

	UProceduralMeshComponent* Chunk = NewObject<UProceduralMeshComponent>(this);
	Chunk->SetupAttachment(TerrainMesh);
	Chunk->SetCollisionProfileName(TEXT("BlockAll"));
	Chunk->bUseAsyncCooking = false;
	Chunk->SetMobility(EComponentMobility::Movable);
	AddInstanceComponent(Chunk);
	return Chunk;
}

bool APrismLdkVirtualWorldActor::BuildWorld(UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& InSettings)
{
	if (AsyncBuildStage != EAsyncBuildStage::None)
	{
		return false;
	}
	bBuilt = false;
	LoadContext(Data, InSettings);

	if (!Data || WorldSizeX <= 1.0 || WorldSizeY <= 1.0 || CellCountX <= 0 || CellCountY <= 0)
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("Monde virtuel : donnees invalides (Data=%s) - rien ne sera genere."),
			Data ? TEXT("ok") : TEXT("null"));
		CleanupNavBlockers();
		ResetTerrainChunks();
		if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
		if (TreeInstances) { TreeInstances->ClearInstances(); }
		if (RockInstances) { RockInstances->ClearInstances(); }
		if (CropInstances) { CropInstances->ClearInstances(); }
		if (ReedInstances) { ReedInstances->ClearInstances(); }
		bBuilt = false;
		return false;
	}

	bBuilt = false;

	CleanupNavBlockers();
	ResetTerrainChunks();
	if (WaterMesh)
	{
		WaterMesh->ClearAllMeshSections();
	}
	if (TreeInstances) { TreeInstances->ClearInstances(); }
	if (RockInstances) { RockInstances->ClearInstances(); }
	if (CropInstances) { CropInstances->ClearInstances(); }
	if (ReedInstances) { ReedInstances->ClearInstances(); }

	if (!BuildTerrain(Settings))
	{
		UE_LOG(LogPrismLdk, Error, TEXT("Monde virtuel : le maillage de terrain n'a pas pu être construit."));
		CleanupNavBlockers();
		ResetTerrainChunks();
		if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
		if (TreeInstances) { TreeInstances->ClearInstances(); }
		if (RockInstances) { RockInstances->ClearInstances(); }
		if (CropInstances) { CropInstances->ClearInstances(); }
		if (ReedInstances) { ReedInstances->ClearInstances(); }
		bBuilt = false;
		return false;
	}

	BuildWaterMesh(Settings);
	BuildResourceScatter(Settings);
	if (!BuildNavBlockers(Settings))
	{
		UE_LOG(LogPrismLdk, Error, TEXT("Monde virtuel : échec de création d'un blocker de navigation."));
		CleanupNavBlockers();
		ResetTerrainChunks();
		if (WaterMesh) { WaterMesh->ClearAllMeshSections(); }
		if (TreeInstances) { TreeInstances->ClearInstances(); }
		if (RockInstances) { RockInstances->ClearInstances(); }
		if (CropInstances) { CropInstances->ClearInstances(); }
		if (ReedInstances) { ReedInstances->ClearInstances(); }
		bBuilt = false;
		return false;
	}


	if (Settings.bRebuildNavigation)
	{
		RebuildNavigation();
	}

	bBuilt = true;

	UE_LOG(LogPrismLdk, Log,
		TEXT("Monde virtuel genere : resolution %d, %d chunks, %d obstacles, %d ressources, %d lacs, %d blockers nav, grille %.0f x %.0f uu, terrain %.0f x %.0f uu."),
		FMath::Clamp(Settings.GridResolution, 16, 512), GetTerrainChunkCount(),
		ObstacleInfluences.Num(), ResourcePoints.Num(), Lakes.Num(), GetNavBlockerCount(),
		WorldSizeX, WorldSizeY, TerrainSizeX, TerrainSizeY);

	return true;
}

bool APrismLdkVirtualWorldActor::BuildTerrain(const FPrismLdkVirtualWorldSettings& InSettings)
{
	if (!TerrainMesh)
	{
		return false;
	}

	const int32 Resolution = FMath::Clamp(InSettings.GridResolution, 16, 512);
	const int32 VertexCount = Resolution + 1;
	const int32 VertexTotal = VertexCount * VertexCount;

	const double Dx = FMath::Max(1.0, TerrainSizeX / static_cast<double>(Resolution));
	const double Dy = FMath::Max(1.0, TerrainSizeY / static_cast<double>(Resolution));

	TArray<double> HeightGrid;
	HeightGrid.SetNumUninitialized(VertexTotal);

	for (int32 YIndex = 0; YIndex < VertexCount; ++YIndex)
	{
		const double Y = TerrainMinY + (static_cast<double>(YIndex) / static_cast<double>(Resolution)) * TerrainSizeY;
		for (int32 XIndex = 0; XIndex < VertexCount; ++XIndex)
		{
			const double X = TerrainMinX + (static_cast<double>(XIndex) / static_cast<double>(Resolution)) * TerrainSizeX;
			HeightGrid[YIndex * VertexCount + XIndex] = ComputeHeight(X, Y);
		}
	}

	// Normales a partir de la grille globale : les coutures entre chunks sont
	// donc exactes (memes hauteurs, memes normales sur les aretes partagees).
	TArray<FVector> NormalGrid;
	NormalGrid.SetNumUninitialized(VertexTotal);

	for (int32 YIndex = 0; YIndex < VertexCount; ++YIndex)
	{
		const int32 YPrev = FMath::Max(0, YIndex - 1);
		const int32 YNext = FMath::Min(Resolution, YIndex + 1);
		const double DeltaY = FMath::Max(1.0, static_cast<double>(YNext - YPrev) * Dy);

		for (int32 XIndex = 0; XIndex < VertexCount; ++XIndex)
		{
			const int32 XPrev = FMath::Max(0, XIndex - 1);
			const int32 XNext = FMath::Min(Resolution, XIndex + 1);
			const double DeltaX = FMath::Max(1.0, static_cast<double>(XNext - XPrev) * Dx);

			const double HLeft = HeightGrid[YIndex * VertexCount + XPrev];
			const double HRight = HeightGrid[YIndex * VertexCount + XNext];
			const double HDown = HeightGrid[YPrev * VertexCount + XIndex];
			const double HUp = HeightGrid[YNext * VertexCount + XIndex];

			const FVector Normal(
				static_cast<float>(-(HRight - HLeft) / DeltaX),
				static_cast<float>(-(HUp - HDown) / DeltaY),
				1.0f);
			NormalGrid[YIndex * VertexCount + XIndex] = Normal.GetSafeNormal();
		}
	}

	const int32 RequestedChunk = FMath::Clamp(InSettings.TerrainChunkResolution, 0, Resolution);
	const int32 ChunkRes = RequestedChunk > 0 ? RequestedChunk : Resolution;
	const int32 ChunksX = (Resolution + ChunkRes - 1) / ChunkRes;
	const int32 ChunksY = (Resolution + ChunkRes - 1) / ChunkRes;

	TArray<FVector2D> EmptyUV;
	int32 ChunkIndex = 0;

	for (int32 ChunkY = 0; ChunkY < ChunksY; ++ChunkY)
	{
		const int32 StartY = ChunkY * ChunkRes;
		const int32 EndY = FMath::Min(StartY + ChunkRes, Resolution);
		const int32 LocalResY = EndY - StartY;

		for (int32 ChunkX = 0; ChunkX < ChunksX; ++ChunkX)
		{
			const int32 StartX = ChunkX * ChunkRes;
			const int32 EndX = FMath::Min(StartX + ChunkRes, Resolution);
			const int32 LocalResX = EndX - StartX;
			const int32 LocalVertexCount = LocalResX + 1;
			const int32 LocalVertexTotal = LocalVertexCount * (LocalResY + 1);

			TArray<FVector> Vertices;
			TArray<FVector> Normals;
			TArray<FVector2D> UV0;
			TArray<FVector2D> UV1;
			TArray<FLinearColor> Colors;
			TArray<FProcMeshTangent> Tangents;
			TArray<int32> Triangles;

			Vertices.Reserve(LocalVertexTotal);
			Normals.Reserve(LocalVertexTotal);
			UV0.Reserve(LocalVertexTotal);
			UV1.Reserve(LocalVertexTotal);
			Colors.Reserve(LocalVertexTotal);
			Tangents.Reserve(LocalVertexTotal);

			for (int32 LocalY = 0; LocalY <= LocalResY; ++LocalY)
			{
				const int32 GlobalY = StartY + LocalY;
				const double Y = TerrainMinY + (static_cast<double>(GlobalY) / static_cast<double>(Resolution)) * TerrainSizeY;

				for (int32 LocalX = 0; LocalX <= LocalResX; ++LocalX)
				{
					const int32 GlobalX = StartX + LocalX;
					const double X = TerrainMinX + (static_cast<double>(GlobalX) / static_cast<double>(Resolution)) * TerrainSizeX;
					const int32 GridIndex = GlobalY * VertexCount + GlobalX;
					const double Z = HeightGrid[GridIndex];
					const FVector Normal = NormalGrid[GridIndex];

					Vertices.Add(FVector(X, Y, Z));
					Normals.Add(Normal);
					UV0.Add(FVector2D(X / Settings.TerrainUvScale, Y / Settings.TerrainUvScale));
					UV1.Add(FVector2D(
						static_cast<double>(GlobalX) / static_cast<double>(Resolution),
						static_cast<double>(GlobalY) / static_cast<double>(Resolution)));

					const double Food = GetResourceMask(X, Y, EPrismSyneResourceType::Food);
					const double Wood = GetResourceMask(X, Y, EPrismSyneResourceType::Wood);
					const double Mineral = GetResourceMask(X, Y, EPrismSyneResourceType::Mineral);

					const double Slope = FMath::Clamp((1.0 - Normal.Z) * 2.0, 0.0, 1.0);
					const double Height01 = FMath::Clamp(
						(Z - Settings.BeachHeight) / FMath::Max(1.0, static_cast<double>(Settings.IslandHeight) - Settings.BeachHeight),
						0.0, 1.0);
					const double Moisture = FMath::Clamp(
						0.45
						+ 0.25 * SampleFbm(
							X * Settings.NoiseScale * 1.7 + DetailOffset.X,
							Y * Settings.NoiseScale * 1.7 + DetailOffset.Y)
						+ 0.45 * Wood
						+ 0.30 * Food
						- 0.20 * Mineral,
						0.0, 1.0);
					const double Rock = FMath::Clamp(0.8 * Slope + 0.15 * Height01 + 0.55 * Mineral, 0.0, 1.0);

					// R = altitude, G = pente, B = humidite/fertilite, A = roche/minerai.
					Colors.Add(FLinearColor(
						static_cast<float>(Height01),
						static_cast<float>(Slope),
						static_cast<float>(Moisture),
						static_cast<float>(Rock)));

					FVector TangentX(1.0f, 0.0f, 0.0f);
					TangentX = (TangentX - Normal * FVector::DotProduct(TangentX, Normal)).GetSafeNormal();
					if (TangentX.IsNearlyZero())
					{
						TangentX = FVector(0.0f, 1.0f, 0.0f);
					}
					Tangents.Add(FProcMeshTangent(TangentX, false));
				}
			}

			Triangles.Reserve(LocalResX * LocalResY * 6);
			for (int32 LocalY = 0; LocalY < LocalResY; ++LocalY)
			{
				for (int32 LocalX = 0; LocalX < LocalResX; ++LocalX)
				{
					const int32 I0 = LocalY * LocalVertexCount + LocalX;
					const int32 I1 = I0 + 1;
					const int32 I2 = I0 + LocalVertexCount;
					const int32 I3 = I2 + 1;

					Triangles.Add(I0); Triangles.Add(I2); Triangles.Add(I1);
					Triangles.Add(I1); Triangles.Add(I2); Triangles.Add(I3);
				}
			}

			UProceduralMeshComponent* Chunk = nullptr;
			if (ChunkIndex == 0)
			{
				Chunk = TerrainMesh;
			}
			else
			{
				Chunk = CreateTerrainChunk();
				if (Chunk)
				{
					TerrainChunks.Add(Chunk);
				}
			}

		if (!Chunk)
		{
			UE_LOG(LogPrismLdk, Error, TEXT("Impossible d'allouer le chunk terrain %d."), ChunkIndex);
			return false;
		}

		// Les sections doivent exister avant l'enregistrement : l'octree de navigation
		// ignore un composant enregistré avec des bounds vides.
		if (Chunk->IsRegistered())
		{
			Chunk->UnregisterComponent();
		}
		Chunk->CreateMeshSection_LinearColor(
			0, Vertices, Triangles, Normals, UV0, UV1, EmptyUV, EmptyUV, Colors, Tangents,
			Settings.bCreateCollision, false);
		if (!Chunk->IsRegistered())
		{
			Chunk->RegisterComponent();
		}
		if (!Chunk->GetProcMeshSection(0))
		{
			UE_LOG(LogPrismLdk, Error, TEXT("Échec de création du chunk terrain %d."), ChunkIndex);
			return false;
		}

		if (TerrainMaterialOverride)
		{
			Chunk->SetMaterial(0, TerrainMaterialOverride);
		}

		++ChunkIndex;
		}
	}

	UE_LOG(LogPrismLdk, Log,
		TEXT("Terrain : %d sommets, %d chunks (%dx%d), coutures exactes."),
		VertexTotal, GetTerrainChunkCount(), ChunksX, ChunksY);
	return ChunkIndex == ChunksX * ChunksY && TerrainChunks.Num() == ChunksX * ChunksY;
}

void APrismLdkVirtualWorldActor::BuildWaterMesh(const FPrismLdkVirtualWorldSettings& InSettings)
{
	if (!WaterMesh)
	{
		return;
	}

	TArray<FVector> Vertices;
	TArray<int32> Triangles;
	TArray<FVector> Normals;
	TArray<FVector2D> UV0;
	TArray<FLinearColor> Colors;
	TArray<FProcMeshTangent> Tangents;

	auto AddDisc = [this, &Vertices, &Triangles, &Normals, &UV0, &Colors, &Tangents](
		double CenterX, double CenterY, double Radius, double Z, int32 Segments,
		double Irregularity, bool bClipToGrid, bool bInward, bool bIsLake, double DepthScale)
	{
		const int32 BaseIndex = Vertices.Num();

		auto ColorAt = [this, Z, DepthScale, bIsLake](double PX, double PY)
		{
			const double Terrain = ComputeHeight(PX, PY);
			const double Depth = FMath::Clamp(
				(Z - Terrain) / FMath::Max(1.0, DepthScale), 0.0, 1.0);
			// R = profondeur (mousse/transparence), G = lac (1) / mer (0).
			return FLinearColor(
				static_cast<float>(Depth),
				bIsLake ? 1.0f : 0.0f,
				0.0f,
				1.0f);
		};

		// Centre
		Vertices.Add(FVector(CenterX, CenterY, Z));
		Normals.Add(FVector::UpVector);
		UV0.Add(FVector2D(0.5f, 0.5f));
		Colors.Add(ColorAt(CenterX, CenterY));
		Tangents.Add(FProcMeshTangent(FVector(1.0f, 0.0f, 0.0f), false));

		for (int32 Index = 0; Index <= Segments; ++Index)
		{
			const double Angle = (2.0 * PI * static_cast<double>(Index)) / static_cast<double>(Segments);
			// Rayon irregulier : le disque n'est pas un cercle parfait, ce qui
			// donne un trac stylise plutot qu'un lac geometrique.
			const double NoiseValue = SampleNoise(
				static_cast<float>(FMath::Cos(Angle)) * Radius * 0.01 + CenterX * 0.0001 + CoastNoiseOffset.X,
				static_cast<float>(FMath::Sin(Angle)) * Radius * 0.01 + CenterY * 0.0001 + CoastNoiseOffset.Y);

			double R = Radius;
			if (bInward)
			{
				// Le disque reste dans la cuvette : jamais d'eau flottante
				// au-dessus de la berge.
				R *= 1.0 - Irregularity * (0.5 + 0.5 * NoiseValue);
			}
			else
			{
				R *= 1.0 + Irregularity * NoiseValue;
			}

			// Une nappe d'eau ne deborde jamais de la grille : au bord de la zone
			// simulee, elle s'arrete net au lieu de flotter au-dessus de la falaise.
			if (bClipToGrid)
			{
				R = FMath::Min(R, GridExitDistance(CenterX, CenterY, FMath::Cos(Angle), FMath::Sin(Angle)));
			}

			const double PX = CenterX + FMath::Cos(Angle) * R;
			const double PY = CenterY + FMath::Sin(Angle) * R;

			Vertices.Add(FVector(PX, PY, Z));
			Normals.Add(FVector::UpVector);
			UV0.Add(FVector2D(
				static_cast<float>(0.5 + 0.5 * FMath::Cos(Angle)),
				static_cast<float>(0.5 + 0.5 * FMath::Sin(Angle))));
			Colors.Add(ColorAt(PX, PY));
			Tangents.Add(FProcMeshTangent(FVector(1.0f, 0.0f, 0.0f), false));
		}

		for (int32 Index = 0; Index < Segments; ++Index)
		{
			// Ordre horaire vu du dessus : c'est le sens "face avant" d'Unreal.
			// L'ordre inverse rendait la surface invisible sans materiau Two Sided.
			Triangles.Add(BaseIndex);
			Triangles.Add(BaseIndex + 2 + Index);
			Triangles.Add(BaseIndex + 1 + Index);
		}
	};

	// Mer : un disque large qui deborde du terrain.
	if (InSettings.bGenerateSea)
	{
		const double SeaRadius = FMath::Max(WorldSizeX, WorldSizeY) * 1.6;
		AddDisc(WorldSizeX * 0.5, WorldSizeY * 0.5, SeaRadius, 0.0, 64, 0.02,
			false, false, false, Settings.SeaDepth);
	}

	// Lacs : un disque par ressource Water, au niveau d'eau calcule au chargement.
	// Rayon 1.0 x l'influence : la surface couvre la cuvette entiere. Le disque
	// est legerement rentre (irregularite inward) pour ne jamais flotter au-dessus
	// de la berge ; l'ancien rayon 0.68 laissait un anneau sec sous la surface.
	for (int32 Index = 0; Index < Lakes.Num() && LakeWaterLevels.IsValidIndex(Index); ++Index)
	{
		const FVector3d& Lake = Lakes[Index];
		AddDisc(Lake.X, Lake.Y, Lake.Z, LakeWaterLevels[Index], 48, 0.08,
			true, true, true, Settings.LakeDepth);
	}

	WaterMesh->ClearAllMeshSections();
	if (Vertices.Num() > 0)
	{
		TArray<FVector2D> EmptyUV;
		WaterMesh->CreateMeshSection_LinearColor(
			0, Vertices, Triangles, Normals, UV0, EmptyUV, EmptyUV, EmptyUV, Colors, Tangents,
			false, false);
	}

	if (WaterMaterialOverride)
	{
		WaterMesh->SetMaterial(0, WaterMaterialOverride);
	}
}

void APrismLdkVirtualWorldActor::BuildResourceScatter(const FPrismLdkVirtualWorldSettings& InSettings)
{
	const FPrismLdkVegetationSettings& Veg = InSettings.Vegetation;

	auto ClearHISM = [](UHierarchicalInstancedStaticMeshComponent* Component, UStaticMesh* Mesh)
	{
		if (!Component)
		{
			return;
		}
		Component->ClearInstances();
		Component->SetStaticMesh(Mesh);
		Component->SetVisibility(Mesh != nullptr);
	};

	ClearHISM(TreeInstances, Veg.TreeMesh);
	ClearHISM(RockInstances, Veg.RockMesh);
	ClearHISM(CropInstances, Veg.CropMesh);
	ClearHISM(ReedInstances, Veg.ReedMesh);

	if (!InSettings.bGenerateResourceScatter || !Veg.bEnabled || CellCountX <= 0 || CellCountY <= 0)
	{
		return;
	}

	auto Place = [this, &Veg](
		UHierarchicalInstancedStaticMeshComponent* Component,
		UStaticMesh* Mesh,
		int32 Count,
		EPrismSyneResourceType Type,
		bool bWater,
		double MinSlope)
	{
		if (!Component || !Mesh || Count <= 0)
		{
			return;
		}

		TArray<const FPrismLdkResourcePoint*> Points;
		for (const FPrismLdkResourcePoint& Point : ResourcePoints)
		{
			if (Point.Type == Type && Point.Radius > 0.0f)
			{
				Points.Add(&Point);
			}
		}

		FRandomStream Stream(Veg.Seed + static_cast<int32>(Type) * 1013 + 17);
		const double Cell = FMath::Max(1.0, CellSizeUu);
		const double MinScale = FMath::Max(0.01f, FMath::Min(Veg.MinScale, Veg.MaxScale));
		const double MaxScale = FMath::Max(MinScale, FMath::Max(Veg.MinScale, Veg.MaxScale));

		int32 Placed = 0;
		int32 Attempts = 0;
		const int32 MaxAttempts = Count * 8;

		while (Placed < Count && Attempts < MaxAttempts)
		{
			++Attempts;

			FVector2D Pos;
			if (Points.Num() > 0 && Stream.FRand() < 0.9f)
			{
				const FPrismLdkResourcePoint* Point = Points[Stream.RandRange(0, Points.Num() - 1)];
				const double Angle = Stream.FRandRange(0.0, 2.0 * PI);
				const double Radius = static_cast<double>(Point->Radius)
					* FMath::Sqrt(Stream.FRand()) * 1.15;
				Pos = FVector2D(
					Point->Center.X + FMath::Cos(Angle) * Radius,
					Point->Center.Y + FMath::Sin(Angle) * Radius);
			}
			else
			{
				Pos = FVector2D(
					Stream.FRandRange(0.0, WorldSizeX),
					Stream.FRandRange(0.0, WorldSizeY));
			}

			if (Pos.X < 0.0 || Pos.Y < 0.0 || Pos.X > WorldSizeX || Pos.Y > WorldSizeY)
			{
				continue;
			}

			const int32 CellX = FMath::Clamp(FMath::FloorToInt(Pos.X / Cell), 0, CellCountX - 1);
			const int32 CellY = FMath::Clamp(FMath::FloorToInt(Pos.Y / Cell), 0, CellCountY - 1);
			if (IsCellBlocked(CellX, CellY))
			{
				continue;
			}

			const double Z = ComputeHeight(Pos.X, Pos.Y);
			const double Water = SampleWaterLevelAt(Pos.X, Pos.Y);
			double PlaceZ = Z;

			if (bWater)
			{
				// Roseaux : uniquement sur une rive de lac, pas au fond.
				if (Water <= 0.0 || Z > Water + 200.0)
				{
					continue;
				}
				PlaceZ = Water - 20.0;
			}
			else if (Z <= Water + 20.0)
			{
				continue;
			}

			const FVector Normal = SampleNormalAt(Pos.X, Pos.Y);
			if (Normal.Z < MinSlope)
			{
				continue;
			}

			const double Scale = Stream.FRandRange(MinScale, MaxScale);
			const double Yaw = Stream.FRandRange(0.0, 360.0);
			const FTransform Transform(
				FQuat(FVector::UpVector, static_cast<float>(FMath::DegreesToRadians(Yaw))),
				FVector(Pos.X, Pos.Y, PlaceZ),
				FVector(Scale));

			Component->AddInstance(Transform, true);
			++Placed;
		}
	};

	Place(TreeInstances, Veg.TreeMesh, Veg.TreeCount, EPrismSyneResourceType::Wood, false, 0.75);
	Place(RockInstances, Veg.RockMesh, Veg.RockCount, EPrismSyneResourceType::Mineral, false, 0.45);
	Place(CropInstances, Veg.CropMesh, Veg.CropCount, EPrismSyneResourceType::Food, false, 0.80);
	Place(ReedInstances, Veg.ReedMesh, Veg.ReedCount, EPrismSyneResourceType::Water, true, 0.50);

	UE_LOG(LogPrismLdk, Log,
		TEXT("Vegetation : %d arbres, %d rochers, %d cultures, %d roseaux."),
		GetTreeInstanceCount(), GetRockInstanceCount(), GetCropInstanceCount(), GetReedInstanceCount());
}

bool APrismLdkVirtualWorldActor::BuildNavBlockers(const FPrismLdkVirtualWorldSettings& InSettings)
{
	CleanupNavBlockers();

	const int64 CellTotal64 = static_cast<int64>(CellCountX) * static_cast<int64>(CellCountY);
	if (!InSettings.bGenerateNavBlockers || CellCountX <= 0 || CellCountY <= 0
		|| CellTotal64 <= 0 || CellTotal64 > TNumericLimits<int32>::Max()
		|| CellBlocked.Num() != static_cast<int32>(CellTotal64))
	{
		return true;
	}

	struct FRect
	{
		int32 X = 0;
		int32 Y = 0;
		int32 W = 0;
		int32 H = 0;
	};

	TArray<uint8> Used;
	Used.Init(0, CellBlocked.Num());
	TArray<FRect> Rects;

	auto BlockedAt = [this](int32 X, int32 Y)
	{
		return X >= 0 && Y >= 0 && X < CellCountX && Y < CellCountY
			&& CellBlocked[Y * CellCountX + X] != 0;
	};

	for (int32 Y = 0; Y < CellCountY; ++Y)
	{
		for (int32 X = 0; X < CellCountX; ++X)
		{
			const int32 Index = Y * CellCountX + X;
			if (!BlockedAt(X, Y) || Used[Index])
			{
				continue;
			}

			int32 Width = 0;
			while (X + Width < CellCountX)
			{
				const int32 RowIndex = Y * CellCountX + X + Width;
				if (!BlockedAt(X + Width, Y) || Used[RowIndex])
				{
					break;
				}
				++Width;
			}

			int32 Height = 1;
			while (Y + Height < CellCountY)
			{
				bool bCanExtend = true;
				for (int32 Offset = 0; Offset < Width; ++Offset)
				{
					const int32 RowIndex = (Y + Height) * CellCountX + X + Offset;
					if (!BlockedAt(X + Offset, Y + Height) || Used[RowIndex])
					{
						bCanExtend = false;
						break;
					}
				}
				if (!bCanExtend)
				{
					break;
				}
				++Height;
			}

			for (int32 OffsetY = 0; OffsetY < Height; ++OffsetY)
			{
				for (int32 OffsetX = 0; OffsetX < Width; ++OffsetX)
				{
					Used[(Y + OffsetY) * CellCountX + X + OffsetX] = 1;
				}
			}

			FRect Rect;
			Rect.X = X;
			Rect.Y = Y;
			Rect.W = Width;
			Rect.H = Height;
			Rects.Add(Rect);
		}
	}

	UWorld* World = GetWorld();
	if (!World)
	{
		return false;
	}

	for (const FRect& Rect : Rects)
	{
		const double CenterX = (static_cast<double>(Rect.X) + static_cast<double>(Rect.W) * 0.5) * CellSizeUu;
		const double CenterY = (static_cast<double>(Rect.Y) + static_cast<double>(Rect.H) * 0.5) * CellSizeUu;
		const double HalfX = static_cast<double>(Rect.W) * CellSizeUu * 0.5 + 1.0;
		const double HalfY = static_cast<double>(Rect.H) * CellSizeUu * 0.5 + 1.0;
		const FVector Extent(HalfX, HalfY, 30000.0);
		const FTransform Transform(FRotator::ZeroRotator, FVector(CenterX, CenterY, 0.0));

		AActor* Blocker = World->SpawnActorDeferred<AActor>(
			AActor::StaticClass(), Transform, nullptr, nullptr,
			ESpawnActorCollisionHandlingMethod::AlwaysSpawn);
		if (!Blocker)
		{
			CleanupNavBlockers();
			return false;
		}

		UNavModifierComponent* Modifier = NewObject<UNavModifierComponent>(Blocker);
		if (!Modifier)
		{
			Blocker->Destroy();
			CleanupNavBlockers();
			return false;
		}
		Modifier->FailsafeExtent = Extent;
		Modifier->bIncludeAgentHeight = false;
		Modifier->SetAreaClass(UNavArea_Null::StaticClass());
		Blocker->AddInstanceComponent(Modifier);
		Blocker->FinishSpawning(Transform);
		if (!Modifier->IsRegistered())
		{
			Modifier->RegisterComponent();
		}
		Blocker->SetActorHiddenInGame(true);
		Blocker->SetActorEnableCollision(false);

		NavBlockers.Add(Blocker);
	}

	UE_LOG(LogPrismLdk, Log,
		TEXT("Navigation : %d cellules bloquees fusionnees en %d volumes NavArea_Null."),
		GetBlockedCellCount(), NavBlockers.Num());
	return NavBlockers.Num() == Rects.Num();
}

void APrismLdkVirtualWorldActor::CleanupNavBlockers()
{
	for (const TObjectPtr<AActor>& Blocker : NavBlockers)
	{
		if (Blocker)
		{
			Blocker->Destroy();
		}
	}
	NavBlockers.Reset();
}bool APrismLdkVirtualWorldActor::BeginNavigationBuild()
{
	UWorld* World = GetWorld();
	UNavigationSystemV1* Nav = World ? UNavigationSystemV1::GetCurrent(World) : nullptr;
	if (!Nav)
	{
		return false;
	}

	// Use le NavData principal du monde, celui auquel Build() soumet les tâches.
	// La notification est globale au NavSystem: on filtre sur cette instance.
	ANavigationData* NavData = Nav->GetDefaultNavDataInstance();
	if (!NavData)
	{
		return false;
	}

	// Les acteurs de contrôle appartiennent à la grille SYNE; le centre de la
	// cellule marchable la plus proche est un point de projection déterministe.
	const int32 ProbeX = FMath::Clamp(CellCountX / 2, 0, CellCountX - 1);
	const int32 ProbeY = FMath::Clamp(CellCountY / 2, 0, CellCountY - 1);
	bool bFoundWalkableCell = false;
	for (int32 Radius = 0; Radius < FMath::Max(CellCountX, CellCountY) && !bFoundWalkableCell; ++Radius)
	{
		for (int32 Y = FMath::Max(0, ProbeY - Radius); Y <= FMath::Min(CellCountY - 1, ProbeY + Radius) && !bFoundWalkableCell; ++Y)
		{
			for (int32 X = FMath::Max(0, ProbeX - Radius); X <= FMath::Min(CellCountX - 1, ProbeX + Radius); ++X)
			{
				if (IsCellBlocked(X, Y))
				{
					continue;
				}
				const FVector CellCenter = UPrismLdkProjection::CellToTileCenter(FIntPoint(X, Y), 1.0, CellSizeUu);
				NavigationProbePoint = FVector(CellCenter.X, CellCenter.Y, SampleHeightAt(CellCenter.X, CellCenter.Y));
				bFoundWalkableCell = true;
				break;
			}
		}
	}
	if (!bFoundWalkableCell)
	{
		return false;
	}

	ExpectedNavData = NavData;
	PendingNavigationSystem = Nav;
	bNavigationGenerationFinished = false;
	bNavigationBuildRequested = true;
	bHasNavigationProbe = true;
	Nav->OnNavigationGenerationFinishedDelegate.RemoveDynamic(this, &APrismLdkVirtualWorldActor::HandleNavigationGenerationFinished);
	Nav->OnNavigationGenerationFinishedDelegate.AddDynamic(this, &APrismLdkVirtualWorldActor::HandleNavigationGenerationFinished);
	Nav->Build();
	return true;
}

void APrismLdkVirtualWorldActor::HandleNavigationGenerationFinished(ANavigationData* FinishedNavData)
{
	if (!bNavigationBuildRequested || !FinishedNavData || FinishedNavData != ExpectedNavData)
	{
		return;
	}

	// The delegate is global and does not carry a request id. A matching
	// NavData callback is only a candidate; the tick path waits for all
	// changed-area tasks to idle and validates the probe then.
	bNavigationGenerationFinished = true;
}

void APrismLdkVirtualWorldActor::UnbindNavigationBuild()
{
	if (UNavigationSystemV1* Nav = PendingNavigationSystem.Get())
	{
		Nav->OnNavigationGenerationFinishedDelegate.RemoveDynamic(this, &APrismLdkVirtualWorldActor::HandleNavigationGenerationFinished);
	}
	PendingNavigationSystem.Reset();
	ExpectedNavData = nullptr;
	bNavigationBuildRequested = false;
	bNavigationGenerationFinished = false;
	bHasNavigationProbe = false;
}

bool APrismLdkVirtualWorldActor::ValidateNavigationBuild() const
{
	UNavigationSystemV1* Nav = PendingNavigationSystem.Get();
	ANavigationData* NavData = ExpectedNavData;
	if (!bHasNavigationProbe || !Nav || !NavData)
	{
		return false;
	}

	FNavLocation ProjectedProbe;
	if (!Nav->ProjectPointToNavigation(NavigationProbePoint, ProjectedProbe, NavigationProbeExtent, NavData, FSharedConstNavQueryFilter()))
	{
		return false;
	}

	// S'il existe un blocker, sonder directement une cellule marquée bloquée.
	// UNavModifierComponent n'est pas une PrimitiveComponent : ne pas dépendre
	// des bounds de l'acteur modifier pour retrouver sa cellule logique.
	if (NavBlockers.Num() > 0 && CellSizeUu > 0.0 && CellCountX > 0 && CellCountY > 0)
	{
		bool bFoundBlockedProbe = false;
		const double ProbeExtentXY = FMath::Min(CellSizeUu * 0.1, 50.0);
		for (int32 Y = 0; Y < CellCountY && !bFoundBlockedProbe; ++Y)
		{
			for (int32 X = 0; X < CellCountX; ++X)
			{
				if (!IsCellBlocked(X, Y))
				{
					continue;
				}
				const FVector CellCenter = UPrismLdkProjection::CellToTileCenter(FIntPoint(X, Y), 1.0, CellSizeUu);
				const FVector BlockerPoint(CellCenter.X, CellCenter.Y, SampleHeightAt(CellCenter.X, CellCenter.Y));
				FNavLocation ProjectedBlocker;
				if (Nav->ProjectPointToNavigation(BlockerPoint, ProjectedBlocker,
					FVector(ProbeExtentXY, ProbeExtentXY, 100.0), NavData, FSharedConstNavQueryFilter()))
				{
					return false;
				}
				bFoundBlockedProbe = true;
				break;
			}
		}
		if (!bFoundBlockedProbe)
		{
			return false;
		}
	}

	return true;
}

void APrismLdkVirtualWorldActor::RebuildNavigation()
{
	if (UWorld* World = GetWorld())
	{
		if (UNavigationSystemV1* NavigationSystem = UNavigationSystemV1::GetCurrent(World))
		{
			NavigationSystem->Build();
		}
	}
}
