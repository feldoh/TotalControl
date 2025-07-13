using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

[HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
public static class ApparelGenPatch
{
    private static HashSet<ThingDef> apparelRequired = [];
    private static HashSet<string> apparelTagsAllowed = [];
    private static List<SpecRequirementEdit> always = [];
    private static List<SpecRequirementEdit> chance = [];
    private static List<SpecRequirementEdit> pool1 = [];
    private static List<SpecRequirementEdit> pool2 = [];
    private static List<SpecRequirementEdit> pool3 = [];
    private static List<SpecRequirementEdit> pool4 = [];
    private static List<HairDef> hairs = [];
    private static List<BeardDef> beards = [];
    private static List<Color> hairColors = [];
    private static int edits;
    private static bool anyForceNaked = false;
    private static bool anyForceOnlySelected = false;

    private static void Postfix(Pawn pawn)
    {
        if (pawn == null)
            return;

        anyForceNaked = false;
        anyForceOnlySelected = false;
        always.Clear();
        chance.Clear();
        pool1.Clear();
        pool2.Clear();
        pool3.Clear();
        pool4.Clear();
        hairs.Clear();
        beards.Clear();
        hairColors.Clear();
        edits = 0;

        foreach (PawnKindEdit edit in PawnKindEdit.GetEditsFor(pawn.kindDef, pawn.Faction?.def))
        {
            Accumulate(edit);
            edits++;
        }

        if (anyForceNaked)
            pawn.apparel?.DestroyAll();

        if (anyForceOnlySelected)
        {
            List<Apparel> enumerable =
                pawn.apparel?.WornApparel?.Where(a => !apparelRequired.Contains(a.def) && !(a.def?.apparel?.tags ?? []).Any(t => apparelTagsAllowed.Contains(t))).ToList() ?? [];
            foreach (Apparel a in enumerable)
            {
                ModCore.Debug(a.def.LabelCap + "Destroyed");
                a.Destroy();
            }
        }

        if (edits > 0 && pawn.RaceProps.ToolUser)
            ForceGiveClothes(pawn);

        HairDef hair = GetForcedHair();
        BeardDef beard = GetForcedBeard();
        Color? color = GetForcedHairColor();
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

    private static void ForceGiveClothes(Pawn pawn)
    {
        if (pawn.apparel == null)
            return;

        foreach (SpecRequirementEdit item in GetWhatToGive(pawn))
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

    private static void Accumulate(PawnKindEdit edit)
    {
        if (edit.CustomHair != null)
            hairs.AddRange(edit.CustomHair);

        if (edit.CustomBeards != null)
            beards.AddRange(edit.CustomBeards);

        if (edit.CustomHairColors != null)
            hairColors.AddRange(edit.CustomHairColors);

        if (edit.ForceNaked)
        {
            anyForceNaked = true;
            return;
        }

        if (edit.ForceOnlySelected)
            anyForceOnlySelected = true;

        apparelRequired.AddRange(edit.ApparelRequired ?? []);
        apparelTagsAllowed.AddRange(edit.ApparelTags ?? []);

        if (edit.SpecificApparel == null)
            return;

        foreach (SpecRequirementEdit item in edit.SpecificApparel)
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
                default:
                    Log.Warning($"Unknown selection mode '{item.SelectionMode} for '{item.Thing.LabelCap}'");
                    break;
            }
    }

    private static IEnumerable<SpecRequirementEdit> GetWhatToGive(Pawn pawn)
    {
        foreach (SpecRequirementEdit item in always)
            yield return item;

        foreach (SpecRequirementEdit item in chance)
            if (Rand.Chance(item.SelectionChance))
                yield return item;

        SpecRequirementEdit selected = pool1.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = pool2.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = pool3.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (selected != null)
            yield return selected;

        selected = pool4.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
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

    private static HairDef GetForcedHair()
    {
        if (hairs.Count == 0)
            return null;

        hairs.RemoveAll(h => h == null);
        hairs.RemoveDuplicates((a, b) => a == b);
        return hairs.Count > 0 ? hairs.RandomElement() : null;
    }

    private static BeardDef GetForcedBeard()
    {
        if (beards.Count == 0)
            return null;

        beards.RemoveAll(h => h == null);
        beards.RemoveDuplicates((a, b) => a == b);
        return beards.Count > 0 ? beards.RandomElement() : null;
    }

    private static Color? GetForcedHairColor()
    {
        if (hairColors.Count == 0)
            return null;

        Color c = hairColors.RandomElement();
        c.a = 1f;
        return c;
    }
}
