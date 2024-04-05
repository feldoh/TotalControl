﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout;

[HarmonyPatch(typeof(PawnGenerator), "GetBodyTypeFor")]
public static class PawnGenPatchBodyTypeDef
{
    [HarmonyPostfix]
    public static void Postfix(ref BodyTypeDef __result, Pawn pawn)
    {
        PawnKindEdit.GetEditsFor(pawn.kindDef).SelectMany(e => e.BodyTypes ?? []).TryRandomElement(out BodyTypeDef bodyTypeDef);
        if (bodyTypeDef != null)
        {
            __result = bodyTypeDef;
        }
    }
}

[HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
public static class PawnGenPatchCore
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __result, PawnGenerationRequest request)
    {
        if (__result?.kindDef?.GetModExtension<ForcedHediffModExtension>() is not { } ext) return;

        foreach (ForcedHediff forcedHediff in ext.forcedHediffs)
        {
            if (forcedHediff.HediffDef == null || !Rand.Chance(forcedHediff.chance)) continue;
            Stack<BodyPartRecord> validParts = forcedHediff.parts == null || forcedHediff.parts.Count == 0
                ? null
                : new Stack<BodyPartRecord>(__result.health.hediffSet.GetNotMissingParts().Where(p => forcedHediff.parts.Contains(p.def)).InRandomOrder());

            int maxToApply = Math.Min(forcedHediff.PartsToHit(), validParts?.Count ?? 1);
            for (int i = 0; i < maxToApply; i++)
            {
                if (__result.health.hediffSet.GetHediffCount(forcedHediff.HediffDef) >= maxToApply) break;
                {
                    Hediff hediff = HediffMaker.MakeHediff(forcedHediff.HediffDef, __result, validParts?.Pop());
                    __result.health.AddHediff(hediff);
                }
            }
        }
    }
}