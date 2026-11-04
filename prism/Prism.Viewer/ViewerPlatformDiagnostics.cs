namespace Prism.Viewer;

public static class ViewerPlatformDiagnostics
{
    public static string? GetMissingLinuxDisplayMessage(
        bool isLinux,
        string? display,
        string? waylandDisplay)
    {
        if (!isLinux ||
            !string.IsNullOrWhiteSpace(display) ||
            !string.IsNullOrWhiteSpace(waylandDisplay))
        {
            return null;
        }

        return "PRISM Viewer needs an active X11 or Wayland display. " +
            "Run it from a graphical Linux desktop terminal, or configure " +
            "DISPLAY/WAYLAND_DISPLAY when using a remote session.";
    }

    public static string GetWindowPlatformFailureMessage(string detail) =>
        "Silk.NET could not initialize a window platform. Run the viewer " +
        "from a graphical Linux desktop session and verify access to " +
        "DISPLAY or WAYLAND_DISPLAY. Backend detail: " + detail;

    public static string GetMissingGlfwLibraryMessage(
        string detail,
        string runtimeIdentifier,
        string? bundledLibraryPath,
        bool bundledLibraryExists)
    {
        var bundleStatus = bundledLibraryPath is null
            ? $"No bundled GLFW asset is defined for runtime '{runtimeIdentifier}'."
            : bundledLibraryExists
                ? $"Bundled GLFW asset exists at '{bundledLibraryPath}' but could not be loaded."
                : $"Bundled GLFW asset is missing at '{bundledLibraryPath}'.";

        return "PRISM Viewer could not load the native GLFW library. " +
            bundleStatus +
            " From the prism directory, run " +
            "'dotnet clean Prism.Viewer/Prism.Viewer.csproj && " +
            "dotnet restore Prism.Viewer/Prism.Viewer.csproj', then retry. " +
            "If the file exists but cannot load, check dependencies with " +
            "'ldd <path-to-libglfw.so.3>' and on Ubuntu install the system " +
            "fallback with 'sudo apt install libglfw3'. Backend detail: " +
            detail;
    }
}
