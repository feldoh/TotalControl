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
        public List<HairDef> hairs = [];
        public List<BeardDef> beards = [];
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
            edits.hairs.AddRange(edit.CustomHair);

        if (edit.CustomBeards != null)
            edits.beards.AddRange(edit.CustomBeards);

        if (edit.CustomHairColors != null)
            edits.hairColors.AddRange(edit.CustomHairColors);

        if (edit.ForceNaked)
        {
            edits.anyForceNaked = true;
            return;
        }

        if (edit.ForceOnlySelected)
            edits.anyForceOnlySelected = true;

        edits.apparelRequired.AddRange(edit.ApparelRequired ?? []);
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

        if (spec.Quality != null)
            thing.TryGetComp<CompQuality>()?.SetQuality(spec.Quality.Value, ArtGenerationContext.Outsider);

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

    private static HairDef GetForcedHair(AccumulatedApparelEdits edits)
    {
        if (edits.hairs.Count == 0)
            return null;

        edits.hairs.RemoveAll(h => h == null);
        edits.hairs.RemoveDuplicates((a, b) => a == b);
        return edits.hairs.Count > 0 ? edits.hairs.RandomElement() : null;
    }

    private static BeardDef GetForcedBeard(AccumulatedApparelEdits edits)
    {
        if (edits.beards.Count == 0)
            return null;

        edits.beards.RemoveAll(h => h == null);
        edits.beards.RemoveDuplicates((a, b) => a == b);
        return edits.beards.Count > 0 ? edits.beards.RandomElement() : null;
    }

    private static Color? GetForcedHairColor(AccumulatedApparelEdits edits)
    {
        if (edits.hairColors.Count == 0)
            return null;

        Color c = edits.hairColors.RandomElement();
        c.a = 1f;
        return c;
    }
}

/// <summary>
/// Prevents blacklisted ThingDefs from entering vanilla's apparel candidate pool.
/// Uses <see cref="DefCache.ApparelBlacklistCache"/> populated at Apply() time
/// for O(1) lookup per pair — no per-pawn edit iteration at patch time.
/// </summary>
[HarmonyPatch(typeof(PawnApparelGenerator), "CanUsePair")]
public static class CanUsePairBlacklistPatch
{
    static void Postfix(ThingStuffPair pair, Pawn pawn, ref bool __result)
    {
        if (!__result)
            return;

        if (DefCache.ApparelBlacklistCache.TryGetValue(pawn.kindDef, out HashSet<ThingDef> bl) && bl.Contains(pair.thing))
            __result = false;
    }
}
