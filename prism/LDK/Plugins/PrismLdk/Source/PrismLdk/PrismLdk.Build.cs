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
