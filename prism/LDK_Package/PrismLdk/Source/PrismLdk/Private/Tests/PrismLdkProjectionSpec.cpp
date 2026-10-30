// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests automatisés de la projection SYNE ↔ Unreal (valeurs de contrôle du contrat).

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "PrismLdkProjection.h"
#include "PrismLdkTypes.h"

/**
 * Verrouille la projection SYNE → Unreal sur les valeurs de contrôle du profil
 * PRISM (`CellSize = 32`, `K = 100`) et sur les comportements dégradés
 * (échelle ou taille de cellule invalides).
 *
 * Exécution : Session Frontend → Automation → filtre « PrismLdk.Projection »,
 * ou ligne de commande `-ExecCmds="Automation RunTests PrismLdk.Projection"`.
 */
BEGIN_DEFINE_SPEC(FPrismLdkProjectionSpec, TEXT("PrismLdk.Projection"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkProjectionSpec)

void FPrismLdkProjectionSpec::Define()
{
	/** Profil PRISM de référence : `K = TileSizeUu / CellSize = 3200 / 32`. */
	constexpr double K = 100.0;
	constexpr double CellSize = 32.0;
	constexpr double Tolerance = 1e-9;

	Describe(TEXT("SyneToUnreal"), [this]()
	{
		It(TEXT("projette une position continue en unités Unreal"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 12.5;
			Position.Y = 23.0;

			const FVector Result = UPrismLdkProjection::SyneToUnreal(Position, K);

			TestEqual(TEXT("X"), Result.X, 1250.0, Tolerance);
			TestEqual(TEXT("Y"), Result.Y, 2300.0, Tolerance);
			TestEqual(TEXT("Z reste nul (projection 3D = présentation)"), Result.Z, 0.0, Tolerance);
		});

		It(TEXT("projette un obstacle continu"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 150.0;
			Position.Y = 120.0;

			const FVector Result = UPrismLdkProjection::SyneToUnreal(Position, K);

			TestEqual(TEXT("X"), Result.X, 15000.0, Tolerance);
			TestEqual(TEXT("Y"), Result.Y, 12000.0, Tolerance);
		});

		It(TEXT("laisse l'origine en place"), [this]()
		{
			const FVector Result = UPrismLdkProjection::SyneToUnreal(FPrismSyneVector2D(), K);

			TestEqual(TEXT("X"), Result.X, 0.0, Tolerance);
			TestEqual(TEXT("Y"), Result.Y, 0.0, Tolerance);
		});

		It(TEXT("renvoie un vecteur nul si l'échelle est invalide"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 12.5;
			Position.Y = 23.0;

			const FVector Zero = UPrismLdkProjection::SyneToUnreal(Position, 0.0);
			const FVector Negative = UPrismLdkProjection::SyneToUnreal(Position, -100.0);

			TestEqual(TEXT("K = 0 → X"), Zero.X, 0.0, Tolerance);
			TestEqual(TEXT("K = 0 → Y"), Zero.Y, 0.0, Tolerance);
			TestEqual(TEXT("K < 0 → X"), Negative.X, 0.0, Tolerance);
			TestEqual(TEXT("K < 0 → Y"), Negative.Y, 0.0, Tolerance);
		});
	});

	Describe(TEXT("UnrealToSyne"), [this]()
	{
		It(TEXT("inverse SyneToUnreal"), [this]()
		{
			FPrismSyneVector2D Source;
			Source.X = 12.5;
			Source.Y = 23.0;

			const FVector Unreal = UPrismLdkProjection::SyneToUnreal(Source, K);
			const FPrismSyneVector2D RoundTrip = UPrismLdkProjection::UnrealToSyne(Unreal, K);

			TestEqual(TEXT("X"), RoundTrip.X, Source.X, Tolerance);
			TestEqual(TEXT("Y"), RoundTrip.Y, Source.Y, Tolerance);
		});

		It(TEXT("renvoie une position nulle si l'échelle est invalide (pas de division)"), [this]()
		{
			const FPrismSyneVector2D Result = UPrismLdkProjection::UnrealToSyne(FVector(1250.0, 2300.0, 0.0), 0.0);

			TestTrue(TEXT("X est fini"), FMath::IsFinite(Result.X));
			TestTrue(TEXT("Y est fini"), FMath::IsFinite(Result.Y));
			TestEqual(TEXT("X"), Result.X, 0.0, Tolerance);
			TestEqual(TEXT("Y"), Result.Y, 0.0, Tolerance);
		});
	});

	Describe(TEXT("SyneToCell"), [this]()
	{
		It(TEXT("range une position continue dans sa cellule"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 12.5;
			Position.Y = 23.0;

			const FIntPoint Cell = UPrismLdkProjection::SyneToCell(Position, CellSize, 70, 70);

			TestEqual(TEXT("X"), Cell.X, 0);
			TestEqual(TEXT("Y"), Cell.Y, 0);
		});

		It(TEXT("tronque vers le bas sur une frontière de cellule"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 32.0;
			Position.Y = 64.0;

			const FIntPoint Cell = UPrismLdkProjection::SyneToCell(Position, CellSize, 70, 70);

			TestEqual(TEXT("X"), Cell.X, 1);
			TestEqual(TEXT("Y"), Cell.Y, 2);
		});

		It(TEXT("projette une position hors-bornes sur la cellule de bord"), [this]()
		{
			FPrismSyneVector2D Negative;
			Negative.X = -1.0;
			Negative.Y = -1.0;

			FPrismSyneVector2D Far;
			Far.X = 100000.0;
			Far.Y = 100000.0;

			const FIntPoint Low = UPrismLdkProjection::SyneToCell(Negative, CellSize, 70, 70);
			const FIntPoint High = UPrismLdkProjection::SyneToCell(Far, CellSize, 70, 70);

			TestEqual(TEXT("bord bas X"), Low.X, 0);
			TestEqual(TEXT("bord bas Y"), Low.Y, 0);
			TestEqual(TEXT("bord haut X"), High.X, 69);
			TestEqual(TEXT("bord haut Y"), High.Y, 69);
		});

		It(TEXT("renvoie l'origine si la cellule ou le nombre de cellules est invalide"), [this]()
		{
			FPrismSyneVector2D Position;
			Position.X = 12.5;
			Position.Y = 23.0;

			const FIntPoint ZeroCellSize = UPrismLdkProjection::SyneToCell(Position, 0.0, 70, 70);
			const FIntPoint ZeroCount = UPrismLdkProjection::SyneToCell(Position, CellSize, 0, 70);

			TestEqual(TEXT("CellSize = 0 → X"), ZeroCellSize.X, 0);
			TestEqual(TEXT("CellSize = 0 → Y"), ZeroCellSize.Y, 0);
			TestEqual(TEXT("CellCountX = 0 → X"), ZeroCount.X, 0);
			TestEqual(TEXT("CellCountX = 0 → Y"), ZeroCount.Y, 0);
		});
	});

	Describe(TEXT("CellToTileOrigin et CellToTileCenter"), [this]()
	{
		It(TEXT("donne l'origine et le centre d'une tuile"), [this]()
		{
			const FIntPoint Cell(3, 4);

			const FVector Origin = UPrismLdkProjection::CellToTileOrigin(Cell, CellSize, K);
			const FVector Center = UPrismLdkProjection::CellToTileCenter(Cell, CellSize, K);

			TestEqual(TEXT("origine X"), Origin.X, 9600.0, Tolerance);
			TestEqual(TEXT("origine Y"), Origin.Y, 12800.0, Tolerance);
			TestEqual(TEXT("centre X"), Center.X, 11200.0, Tolerance);
			TestEqual(TEXT("centre Y"), Center.Y, 14400.0, Tolerance);
		});

		It(TEXT("place le marqueur d'une ressource au centre de sa cellule"), [this]()
		{
			// Les ressources de `WorldDescription` sont des indices de cellule.
			const FVector Center = UPrismLdkProjection::CellToTileCenter(FIntPoint(5, 7), CellSize, K);

			TestEqual(TEXT("centre X"), Center.X, 17600.0, Tolerance);
			TestEqual(TEXT("centre Y"), Center.Y, 24000.0, Tolerance);
		});

		It(TEXT("renvoie une tuile nulle si une dimension est invalide"), [this]()
		{
			const FVector Origin = UPrismLdkProjection::CellToTileOrigin(FIntPoint(3, 4), CellSize, 0.0);

			TestEqual(TEXT("origine X"), Origin.X, 0.0, Tolerance);
			TestEqual(TEXT("origine Y"), Origin.Y, 0.0, Tolerance);
		});
	});

	Describe(TEXT("SyneRadiusToUnreal"), [this]()
	{
		It(TEXT("met un rayon continu à l'échelle"), [this]()
		{
			TestEqual(TEXT("rayon 8 → 800"), UPrismLdkProjection::SyneRadiusToUnreal(8.0, K), 800.0, Tolerance);
		});

		It(TEXT("laisse passer un rayon négatif (donnée, pas échelle)"), [this]()
		{
			TestEqual(TEXT("rayon -8 → -800"), UPrismLdkProjection::SyneRadiusToUnreal(-8.0, K), -800.0, Tolerance);
		});
	});

	Describe(TEXT("MaxWalkSpeedForSpeed"), [this]()
	{
		It(TEXT("convertit une vitesse par tick en vitesse par seconde"), [this]()
		{
			// Bornes du trait `speed` (0,5 à 1,5) au ticksPerSecond du profil.
			TestEqual(TEXT("vitesse 0,5"), UPrismLdkProjection::MaxWalkSpeedForSpeed(0.5, K, 6), 300.0, Tolerance);
			TestEqual(TEXT("vitesse 1,5"), UPrismLdkProjection::MaxWalkSpeedForSpeed(1.5, K, 6), 900.0, Tolerance);
		});

		It(TEXT("renvoie zéro si le rythme de tick est invalide"), [this]()
		{
			TestEqual(TEXT("TicksPerSecond = 0"), UPrismLdkProjection::MaxWalkSpeedForSpeed(1.0, K, 0), 0.0, Tolerance);
		});
	});

	Describe(TEXT("TileSizeFromCellSize"), [this]()
	{
		It(TEXT("donne la taille de tuile du profil"), [this]()
		{
			TestEqual(TEXT("CellSize 32 → 3200 uu"), UPrismLdkProjection::TileSizeFromCellSize(CellSize, K), 3200.0, Tolerance);
		});

		It(TEXT("reste cohérent avec metersPerUnit = 1 (1 unité SYNE = 1 m = 100 uu)"), [this]()
		{
			// Vigilance de contrat : K = TileSizeUu / CellSize n'est égal à
			// `metersPerUnit * 100` que tant que 1 unité SYNE vaut 1 mètre et
			// que 1 unité Unreal vaut 1 cm. Un profil qui change `metersPerUnit`
			// doit recalculer K, sinon l'échelle du monde devient fausse.
			constexpr double MetersPerUnit = 1.0;
			constexpr double KFromContract = MetersPerUnit * 100.0;

			TestEqual(TEXT("K du profil"), K, KFromContract, Tolerance);
			TestEqual(TEXT("tuile = CellSize × metersPerUnit × 100"),
				UPrismLdkProjection::TileSizeFromCellSize(CellSize, K), CellSize * MetersPerUnit * 100.0, Tolerance);
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
