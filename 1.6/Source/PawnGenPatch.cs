using System;
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
        PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def).SelectMany(e => e.BodyTypes ?? []).TryRandomElement(out BodyTypeDef bodyTypeDef);
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
        if ((__result?.kindDef?.GetModExtension<ForcedExtrasModExtension>() ?? __result?.kindDef?.GetModExtension<ForcedHediffModExtension>()) is not { } ext)
            return;

        foreach (ForcedHediff forcedHediff in ext.forcedHediffs)
        {
            if (forcedHediff.HediffDef == null || !Rand.Chance(forcedHediff.chance))
                continue;
            Stack<BodyPartRecord> validParts =
                forcedHediff.parts == null || forcedHediff.parts.Count == 0
                    ? null
                    : new Stack<BodyPartRecord>(__result.health.hediffSet.GetNotMissingParts().Where(p => forcedHediff.parts.Contains(p.def)).InRandomOrder());

            int maxToApply = Math.Min(forcedHediff.PartsToHit(), validParts?.Count ?? 1);
            for (int i = 0; i < maxToApply; i++)
            {
                if (__result.health.hediffSet.GetHediffCount(forcedHediff.HediffDef) >= maxToApply)
                    break;
                {
                    Hediff hediff = HediffMaker.MakeHediff(forcedHediff.HediffDef, __result, validParts?.Pop());
                    __result.health.AddHediff(hediff);
                }
            }
        }

        foreach (ForcedGene forcedGene in ext.forcedGenes)
        {
            if (forcedGene.GeneDef == null || !Rand.Chance(forcedGene.chance))
                continue;

            Gene newGene = __result.genes?.AddGene(forcedGene.GeneDef, xenogene: false);
            if (forcedGene.forceActive && newGene != null)
            {
                newGene.OverrideBy(null);
                foreach (Gene gene in __result.genes.GenesListForReading)
                {
                    if (gene != newGene && gene.def.ConflictsWith(newGene.def))
                    {
                        __result.genes.RemoveGene(gene);
                    }
                }
            }
        }
    }
}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateRandomAge))]
public static class PawnGenAgePatchCore
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, ref PawnGenerationRequest request)
    {
        if (!pawn.RaceProps.Humanlike || request.AllowedDevelopmentalStages.Newborn())
            return true;
        int? minAge = null;
        int? maxAge = null;
        foreach (PawnKindEdit pawnKindEdit in PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def))
        {
            if (pawnKindEdit.MinGenerationAge != null && (!pawnKindEdit.IsGlobal || minAge == null))
                minAge = pawnKindEdit.MinGenerationAge;
            if (pawnKindEdit.MaxGenerationAge != null && (!pawnKindEdit.IsGlobal || maxAge == null))
                maxAge = pawnKindEdit.MaxGenerationAge;
        }

        if (minAge == null && maxAge == null)
            return true;
        FloatRange allowedAges = new(minAge ?? pawn.kindDef.minGenerationAge, maxAge ?? pawn.kindDef.maxGenerationAge);
        request.FixedBiologicalAge = allowedAges.RandomInRange;
        request.AllowedDevelopmentalStages = LifeStageUtility.CalculateDevelopmentalStage(pawn, (float)request.FixedBiologicalAge);
        if (request.FixedChronologicalAge.HasValue && request.FixedBiologicalAge.GetValueOrDefault() > (double)request.FixedChronologicalAge.GetValueOrDefault())
        {
            request.FixedChronologicalAge = request.FixedBiologicalAge;
        }

        return true;
    }
}

[HarmonyPatch(typeof(PawnGenerationRequest), nameof(PawnGenerationRequest.KindDef), MethodType.Getter)]
public static class PawnGenRequestKindPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref PawnKindDef __result, PawnGenerationRequest __instance)
    {
        if (__result == null || __instance.Faction == null)
            return;
        __result = FactionEdit.GetReplacementForPawnKind(__instance.Faction.def, __result);
    }
}

[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetupRecruitable))]
public static class PawnGenPatchRecruitable
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn_GuestTracker __instance)
    {
        if (__instance.pawn.Faction == null)
            return true;
        float? maxUnwaveringlyLoyalChance = null;
        foreach (PawnKindEdit pawnKindEdit in PawnKindEdit.GetEditsFor(__instance.pawn.kindDef, __instance.pawn.Faction?.def))
        {
            if (pawnKindEdit.UnwaveringlyLoyalChance != null && (!pawnKindEdit.IsGlobal || maxUnwaveringlyLoyalChance == null))
            {
                maxUnwaveringlyLoyalChance = pawnKindEdit.UnwaveringlyLoyalChance ?? 0;
            }
        }
        if (maxUnwaveringlyLoyalChance == null)
            return true;

        __instance.Recruitable = !Rand.Chance(maxUnwaveringlyLoyalChance.Value);
        return false;
    }
}
