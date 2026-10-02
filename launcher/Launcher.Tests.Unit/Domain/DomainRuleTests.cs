using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Domain;

/// <summary>Journal en mémoire pour les bancs unitaires.</summary>
public sealed class InMemoryJournal : ISessionJournal
{
    /// <summary>Événements enregistrés.</summary>
    public List<SessionEvent> Entries { get; } = new();

    /// <inheritdoc />
    public void Log(SessionEvent entry) => Entries.Add(entry);

    /// <inheritdoc />
    public void Info(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message, Level = "Info" });

    /// <inheritdoc />
    public void Warn(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message, Level = "Warn" });

    /// <inheritdoc />
    public void Fail(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message, Level = "Error" });
}

public sealed class ServiceRegistryInstallationSelectionTests
{
    [Fact]
    public void Installation_active_peut_etre_selectionnee_et_doit_etre_enregistree()
    {
        var registry = new ServiceRegistry();
        var first = ValidInstallation("/components/syne-1");
        var second = ValidInstallation("/components/syne-2");
        registry.RegisterInstallation(first);
        registry.RegisterInstallation(second);

        Assert.Same(first, registry.GetActiveInstallation("syne"));
        Assert.Equal(2, registry.GetInstallations("syne").Count);

        registry.SetActiveInstallation("syne", second);

        Assert.Same(second, registry.GetActiveInstallation("syne"));
        Assert.Throws<ArgumentException>(() =>
            registry.SetActiveInstallation("syne", ValidInstallation("/components/not-registered")));
        Assert.Throws<ArgumentException>(() =>
            registry.SetActiveInstallation("echos", second));
    }

    private static ComponentInstallation ValidInstallation(string location) => new()
    {
        ComponentId = "syne",
        Location = location,
        BinaryPresent = true,
        Manifest = new ComponentManifest { Id = "syne", Name = "SYNE", Type = "engine", Version = "1.0.0" },
    };
}

public sealed class ComponentManifestEndpointTests
{
    [Fact]
    public void Endpoint_peut_declarer_l_argument_du_port_alloue()
    {
        var endpoint = System.Text.Json.JsonSerializer.Deserialize<JsonEndpoint>(
            """{"transport":"websocket","port":5180,"enabled":true,"launchArgument":"--data-port"}""",
            ContractJson.Options);

        Assert.NotNull(endpoint);
        Assert.Equal("websocket", endpoint.Transport);
        Assert.Equal(5180, endpoint.Port);
        Assert.Equal("--data-port", endpoint.LaunchArgument);
    }
}

/// <summary>Dérivation des graines (EXPERIMENTS.md §5) : prévisible et dérivable, jamais inventée.</summary>
public sealed class SeedDeriverTests
{
    [Fact]
    public void Derived_egal_base_plus_n()
    {
        Assert.Equal(1000, SeedDeriver.Derive(SeedStrategy.Derived, 1000, 0, null));
        Assert.Equal(1042, SeedDeriver.Derive(SeedStrategy.Derived, 1000, 42, null));
    }

    [Fact]
    public void Explicit_utilise_la_liste_fournie()
    {
        Assert.Equal(77, SeedDeriver.Derive(SeedStrategy.Explicit, 0, 1, [11, 77, 99]));
    }

    [Fact]
    public void Explicit_sans_graine_est_refuse()
    {
        Assert.Throws<ArgumentException>(() => SeedDeriver.Derive(SeedStrategy.Explicit, 0, 5, [1, 2]));
    }

    [Fact]
    public void DerivedHashed_est_deterministe_et_positif()
    {
        var first = SeedDeriver.Derive(SeedStrategy.DerivedHashed, 42, 7, null);
        var second = SeedDeriver.Derive(SeedStrategy.DerivedHashed, 42, 7, null);
        Assert.Equal(first, second);
        Assert.True(first > 0);
        Assert.NotEqual(SeedDeriver.Derive(SeedStrategy.DerivedHashed, 42, 8, null), first);
    }

    [Fact]
    public void Random_est_refuse_car_non_dérivable()
    {
        Assert.Throws<ArgumentException>(() => SeedDeriver.Derive(SeedStrategy.Random, 1, 0, null));
    }
}

/// <summary>Règles normatives de transition (COMPONENTS.md §4.1).</summary>
public sealed class StateRulesTests
{
    [Fact]
    public void Le_delai_prime_sur_etat()
    {
        var report = StateRules.Timeout(ComponentState.Demarrage, "sonde muette", DateTimeOffset.UtcNow);
        Assert.Equal(ComponentState.Defaillant, report.State);
        Assert.Contains("délai", report.Cause);
    }

    [Fact]
    public void Trois_sondes_manquees_produisent_injoignable()
    {
        var state = StateRules.Ready(DateTimeOffset.UtcNow, ComponentState.Actif, "sonde au vert");
        state = StateRules.Missed(state, DateTimeOffset.UtcNow);
        Assert.NotEqual(ComponentState.Defaillant, state.State);
        state = StateRules.Missed(state, DateTimeOffset.UtcNow);
        Assert.NotEqual(ComponentState.Defaillant, state.State);
        state = StateRules.Missed(state, DateTimeOffset.UtcNow);
        Assert.Equal(ComponentState.Defaillant, state.State);
        Assert.Contains("perte de contact", state.Cause);
    }

    [Fact]
    public void Requis_defaillant_produit_hors_service_avec_cause()
    {
        var instance = new ComponentInstance
        {
            InstanceId = "syne-0001",
            ComponentId = "syne",
            Installation = new ComponentInstallation
            {
                ComponentId = "syne",
                Manifest = new ComponentManifest { Id = "syne", Name = "SYNE" },
            },
            Health = StateRules.ProcessLost(DateTimeOffset.UtcNow, 1, ExitOutcomeNames.ProcessError),
        };
        var (state, cause) = StateRules.Aggregate([(instance, true)]);
        Assert.Equal("Hors service", state);
        Assert.Contains("SYNE", cause);
    }

    [Fact]
    public void Optionnel_defaillant_produit_degrade()
    {
        var required = new ComponentInstance
        {
            InstanceId = "syne-0001",
            ComponentId = "syne",
            Installation = new ComponentInstallation { ComponentId = "syne", Manifest = new ComponentManifest { Id = "syne", Name = "SYNE" } },
            Health = StateRules.Ready(DateTimeOffset.UtcNow, ComponentState.Actif, "en service"),
        };
        var optional = new ComponentInstance
        {
            InstanceId = "echos-0001",
            ComponentId = "echos",
            Installation = new ComponentInstallation { ComponentId = "echos", Manifest = new ComponentManifest { Id = "echos", Name = "ECHOS" } },
            Health = StateRules.ProcessLost(DateTimeOffset.UtcNow, 1, ExitOutcomeNames.ProcessError),
        };
        var (state, _) = StateRules.Aggregate([(required, true), (optional, false)]);
        Assert.Equal("Dégradé", state);
    }

    [Fact]
    public void Aucun_composant_produit_inactif()
    {
        var (state, _) = StateRules.Aggregate(Array.Empty<(ComponentInstance, bool)>());
        Assert.Equal("Inactif", state);
    }
}

/// <summary>Allocation des adresses (NETWORK.md §6.2) : priorité au manifeste, pré-vol, unicité.</summary>
public sealed class PortAllocatorTests
{
    [Fact]
    public void Port_declare_libre_est_honore()
    {
        var allocator = new PortAllocator(5200, 5399, _ => true);
        var endpoint = allocator.Resolve("syne", "syne-0001", "control", 5181);
        Assert.Equal(5181, endpoint.Port);
    }

    [Fact]
    public void Port_declare_occupe_est_refuse_au_prevol()
    {
        var allocator = new PortAllocator(5200, 5399, port => port != 5181);
        Assert.Throws<PortUnavailableException>(() => allocator.Resolve("syne", "syne-0001", "control", 5181));
    }

    [Fact]
    public void Allocation_plage_sans_collision()
    {
        var allocator = new PortAllocator(5200, 5399, _ => true);
        var first = allocator.Resolve("syne", "syne-0001", "control", null);
        var second = allocator.Resolve("syne", "syne-0002", "control", null);
        Assert.NotEqual(first.Port, second.Port);
    }

    [Fact]
    public void Release_libere_le_port()
    {
        var allocator = new PortAllocator(5200, 5399, _ => true);
        var first = allocator.Resolve("syne", "syne-0001", "control", null);
        allocator.Release("syne-0001");
        var second = allocator.Resolve("syne", "syne-0002", "control", null);
        Assert.Equal(first.Port, second.Port);
    }

    [Fact]
    public void Plage_epuisee_est_refusee_explicitement()
    {
        var allocator = new PortAllocator(5200, 5200, _ => true);
        allocator.Resolve("syne", "syne-0001", "control", null);
        Assert.Throws<PortUnavailableException>(() => allocator.Resolve("syne", "syne-0002", "control", null));
    }
}

/// <summary>Résolution de profils (COMPONENTS.md §6, §7) et verrou Immersion (ADR-006).</summary>
public sealed class OrchestrationServiceTests
{
    private static OrchestrationService BuildService(params ComponentInstallation[] installations)
    {
        var registry = new ServiceRegistry();
        foreach (var installation in installations)
        {
            registry.RegisterInstallation(installation);
        }

        return new OrchestrationService(registry, new PortAllocator(5200, 5399, _ => true), new SystemClock(), new InMemoryJournal());
    }

    private static ComponentInstallation Valid(string id, string name) => new()
    {
        ComponentId = id,
        Location = $"/fake/{id}",
        Manifest = new ComponentManifest { Id = id, Name = name, Type = id, Version = "0.1.0" },
        BinaryPresent = true,
    };

    [Fact]
    public void Composant_absent_rend_le_profil_non_satisfiable()
    {
        var resolution = BuildService().ResolveProfile(WellKnownProfiles.SimulationSeule);
        Assert.False(resolution.Satisfiable);
        Assert.Contains(resolution.Problems, p => p.Contains("non détecté"));
    }

    [Fact]
    public void Profil_analyse_complete_avec_le_moteur_requis()
    {
        var resolution = BuildService(Valid("syne", "SYNE"), Valid("echos", "ECHOS")).ResolveProfile(WellKnownProfiles.Analyse);
        Assert.True(resolution.Satisfiable);
        Assert.Equal("syne", resolution.StartupOrder[0]);
    }

    [Fact]
    public void Echos_sans_syne_complete_le_moteur()
    {
        var resolution = BuildService(Valid("syne", "SYNE"), Valid("echos", "ECHOS")).ResolveProfile(
            WellKnownProfiles.Personnalise, extraComponents: ["echos"]);
        Assert.True(resolution.Satisfiable);
        Assert.Equal(["syne", "echos"], resolution.StartupOrder);
    }

    [Fact]
    public void Profil_developpement_remplace_syne_par_le_mock()
    {
        var resolution = BuildService(Valid("syne-mock", "SYNE mock"), Valid("echos", "ECHOS"))
            .ResolveProfile(WellKnownProfiles.Developpement, SessionKind.Developpement, allowStubs: true);

        Assert.True(resolution.Satisfiable, string.Join(" ; ", resolution.Problems));
        Assert.Equal(["syne-mock", "echos"], resolution.StartupOrder);
        Assert.True(resolution.StubsAllowed);
    }

    [Fact]
    public void Profil_refuse_syne_reel_et_emule_simultanement()
    {
        var resolution = BuildService(Valid("syne", "SYNE"), Valid("syne-mock", "SYNE mock"))
            .ResolveProfile(WellKnownProfiles.Personnalise, extraComponents: ["syne", "syne-mock"]);

        Assert.False(resolution.Satisfiable);
        Assert.Empty(resolution.StartupOrder);
        Assert.Contains(resolution.Problems, problem => problem.Contains("sont exclusifs", StringComparison.Ordinal));
    }

    [Fact]
    public void Profil_personnalise_vide_est_non_satisfaisable()
    {
        var resolution = BuildService().ResolveProfile(WellKnownProfiles.Personnalise);

        Assert.False(resolution.Satisfiable);
        Assert.Empty(resolution.StartupOrder);
    }

    [Fact]
    public void Mode_console_refuse_prism()
    {
        var resolution = BuildService(Valid("syne", "SYNE"), PrismConformant())
            .ResolveProfile(WellKnownProfiles.Immersion, SessionKind.Experience);

        Assert.False(resolution.Satisfiable);
        Assert.Contains(resolution.Problems, problem => problem.Contains("Console ne démarre pas PRISM", StringComparison.Ordinal));
    }

    [Fact]
    public void Immersion_verrouillee_sans_prism_avec_raison_et_jalon()
    {
        var resolution = BuildService(Valid("syne", "SYNE")).ResolveProfile(WellKnownProfiles.Immersion);
        Assert.True(resolution.ImmersionLocked);
        Assert.False(resolution.Satisfiable);
        Assert.Contains(resolution.Problems, p => p.Contains("G7"));
    }

    [Fact]
    public void Immersion_deverrouillee_avec_manifeste_prism_conforme()
    {
        var resolution = BuildService(Valid("syne", "SYNE"), PrismConformant()).ResolveProfile(WellKnownProfiles.Immersion);
        Assert.False(resolution.ImmersionLocked);
        Assert.True(resolution.Satisfiable, string.Join(" ; ", resolution.Problems));
        Assert.Equal(["syne", "prism"], resolution.StartupOrder);
    }

    [Fact]
    public void Immersion_verrouillee_avec_cause_exacte_si_exigence_manquante()
    {
        var prism = PrismConformant();
        var nonConformant = new ComponentInstallation
        {
            ComponentId = prism.ComponentId,
            Location = prism.Location,
            BinaryPresent = prism.BinaryPresent,
            Manifest = new ComponentManifest
            {
                Id = "prism",
                Name = "PRISM",
                Type = "service", // ne se déclare pas porteur du mode
                Version = "0.1.0",
                Capabilities = ["snapshotStream"], // renderCadence absent
                Endpoints = prism.Manifest!.Endpoints,
            },
        };

        var resolution = BuildService(Valid("syne", "SYNE"), nonConformant).ResolveProfile(WellKnownProfiles.Immersion);
        Assert.True(resolution.ImmersionLocked);
        Assert.Contains(resolution.Problems, p => p.Contains("PRISM ne se déclare pas porteur du mode Immersion"));
        Assert.Contains(resolution.Problems, p => p.Contains("capacité « renderCadence » absente du manifeste"));
        Assert.Contains(resolution.Problems, p => p.Contains("G7"));
        Assert.DoesNotContain("prism", resolution.StartupOrder);
    }

    /// <summary>Installation PRISM conforme à INTEGRATION_CONTRACT.md §11.1.</summary>
    private static ComponentInstallation PrismConformant() => new()
    {
        ComponentId = "prism",
        Location = "/fake/prism",
        BinaryPresent = true,
        Manifest = new ComponentManifest
        {
            Id = "prism",
            Name = "PRISM",
            Type = "immersion",
            Version = "0.1.0",
            Capabilities = ["snapshotStream", "renderCadence"],
            Endpoints = new Dictionary<string, JsonEndpoint> { ["control"] = new() { Transport = "http" } },
            ContributesTo = ["immersion"],
        },
    };

    [Fact]
    public void Ordre_d_arret_inverse_strict()
    {
        Assert.Equal(["prism", "echos", "syne"], OrchestrationService.ShutdownOrder(["syne", "echos", "prism"]));
    }
}

public sealed class ExperimentDefinitionValidationTests
{
    [Fact]
    public void Identifiant_vide_produit_un_seul_diagnostic_specifique()
    {
        var definition = new ExperimentDefinition
        {
            Id = string.Empty,
            RunCount = 1,
            Ticks = 1,
            Simulation = "ecosystem_01",
        };

        var identifierProblems = definition.Validate()
            .Where(problem => problem.Contains("identifiant de campagne", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["identifiant de campagne manquant"], identifierProblems);
    }

    [Fact]
    public void Identifiant_avec_separateur_de_chemin_est_refuse()
    {
        var definition = new ExperimentDefinition
        {
            Id = "../outside",
            RunCount = 1,
            Ticks = 1,
            Simulation = "ecosystem_01",
        };

        Assert.Contains(definition.Validate(), problem => problem.Contains("identifiant de campagne invalide", StringComparison.Ordinal));
    }
}
