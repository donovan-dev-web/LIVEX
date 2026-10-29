// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests automatisés de l'authentification client : le jeton de session présenté à SYNE.

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "Engine/GameInstance.h"
#include "UObject/UObjectGlobals.h"
#include "UObject/Package.h"
#include "PrismLdkSubsystem.h"
#include "PrismLdkTypes.h"

namespace
{
	UPrismLdkSubsystem* MakeAuthSubsystem()
	{
		// `UGameInstanceSubsystem` exige un outer `UGameInstance` (règle
		// `ClassWithin`) : le sous-système n'est jamais initialisé par le moteur
		// ici — seules `Configure` / `SetSessionToken` sont exercées.
		UGameInstance* GameInstance = NewObject<UGameInstance>(GetTransientPackage(), NAME_None, RF_Transient);
		return NewObject<UPrismLdkSubsystem>(GameInstance, NAME_None, RF_Transient);
	}
}

/**
 * Verrouille la forme du header `Authorization` que PRISM, en **client**,
 * présente à SYNE sur les commandes de contrôle (`POST /api/control/*`).
 *
 * C'est le chaînon qui manquait : sans jeton, SYNE répond `401 unauthorized`
 * et le `prepare` échoue — alors que `syne-mock`, plus permissif, n'exige pas
 * le jeton. Le jeton vient de `LIVEX_SESSION_TOKEN` (environnement hérité du
 * Launcher) ou d'un appel explicite à `SetSessionToken`.
 *
 * Exécution : Session Frontend → Automation → filtre « PrismLdk.Auth »,
 * ou ligne de commande `-ExecCmds="Automation RunTests PrismLdk.Auth"`.
 */
BEGIN_DEFINE_SPEC(FPrismLdkAuthSpec, TEXT("PrismLdk.Auth"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkAuthSpec)

void FPrismLdkAuthSpec::Define()
{
	Describe(TEXT("PrismLdkAuthorizationHeader"), [this]()
	{
		It(TEXT("préfixe le jeton par Bearer"), [this]()
		{
			TestEqual(TEXT("header"), PrismLdkAuthorizationHeader(TEXT("lxt-abc123")),
				FString(TEXT("Bearer lxt-abc123")));
		});

		It(TEXT("ne pose aucun header sans jeton"), [this]()
		{
			TestTrue(TEXT("jeton vide ⇒ chaîne vide"), PrismLdkAuthorizationHeader(FString()).IsEmpty());
		});

		It(TEXT("ne fabrique jamais un Bearer sans valeur"), [this]()
		{
			// Un `Bearer ` sans jeton serait refusé comme un header absent : la
			// construction ne doit pas le produire, pour que la cause (jeton
			// manquant) reste lisible dans le journal.
			TestFalse(TEXT("aucun préfixe seul"), PrismLdkAuthorizationHeader(FString()) == TEXT("Bearer "));
		});

		It(TEXT("transmet le jeton tel quel, espaces compris"), [this]()
		{
			TestEqual(TEXT("jeton libre"), PrismLdkAuthorizationHeader(TEXT("jeton-de-test")),
				FString(TEXT("Bearer jeton-de-test")));
		});
	});

	Describe(TEXT("Jeton de session manuel (mode dev)"), [this]()
	{
		It(TEXT("Configure applique le jeton manuel quand le mode est actif"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeAuthSubsystem();
			FPrismSyneConnectionOptions Options;
			Options.bUseManualSessionToken = true;
			Options.SessionToken = TEXT("lxt-manuel-42");
			Subsystem->Configure(Options);

			TestEqual(TEXT("jeton manuel en vigueur"), Subsystem->GetSessionToken(), FString(TEXT("lxt-manuel-42")));
		});

		It(TEXT("Configure ignore le jeton manuel quand le mode est inactif"), [this]()
		{
			// Sans mode manuel, `Configure` ne touche pas au jeton : celui posé
			// hors ligne (initialisation depuis l'environnement, ou appel direct)
			// reste en vigueur.
			UPrismLdkSubsystem* Subsystem = MakeAuthSubsystem();
			Subsystem->SetSessionToken(TEXT("lxt-environnement"));

			FPrismSyneConnectionOptions Options;
			Options.bUseManualSessionToken = false;
			Options.SessionToken = TEXT("lxt-ignore");
			Subsystem->Configure(Options);

			TestEqual(TEXT("jeton d'environnement conservé"), Subsystem->GetSessionToken(), FString(TEXT("lxt-environnement")));
		});

		It(TEXT("Configure en mode manuel avec jeton vide n'émet aucun header"), [this]()
		{
			UPrismLdkSubsystem* Subsystem = MakeAuthSubsystem();
			FPrismSyneConnectionOptions Options;
			Options.bUseManualSessionToken = true;
			Subsystem->Configure(Options);

			TestTrue(TEXT("jeton vide"), Subsystem->GetSessionToken().IsEmpty());
			TestTrue(TEXT("aucun header"), PrismLdkAuthorizationHeader(Subsystem->GetSessionToken()).IsEmpty());
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
