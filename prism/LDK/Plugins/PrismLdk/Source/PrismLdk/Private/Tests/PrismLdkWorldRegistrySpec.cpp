// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests automatisés du registre de monde PRISM (échelle dérivée et correspondances id → acteur).

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include <limits>

#include "PrismLdkWorldRegistry.h"
#include "PrismLdkTypes.h"

namespace
{
	/**
	 * Acteur transient : le registre ne teste qu'une correspondance
	 * identifiant → acteur, aucun monde n'est nécessaire.
	 */
	static AActor* MakeTransientActor()
	{
		return NewObject<AActor>(GetTransientPackage(), NAME_None, RF_Transient);
	}

	/** Description de contrôle du profil PRISM : `CellSize = 32`, `TileSizeUu = 3200` ⇒ `K = 100`. */
	static FPrismSyneWorldDescription MakeDescription(double CellSize = 32.0)
	{
		FPrismSyneWorldDescription World;
		World.Version = TEXT("1.1");
		World.Seed = 42;
		World.CellSize = CellSize;
		World.CellCountX = 70;
		World.CellCountY = 60;
		World.TicksPerSecond = 6;
		World.SimulatedSecondsPerTick = 60;
		World.MetersPerUnit = 1.0;
		return World;
	}
}

/**
 * Verrouille l'échelle dérivée (`K = TileSizeUu / CellSize`) et les
 * correspondances du registre : remplacement, refus des entrées invalides,
 * disparition des acteurs détruits, cycle de vie de run (`ClearAll`).
 *
 * Exécution : Session Frontend → Automation → filtre « PrismLdk.WorldRegistry »,
 * ou ligne de commande `-ExecCmds="Automation RunTests PrismLdk.WorldRegistry"`.
 */
BEGIN_DEFINE_SPEC(FPrismLdkWorldRegistrySpec, TEXT("PrismLdk.WorldRegistry"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkWorldRegistrySpec)

void FPrismLdkWorldRegistrySpec::Define()
{
	constexpr double Tolerance = 1e-9;

	Describe(TEXT("Échelle de projection"), [this]()
	{
		It(TEXT("dérive K de la description et de la taille de cellule configurée"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			TestTrue(TEXT("taille de cellule acceptée"), Registry.SetTileSizeUu(3200.0));

			Registry.SetStatsFromDescription(MakeDescription());

			const FPrismLdkWorldStats& Stats = Registry.GetStats();
			TestTrue(TEXT("échelle exploitable"), Stats.bScaleValid);
			TestEqual(TEXT("K"), Stats.K, 100.0, Tolerance);
			TestEqual(TEXT("CellSize"), Stats.CellSize, 32.0, Tolerance);
			TestEqual(TEXT("TileSizeUu"), Stats.TileSizeUu, 3200.0, Tolerance);
		});

		It(TEXT("reporte les dimensions et la cadence de la description"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			Registry.SetTileSizeUu(3200.0);
			Registry.SetStatsFromDescription(MakeDescription());

			const FPrismLdkWorldStats& Stats = Registry.GetStats();
			TestEqual(TEXT("version"), Stats.WorldVersion, FString(TEXT("1.1")));
			TestEqual(TEXT("cellules X"), Stats.CellCountX, 70);
			TestEqual(TEXT("cellules Y"), Stats.CellCountY, 60);
			TestEqual(TEXT("ticks par seconde"), Stats.TicksPerSecond, 6);
			TestEqual(TEXT("secondes simulées par tick"), Stats.SimulatedSecondsPerTick, 60);
			TestEqual(TEXT("mètres par unité"), Stats.MetersPerUnit, 1.0, Tolerance);
		});

		It(TEXT("donne le même K quel que soit l'ordre des deux appels"), [this]()
		{
			FPrismLdkWorldRegistry DescriptionFirst;
			DescriptionFirst.SetStatsFromDescription(MakeDescription());
			DescriptionFirst.SetTileSizeUu(3200.0);

			FPrismLdkWorldRegistry TileSizeFirst;
			TileSizeFirst.SetTileSizeUu(3200.0);
			TileSizeFirst.SetStatsFromDescription(MakeDescription());

			TestEqual(TEXT("description d'abord"), DescriptionFirst.GetStats().K, 100.0, Tolerance);
			TestEqual(TEXT("taille de cellule d'abord"), TileSizeFirst.GetStats().K, 100.0, Tolerance);
		});

		It(TEXT("refuse une taille de cellule non finie ou non positive"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			Registry.SetStatsFromDescription(MakeDescription());

			TestFalse(TEXT("zéro"), Registry.SetTileSizeUu(0.0));
			TestFalse(TEXT("négative"), Registry.SetTileSizeUu(-3200.0));
			TestFalse(TEXT("NaN"), Registry.SetTileSizeUu(std::numeric_limits<double>::quiet_NaN()));

			const FPrismLdkWorldStats& Stats = Registry.GetStats();
			TestTrue(TEXT("valeurs refusées : pas d'infini"), FMath::IsFinite(Stats.K));
			TestEqual(TEXT("K reste nul"), Stats.K, 0.0, Tolerance);
			TestFalse(TEXT("échelle inexploitable"), Stats.bScaleValid);
		});

		It(TEXT("marque l'échelle inexploitable sans taille de cellule valide"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			Registry.SetTileSizeUu(3200.0);
			Registry.SetStatsFromDescription(MakeDescription(0.0));

			const FPrismLdkWorldStats& Stats = Registry.GetStats();
			TestFalse(TEXT("échelle inexploitable"), Stats.bScaleValid);
			TestEqual(TEXT("K nul (pas de division par zéro)"), Stats.K, 0.0, Tolerance);
		});
	});

	Describe(TEXT("Registre des agents"), [this]()
	{
		It(TEXT("enregistre, relit et remplace sans doublon"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* First = MakeTransientActor();
			AActor* Second = MakeTransientActor();

			TestTrue(TEXT("premier acteur"), Registry.RegisterAgent(7, First));
			TestTrue(TEXT("agent relu"), Registry.HasAgent(7));
			TestTrue(TEXT("acteur identique"), Registry.GetAgentActor(7) == First);

			TestTrue(TEXT("remplacement accepté"), Registry.RegisterAgent(7, Second));
			TestTrue(TEXT("acteur remplacé"), Registry.GetAgentActor(7) == Second);
			TestEqual(TEXT("un seul agent"), Registry.GetAgentCount(), 1);
		});

		It(TEXT("refuse un acteur invalide"), [this]()
		{
			FPrismLdkWorldRegistry Registry;

			TestFalse(TEXT("acteur nul"), Registry.RegisterAgent(7, nullptr));
			TestFalse(TEXT("agent absent"), Registry.HasAgent(7));
			TestEqual(TEXT("registre vide"), Registry.GetAgentCount(), 0);
		});

		It(TEXT("retire un agent et signale les identifiants absents"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			Registry.RegisterAgent(7, MakeTransientActor());

			TestTrue(TEXT("retrait"), Registry.UnregisterAgent(7));
			TestFalse(TEXT("retrait d'un identifiant absent"), Registry.UnregisterAgent(7));
			TestTrue(TEXT("acteur nul après retrait"), Registry.GetAgentActor(7) == nullptr);
		});

		It(TEXT("trie les identifiants et ignore les acteurs détruits"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* Destroyed = MakeTransientActor();
			Registry.RegisterAgent(12, MakeTransientActor());
			Registry.RegisterAgent(3, Destroyed);
			Registry.RegisterAgent(7, MakeTransientActor());

			Destroyed->MarkAsGarbage();

			const TArray<int64> Ids = Registry.GetAgentIds();
			TestEqual(TEXT("deux agents vivants"), Ids.Num(), 2);
			TestEqual(TEXT("premier identifiant"), Ids[0], static_cast<int64>(7));
			TestEqual(TEXT("second identifiant"), Ids[1], static_cast<int64>(12));
			TestFalse(TEXT("agent détruit signalé absent"), Registry.HasAgent(3));
			TestTrue(TEXT("aucun pointeur pendant"), Registry.GetAgentActor(3) == nullptr);
			TestEqual(TEXT("comptage des vivants"), Registry.GetAgentCount(), 2);
		});
	});

	Describe(TEXT("Registre des obstacles"), [this]()
	{
		It(TEXT("indexe par identifiant SYNE et trie les clés"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* Rock = MakeTransientActor();
			Registry.RegisterObstacle(TEXT("obstacle-2"), Rock);
			Registry.RegisterObstacle(TEXT("obstacle-1"), MakeTransientActor());

			TestTrue(TEXT("obstacle relu"), Registry.GetObstacleActor(TEXT("obstacle-2")) == Rock);
			TestEqual(TEXT("comptage"), Registry.GetObstacleCount(), 2);

			const TArray<FString> Ids = Registry.GetObstacleIds();
			TestEqual(TEXT("deux identifiants"), Ids.Num(), 2);
			TestEqual(TEXT("ordre stable"), Ids[0], FString(TEXT("obstacle-1")));
			TestEqual(TEXT("ordre stable"), Ids[1], FString(TEXT("obstacle-2")));

			TestTrue(TEXT("retrait"), Registry.UnregisterObstacle(TEXT("obstacle-1")));
			TestFalse(TEXT("retrait d'un identifiant absent"), Registry.UnregisterObstacle(TEXT("obstacle-1")));
		});

		It(TEXT("refuse un identifiant vide ou un acteur invalide"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* Actor = MakeTransientActor();

			TestFalse(TEXT("identifiant vide"), Registry.RegisterObstacle(FString(), Actor));
			TestFalse(TEXT("acteur nul"), Registry.RegisterObstacle(TEXT("obstacle-1"), nullptr));
			TestEqual(TEXT("registre vide"), Registry.GetObstacleCount(), 0);
		});
	});

	Describe(TEXT("Marqueurs de ressource"), [this]()
	{
		It(TEXT("ajoute, refuse les doublons et retire"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* Marker = MakeTransientActor();

			TestTrue(TEXT("ajout"), Registry.AddResourceMarker(Marker));
			TestFalse(TEXT("doublon refusé"), Registry.AddResourceMarker(Marker));
			TestEqual(TEXT("un seul marqueur"), Registry.GetResourceMarkerCount(), 1);

			TestTrue(TEXT("retrait"), Registry.RemoveResourceMarker(Marker));
			TestFalse(TEXT("retrait d'un marqueur absent"), Registry.RemoveResourceMarker(Marker));
			TestEqual(TEXT("registre vide"), Registry.GetResourceMarkerCount(), 0);
		});

		It(TEXT("ignore un acteur invalide"), [this]()
		{
			FPrismLdkWorldRegistry Registry;

			TestFalse(TEXT("acteur nul"), Registry.AddResourceMarker(nullptr));
			TestEqual(TEXT("registre vide"), Registry.GetResourceMarkerCount(), 0);
		});

		It(TEXT("compacte les références mortes à l'ajout suivant"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			AActor* Destroyed = MakeTransientActor();
			AActor* Alive = MakeTransientActor();

			Registry.AddResourceMarker(Destroyed);
			Destroyed->MarkAsGarbage();

			TestTrue(TEXT("ajout après destruction"), Registry.AddResourceMarker(Alive));

			const TArray<AActor*> Markers = Registry.GetResourceMarkers();
			TestEqual(TEXT("un marqueur vivant"), Markers.Num(), 1);
			TestTrue(TEXT("le marqueur vivant est conservé"), Markers[0] == Alive);
			TestEqual(TEXT("comptage des vivants"), Registry.GetResourceMarkerCount(), 1);
		});
	});

	Describe(TEXT("Cycle de vie du run"), [this]()
	{
		It(TEXT("vide les registres et conserve la taille de cellule configurée"), [this]()
		{
			FPrismLdkWorldRegistry Registry;
			Registry.SetTileSizeUu(3200.0);
			Registry.SetStatsFromDescription(MakeDescription());
			Registry.RegisterAgent(7, MakeTransientActor());
			Registry.RegisterObstacle(TEXT("obstacle-1"), MakeTransientActor());
			Registry.AddResourceMarker(MakeTransientActor());

			Registry.ClearAll();

			TestEqual(TEXT("agents"), Registry.GetAgentCount(), 0);
			TestEqual(TEXT("obstacles"), Registry.GetObstacleCount(), 0);
			TestEqual(TEXT("marqueurs"), Registry.GetResourceMarkerCount(), 0);

			const FPrismLdkWorldStats& Stats = Registry.GetStats();
			TestEqual(TEXT("taille de cellule conservée (paramètre produit)"), Stats.TileSizeUu, 3200.0, Tolerance);
			TestEqual(TEXT("échelle dérivée remise à zéro"), Stats.K, 0.0, Tolerance);
			TestFalse(TEXT("échelle inexploitable avant la prochaine description"), Stats.bScaleValid);

			// Un nouveau run doit reconstruire l'échelle sans reconfigurer la taille de cellule.
			Registry.SetStatsFromDescription(MakeDescription());
			TestEqual(TEXT("échelle reconstruite"), Registry.GetStats().K, 100.0, Tolerance);
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
