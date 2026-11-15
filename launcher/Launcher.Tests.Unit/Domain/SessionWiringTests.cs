// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Ordre de démarrage et d'arrêt d'une session dont un composant consomme le point d'accès d'un autre.

using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Domain;

/// <summary>
/// Ordre d'une session SYNE + PRISM + ECHOS. Le consommateur (ECHOS) ne peut
/// recevoir le flux de lecture qu'une fois le producteur (PRISM) démarré : l'ordre
/// se déduit du manifeste, et l'arrêt est l'inverse strict.
/// </summary>
public sealed class SessionWiringTests
{
    private static OrchestrationService Service(out ServiceRegistry registry)
    {
        registry = new ServiceRegistry();
        var service = new OrchestrationService(
            registry, new PortAllocator(5200, 5399, _ => true), new SystemClock(), new InMemoryJournal());
        registry.RegisterInstallation(Installation("syne", new ComponentManifest
        {
            Id = "syne",
            Name = "SYNE",
            Type = "engine",
            Version = "0.17.0",
            Capabilities = ["seed", "tickLimit"],
        }));
        registry.RegisterInstallation(Installation("prism", new ComponentManifest
        {
            Id = "prism",
            Name = "PRISM",
            Type = "immersion",
            Version = "2.0.0-preview.1",
            Capabilities = ["headless", "snapshotStream", "renderCadence"],
            ContributesTo = ["immersion"],
            Endpoints = new Dictionary<string, JsonEndpoint>
            {
                ["control"] = new() { Transport = "http" },
                ["readStream"] = new() { Transport = "websocket", LaunchArgument = "--read-port" },
            },
            Timeouts = new JsonTimeouts { StartupMs = 30_000, ShutdownMs = 5_000 },
        }));
        registry.RegisterInstallation(Installation("echos", new ComponentManifest
        {
            Id = "echos",
            Name = "ECHOS",
            Type = "analysis",
            Version = "0.1.0",
            Capabilities = ["headless", "analytics"],
            Requires = ["syne"],
            Consumes = new Dictionary<string, JsonConsume>
            {
                ["readStream"] = new() { From = "prism", LaunchArgument = "--prism-stream" },
            },
        }));
        return service;
    }

    [Fact]
    public void Un_consommateur_demarre_apres_le_producteur_quel_que_soit_l_ordre_demande()
    {
        var service = Service(out _);

        // L'opérateur demande ECHOS avant PRISM : le domaine remet PRISM en tête de
        // la chaîne, sinon ECHOS recevrait une adresse qui n'existe pas encore.
        var resolution = service.ResolveProfile(
            WellKnownProfiles.Personnalise, extraComponents: ["echos", "prism"]);

        Assert.True(resolution.Satisfiable, string.Join(" ; ", resolution.Problems));
        Assert.Equal(["syne", "prism", "echos"], resolution.StartupOrder);
    }

    [Fact]
    public void L_arret_est_l_inverse_strict_du_demarrage()
    {
        var service = Service(out _);
        var resolution = service.ResolveProfile(
            WellKnownProfiles.Personnalise, extraComponents: ["echos", "prism"]);

        Assert.Equal(["echos", "prism", "syne"], OrchestrationService.ShutdownOrder(resolution.StartupOrder));
    }

    [Fact]
    public void Un_composant_sans_consommation_conserve_l_ordre_demande()
    {
        var service = Service(out _);

        // Sans lien de consommation, rien ne justifie de réordonner : le mode Immersion
        // reste SYNE puis PRISM, comme avant LAUNCHER-202.
        var resolution = service.ResolveProfile(WellKnownProfiles.Immersion);

        Assert.True(resolution.Satisfiable, string.Join(" ; ", resolution.Problems));
        Assert.Equal(["syne", "prism"], resolution.StartupOrder);
    }

    private static ComponentInstallation Installation(string id, ComponentManifest manifest) => new()
    {
        ComponentId = id,
        Location = $"/fake/{id}",
        Manifest = manifest,
        BinaryPresent = true,
    };
}
