using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;
using VFECore.Abilities;
using AbilityDef = VFECore.Abilities.AbilityDef;

namespace FactionLoadout
{
    [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
    [HarmonyAfter("OskarPotocki.VanillaPsycastsExpanded")]
    public class PawnGenPatch
    {
        public static Lazy<bool> _VEPsycastsLoaded = new(() => ModLister.GetActiveModWithIdentifier("vanillaexpanded.vpsycastse") is not null);

        [HarmonyPostfix]
        public static void Postfix(Pawn __result, PawnGenerationRequest request)
        {
            if (__result == null || request.AllowedDevelopmentalStages.Newborn() || !_VEPsycastsLoaded.Value) return;

            var psycastExtension = __result.kindDef.GetModExtension<PawnKindAbilityExtension_Psycasts>();

            CompAbilities comp = null;

            if (psycastExtension != null)
            {
                comp = __result.GetComp<CompAbilities>();


                var psylink = __result.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier) as Hediff_Psylink;

                if (psylink == null)
                {
                    psylink = HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, __result, __result.health.hediffSet.GetBrain()) as Hediff_Psylink;
                    __result.health.AddHediff(psylink);
                }

                var implant =
                    __result.health.hediffSet.GetFirstHediffOfDef(VPE_DefOf.VPE_PsycastAbilityImplant) as Hediff_PsycastAbilities ??
                    HediffMaker.MakeHediff(VPE_DefOf.VPE_PsycastAbilityImplant, __result,
                        __result.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Brain).FirstOrFallback()) as Hediff_PsycastAbilities;

                if (implant == null) return;
                if ((implant.psylink == null || (psycastExtension.giveRandomAbilities && implant.unlockedPaths?.Count == 0)))
                {
                    implant.InitializeFromPsylink(psylink);
                    implant.SetLevelTo(psycastExtension.initialLevel);
                    var statCount = psycastExtension.statUpgradePoints.RandomInRange;
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

                if (implant.points <= 0) return;
                var abilities = path?.abilities?.Except(comp.LearnedAbilities.Select(ab => ab.def)).ToList() ?? new List<AbilityDef>();

                do
                {
                    if (abilities.Where(abilityDef => abilityDef.GetModExtension<AbilityExtension_Psycast>().PrereqsCompleted(comp)).TryRandomElement(out var ab))
                    {
                        comp.GiveAbility(ab);
                        if (implant.points <= 0)
                            implant.ChangeLevel(1, false);
                        implant.points--;
                        abilities.Remove(ab);
                    }
                    else
                        break;
                } while (implant.points > 0 && abilities.Count > 0);
            }
        }
//
//     [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
//     [HarmonyAfter("VanillaExpanded.VPsycastsE")]
//     public class PawnGen_Patch
//     {
//         [HarmonyPostfix]
//         public static void Postfix(Pawn __result, PawnGenerationRequest request)
//         {
//             if (__result == null || request.AllowedDevelopmentalStages.Newborn()) return;
//             Log.Message("1");
//             var psycastExtension = VEPsycastsReflectionHelper.FindVEPsycastsExtension(__result.kindDef);
//             Log.Message("2");
//             if (psycastExtension != null && VEPsycastsReflectionHelper.GiveRandomAbilitiesField.Value.GetValue(psycastExtension) as bool? == true)
//             {
//                 Log.Message("3");
//                 var psylink = __result.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier) as Hediff_Psylink;
//                 Log.Message("4");
//                 var VPE_PsycastAbilityImplantDef = DefDatabase<HediffDef>.GetNamed("VPE_PsycastAbilityImplant");
// Log.Message("5");
//                 dynamic implant =
//                     __result.health.hediffSet.GetFirstHediffOfDef(VPE_PsycastAbilityImplantDef) ??
//                     HediffMaker.MakeHediff(VPE_PsycastAbilityImplantDef, __result,
//                         __result.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Brain).FirstOrFallback());
//                 Log.Message("6");
//                 if (implant.psylink == null)
//                     implant.InitializeFromPsylink(psylink);
//                 Log.Message("7");
        //     dynamic pList = VEPsycastsReflectionHelper.GetPsycasterPathDefsMethod.Value.GetValue(null);
        //     dynamic PossiblePsycasts = AccessTools.CreateInstance(VEPsycastsReflectionHelper.ClosedPsycastPathListGenericType.Value);
        //     foreach (var ppd in pList)
        //     {
        //         if (ppd.CanPawnUnlock(__result)) PossiblePsycasts.Add(ppd);
        //     }
        //
        //     var path = pList.RandomElement();
        //     implant.UnlockPath(path);
        //     dynamic aList = AccessTools.CreateInstance(VEPsycastsReflectionHelper.ClosedAbilityListGenericType.Value);
        //     dynamic comp = VEPsycastsReflectionHelper.ClosedAbilityGetCompGenericMethod.Value.Invoke(__result, new object[] { });
        //     foreach (var ability in comp.LearnedAbilities)
        //     {
        //         aList.Add(ability);
        //     }
        //
        //     var abilities = path.abilities.Except(aList);
        //
        //     do
        //     {
        //         dynamic aListValid = AccessTools.CreateInstance(VEPsycastsReflectionHelper.ClosedAbilityListGenericType.Value);
        //         foreach (var ability in abilities)
        //         {
        //             IList<DefModExtension> exts = ability.modExtensions ?? new List<DefModExtension>();
        //             foreach (var modExt in exts)
        //             {
        //                 if (modExt.GetType().FullName == VEPsycastsReflectionHelper.VEAbilityExtensionClassName)
        //                 {
        //                     aListValid.AddRange(VEPsycastsReflectionHelper.VEAbilityExtension_PrereqsCompletedMethod.Value.Invoke(modExt, new[] { comp }));
        //                 }
        //             }
        //         }
        //
        //         if (aListValid.Count > 0)
        //         {
        //             var ab = aListValid.RandomElement();
        //             comp.GiveAbility(ab);
        //             if (implant.points <= 0)
        //                 implant.ChangeLevel(1, false);
        //             implant.points--;
        //             abilities = abilities.Except(ab);
        //         }
        //         else
        //             break;
        //     } while (implant.points > 0);
        //     }
        // }

        // }
    }
}
