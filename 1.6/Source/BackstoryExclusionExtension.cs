using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Attached to cloned PawnKindDefs at Apply time when specific backstory defs are excluded.
/// Holds precomputed candidate lists so that <see cref="HarmonyPatches.BackstoryGenPatch"/>
/// can swap excluded backstories cheaply without scanning the entire DefDatabase per pawn.
/// </summary>
public class BackstoryExclusionExtension : DefModExtension
{
    /// <summary>Specific BackstoryDefs that must not appear on this pawn kind.</summary>
    public HashSet<BackstoryDef> excludedDefs = [];

    /// <summary>
    /// Precomputed valid childhood backstories (matching the def's active categories, minus exclusions).
    /// Null when there are no valid childhood candidates (shouldn't normally happen).
    /// </summary>
    public List<BackstoryDef> validChildhood;

    /// <summary>
    /// Precomputed valid adulthood backstories (matching the def's active categories, minus exclusions).
    /// Null when there are no valid adulthood candidates (shouldn't normally happen).
    /// </summary>
    public List<BackstoryDef> validAdulthood;
}
