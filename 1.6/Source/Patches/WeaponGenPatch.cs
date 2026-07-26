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

        // For TC-managed kinds, surface (and optionally fix) weapon slots left empty by a too-low budget.
        if (edits.editCount > 0)
            HandleWeaponPriceLimit(pawn);
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

    // ==================== Price-limited weapon fallback ====================

    /// <summary>
    /// When a TC-managed pawn ends up with no primary weapon, determines whether price was the
    /// limiting factor (matching weapons exist but none were affordable) and, if so, logs it
    /// (verbose) and optionally equips the cheapest matching weapon (<see cref="MySettings.IgnorePriceLimits"/>).
    /// </summary>
    static void HandleWeaponPriceLimit(Pawn pawn)
    {
        // Nothing to log or fix when both toggles are off - skip the allWeaponPairs scan entirely.
        if (!MySettings.VerboseLogging && !MySettings.IgnorePriceLimits)
            return;

        if (pawn.equipment == null || pawn.equipment.Primary != null)
            return;

        PawnKindDef kind = pawn.kindDef;
        if (kind.weaponTags == null || kind.weaponTags.Count == 0)
            return;
        // Mirror Postfix's guards exactly so the fallback enforces the same restrictions: the tool-user
        // and violent checks only apply when vanilla restrictions are enabled.
        if (MySettings.VanillaRestrictions && !pawn.RaceProps.ToolUser)
            return;
        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            return;
        if (MySettings.VanillaRestrictions && pawn.WorkTagIsDisabled(WorkTags.Violent))
            return;

        ThingStuffPair? cheapest = null;
        List<ThingStuffPair> pairs = PawnWeaponGenerator.allWeaponPairs;
        for (int i = 0; i < pairs.Count; i++)
        {
            ThingStuffPair w = pairs[i];
            if (!WeaponMatchesKind(w, pawn, kind))
                continue;
            // GetCommonality already includes our blocklist/material zeroing, so a positive value means TC allows it.
            if (PawnWeaponGenerator.GetCommonality(pawn, w) <= 0f)
                continue;
            if (cheapest == null || w.Price < cheapest.Value.Price)
                cheapest = w;
        }

        // No matching weapon at all → the cause is tags/filters, not price. Stay quiet.
        if (cheapest == null)
            return;

        ThingStuffPair pair = cheapest.Value;

        // Only act when price was genuinely the limiter: the cheapest matching weapon must exceed the
        // budget. Otherwise the pawn went unarmed for another reason (RNG, another mod) and attributing
        // it to budget - or "fixing" it - would be wrong.
        if (pair.Price <= kind.weaponMoney.max)
            return;

        if (MySettings.VerboseLogging)
        {
            string mat = pair.stuff != null ? $" ({pair.stuff.LabelCap})" : "";
            ModCore.Warn(
                $"Weapon slot left empty by price for '{pawn.kindDef.LabelCap}': nothing affordable within weaponMoney {kind.weaponMoney}. "
                    + $"Cheapest matching option is {pair.thing.LabelCap}{mat} at ${pair.Price:F0} - raise weaponMoney or relax the weapon/material filters."
            );
        }

        if (MySettings.IgnorePriceLimits)
            EquipFallbackWeapon(pawn, pair);
    }

    static bool WeaponMatchesKind(ThingStuffPair w, Pawn pawn, PawnKindDef kind)
    {
        bool tagMatch = false;
        for (int i = 0; i < kind.weaponTags.Count; i++)
        {
            if (w.thing.weaponTags != null && w.thing.weaponTags.Contains(kind.weaponTags[i]))
            {
                tagMatch = true;
                break;
            }
        }

        if (!tagMatch)
            return false;
        if (kind.weaponStuffOverride != null && w.stuff != kind.weaponStuffOverride)
            return false;
        if (w.thing.IsRangedWeapon && pawn.WorkTagIsDisabled(WorkTags.Shooting))
            return false;
        if (w.stuff != null && !w.stuff.stuffProps.allowedInStuffGeneration)
            return false;
        return true;
    }

    static void EquipFallbackWeapon(Pawn pawn, ThingStuffPair pair)
    {
        try
        {
            if (ThingMaker.MakeThing(pair.thing, pair.stuff) is not ThingWithComps weapon)
                return;
            PawnGenerator.PostProcessGeneratedGear(weapon, pawn);
            if (pawn.equipment.Primary != null)
                pawn.equipment.Remove(pawn.equipment.Primary);
            pawn.equipment.AddEquipment(weapon);
            ModCore.Debug($"Ignore-price fallback armed '{pawn.kindDef.LabelCap}' with {weapon.LabelCap}.");
        }
        catch (Exception e)
        {
            ModCore.Error($"Ignore-price weapon fallback failed for '{pawn.kindDef.LabelCap}'", e);
        }
    }
}

/// <summary>
/// Prevents blacklisted ThingDefs from being selected during vanilla weapon generation
/// by zeroing their commonality weight. Vanilla then naturally picks the next best
/// alternative. Uses <see cref="DefCache.WeaponBlacklistCache"/> populated at
/// Apply() time for O(1) lookup per pair - no per-pawn edit iteration at patch time.
/// </summary>
[HarmonyPatch(typeof(PawnWeaponGenerator), nameof(PawnWeaponGenerator.GetCommonality))]
public static class WeaponGetCommonalityBlacklistPatch
{
    static void Postfix(Pawn pawn, ThingStuffPair pair, ref float __result)
    {
        if (__result <= 0f)
            return;

        if (DefCache.WeaponBlacklistCache.TryGetValue(pawn.kindDef, out HashSet<ThingDef> bl) && bl.Contains(pair.thing))
        {
            __result = 0f;
            return;
        }

        // Material rule (allowlist or blocklist): zero out stuff-based weapons the kind's rule disallows.
        if (!DefCache.WeaponMaterialAllows(pawn.kindDef, pair.stuff))
            __result = 0f;
    }
}
