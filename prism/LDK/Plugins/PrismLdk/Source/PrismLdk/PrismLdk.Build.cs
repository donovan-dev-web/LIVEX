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
			// Surface de service locale du composant (sant�, info, m�triques, arr�t) :
			// serveur HTTP du moteur, utilis� uniquement par PrismLdkService.
			// Casse exacte du r�pertoire du module (`Runtime/Online/HTTPServer`) :
			// UBT avertit sinon, et la r�solution �choue sur un syst�me sensible �
			// la casse (build Linux du Launcher).
			"HTTPServer",
			// PCG pour le mode "Monde Virtuel" (hand-painted / g�n�ration runtime)
			"PCG",
			// Maillage procedural runtime (terrain de l'ile, lacs) : le
			// heightfield est construit en C++, pas en Blueprint.
			"ProceduralMeshComponent"
		});
	}
}
