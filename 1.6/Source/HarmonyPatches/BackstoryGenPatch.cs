using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout.HarmonyPatches;

/// <summary>
/// Postfix on <see cref="PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo"/> to enforce
/// specific backstory exclusions that cannot be expressed through <see cref="BackstoryCategoryFilter"/> alone.
/// Category-based exclusions are already handled at the def level via <see cref="PawnKindEdit.ApplyBackstoryExclusions"/>.
/// This patch covers the case where a user excludes a specific <see cref="BackstoryDef"/> by def name,
/// using precomputed candidate lists from <see cref="BackstoryExclusionExtension"/> for O(1) lookups.
/// </summary>
[HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
public static class BackstoryGenPatch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, FactionDef factionType)
    {
        if (pawn?.story == null || pawn.kindDef == null)
        {
            return;
        }

        // Fast path: check for precomputed exclusion data on this pawn kind.
        BackstoryExclusionExtension ext = pawn.kindDef.GetModExtension<BackstoryExclusionExtension>();
        if (ext?.excludedDefs == null || ext.excludedDefs.Count == 0) return;

        if (pawn.story.Childhood != null && ext.excludedDefs.Contains(pawn.story.Childhood))
        {
            if (ext.validChildhood is { Count: > 0 })
            {
                BackstoryDef replacement = ext.validChildhood.RandomElement();
                ModCore.Debug($"Replaced excluded childhood backstory {pawn.story.Childhood.defName} with {replacement.defName} for {pawn.kindDef.defName}");
                pawn.story.Childhood = replacement;
            }
            else
            {
                ModCore.Warn($"No replacement found for excluded childhood backstory {pawn.story.Childhood.defName} on {pawn.kindDef.defName}; precomputed candidates empty.");
            }
        }

        if (pawn.story.Adulthood != null && ext.excludedDefs.Contains(pawn.story.Adulthood))
        {
            if (ext.validAdulthood is { Count: > 0 })
            {
                BackstoryDef replacement = ext.validAdulthood.RandomElement();
                ModCore.Debug($"Replaced excluded adulthood backstory {pawn.story.Adulthood.defName} with {replacement.defName} for {pawn.kindDef.defName}");
                pawn.story.Adulthood = replacement;
            }
            else
            {
                ModCore.Warn($"No replacement found for excluded adulthood backstory {pawn.story.Adulthood.defName} on {pawn.kindDef.defName}; precomputed candidates empty.");
            }
        }
    }
}
