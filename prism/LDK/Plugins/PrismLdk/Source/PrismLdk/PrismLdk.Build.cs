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
			"Core", "CoreUObject", "Engine", "HTTP", "Json", "JsonUtilities", "WebSockets"
		});
	}
}
