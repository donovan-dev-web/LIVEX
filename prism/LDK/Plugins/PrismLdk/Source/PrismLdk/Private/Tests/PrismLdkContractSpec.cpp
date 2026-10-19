// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Contrat consommé par PRISM — une même trame SYNE ou syne-mock produit le même état.

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "PrismLdkSubsystem.h"

#include "Engine/GameInstance.h"

/**
 * Alignement PRISM / SYNE / syne-mock (contrat d'observabilité 0.5.0, moteur
 * 0.17.0). Le mock est une émulation du moteur : un consommateur ne doit pas
 * lire deux états différents selon la source.
 *
 * Cette spec rejoue des trames **réelles** dans les parseurs du sous-système
 * (`ParseMessage`, `ParseStatus`, via les points d'entrée de test) et vérifie :
 * - la description 1.1 (`simulatedSecondsPerTick`, `metersPerUnit`) ;
 * - `simulatedTimeSeconds` exact et son repli `tick × pas` sur un flux 0.3.0 ;
 * - l'instrumentation A0-3 des groupes, `null` = non mesuré (jamais 0 observé),
 *   `leaderId: null` = pas de leader ;
 * - la fusion du statut : un accusé de commande (`{ok,action}`) ne remet pas à
 *   zéro l'état publié, et `null` sur `ticksPerSecond` n'invente pas 10.
 *
 * Exécution : `-ExecCmds="Automation RunTests PrismLdk.Contract"` (voir
 * `docs/prism/TESTING.md`).
 */
BEGIN_DEFINE_SPEC(FPrismLdkContractSpec, TEXT("PrismLdk.Contract"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkContractSpec)

namespace
{
	/** Description de monde 1.1 telle que SYNE et le mock la publient. */
	const TCHAR* WorldInitializedFrame = TEXT(R"({
		"type": "world_initialized",
		"version": "1.1",
		"seed": 42,
		"world": {
			"version": "1.1",
			"width": 2240.0,
			"height": 2240.0,
			"cellSize": 32.0,
			"cellCountX": 70,
			"cellCountY": 70,
			"ticksPerSecond": 6,
			"simulatedSecondsPerTick": 5,
			"metersPerUnit": 1,
			"agents": [],
			"cells": [],
			"obstacles": [],
			"resources": [],
			"territories": [],
			"groups": []
		}
	})");

	/**
	 * Snapshot 0.5.0 : `leaderId` nul (groupe sans leader) et mesures d'analyse
	 * A0-3 (`maxBondDistance` / `meanBondDistance`) présentes dans la trame —
	 * PRISM les tolère sans les lire, puisqu'il ne sert qu'au rendu.
	 */
	const TCHAR* SnapshotFrame = TEXT(R"({
		"type": "snapshot",
		"version": "0.5.0",
		"engineVersion": "0.17.0",
		"runId": "run-42-0123456789ab",
		"seed": 42,
		"tick": 42,
		"simulatedTimeMinutes": 3,
		"simulatedTimeSeconds": 210,
		"aliveCount": 2,
		"season": "spring",
		"seasonIndex": 0,
		"agents": [],
		"resources": [],
		"obstacles": [],
		"territories": [],
		"books": [],
		"groups": [{
			"groupId": 1,
			"size": 2,
			"leaderId": null,
			"bornTick": 10,
			"cohesion": 0.4,
			"consensus": 0.5,
			"decision": "hold",
			"maxBondDistance": null,
			"meanBondDistance": 2.5,
			"members": [1, 2]
		}]
	})");

	/** Même snapshot sur un flux antérieur à 0.4.0 : pas de secondes exactes. */
	const TCHAR* LegacySnapshotFrame = TEXT(R"({
		"type": "snapshot",
		"version": "0.3.0",
		"engineVersion": "0.15.0",
		"runId": "run-42-0123456789ab",
		"seed": 42,
		"tick": 43,
		"simulatedTimeMinutes": 3,
		"aliveCount": 2,
		"season": "spring",
		"seasonIndex": 0,
		"agents": [],
		"resources": [],
		"obstacles": [],
		"territories": [],
		"books": [],
		"groups": []
	})");

	UPrismLdkSubsystem* MakeSubsystem()
	{
		// `UGameInstanceSubsystem` exige un outer `UGameInstance` (règle
		// `ClassWithin`) : le sous-système n'est jamais initialisé ici, seuls ses
		// parseurs sont exercés — aucun socket, aucun tick.
		UGameInstance* GameInstance = NewObject<UGameInstance>(GetTransientPackage(), NAME_None, RF_Transient);
		return NewObject<UPrismLdkSubsystem>(GameInstance, NAME_None, RF_Transient);
	}
}

void FPrismLdkContractSpec::Define()
{
	Describe(TEXT("Description du monde 1.1"), [this]()
	{
		It(TEXT("lit simulatedSecondsPerTick et metersPerUnit sans valeur en dur"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseMessage(WorldInitializedFrame);

			FPrismSyneWorldDescription World;
			TestTrue(TEXT("description reçue"), Subsystem->GetWorldDescription(World));
			TestEqual(TEXT("version de description"), World.Version, TEXT("1.1"));
			TestEqual(TEXT("cadence du monde"), World.TicksPerSecond, 6);
			TestEqual(TEXT("pas temporel du monde"), World.SimulatedSecondsPerTick, 5);
			TestTrue(TEXT("échelle métrique"), FMath::IsNearlyEqual(World.MetersPerUnit, 1.0));
			TestTrue(TEXT("largeur du monde"), FMath::IsNearlyEqual(World.Width, 2240.0));
		});
	});

	Describe(TEXT("Snapshot 0.5.0"), [this]()
	{
		It(TEXT("lit les champs de rendu du groupe et ignore l'instrumentation d'analyse"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseMessage(WorldInitializedFrame);
			Subsystem->DebugParseMessage(SnapshotFrame);

			FPrismSyneWorldSnapshot Snapshot;
			TestTrue(TEXT("snapshot reçu"), Subsystem->GetLatestSnapshot(Snapshot));
			TestEqual(TEXT("tick"), Snapshot.Tick, static_cast<int64>(42));
			TestTrue(TEXT("minutes simulées (plancher)"), FMath::IsNearlyEqual(Snapshot.SimulatedTimeMinutes, 3.0));
			TestEqual(TEXT("secondes simulées exactes"), Snapshot.SimulatedTimeSeconds, static_cast<int64>(210));
			TestEqual(TEXT("un groupe"), Snapshot.Groups.Num(), 1);
			if (Snapshot.Groups.Num() != 1)
			{
				return;
			}

			const FPrismSyneGroup& Group = Snapshot.Groups[0];
			TestEqual(TEXT("identifiant de groupe"), Group.GroupId, static_cast<int64>(1));
			TestFalse(TEXT("leaderId null : aucun leader annoncé"), Group.bHasLeader);
			TestEqual(TEXT("taille du groupe"), Group.Size, 2);
			TestTrue(TEXT("cohésion lue"), FMath::IsNearlyEqual(Group.Cohesion, 0.4));
			TestEqual(TEXT("décision exposée"), Group.Decision, TEXT("hold"));
			TestEqual(TEXT("membres lus"), Group.Members.Num(), 2);
			// `maxBondDistance` / `meanBondDistance` restent dans la trame : le
			// parseur les ignore sans rejeter le groupe (tolérance des champs
			// d'analyse, dont PRISM n'a pas l'usage).
		});

		It(TEXT("replie les secondes simulées sur tick x pas pour un flux 0.3.0"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseMessage(WorldInitializedFrame);
			Subsystem->DebugParseMessage(LegacySnapshotFrame);

			FPrismSyneWorldSnapshot Snapshot;
			TestTrue(TEXT("snapshot reçu"), Subsystem->GetLatestSnapshot(Snapshot));
			TestEqual(TEXT("secondes dérivées du pas du monde"), Snapshot.SimulatedTimeSeconds, static_cast<int64>(43 * 5));
		});
	});

	Describe(TEXT("Statut de contrôle"), [this]()
	{
		It(TEXT("publie le pas temporel et la cadence du monde préparé"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseStatusBody(TEXT(R"({
				"state": "running", "runId": "run-42-0123456789ab", "tick": 42,
				"aliveCount": 2, "ticksPerSecond": 6, "simulatedSecondsPerTick": 5,
				"seed": 42, "maxTicks": 400, "worldPrepared": true,
				"worldVersion": "1.1", "worldReadyAcknowledged": true
			})"));

			const FPrismSyneStatus Status = Subsystem->DebugGetStatus();
			TestEqual(TEXT("état"), static_cast<int32>(Status.State), static_cast<int32>(EPrismSyneRunState::Running));
			TestEqual(TEXT("cadence"), Status.TicksPerSecond, 6);
			TestTrue(TEXT("pas temporel renseigné"), Status.bHasSimulatedSecondsPerTick);
			TestEqual(TEXT("pas temporel"), Status.SimulatedSecondsPerTick, 5);
			TestTrue(TEXT("monde préparé"), Status.bWorldPrepared);
		});

		It(TEXT("n'invente pas 10 quand ticksPerSecond vaut null"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseStatusBody(TEXT(R"({
				"state": "ready", "ticksPerSecond": null,
				"simulatedSecondsPerTick": null, "worldPrepared": false
			})"));

			const FPrismSyneStatus Status = Subsystem->DebugGetStatus();
			TestEqual(TEXT("cadence non renseignée = 0"), Status.TicksPerSecond, 0);
			TestFalse(TEXT("pas temporel non mesuré"), Status.bHasSimulatedSecondsPerTick);
		});

		It(TEXT("un accusé de commande ne remet pas l'état publié à zéro"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeSubsystem();
			Subsystem->DebugParseStatusBody(TEXT(R"({
				"state": "running", "runId": "run-42-0123456789ab", "tick": 42,
				"aliveCount": 2, "ticksPerSecond": 6, "simulatedSecondsPerTick": 5,
				"seed": 42, "maxTicks": 400, "worldPrepared": true, "worldVersion": "1.1"
			})"));
			// Réponse réelle de `POST /api/control/ready` : aucune clé d'état.
			Subsystem->DebugParseStatusBody(TEXT(R"({"ok": true, "action": "ready", "worldReadyAcknowledged": true})"));

			const FPrismSyneStatus Status = Subsystem->DebugGetStatus();
			TestEqual(TEXT("tick conservé"), Status.Tick, static_cast<int64>(42));
			TestEqual(TEXT("population conservée"), Status.AliveCount, 2);
			TestEqual(TEXT("cadence conservée"), Status.TicksPerSecond, 6);
			TestEqual(TEXT("état conservé"), static_cast<int32>(Status.State), static_cast<int32>(EPrismSyneRunState::Running));
			TestTrue(TEXT("monde toujours préparé"), Status.bWorldPrepared);
			TestTrue(TEXT("accusé de ready enregistré"), Status.bWorldReadyAcknowledged);
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
