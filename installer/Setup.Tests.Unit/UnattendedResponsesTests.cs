// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.CommandLine;
using Setup.Core.Execution;
using Setup.Core.Simulation;
using Setup.Tests.Support;
using Xunit;

namespace Setup.Tests.Unit;

/// <summary>
/// Fichier de réponses de <c>--unattended --config</c> : aucun chemin deviné,
/// toute valeur manquante ou invalide est refusée (ARCHITECTURE §18, INS-F-91).
/// </summary>
public class UnattendedResponsesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "livex-unattended-" + Guid.NewGuid().ToString("N"));

    public UnattendedResponsesTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { /* nettoyage au mieux */ }
    }

    private string Write(string json)
    {
        var path = Path.Combine(_directory, "responses.json");
        File.WriteAllText(path, json);
        return path;
    }

    private bool Load(string json, out UnattendedResponses? responses, out IReadOnlyList<string> errors) =>
        UnattendedResponsesFile.TryLoad(
            Write(json), DemoWorld.CreateManifest(), @"C:\Users\demo\AppData\Roaming\LIVEX", false,
            out responses, out errors);

    [Fact]
    public void Un_Fichier_Complet_Construit_La_Requete()
    {
        var ok = Load("""
            {
              "installDir": "C:\\Users\\demo\\AppData\\Local\\Programs\\LIVEX",
              "workspaceDir": "D:\\LIVEX\\workspace",
              "components": ["launcher", "prism"],
              "acceptLicense": true,
              "desktopShortcut": true,
              "launchAfterInstall": false
            }
            """, out var responses, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.NotNull(responses);
        Assert.True(responses!.AcceptLicense);
        Assert.True(responses.CreateDesktopShortcut);
        Assert.Equal(@"C:\Users\demo\AppData\Local\Programs\LIVEX", responses.InstallDir);
        // Les composants obligatoires sont toujours ajoutés.
        Assert.Contains("syne", responses.Components);
        Assert.Contains("echos", responses.Components);
    }

    [Fact]
    public void Un_Chemin_Relatif_Est_Refuse()
    {
        var ok = Load("""
            { "installDir": "LIVEX", "workspaceDir": "D:\\LIVEX\\workspace", "acceptLicense": true }
            """, out var responses, out var errors);

        Assert.False(ok);
        Assert.Null(responses);
        Assert.Contains(errors, error => error.Contains("chemin absolu", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_Composant_Inconnu_Est_Refuse()
    {
        var ok = Load("""
            {
              "installDir": "C:\\LIVEX\\app",
              "workspaceDir": "C:\\LIVEX\\workspace",
              "acceptLicense": true,
              "components": ["prism-xyz"]
            }
            """, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Contains("prism-xyz", StringComparison.Ordinal));
    }

    [Fact]
    public void Une_Licence_Non_Acceptee_Echoue()
    {
        var ok = Load("""
            { "installDir": "C:\\LIVEX\\app", "workspaceDir": "C:\\LIVEX\\workspace" }
            """, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Contains("acceptLicense", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_Fichier_Absent_Echoue_Explicitement()
    {
        var ok = UnattendedResponsesFile.TryLoad(
            Path.Combine(_directory, "absent.json"), DemoWorld.CreateManifest(),
            Path.GetTempPath(), false, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Contains("introuvable", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_Json_Invalide_Echoue_Explicitement()
    {
        var ok = Load("{ pas du json", out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Contains("illisible", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_Fichier_Invalide_Retourne_Le_Code_2_Sans_Executer()
    {
        var (engine, _) = TestEngine.Create();
        var output = new StringWriter();
        var path = Write("{ \"installDir\": \"relatif\" }");

        var outcome = await HeadlessRunner.InstallFromResponsesAsync(
            engine, DemoWorld.CreateManifest(), path, Path.GetTempPath(), false, output);

        Assert.Equal(ExitCodes.PreflightFailed, outcome.ExitCode);
        Assert.Contains("[Error]", output.ToString(), StringComparison.Ordinal);
    }
}
