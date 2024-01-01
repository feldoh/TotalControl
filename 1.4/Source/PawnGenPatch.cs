using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace FactionLoadout;

[HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
public static class PawnGenPatchCore
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __result, PawnGenerationRequest request)
    {
        if (__result?.kindDef?.GetModExtension<PawnKindEdit.ForcedHediffModExtension>() is not { } ext) return;

        foreach (PawnKindEdit.ForcedHediff forcedHediff in ext.forcedHediffs)
        {
            Stack<BodyPartRecord> validParts = forcedHediff.parts == null || forcedHediff.parts.Count == 0
                ? null
                : new Stack<BodyPartRecord>(__result.health.hediffSet.GetNotMissingParts().Where(p => forcedHediff.parts.Contains(p.def)).InRandomOrder());

            int maxToApply = Math.Min(forcedHediff.maxParts, validParts?.Count ?? 1);
            for (int i = 0; i < maxToApply; i++)
            {
                if (!Rand.Chance(forcedHediff.chance)) continue;
                if (__result.health.hediffSet.GetHediffCount(forcedHediff.hediffDef) >= forcedHediff.maxParts) break;
                {
                    Hediff hediff = HediffMaker.MakeHediff(forcedHediff.hediffDef, __result, validParts?.Pop());
                    __result.health.AddHediff(hediff);
                }
            }
        }
    }
}
