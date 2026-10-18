// Copyright Epic Games, Inc. All Rights Reserved.

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Cible de build de l'éditeur LDK (variante éditeur).

using UnrealBuildTool;
using System.Collections.Generic;

public class LDKEditorTarget : TargetRules
{
	public LDKEditorTarget( TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.V7;
		IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_8;
		ExtraModuleNames.Add("LDK");
	}
}
