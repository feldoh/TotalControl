using System.Collections.Generic;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Faction-level GiddyUp configuration, corresponding to GiddyUp's FactionRestrictions DefModExtension.
/// All fields are nullable: null means "no override" (GiddyUp uses its own defaults).
/// </summary>
public class GiddyUpFactionData
{
    /// <summary>Faction-wide mount chance (0-100). Null = use GiddyUp global settings.</summary>
    public int? MountChance;

    /// <summary>Weight multiplier for wild animal selection (0+). Null = use GiddyUp default.</summary>
    public int? WildAnimalWeight;

    /// <summary>Weight multiplier for domestic animal selection (0+). Null = use GiddyUp default.</summary>
    public int? NonWildAnimalWeight;

    /// <summary>Whitelist of wild animal defNames this faction can mount. Null/empty = no restriction.</summary>
    public List<string> AllowedWildAnimals;

    /// <summary>Whitelist of domestic animal defNames this faction can mount. Null/empty = no restriction.</summary>
    public List<string> AllowedNonWildAnimals;
}
