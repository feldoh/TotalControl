using System.Collections.Generic;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Module data for a single PawnKindEdit's GiddyUp mount configuration.
/// </summary>
public class GiddyUpData
{
    public int? MountChance;
    public Dictionary<string, int> PossibleMounts; // defName -> weight
}
