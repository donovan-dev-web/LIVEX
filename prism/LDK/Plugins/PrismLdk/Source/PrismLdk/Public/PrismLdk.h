// Copyright Epic Games, Inc. All Rights Reserved.

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Module principal du plugin PrismLdk.

#pragma once

#include "Modules/ModuleManager.h"

class FPrismLdkModule : public IModuleInterface
{
public:

	/** IModuleInterface implementation */
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;
};
