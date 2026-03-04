using Verse;

namespace TotalControlCECompat;

/// <summary>
/// Module data for a single PawnKindEdit's Combat Extended loadout configuration.
/// </summary>
public class CEData
{
    /// <summary>DefName of an AmmoCategoryDef to force. Null = no override (CE picks randomly).</summary>
    public string ForcedAmmoCategoryDefName;

    /// <summary>How many extra primary magazines the pawn carries. Null = no override.</summary>
    public FloatRange? PrimaryMagazineCount;

    /// <summary>Minimum ammo count. Null = no override.</summary>
    public int? MinAmmoCount;
}
