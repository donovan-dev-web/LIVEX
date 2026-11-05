namespace Prism.Core.World;

/// <summary>
/// Independent on/off levers used by the ablation experiments (PRISM-101). Every lever defaults
/// to <c>true</c> (feature enabled); setting one to <c>false</c> disables that mechanism in
/// isolation so its contribution can be measured alone. Levers are pure configuration: they do
/// not change content, recipes or the world, only whether a mechanism takes effect.
/// </summary>
public sealed record AblationMechanisms(
    bool Shelter = true,
    bool WaterAccess = true,
    bool Storage = true,
    bool Production = true,
    bool Wear = true,
    bool Mastery = true)
{
    public static AblationMechanisms AllEnabled { get; } = new();

    public static AblationMechanisms AllDisabled { get; } = new(false, false, false, false, false, false);

    public bool IsEnabled(string mechanism) =>
        mechanism switch
        {
            "shelter" => Shelter,
            "water_access" => WaterAccess,
            "water" => WaterAccess,
            "storage" => Storage,
            "production" => Production,
            "wear" => Wear,
            "mastery" => Mastery,
            _ => true,
        };
}