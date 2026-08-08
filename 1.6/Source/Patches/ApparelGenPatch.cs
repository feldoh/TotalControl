using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.Patches;

[HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
public static class ApparelGenPatch
{
    public class AccumulatedApparelEdits
    {
        public HashSet<ThingDef> apparelRequired = [];
        public HashSet<string> apparelTagsAllowed = [];
        public List<SpecRequirementEdit> always = [];
        public List<SpecRequirementEdit> chance = [];
        public List<SpecRequirementEdit> pool1 = [];
        public List<SpecRequirementEdit> pool2 = [];
        public List<SpecRequirementEdit> pool3 = [];
        public List<SpecRequirementEdit> pool4 = [];
        public HashSet<HairDef> hairs = [];
        public HashSet<BeardDef> beards = [];
        public HashSet<TattooDef> faceTattoos = [];
        public HashSet<TattooDef> bodyTattoos = [];
        public List<Color> hairColors = [];
        public int editCount;
        public bool anyForceNaked;
        public bool anyForceOnlySelected;
    }

    private static void Postfix(Pawn pawn)
    {
        if (pawn == null)
            return;

        var edits = new AccumulatedApparelEdits();
        foreach (PawnKindEdit edit in PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def))
        {
            Accumulate(edits, edit);
            edits.editCount++;
        }

        if (edits.anyForceNaked)
            pawn.apparel?.DestroyAll();

        if (edits.anyForceOnlySelected)
        {
            List<Apparel> enumerable =
                pawn.apparel?.WornApparel?.Where(a => !edits.apparelRequired.Contains(a.def) && !(a.def?.apparel?.tags ?? []).Any(t => edits.apparelTagsAllowed.Contains(t)))
                    .ToList()
                ?? [];
            foreach (Apparel a in enumerable)
            {
                ModCore.Debug(a.def.LabelCap + "Destroyed");
                a.Destroy();
            }
        }

        if (edits.editCount > 0 && pawn.RaceProps.ToolUser)
            ForceGiveClothes(pawn, edits);

        // For TC-managed kinds, surface (and optionally fix) a bare torso left by a too-low budget.
        if (edits.editCount > 0 && !edits.anyForceNaked)
            HandleApparelPriceLimit(pawn);

        HairDef hair = GetForcedHair(edits);
        BeardDef beard = GetForcedBeard(edits);
        Color? color = GetForcedHairColor(edits);
        if (pawn.story == null)
            return;
        if (beard != null && pawn.style != null && pawn.style.beardDef != beard)
            pawn.style.beardDef = beard;
        if (hair != null)
            pawn.story.hairDef = hair;
        if (color != null)
            pawn.story.HairColor = color.Value;

        // Tattoos are an Ideology feature; only meaningful when the DLC is active.
        if (ModsConfig.IdeologyActive && pawn.style != null)
        {
            TattooDef faceTattoo = GetForcedTattoo(edits.faceTattoos);
            TattooDef bodyTattoo = GetForcedTattoo(edits.bodyTattoos);
            if (faceTattoo != null)
                pawn.style.FaceTattoo = faceTattoo;
            if (bodyTattoo != null)
                pawn.style.BodyTattoo = bodyTattoo;
        }

        if (ModLister.IdeologyInstalled)
        {
            pawn.style?.Notify_StyleItemChanged();
        }
        else
        {
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }

    private static void ForceGiveClothes(Pawn pawn, AccumulatedApparelEdits edits)
    {
        if (pawn.apparel == null)
            return;

        foreach (SpecRequirementEdit item in GetWhatToGive(pawn, edits))
        {
            if (item.Thing == null)
                continue;

            Apparel created;
            try
            {
                created = GenerateNewApparel(pawn, item);
                if (created == null)
                    continue;
            }
            catch (Exception e)
            {
                ModCore.Error($"Exception generating required apparel '{item.Thing.LabelCap}'", e);
                continue;
            }

            pawn.apparel.Wear(created, false);
        }
    }

    private static void Accumulate(AccumulatedApparelEdits edits, PawnKindEdit edit)
    {
        if (edit.CustomHair != null)
            edits.hairs.AddRange(edit.CustomHair.Select(r => r.Def).Where(d => d != null));

        if (edit.CustomBeards != null)
            edits.beards.AddRange(edit.CustomBeards.Select(r => r.Def).Where(d => d != null));

        if (edit.CustomFaceTattoos != null)
            edits.faceTattoos.AddRange(edit.CustomFaceTattoos.Select(r => r.Def).Where(d => d != null));

        if (edit.CustomBodyTattoos != null)
            edits.bodyTattoos.AddRange(edit.CustomBodyTattoos.Select(r => r.Def).Where(d => d != null));

        if (edit.CustomHairColors != null)
            edits.hairColors.AddRange(edit.CustomHairColors);

        if (edit.ForceNaked)
        {
            edits.anyForceNaked = true;
            return;
        }

        if (edit.ForceOnlySelected)
            edits.anyForceOnlySelected = true;

        edits.apparelRequired.AddRange((edit.ApparelRequired ?? []).Select(r => r.Def).Where(d => d != null));
        edits.apparelTagsAllowed.AddRange(edit.ApparelTags ?? []);

        if (edit.SpecificApparel == null)
            return;

        foreach (SpecRequirementEdit item in edit.SpecificApparel)
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
                default:
                    Log.Warning($"Unknown selection mode '{item.SelectionMode} for '{item.Thing.LabelCap}'");
                    break;
            }
    }

    private static IEnumerable<SpecRequirementEdit> GetWhatToGive(Pawn pawn, AccumulatedApparelEdits edits)
    {
        foreach (SpecRequirementEdit item in edits.always)
            yield return item;

        foreach (SpecRequirementEdit item in edits.chance)
            if (Rand.Chance(item.SelectionChance))
                yield return item;

        SpecRequirementEdit selected = edits.pool1.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = edits.pool2.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = edits.pool3.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = edits.pool4.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;
    }

    private static Apparel GenerateNewApparel(Pawn pawn, SpecRequirementEdit spec)
    {
        Thing thing = ThingMaker.MakeThing(spec.Thing, spec.Material);
        if (thing == null)
        {
            ModCore.Error($"Failed to generate a '{spec.Thing.LabelCap}' made out of '{spec.Material?.LabelCap ?? "<nothing>"}'.");
            return null;
        }

        if (thing is not Apparel app)
        {
            ModCore.Error($"Generated a {thing.LabelCap} but it is not apparel?!?");
            thing.Destroy();
            return null;
        }

        if (spec.Style != null)
            thing.SetStyleDef(spec.Style);

        CompQuality compQuality = thing.TryGetComp<CompQuality>();
        if (compQuality != null)
        {
            if (spec.Quality != null)
            {
                compQuality.SetQuality(spec.Quality.Value, ArtGenerationContext.Outsider);
            }
            else
            {
                QualityCategory quality = QualityUtility.GenerateQualityGeneratingPawn(pawn.kindDef, thing.def);
                if (pawn.royalty != null && pawn.Faction != null)
                {
                    RoyalTitleDef currentTitle = pawn.royalty.GetCurrentTitle(pawn.Faction);
                    if (currentTitle != null)
                    {
                        quality = (QualityCategory)Mathf.Clamp((int)quality, (int)currentTitle.requiredMinimumApparelQuality, 6);
                    }
                }
                compQuality.SetQuality(quality, ArtGenerationContext.Outsider);
            }
        }

        if (thing.def.useHitPoints)
        {
            float healthFactor = pawn.kindDef.gearHealthRange.RandomInRange;
            if (healthFactor < 1f)
            {
                int hitPoints = Mathf.Max(1, Mathf.RoundToInt(healthFactor * thing.MaxHitPoints));
                thing.HitPoints = hitPoints;
            }
        }

        if (spec.Color != default)
            thing.SetColor(spec.Color, false);

        CompBiocodable code = thing.TryGetComp<CompBiocodable>();
        if (code is not { Biocodable: true })
            return app;
        if (code.Biocoded)
            code.UnCode();
        if (spec.Biocode)
            code.CodeFor(pawn);

        return app;
    }

    private static HairDef GetForcedHair(AccumulatedApparelEdits edits) => edits.hairs.Count > 0 ? edits.hairs.RandomElement() : null;

    private static BeardDef GetForcedBeard(AccumulatedApparelEdits edits) => edits.beards.Count > 0 ? edits.beards.RandomElement() : null;

    private static TattooDef GetForcedTattoo(HashSet<TattooDef> tattoos) => tattoos.Count > 0 ? tattoos.RandomElement() : null;

    private static Color? GetForcedHairColor(AccumulatedApparelEdits edits)
    {
        if (edits.hairColors.Count == 0)
            return null;

        Color c = edits.hairColors.RandomElement();
        c.a = 1f;
        return c;
    }

    // ==================== Price-limited apparel fallback ====================

    /// <summary>
    /// When a TC-managed pawn ends up with no torso apparel, determines whether price was the
    /// limiting factor (an allowed torso item exists but costs more than the budget) and, if so,
    /// logs it (verbose) and optionally wears the cheapest matching item (<see cref="MySettings.IgnorePriceLimits"/>).
    /// </summary>
    private static void HandleApparelPriceLimit(Pawn pawn)
    {
        // Nothing to log or fix when both toggles are off - skip the allApparelPairs scan entirely.
        if (!MySettings.VerboseLogging && !MySettings.IgnorePriceLimits)
            return;

        if (pawn.apparel == null || !pawn.RaceProps.ToolUser || !pawn.RaceProps.IsFlesh || !BodyHasTorso(pawn) || CoversTorso(pawn))
            return;

        ThingStuffPair? cheapest = CheapestEligibleTorsoApparel(pawn);
        if (cheapest == null)
            return; // No allowed torso apparel exists → the cause is tags/filters, not price.

        ThingStuffPair pair = cheapest.Value;

        // Only act when price was genuinely the limiter. Vanilla draws a random budget in apparelMoney and
        // spends it down as it adds items then prunes anything pricier than what's left.
        // Subtract what was actually spent on the pawn's other apparel and only bail when even the best-case can't cover it.
        // Prices use the same abstract MarketValue as vanilla's budget math; free warmth/vacuum layers are
        // not budget-charged, so spent is a slight overestimate - hence the clamp.
        float spent = pawn.apparel.WornApparel.Sum(a => a.def.GetStatValueAbstract(StatDefOf.MarketValue, a.Stuff));
        float budgetLeft = Mathf.Max(0f, pawn.kindDef.apparelMoney.max - spent);
        if (pair.Price <= budgetLeft)
            return;

        if (MySettings.VerboseLogging)
        {
            string mat = pair.stuff != null ? $" ({pair.stuff.LabelCap})" : "";
            string spentNote = spent > 0f ? $" (${spent:F0} already spent on other apparel)" : "";
            ModCore.Warn(
                $"Apparel slot left empty by price for '{pawn.kindDef.LabelCap}': no torso apparel affordable within apparelMoney {pawn.kindDef.apparelMoney}{spentNote}. "
                    + $"Cheapest matching option is {pair.thing.LabelCap}{mat} at ${pair.Price:F0} - raise apparelMoney or relax the apparel/material filters."
            );
        }

        if (MySettings.IgnorePriceLimits)
            WearFallbackApparel(pawn, pair);
    }

    /// <summary>
    /// True if the pawn's body has any part in the Torso group. Non-humanlike ToolUsers (some modded
    /// races/mechs) can lack a torso entirely, in which case a "bare torso" is normal, not a price failure.
    /// </summary>
    private static bool BodyHasTorso(Pawn pawn) => Enumerable.Any(pawn.RaceProps?.body?.AllParts ?? [], t => t.IsInGroup(BodyPartGroupDefOf.Torso));

    private static bool CoversTorso(Pawn pawn) =>
        pawn.apparel?.WornApparel?.Select(t => t.def.apparel?.bodyPartGroups).Any(groups => groups != null && groups.Contains(BodyPartGroupDefOf.Torso)) ?? false;

    private static ThingStuffPair? CheapestEligibleTorsoApparel(Pawn pawn)
    {
        ThingStuffPair? best = null;
        List<ThingStuffPair> pairs = PawnApparelGenerator.allApparelPairs;
        foreach (ThingStuffPair p in pairs)
        {
            List<BodyPartGroupDef> groups = p.thing.apparel?.bodyPartGroups;
            if (groups == null || !groups.Contains(BodyPartGroupDefOf.Torso))
                continue;
            // CanUsePair (with unlimited money) applies all of vanilla's filters plus our own
            // blocklist/material postfix, so we reuse it rather than duplicating that logic.
            if (!PawnApparelGenerator.CanUsePair(p, pawn, float.MaxValue, true, pawn.thingIDNumber))
                continue;
            if (best == null || p.Price < best.Value.Price)
                best = p;
        }

        return best;
    }

    private static void WearFallbackApparel(Pawn pawn, ThingStuffPair pair)
    {
        try
        {
            if (ThingMaker.MakeThing(pair.thing, pair.stuff) is not Apparel apparel)
                return;
            PawnApparelGenerator.PostProcessApparel(apparel, pawn);
            pawn.apparel.Wear(apparel, false);
            ModCore.Debug($"Ignore-price fallback dressed '{pawn.kindDef.LabelCap}' in {apparel.LabelCap}.");
        }
        catch (Exception e)
        {
            ModCore.Error($"Ignore-price apparel fallback failed for '{pawn.kindDef.LabelCap}'", e);
        }
    }
}

/// <summary>
/// Prevents blocklisted ThingDefs from entering vanilla's apparel candidate pool.
/// Uses <see cref="DefCache.ApparelBlacklistCache"/> populated at Apply() time
/// for O(1) lookup per pair - no per-pawn edit iteration at patch time.
/// </summary>
[HarmonyPatch(typeof(PawnApparelGenerator), "CanUsePair")]
public static class CanUsePairBlacklistPatch
{
    static void Postfix(ThingStuffPair pair, Pawn pawn, ref bool __result)
    {
        if (!__result || pawn?.kindDef == null)
            return;

        if (DefCache.ApparelBlacklistCache.TryGetValue(pawn.kindDef, out HashSet<ThingDef> bl) && bl.Contains(pair.thing))
        {
            __result = false;
            return;
        }

        // Material rule (allowlist or blocklist) - keeps disallowed materials out of the candidate pool.
        if (!DefCache.ApparelMaterialAllows(pawn.kindDef, pair.stuff))
            __result = false;
    }
}

/// <summary>
/// Applies the per-kind apparel material rule to vanilla's stuff gate. REQUIRED apparel
/// (PawnKindDef.apparelRequired) picks its stuff via CanUseStuff and never touches CanUsePair,
/// so without this, a required item could spawn in a disallowed material.
/// </summary>
[HarmonyPatch(typeof(PawnApparelGenerator), "CanUseStuff")]
public static class CanUseStuffMaterialPatch
{
    static void Postfix(Pawn pawn, ThingStuffPair pair, ref bool __result)
    {
        if (__result && pawn?.kindDef != null && !DefCache.ApparelMaterialAllows(pawn.kindDef, pair.stuff))
            __result = false;
    }
}
