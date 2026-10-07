using Launcher.Domain;
using Launcher.Domain.Model;
using Xunit;

namespace Launcher.Tests.EndToEnd;

[Collection("Composition")]
public sealed class EngineStartupEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"livex-engine-{Guid.NewGuid():N}");
    private readonly string _componentsParent;

    public EngineStartupEndToEndTests()
    {
        _componentsParent = Path.Combine(_root, "components");
        StubInstall.Install(_componentsParent, "syne", "Stub.Syne", Manifests.Syne);
    }

    [Fact]
    public async Task Demarrage_supervise_passe_le_scenario_SYNE_de_reference()
    {
        using var composition = new Launcher.App.Composition.LauncherComposition(
            Path.Combine(_root, "packages"),
            _componentsParent,
            Path.Combine(_root, "data"));
        composition.DetectComponents();

        var startError = await composition.Facade.ToggleComponentAsync("syne", true);
        Assert.Null(startError);

        var instance = composition.Orchestration.Registry.FindByComponent("syne");
        Assert.NotNull(instance);
        try
        {
            await StubInstall.WaitForHealthyAsync(instance!.Endpoints["control"].Url);

            var launchPath = Path.Combine(
                _root, "data", "workspace", "sessions", instance.InstanceId, "launch.json");
            using var launch = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(launchPath));

            Assert.Equal(
                WellKnownSimulations.Reference,
                launch.RootElement.GetProperty("simulation").GetString());
        }
        finally
        {
            await composition.Facade.ToggleComponentAsync("syne", false);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
