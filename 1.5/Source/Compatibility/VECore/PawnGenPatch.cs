using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;
using VFECore.Abilities;
using AbilityDef = VFECore.Abilities.AbilityDef;

namespace TotalControlVEPsycastsCompat;

[HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
[HarmonyAfter("OskarPotocki.VanillaPsycastsExpanded")]
public static class PawnGenPatch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __result, PawnGenerationRequest request)
    {
        if (__result == null || request.AllowedDevelopmentalStages.Newborn())
            return;

        PawnKindAbilityExtension_Psycasts psycastExtension = __result.kindDef.GetModExtension<PawnKindAbilityExtension_Psycasts>();

        CompAbilities comp = null;

        if (psycastExtension == null)
            return;
        comp = __result.GetComp<CompAbilities>();

        Hediff_Psylink psylink = __result.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier) as Hediff_Psylink;

        if (psylink == null)
        {
            psylink = HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, __result, __result.health.hediffSet.GetBrain()) as Hediff_Psylink;
            __result.health.AddHediff(psylink);
        }

        Hediff_PsycastAbilities implant =
            __result.health.hediffSet.GetFirstHediffOfDef(VPE_DefOf.VPE_PsycastAbilityImplant) as Hediff_PsycastAbilities
            ?? HediffMaker.MakeHediff(VPE_DefOf.VPE_PsycastAbilityImplant, __result, __result.RaceProps.body.GetPartsWithDef(VPE_DefOf.Brain).FirstOrFallback())
                as Hediff_PsycastAbilities;

        if (implant == null)
            return;
        if (implant.psylink == null || (psycastExtension.giveRandomAbilities && implant.unlockedPaths?.Count == 0))
        {
            implant.InitializeFromPsylink(psylink);
            implant.SetLevelTo(psycastExtension.initialLevel);
            int statCount = psycastExtension.statUpgradePoints.RandomInRange;
            implant.ChangeLevel(statCount);
            implant.points -= statCount;
            implant.ImproveStats(statCount);
        }

        comp ??= __result.GetComp<CompAbilities>();
        PsycasterPathDef path = implant.unlockedPaths?.RandomElement();
        if (path == null)
        {
            path = DefDatabase<PsycasterPathDef>.AllDefsListForReading.Where(ppd => ppd.CanPawnUnlock(__result)).RandomElement();
            implant.UnlockPath(path);
        }

        if (implant.points <= 0)
            return;
        List<AbilityDef> abilities = path?.abilities?.Except(comp.LearnedAbilities.Select(ab => ab.def)).ToList() ?? [];

        do
        {
            if (abilities.Where(abilityDef => abilityDef.GetModExtension<AbilityExtension_Psycast>().PrereqsCompleted(comp)).TryRandomElement(out AbilityDef ab))
            {
                comp.GiveAbility(ab);
                if (implant.points <= 0)
                    implant.ChangeLevel(1, false);
                implant.points--;
                abilities.Remove(ab);
            }
            else
            {
                break;
            }
        } while (implant.points > 0 && abilities.Count > 0);
    }
}
