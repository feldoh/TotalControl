using Verse;

namespace TotalControlMechsuitCompat;

/// <summary>
/// Per-PawnKindEdit data for MechsuitFramework configuration.
/// </summary>
public class MechsuitData
{
    /// <summary>
    /// Override for the structure point (health) range on spawn.
    /// Null means "use the def default or framework default (1, 1)".
    /// </summary>
    public FloatRange? StructurePointRange;
}
