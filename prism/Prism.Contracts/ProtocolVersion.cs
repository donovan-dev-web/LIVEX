using System.Globalization;

namespace Prism.Contracts;

public readonly record struct ProtocolVersion(int Major, int Minor, int Patch)
{
    public static ProtocolVersion Current { get; } = new(1, 0, 0);

    public bool IsCompatibleWith(ProtocolVersion supported) =>
        Major == supported.Major;

    public static bool TryParse(string? value, out ProtocolVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 3 ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
        {
            return false;
        }

        version = new ProtocolVersion(major, minor, patch);
        return true;
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");

    private static bool TryParsePart(string value, out int result)
    {
        result = 0;
        return value.Length > 0 &&
               (value.Length == 1 || value[0] != '0') &&
               value.All(character => character is >= '0' and <= '9') &&
               int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }
}
