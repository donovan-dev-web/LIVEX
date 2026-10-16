// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Règles de build du plugin Unreal PrismLdk (dépendances Core, HTTP, Json, WebSockets).

using UnrealBuildTool;

public class PrismLdk : ModuleRules
{
	public PrismLdk(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core", "CoreUObject", "Engine", "HTTP", "Json", "JsonUtilities", "WebSockets",
			// Surface de service locale du composant (santé, info, métriques, arrêt) :
			// serveur HTTP du moteur, utilisé uniquement par PrismLdkService.
			// Casse exacte du répertoire du module (`Runtime/Online/HTTPServer`) :
			// UBT avertit sinon, et la résolution échoue sur un système sensible à
			// la casse (build Linux du Launcher).
			"HTTPServer"
		});
	}
}
