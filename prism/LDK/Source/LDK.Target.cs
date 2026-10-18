// Copyright Epic Games, Inc. All Rights Reserved.

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Cible de build de l'éditeur LDK.

using UnrealBuildTool;
using System.Collections.Generic;

public class LDKTarget : TargetRules
{
	public LDKTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.V7;
		IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_8;
		ExtraModuleNames.Add("LDK");
	}
}
