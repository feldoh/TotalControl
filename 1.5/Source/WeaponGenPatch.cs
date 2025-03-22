using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    [HarmonyPatch(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor")]
    public static class WeaponGenPatch
    {
        private static List<SpecRequirementEdit> always = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> chance = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool1 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool2 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool3 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool4 = new List<SpecRequirementEdit>();
        private static int edits;

        static void Postfix(Pawn pawn)
        {
            if (pawn == null)
                return;

            if (MySettings.VanillaRestrictions && !pawn.RaceProps.ToolUser)
            {
                return;
            }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return;
            }
            if (MySettings.VanillaRestrictions && pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }

            always.Clear();
            chance.Clear();
            pool1.Clear();
            pool2.Clear();
            pool3.Clear();
            pool4.Clear();
            edits = 0;

            foreach (var edit in PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def))
            {
                Accumulate(edit);
                edits++;
            }

            if (edits > 0 && pawn.RaceProps.ToolUser)
                ForceGiveClothes(pawn);
        }

        static void ForceGiveClothes(Pawn pawn)
        {
            if (pawn.apparel == null)
                return;

            foreach (var item in GetWhatToGive())
            {
                if (item.Thing == null)
                    continue;

                ThingWithComps created;
                try
                {
                    created = GenerateNewWeapon(pawn, item);
                    if (created == null)
                        continue;
                }
                catch (Exception e)
                {
                    ModCore.Error($"Exception generating required apparel '{item.Thing.LabelCap}'", e);
                    continue;
                }

                if (created.def.equipmentType == EquipmentType.Primary && pawn.equipment.Primary != null)
                    pawn.equipment.Remove(pawn.equipment.Primary);
                pawn.equipment.AddEquipment(created);
            }
        }

        static void Accumulate(PawnKindEdit edit)
        {
            if (edit?.SpecificWeapons == null)
                return;

            foreach (var item in edit.SpecificWeapons)
            {
                switch (item.SelectionMode)
                {
                    case ApparelSelectionMode.AlwaysTake:
                        always.Add(item);
                        break;
                    case ApparelSelectionMode.RandomChance:
                        chance.Add(item);
                        break;
                    case ApparelSelectionMode.FromPool1:
                        pool1.Add(item);
                        break;
                    case ApparelSelectionMode.FromPool2:
                        pool2.Add(item);
                        break;
                    case ApparelSelectionMode.FromPool3:
                        pool3.Add(item);
                        break;
                    case ApparelSelectionMode.FromPool4:
                        pool4.Add(item);
                        break;
                }
            }
        }

        static IEnumerable<SpecRequirementEdit> GetWhatToGive()
        {
            foreach (var item in always)
                yield return item;

            foreach (var item in chance)
                if (Rand.Chance(item.SelectionChance))
                    yield return item;

            var selected = pool1.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
            if (selected != null)
                yield return selected;

            selected = pool2.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
            if (selected != null)
                yield return selected;

            selected = pool3.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
            if (selected != null)
                yield return selected;

            selected = pool4.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
            if (selected != null)
                yield return selected;
        }

        static ThingWithComps GenerateNewWeapon(Pawn pawn, SpecRequirementEdit spec)
        {
            var thing = ThingMaker.MakeThing(spec.Thing, spec.Material) as ThingWithComps;
            if (thing == null)
            {
                ModCore.Error($"Failed to generate a '{spec.Thing.LabelCap}' made out of '{spec.Material?.LabelCap ?? "<nothing>"}'.");
                return null;
            }

            if (spec.Style != null)
                thing.SetStyleDef(spec.Style);

            if (spec.Quality != null)
                thing.TryGetComp<CompQuality>()?.SetQuality(spec.Quality.Value, ArtGenerationContext.Outsider);

            if (spec.Color != default)
                thing.SetColor(spec.Color, false);

            var code = thing.TryGetComp<CompBiocodable>();
            if (code != null && code.Biocodable)
            {
                if (code.Biocoded)
                    code.UnCode();

                if (spec.Biocode)
                    code.CodeFor(pawn);
            }

            return thing;
        }
    }
}
