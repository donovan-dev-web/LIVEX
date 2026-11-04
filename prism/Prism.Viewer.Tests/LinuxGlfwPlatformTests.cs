using System.Runtime.InteropServices;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class LinuxGlfwPlatformTests
{
    [Theory]
    [InlineData(Architecture.X64, "linux-x64")]
    [InlineData(Architecture.Arm64, "linux-arm64")]
    [InlineData(Architecture.Arm, "linux-arm")]
    public void ResolvesBundledGlfwPathByLinuxArchitecture(
        Architecture architecture,
        string runtime)
    {
        var path = LinuxGlfwPlatform.GetBundledLibraryPath("/app", architecture);

        Assert.Equal(
            Path.Combine("/app", "runtimes", runtime, "native", "libglfw.so.3"),
            path);
    }

    [Fact]
    public void DoesNotResolveBundledGlfwForUnsupportedArchitecture()
    {
        Assert.Null(LinuxGlfwPlatform.GetBundledLibraryPath("/app", Architecture.X86));
    }

    [Fact]
    public void PrefersNativeWaylandInWaylandSessionEvenWhenXwaylandIsAvailable()
    {
        Assert.True(LinuxGlfwPlatform.ShouldPreferWayland(
            isLinux: true,
            waylandDisplay: "wayland-0",
            sessionType: "wayland"));
    }

    [Theory]
    [InlineData(false, "wayland-0", "wayland")]
    [InlineData(true, null, "wayland")]
    [InlineData(true, "wayland-0", "x11")]
    [InlineData(true, "wayland-0", null)]
    public void DoesNotPreferWaylandOutsideAnIdentifiedWaylandSession(
        bool isLinux,
        string? waylandDisplay,
        string? sessionType)
    {
        Assert.False(LinuxGlfwPlatform.ShouldPreferWayland(
            isLinux,
            waylandDisplay,
            sessionType));
    }

    [Fact]
    public void PrefersX11InX11SessionEvenWhenWaylandVariablesAreAlsoPresent()
    {
        Assert.True(LinuxGlfwPlatform.ShouldPreferX11(
            isLinux: true,
            display: ":0",
            waylandDisplay: "wayland-0",
            sessionType: "x11"));
    }

    [Theory]
    [InlineData(false, ":0", null, "x11")]
    [InlineData(true, null, "wayland-0", "x11")]
    [InlineData(true, ":0", "wayland-0", "wayland")]
    [InlineData(true, ":0", "wayland-0", null)]
    public void DoesNotPreferX11OutsideAnIdentifiedX11Session(
        bool isLinux,
        string? display,
        string? waylandDisplay,
        string? sessionType)
    {
        Assert.False(LinuxGlfwPlatform.ShouldPreferX11(
            isLinux,
            display,
            waylandDisplay,
            sessionType));
    }
}
