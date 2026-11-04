namespace Prism.Core.Actions;

/// <summary>
/// Spoilage rates for containers: the base rate applies everywhere, and the storage slowdown is
/// subtracted (in permille) for containers that sit inside an active <c>storage</c> structure such as
/// a granary.
/// </summary>
public sealed record ContainerDecayRules(
    int BasePermillePerTick = 1_000,
    int StorageSlowdownPermille = 0)
{
    public void Validate()
    {
        if (BasePermillePerTick is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(BasePermillePerTick));
        }

        if (StorageSlowdownPermille is < 0 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(StorageSlowdownPermille));
        }
    }

    /// <summary>
    /// Effective decay for one tick. A slowdown of 500 halves the rate: 1 000 ‰/tick becomes 500.
    /// </summary>
    public int EffectivePermillePerTick(bool insideStorage)
    {
        if (!insideStorage || StorageSlowdownPermille == 0)
        {
            return BasePermillePerTick;
        }

        return BasePermillePerTick * (1_000 - StorageSlowdownPermille) / 1_000;
    }
}
