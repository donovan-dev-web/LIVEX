using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerPlatformDiagnosticsTests
{
    [Fact]
    public void MissingDisplayVariablesOnlyWarnForLinux()
    {
        Assert.NotNull(ViewerPlatformDiagnostics.GetMissingLinuxDisplayMessage(
            isLinux: true,
            display: null,
            waylandDisplay: null));
        Assert.Null(ViewerPlatformDiagnostics.GetMissingLinuxDisplayMessage(
            isLinux: false,
            display: null,
            waylandDisplay: null));
    }

    [Theory]
    [InlineData(":0", null)]
    [InlineData(null, "wayland-0")]
    public void X11OrWaylandDisplayMakesSessionAvailable(string? display, string? waylandDisplay)
    {
        Assert.Null(ViewerPlatformDiagnostics.GetMissingLinuxDisplayMessage(
            isLinux: true,
            display,
            waylandDisplay));
    }

    [Fact]
    public void WindowPlatformFailureMessageIncludesBackendDetailAndNextStep()
    {
        var message = ViewerPlatformDiagnostics.GetWindowPlatformFailureMessage(
            "GlfwPlatform - not applicable");

        Assert.Contains("graphical Linux desktop session", message);
        Assert.Contains("DISPLAY or WAYLAND_DISPLAY", message);
        Assert.Contains("GlfwPlatform - not applicable", message);
    }

    [Fact]
    public void MissingGlfwLibraryMessageIncludesRuntimeAssetAndRecoverySteps()
    {
        var message = ViewerPlatformDiagnostics.GetMissingGlfwLibraryMessage(
            "Could not load GLFW",
            "ubuntu.24.04-x64",
            "/app/runtimes/linux-x64/native/libglfw.so.3",
            bundledLibraryExists: true);

        Assert.Contains("Bundled GLFW asset exists at", message);
        Assert.Contains("/app/runtimes/linux-x64/native/libglfw.so.3", message);
        Assert.Contains("dotnet restore Prism.Viewer/Prism.Viewer.csproj", message);
        Assert.Contains("sudo apt install libglfw3", message);
        Assert.Contains("Could not load GLFW", message);
    }
}
