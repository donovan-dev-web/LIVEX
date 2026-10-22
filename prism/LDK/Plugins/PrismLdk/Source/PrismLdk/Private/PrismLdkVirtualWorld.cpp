// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Role : Generation runtime du "Monde Virtuel" (ile stylisee hand-painted).

#include "PrismLdkVirtualWorld.h"

#include "PrismLdkPCGData.h"
#include "PrismLdkSubsystem.h"
#include "ProceduralMeshComponent.h"
#include "Materials/MaterialInterface.h"
#include "Math/RandomStream.h"

namespace
{
	/** Lisseur de bord : t -> 3t^2 - 2t^3, borne a [0,1]. */
	inline double SmoothEdge(double T)
	{
		T = FMath::Clamp(T, 0.0, 1.0);
		return T * T * (3.0 - 2.0 * T);
	}
}

APrismLdkVirtualWorldActor::APrismLdkVirtualWorldActor()
{
	PrimaryActorTick.bCanEverTick = false;

	TerrainMesh = CreateDefaultSubobject<UProceduralMeshComponent>(TEXT("TerrainMesh"));
	SetRootComponent(TerrainMesh);
	TerrainMesh->SetCollisionProfileName(TEXT("BlockAll"));
	TerrainMesh->bUseAsyncCooking = false;

	WaterMesh = CreateDefaultSubobject<UProceduralMeshComponent>(TEXT("WaterMesh"));
	WaterMesh->SetupAttachment(TerrainMesh);
	WaterMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	WaterMesh->bUseAsyncCooking = false;
}

void APrismLdkVirtualWorldActor::LoadContext(const UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& InSettings)
{
	Settings = InSettings;

	WorldSizeX = 1.0;
	WorldSizeY = 1.0;
	ObstacleInfluences.Reset();
	Lakes.Reset();

	if (!Data)
	{
		return;
	}

	WorldSizeX = FMath::Max(1.0, Data->CellCountX * Data->TileSizeUu);
	WorldSizeY = FMath::Max(1.0, Data->CellCountY * Data->TileSizeUu);

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

	// Lacs : un par ressource Water positionnee.
	const double LakeRadius = FMath::Max(1.0, Settings.LakeRadiusFraction * Data->TileSizeUu);
	for (int32 Index = 0; Index < Data->WaterResourceCentersUnreal.Num(); ++Index)
	{
		const FVector& Center = Data->WaterResourceCentersUnreal[Index];
		Lakes.Add(FVector3d(Center.X, Center.Y, LakeRadius));
	}

	// Bruits : graine stable (graine SYNE si fournie, sinon 0). On decale les
	// coordonnees echantillonnees plutot que d'embarquer une table de bruit :
	// le resultat reste deterministe, sans dependance a une version d'engine.
	const int32 Seed = Settings.NoiseSeed != 0 ? Settings.NoiseSeed : static_cast<int32>(Data->Seed);
	const double SeedA = static_cast<double>(Seed % 10007) * 13.37;
	const double SeedB = static_cast<double>(Seed % 9973) * 7.91;

	NoiseOffset = FVector2D(SeedA, SeedB);
	CoastNoiseOffset = FVector2D(SeedA + 7919.0, SeedB - 4231.0);
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

double APrismLdkVirtualWorldActor::ComputeHeight(double X, double Y) const
{
	// --- 1. distance normalisee au centre du monde (blob) ---
	// Norme de superellipse : 0 au centre, 1 au milieu d'un bord, ~1.33 au coin
	// (puissance 2.4). Ainsi `IslandRadius = 0.92` laisse une bande de mer sur
	// tout le pourtour. Ne PAS multiplier par 0.7071 : cela ramenerait les
	// milieux de bord a 0.707 et l'ile toucherait les bords de la map.
	const double Nx = (X / WorldSizeX) * 2.0 - 1.0;
	const double Ny = (Y / WorldSizeY) * 2.0 - 1.0;
	const double Power = FMath::Max(1.0, static_cast<double>(Settings.BlobPower));
	const double AbsX = FMath::Abs(Nx);
	const double AbsY = FMath::Abs(Ny);
	const double Blob = FMath::Pow(FMath::Pow(AbsX, Power) + FMath::Pow(AbsY, Power), 1.0 / Power);

	// --- 2. deplacement de la cote par bruit ---
	double Coast = Blob;
	Coast += SampleNoise(X * Settings.CoastNoiseScale + CoastNoiseOffset.X, Y * Settings.CoastNoiseScale + CoastNoiseOffset.Y)
		* static_cast<double>(Settings.CoastNoiseAmplitude);

	// --- 3. profil altimetrique en fonction de la distance a la cote ---
	const double CoastDistance = static_cast<double>(Settings.IslandRadius) - Coast;
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
		// Falaise : remontee raide jusqu'a `CliffHeight`.
		const double CliffT = SmoothEdge((CoastDistance - BeachWidth) / CliffWidth);
		const double Cliff = FMath::Lerp(static_cast<double>(Settings.BeachHeight), static_cast<double>(Settings.CliffHeight), CliffT);

		// Plateau : le reste de l'ile monte lentement vers `IslandHeight`.
		const double Remaining = FMath::Max(0.001, 1.0 - BeachWidth - CliffWidth);
		const double PlateauT = SmoothEdge((CoastDistance - BeachWidth - CliffWidth) / Remaining);
		Height = FMath::Lerp(Cliff, static_cast<double>(Settings.IslandHeight), PlateauT);
	}

	// --- 4. obstacles SYNE = montagnes / cretes / amers ---
	if (Settings.bObstaclesCreateLand)
	{
		for (const FVector3d& Obstacle : ObstacleInfluences)
		{
			const double Dx = X - Obstacle.X;
			const double Dy = Y - Obstacle.Y;
			const double Distance = FMath::Sqrt(Dx * Dx + Dy * Dy);
			const double T = 1.0 - (Distance / Obstacle.Z);
			if (T <= 0.0)
			{
				continue;
			}
			const double Profile = SmoothEdge(T);
			const double Peak = Obstacle.Z * static_cast<double>(Settings.ObstacleHeightFactor);
			Height = FMath::Max(Height, Peak * Profile);
		}
	}

	// --- 5. bruit organique ---
	Height += SampleFbm(X * Settings.NoiseScale, Y * Settings.NoiseScale) * static_cast<double>(Settings.NoiseAmplitude);

	// --- 6. lacs et etangs (ressources Water) ---
	if (!Lakes.IsEmpty())
	{
		double BestMask = 0.0;
		double BestRadius = 1.0;
		for (const FVector3d& Lake : Lakes)
		{
			const double Dx = X - Lake.X;
			const double Dy = Y - Lake.Y;
			const double Distance = FMath::Sqrt(Dx * Dx + Dy * Dy);
			const double T = 1.0 - (Distance / Lake.Z);
			if (T <= 0.0)
			{
				continue;
			}
			const double Mask = SmoothEdge(T);
			if (Mask > BestMask)
			{
				BestMask = Mask;
				BestRadius = Distance / Lake.Z;
			}
		}

		if (BestMask > 0.0)
		{
			// Cuvette : parabole qui atteint `LakeLevel` au bord de l'influence.
			const double Bowl = static_cast<double>(Settings.LakeLevel)
				- static_cast<double>(Settings.LakeDepth) * (1.0 - BestRadius * BestRadius);
			Height = FMath::Lerp(Height, FMath::Min(Height, Bowl), BestMask);

			// Plage autour du lac : on aplatit l'anneau exterieur pour que la
			// rive ne soit pas un entonnoir.
			const double ShoreT = FMath::Clamp((BestRadius - 0.6) / 0.4, 0.0, 1.0);
			const double ShoreHeight = static_cast<double>(Settings.LakeLevel)
				+ static_cast<double>(Settings.LakeShoreHeight) * ShoreT;
			Height = FMath::Min(Height, ShoreHeight);
		}
	}

	return Height;
}

float APrismLdkVirtualWorldActor::SampleHeightAt(double X, double Y) const
{
	return static_cast<float>(ComputeHeight(X, Y));
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

bool APrismLdkVirtualWorldActor::BuildWorld(UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& InSettings)
{
	LoadContext(Data, InSettings);

	if (!Data || WorldSizeX <= 1.0 || WorldSizeY <= 1.0)
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("Monde virtuel : donnees invalides (Data=%s) - rien ne sera genere."), Data ? TEXT("ok") : TEXT("null"));
		bBuilt = false;
		return false;
	}

	// --- 1. Terrain ---
	const int32 Resolution = FMath::Clamp(InSettings.GridResolution, 16, 512);
	const int32 VertexCount = Resolution + 1;

	TArray<FVector> Vertices;
	TArray<int32> Triangles;
	TArray<FVector> Normals;
	TArray<FVector2D> UV0;
	TArray<FProcMeshTangent> Tangents;

	Vertices.Reserve(VertexCount * VertexCount);
	Normals.Reserve(VertexCount * VertexCount);
	UV0.Reserve(VertexCount * VertexCount);
	Tangents.Reserve(VertexCount * VertexCount);

	for (int32 YIndex = 0; YIndex < VertexCount; ++YIndex)
	{
		const double V = static_cast<double>(YIndex) / static_cast<double>(Resolution);
		const double Y = V * WorldSizeY;
		for (int32 XIndex = 0; XIndex < VertexCount; ++XIndex)
		{
			const double U = static_cast<double>(XIndex) / static_cast<double>(Resolution);
			const double X = U * WorldSizeX;
			const double Z = ComputeHeight(X, Y);

			Vertices.Add(FVector(static_cast<float>(X), static_cast<float>(Y), static_cast<float>(Z)));
			Normals.Add(SampleNormalAt(X, Y));
			UV0.Add(FVector2D(static_cast<float>(U), static_cast<float>(V)));

			// Tangente X : direction de la pente sur l'axe X. Orthonormalisee
			// avec la normale pour un mapping coherent.
			const FVector Normal = Normals.Last();
			FVector TangentX(1.0f, 0.0f, 0.0f);
			TangentX = (TangentX - Normal * FVector::DotProduct(TangentX, Normal)).GetSafeNormal();
			if (TangentX.IsNearlyZero())
			{
				TangentX = FVector(0.0f, 1.0f, 0.0f);
			}
			Tangents.Add(FProcMeshTangent(TangentX, false));
		}
	}

	Triangles.Reserve(Resolution * Resolution * 6);
	for (int32 YIndex = 0; YIndex < Resolution; ++YIndex)
	{
		for (int32 XIndex = 0; XIndex < Resolution; ++XIndex)
		{
			const int32 I0 = YIndex * VertexCount + XIndex;
			const int32 I1 = I0 + 1;
			const int32 I2 = I0 + VertexCount;
			const int32 I3 = I2 + 1;

			Triangles.Add(I0); Triangles.Add(I2); Triangles.Add(I1);
			Triangles.Add(I1); Triangles.Add(I2); Triangles.Add(I3);
		}
	}

	TerrainMesh->ClearAllMeshSections();
	TerrainMesh->CreateMeshSection_LinearColor(
		0, Vertices, Triangles, Normals, UV0, TArray<FLinearColor>(), Tangents,
		InSettings.bCreateCollision, false);

	if (TerrainMaterialOverride)
	{
		TerrainMesh->SetMaterial(0, TerrainMaterialOverride);
	}

	// --- 2. Eau : mer + lacs ---
	BuildWaterMesh(InSettings);

	bBuilt = true;

	UE_LOG(LogPrismLdk, Log,
		TEXT("Monde virtuel genere : %dx%d sommets, %d obstacles, %d lacs, monde %.0f x %.0f uu."),
		VertexCount, VertexCount, ObstacleInfluences.Num(), Lakes.Num(), WorldSizeX, WorldSizeY);

	return true;
}

void APrismLdkVirtualWorldActor::BuildWaterMesh(const FPrismLdkVirtualWorldSettings& InSettings)
{
	TArray<FVector> Vertices;
	TArray<int32> Triangles;
	TArray<FVector> Normals;
	TArray<FVector2D> UV0;
	TArray<FProcMeshTangent> Tangents;

	auto AddDisc = [this, &Vertices, &Triangles, &Normals, &UV0, &Tangents](double CenterX, double CenterY, double Radius, double Z, int32 Segments, double Irregularity)
	{
		const int32 BaseIndex = Vertices.Num();

		// Centre
		Vertices.Add(FVector(static_cast<float>(CenterX), static_cast<float>(CenterY), static_cast<float>(Z)));
		Normals.Add(FVector::UpVector);
		UV0.Add(FVector2D(0.5f, 0.5f));
		Tangents.Add(FProcMeshTangent(FVector(1.0f, 0.0f, 0.0f), false));

		for (int32 Index = 0; Index <= Segments; ++Index)
		{
			const double Angle = (2.0 * PI * static_cast<double>(Index)) / static_cast<double>(Segments);
			// Rayon irregulier : le disque n'est pas un cercle parfait, ce qui
			// donne un trac stylise plutot qu'un lac geometrique.
			const double NoiseValue = SampleNoise(
				static_cast<float>(FMath::Cos(Angle)) * Radius * 0.01 + CenterX * 0.0001 + CoastNoiseOffset.X,
				static_cast<float>(FMath::Sin(Angle)) * Radius * 0.01 + CenterY * 0.0001 + CoastNoiseOffset.Y);
			const double R = Radius * (1.0 + Irregularity * NoiseValue);
			Vertices.Add(FVector(
				static_cast<float>(CenterX + FMath::Cos(Angle) * R),
				static_cast<float>(CenterY + FMath::Sin(Angle) * R),
				static_cast<float>(Z)));
			Normals.Add(FVector::UpVector);
			UV0.Add(FVector2D(static_cast<float>(0.5 + 0.5 * FMath::Cos(Angle)), static_cast<float>(0.5 + 0.5 * FMath::Sin(Angle))));
			Tangents.Add(FProcMeshTangent(FVector(1.0f, 0.0f, 0.0f), false));
		}

		for (int32 Index = 0; Index < Segments; ++Index)
		{
			Triangles.Add(BaseIndex);
			Triangles.Add(BaseIndex + 1 + Index);
			Triangles.Add(BaseIndex + 2 + Index);
		}
	};

	// Mer : un disque large qui deborde du monde.
	if (InSettings.bGenerateSea)
	{
		const double SeaRadius = FMath::Max(WorldSizeX, WorldSizeY) * 1.6;
		AddDisc(WorldSizeX * 0.5, WorldSizeY * 0.5, SeaRadius, 0.0, 48, 0.02);
	}

	// Lacs : un disque par ressource Water, au niveau `LakeLevel`.
	for (const FVector3d& Lake : Lakes)
	{
		AddDisc(Lake.X, Lake.Y, Lake.Z * 1.05, static_cast<double>(InSettings.LakeLevel), 32, 0.12);
	}

	WaterMesh->ClearAllMeshSections();
	if (Vertices.Num() > 0)
	{
		WaterMesh->CreateMeshSection_LinearColor(
			0, Vertices, Triangles, Normals, UV0, TArray<FLinearColor>(), Tangents, false, false);
	}

	if (WaterMaterialOverride)
	{
		WaterMesh->SetMaterial(0, WaterMaterialOverride);
	}
}
