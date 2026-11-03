using System.Runtime.InteropServices;
using Silk.NET.GLFW;

namespace Prism.Viewer;

public static class LinuxGlfwPlatform
{
    // GLFW 3.4 defines these hints; Silk.NET 2.22 does not expose them yet.
    private const int GlfwPlatformHint = 0x00050003;
    private const int GlfwPlatformWayland = 0x00060003;
    private const int GlfwPlatformX11 = 0x00060004;
    private static nint _nativeLibraryHandle;
    private static readonly GlfwCallbacks.ErrorCallback ErrorCallback =
        (error, description) =>
            Console.Error.WriteLine($"GLFW {error}: {description}");

    public static string? GetBundledLibraryPath(
        string applicationDirectory,
        Architecture architecture)
    {
        var architectureName = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => null
        };
        if (architectureName is null)
        {
            return null;
        }

        return Path.Combine(
            applicationDirectory,
            "runtimes",
            $"linux-{architectureName}",
            "native",
            "libglfw.so.3");
    }

    public static bool ShouldPreferWayland(
        bool isLinux,
        string? waylandDisplay,
        string? sessionType) =>
        isLinux &&
        !string.IsNullOrWhiteSpace(waylandDisplay) &&
        string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldPreferX11(
        bool isLinux,
        string? display,
        string? waylandDisplay,
        string? sessionType) =>
        isLinux &&
        !string.IsNullOrWhiteSpace(display) &&
        (string.IsNullOrWhiteSpace(waylandDisplay) ||
         string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase));

    public static void Configure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        PreloadBundledLibrary();

        var glfw = Glfw.GetApi();
        glfw.SetErrorCallback(ErrorCallback);
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");

        if (ShouldPreferWayland(true, waylandDisplay, sessionType))
        {
            glfw.InitHint((InitHint)GlfwPlatformHint, GlfwPlatformWayland);
        }
        else if (ShouldPreferX11(true, display, waylandDisplay, sessionType))
        {
            glfw.InitHint((InitHint)GlfwPlatformHint, GlfwPlatformX11);
        }
    }

    private static void PreloadBundledLibrary()
    {
        if (_nativeLibraryHandle != 0)
        {
            return;
        }

        var bundledLibrary = GetBundledLibraryPath(
            AppContext.BaseDirectory,
            RuntimeInformation.ProcessArchitecture);
        if (bundledLibrary is not null && File.Exists(bundledLibrary))
        {
            _nativeLibraryHandle = NativeLibrary.Load(bundledLibrary);
        }
    }
}
