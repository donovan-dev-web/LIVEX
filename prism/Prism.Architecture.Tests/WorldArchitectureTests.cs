using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prism.Architecture.Tests;

public sealed class WorldArchitectureTests
{
    [Fact]
    public void CoreHasNoExternalOrProjectReferences()
    {
        var projectPath = Path.Combine(FindPrismRoot(), "Prism.Core", "Prism.Core.csproj");
        var project = System.Xml.Linq.XDocument.Load(projectPath);

        Assert.DoesNotContain(project.Descendants(), element =>
            element.Name.LocalName is "PackageReference" or "ProjectReference");
    }

    [Fact]
    public void CoreDoesNotReferenceGraphicsLibraries()
    {
        var coreDirectory = Path.Combine(FindPrismRoot(), "Prism.Core");
        var forbiddenNamespaces = new[] { "Silk.NET", "OpenGL", "GLFW" };

        foreach (var sourcePath in EnumerateCoreSources(coreDirectory))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetRoot();
            var namespaces = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(directive => directive.Name?.ToString() ?? string.Empty);

            Assert.DoesNotContain(namespaces, name =>
                forbiddenNamespaces.Any(prefix =>
                    name.Equals(prefix, StringComparison.Ordinal) ||
                    name.StartsWith(prefix + ".", StringComparison.Ordinal)));
        }
    }

    [Theory]
    [InlineData("class World { float Value; }")]
    [InlineData("class World { double Value; }")]
    [InlineData("class World { System.DateTime Value => System.DateTime.Now; }")]
    [InlineData("class World { System.DateTime Value => System.DateTime.UtcNow; }")]
    [InlineData("class World { System.Random Value = new(); }")]
    public void DeterminismGuardDetectsForbiddenTypesAndClockAccess(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        Assert.NotNull(FindDeterminismViolation(tree.GetRoot()));
    }

    [Fact]
    public void CoreSourcesPassDeterminismGuard()
    {
        var coreDirectory = Path.Combine(FindPrismRoot(), "Prism.Core");
        var violations = new List<string>();
        foreach (var sourcePath in EnumerateCoreSources(coreDirectory))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetRoot();
            var violation = FindDeterminismViolation(root);
            if (violation is not null)
            {
                violations.Add($"{sourcePath}: {violation}");
            }
        }

        Assert.DoesNotContain(violations, violation => violation.Length > 0);
    }

    private static string? FindDeterminismViolation(SyntaxNode root)
    {
        if (root.DescendantTokens().Any(token =>
                token.IsKind(SyntaxKind.FloatKeyword) || token.IsKind(SyntaxKind.DoubleKeyword)))
        {
            return "floating-point type";
        }

        if (root.DescendantNodes().OfType<TypeSyntax>().Any(type =>
                type.ToString() is "Random" or "System.Random"))
        {
            return "System.Random";
        }

        if (root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(access =>
                (access.Name.Identifier.ValueText is "Now" or "UtcNow") &&
                access.Expression.ToString().EndsWith("DateTime", StringComparison.Ordinal)))
        {
            return "system clock access";
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCoreSources(string coreDirectory) =>
        Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the test output directory.");
    }
}
