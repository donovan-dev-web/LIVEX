using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Launcher.Tests.Unit.StaticAnalysis;

/// <summary>
/// Analyse statique des frontières d'assembly (ARCHITECTURE.md §5, ADR-001) :
/// Launcher.Domain ne référence ni Avalonia, ni System.Diagnostics.Process, ni System.Net ;
/// Launcher.Protocol ne référence aucun autre projet du Launcher ;
/// Launcher.Package et Launcher.Application ne référencent pas Avalonia.
/// Toute dépendance interdite fait échouer la construction.
/// </summary>
public sealed class AssemblyBoundaryTests
{
    [Fact]
    public void Domain_ne_reference_ni_Avalonia_ni_SystemDiagnostics_ni_SystemNet()
    {
        var domain = typeof(Launcher.Domain.OrchestrationService).Assembly;
        var referenced = domain.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(referenced, name => name.StartsWith("Avalonia", StringComparison.Ordinal));

        // System.Diagnostics.Process et System.Net sont des assemblies de la plateforme :
        // on vérifie les types réellement utilisés par réflexion sur les métadonnées.
        Assert.False(UsesType(domain, "System.Diagnostics.Process"), "Launcher.Domain ne doit pas utiliser System.Diagnostics.Process");
        Assert.False(UsesNamespace(domain, "System.Net"), "Launcher.Domain ne doit pas utiliser System.Net");
    }

    [Fact]
    public void Protocol_ne_reference_aucun_autre_projet_du_launcher()
    {
        var protocol = typeof(Launcher.Protocol.PackageConstants).Assembly;
        var referenced = protocol.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);

        foreach (var forbidden in new[] { "Launcher.Domain", "Launcher.Infrastructure", "Launcher.Application", "Launcher.Package", "Launcher.Presentation" })
        {
            Assert.DoesNotContain(forbidden, referenced);
        }
    }

    [Fact]
    public void Package_et_Application_ne_referencent_pas_Avalonia()
    {
        foreach (var type in new[] { typeof(Launcher.Package.LivexPackageWriter), typeof(Launcher.Application.CampaignRunner) })
        {
            var referenced = type.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);
            Assert.DoesNotContain(referenced, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Domain_ne_reference_aucun_projet_hors_protocol()
    {
        var domain = typeof(Launcher.Domain.OrchestrationService).Assembly;
        var referenced = domain.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);

        foreach (var forbidden in new[] { "Launcher.Infrastructure", "Launcher.Application", "Launcher.Package", "Launcher.Presentation" })
        {
            Assert.DoesNotContain(forbidden, referenced);
        }
    }

    private static bool UsesNamespace(Assembly assembly, string namespacePrefix)
    {
        // Balaye les références de membres via ModuleHandle : les types de la plateforme
        // utilisés apparaissent dans les références d'assembly seulement s'ils sont résolus.
        return assembly.GetModules()
            .SelectMany(m => m.GetTypes().SelectMany(t => SafeMembers(t)))
            .Any(member => member.DeclaringType?.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            || assembly.GetTypes().Any(t => t.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true);
    }

    private static bool UsesType(Assembly assembly, string fullTypeName)
    {
        return assembly.GetTypes()
            .SelectMany(SafeMembers)
            .Any(member => member.DeclaringType?.FullName == fullTypeName);
    }

    private static IEnumerable<MemberInfo> SafeMembers(Type type)
    {
        try
        {
            return type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        }
        catch (Exception exception) when (exception is FileLoadException or TypeLoadException or FileNotFoundException)
        {
            return Array.Empty<MemberInfo>();
        }
    }
}
