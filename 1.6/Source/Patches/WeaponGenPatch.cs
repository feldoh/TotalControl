using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout.Patches;

[HarmonyPatch(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor")]
public static class WeaponGenPatch
{
    public class AccumulatedWeaponEdits
    {
        public List<SpecRequirementEdit> always = [];
        public List<SpecRequirementEdit> chance = [];
        public List<SpecRequirementEdit> pool1 = [];
        public List<SpecRequirementEdit> pool2 = [];
        public List<SpecRequirementEdit> pool3 = [];
        public List<SpecRequirementEdit> pool4 = [];
        public int editCount;
    }

    static void Postfix(Pawn pawn)
    {
        if (pawn == null)
            return;

        if (MySettings.VanillaRestrictions && !pawn.RaceProps.ToolUser)
            return;
        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            return;
        if (MySettings.VanillaRestrictions && pawn.WorkTagIsDisabled(WorkTags.Violent))
            return;

        var edits = new AccumulatedWeaponEdits();
        foreach (var edit in PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def))
        {
            Accumulate(edits, edit);
            edits.editCount++;
        }

        if (edits.editCount > 0 && pawn.RaceProps.ToolUser)
            ForceGiveWeapons(pawn, edits);
    }

    static void ForceGiveWeapons(Pawn pawn, AccumulatedWeaponEdits edits)
    {
        if (pawn.apparel == null)
            return;

        bool primarySet = false;
        foreach (var item in GetWhatToGive(edits))
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
                ModCore.Error($"Exception generating required weapon '{item.Thing.LabelCap}'", e);
                continue;
            }

            if (created.def.equipmentType == EquipmentType.Primary)
            {
                if (!primarySet)
                {
                    // First primary weapon: take the equipment slot, displacing any vanilla-generated weapon
                    if (pawn.equipment.Primary != null)
                        pawn.equipment.Remove(pawn.equipment.Primary);
                    pawn.equipment.AddEquipment(created);
                    primarySet = true;
                }
                else
                {
                    // Additional pool primaries go to inventory so sidearm mods (Simple Sidearms, CE)
                    // can register them automatically. Gracefully ignored in vanilla.
                    pawn.inventory.innerContainer.TryAdd(created);
                }
            }
            else
            {
                pawn.equipment.AddEquipment(created);
            }
        }
    }

    static void Accumulate(AccumulatedWeaponEdits edits, PawnKindEdit edit)
    {
        if (edit?.SpecificWeapons == null)
            return;

        foreach (var item in edit.SpecificWeapons)
        {
            switch (item.SelectionMode)
            {
                case ApparelSelectionMode.AlwaysTake:
                    edits.always.Add(item);
                    break;
                case ApparelSelectionMode.RandomChance:
                    edits.chance.Add(item);
                    break;
                case ApparelSelectionMode.FromPool1:
                    edits.pool1.Add(item);
                    break;
                case ApparelSelectionMode.FromPool2:
                    edits.pool2.Add(item);
                    break;
                case ApparelSelectionMode.FromPool3:
                    edits.pool3.Add(item);
                    break;
                case ApparelSelectionMode.FromPool4:
                    edits.pool4.Add(item);
                    break;
            }
        }
    }

    static IEnumerable<SpecRequirementEdit> GetWhatToGive(AccumulatedWeaponEdits edits)
    {
        foreach (var item in edits.always)
            yield return item;

        foreach (var item in edits.chance)
            if (Rand.Chance(item.SelectionChance))
                yield return item;

        var selected = edits.pool1.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (selected != null)
            yield return selected;

        selected = edits.pool2.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (selected != null)
            yield return selected;

        selected = edits.pool3.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (selected != null)
            yield return selected;

        selected = edits.pool4.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (selected != null)
            yield return selected;
    }

    public static ThingWithComps GenerateNewWeapon(Pawn pawn, SpecRequirementEdit spec)
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

/// <summary>
/// Prevents blacklisted ThingDefs from being selected during vanilla weapon generation
/// by zeroing their commonality weight. Vanilla then naturally picks the next best
/// alternative. Uses <see cref="PawnKindEdit.WeaponBlacklistCache"/> populated at
/// Apply() time for O(1) lookup per pair — no per-pawn edit iteration at patch time.
/// </summary>
[HarmonyPatch(typeof(PawnWeaponGenerator), nameof(PawnWeaponGenerator.GetCommonality))]
public static class WeaponGetCommonalityBlacklistPatch
{
    static void Postfix(Pawn pawn, ThingStuffPair pair, ref float __result)
    {
        if (__result <= 0f)
            return;

        if (PawnKindEdit.WeaponBlacklistCache.TryGetValue(pawn.kindDef, out HashSet<ThingDef> bl) && bl.Contains(pair.thing))
            __result = 0f;
    }
}
