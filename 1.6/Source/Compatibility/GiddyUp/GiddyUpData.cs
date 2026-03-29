using System.Collections.Generic;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Module data for a single PawnKindEdit's GiddyUp mount configuration.
/// </summary>
public class GiddyUpData
{
    public int? MountChance;
    public bool? DisableMounts; // When true, force no mounts (writes -1 to GiddyUp's CustomMounts.mountChance)
    public Dictionary<string, int> PossibleMounts; // defName -> weight
}
