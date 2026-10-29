// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Rejeu d'un flux troué/stale : l'état publié est le dernier tick reçu, jamais une valeur inventée.

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "PrismLdkService.h"

/**
 * BL-25 — acceptation ciblée de PRISM sur un flux comportant des trous.
 *
 * Le critère d'acceptation est *« rejeu d'un snapshot reçu seul montre l'état
 * autoritaire ; aucune extrapolation n'est présentée comme état du monde »*.
 * Cette spec rejoue, au niveau de la surface de service (celle que le Launcher
 * consomme : `/metrics`), une séquence où les ticks 42 à 46 ne sont **jamais**
 * reçus : l'état publié doit rester le dernier tick autoritaire (41 puis 47),
 * ne jamais avancer tout seul vers un tick non reçu, et ne produire aucun tick
 * intermédiaire.
 *
 * La spec verrouille la **publication** ; le remplacement en bloc du snapshot
 * par le sous-système (`ParseSnapshot`, `LatestSnapshot`) est audité dans
 * `docs/realignment-evidence/`. Le **chemin d'état** C++ n'introduit ni
 * interpolation ni extrapolation (aucun `Interp`/`Lerp`) ; l'interpolation
 * **visuelle** du rendu entre deux trames reçues (monographie `07-prism/04` §4)
 * est un autre chemin, non autoritaire et jamais réinjecté.
 *
 * Exécution : `-ExecCmds="Automation RunTests PrismLdk.GapReplay"` (voir
 * `docs/prism/TESTING.md`).
 */
BEGIN_DEFINE_SPEC(FPrismLdkGapReplaySpec, TEXT("PrismLdk.GapReplay"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkGapReplaySpec)

namespace
{
	/**
	 * Applique un snapshot reçu exactement comme le sous-système le fait
	 * (`UPrismLdkSubsystem::ParseMessage` → `ReportSnapshot`) : les champs de
	 * monde de la trame écrasent l'état, sans accumulation.
	 */
	void ReplayReceivedSnapshot(FPrismLdkServiceState& State, int64 Tick, int32 AliveCount, const FString& RunId)
	{
		State.bHasSnapshot = true;
		State.Tick = Tick;
		State.Agents = AliveCount;
		if (!RunId.IsEmpty())
		{
			State.RunId = RunId;
		}
	}
}

void FPrismLdkGapReplaySpec::Define()
{
	Describe(TEXT("Rejeu d'un flux troué"), [this]()
	{
		It(TEXT("publie le dernier tick reçu sans interpoler les ticks sautés"), [this]()
		{
			FPrismLdkServiceState State;
			State.bListening = true;
			State.bEngineRunning = true;
			State.bWsConnected = true;
			State.bHasWorld = true;
			State.bHasSnapshot = false;
			State.TicksPerSecond = 6;
			State.RunId = TEXT("run-gap");

			ReplayReceivedSnapshot(State, 41, 100, TEXT("run-gap"));
			const FString AfterFirst = PrismLdkMetricsText(State);
			TestTrue(TEXT("dernier tick reçu publié"), AfterFirst.Contains(TEXT("livex_prism_tick 41")));
			TestTrue(TEXT("population du dernier tick reçu"), AfterFirst.Contains(TEXT("livex_prism_agents 100")));

			// Trous 42..46 : aucune trame reçue. On relit l'état deux fois de
			// suite : le service ne doit pas avancer le tick de lui-même.
			const FString WhileSilent = PrismLdkMetricsText(State);
			TestTrue(TEXT("le tick ne s'avance pas sans donnée"), WhileSilent.Contains(TEXT("livex_prism_tick 41")));
			TestTrue(TEXT("la population ne s'avance pas sans donnée"), WhileSilent.Contains(TEXT("livex_prism_agents 100")));

			ReplayReceivedSnapshot(State, 47, 97, TEXT("run-gap"));
			const FString AfterGap = PrismLdkMetricsText(State);
			TestTrue(TEXT("tick autoritaire après le trou"), AfterGap.Contains(TEXT("livex_prism_tick 47")));
			TestTrue(TEXT("population autoritaire après le trou"), AfterGap.Contains(TEXT("livex_prism_agents 97")));

			for (int64 Skipped = 42; Skipped <= 46; ++Skipped)
			{
				const FString Interpolated = FString::Printf(TEXT("livex_prism_tick %lld"), Skipped);
				TestFalse(FString::Printf(TEXT("aucun tick interpolé %lld publié"), Skipped),
					AfterGap.Contains(Interpolated));
			}
		});

		It(TEXT("déclare le flux dégradé à la déconnexion sans fabriquer de tick"), [this]()
		{
			FPrismLdkServiceState State;
			State.bListening = true;
			State.bEngineRunning = true;
			State.bWsConnected = true;
			State.bHasWorld = true;
			State.Tick = 12;
			State.Agents = 8;

			// Perte de la WebSocket : l'état déjà reçu reste lisible tel quel
			// (aucune rétro-interpolation ni remise à zéro inventée), et le
			// détail de santé signale la dégradation.
			State.bWsConnected = false;
			const FString Details = PrismLdkHealthDetailsJson(State);
			TestTrue(TEXT("flux signalé dégradé"), Details.Contains(TEXT("\"name\":\"snapshot_stream\",\"status\":\"Degraded\"")));

			const FString Metrics = PrismLdkMetricsText(State);
			TestTrue(TEXT("dernier état reçu conservé"), Metrics.Contains(TEXT("livex_prism_tick 12")));
			TestTrue(TEXT("connexion signalée fermée"), Metrics.Contains(TEXT("livex_prism_ws_connected")));
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
