using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    [HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
    public static class ApparelGenPatch
    {
        private static List<SpecRequirementEdit> always = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> chance = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool1 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool2 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool3 = new List<SpecRequirementEdit>();
        private static List<SpecRequirementEdit> pool4 = new List<SpecRequirementEdit>();
        private static List<HairDef> hairs = new List<HairDef>();
        private static List<Color> hairColors = new List<Color>();
        private static int edits;
        private static bool anyForceNaked = false;

        static void Postfix(Pawn pawn)
        {
            if (pawn == null)
                return;

            anyForceNaked = false;
            always.Clear();
            chance.Clear();
            pool1.Clear();
            pool2.Clear();
            pool3.Clear();
            pool4.Clear();
            hairs.Clear();
            hairColors.Clear();
            edits = 0;

            foreach (var edit in PawnKindEdit.GetEditsFor(pawn.kindDef))
            {
                Accumulate(edit);
                edits++;
            }

            if (anyForceNaked)
                pawn.apparel?.DestroyAll();

            if(edits > 0 && pawn.RaceProps.ToolUser)
                ForceGiveClothes(pawn);

            var hair = GetForcedHair();
            var color = GetForcedHairColor();
            if(pawn.story != null)
            {
                if(hair != null)                
                    pawn.story.hairDef = hair;
                if (color != null)
                    pawn.story.hairColor = color.Value;
            }
        }

        static void ForceGiveClothes(Pawn pawn)
        {
            if (pawn.apparel == null)
                return;

            foreach(var item in GetWhatToGive())
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

                pawn.apparel.Wear(created, false, false);
            }
        }

        static void Accumulate(PawnKindEdit edit)
        {
            if (edit.CustomHair != null)
                hairs.AddRange(edit.CustomHair);

            if (edit.CustomHairColors != null)
                hairColors.AddRange(edit.CustomHairColors);

            if (edit.ForceNaked)
            {
                anyForceNaked = true;
                return;
            }
            
            if (edit.SpecificApparel == null)
                return;

            foreach(var item in edit.SpecificApparel)
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
    
        static Apparel GenerateNewApparel(Pawn pawn, SpecRequirementEdit spec)
        {
            var thing = ThingMaker.MakeThing(spec.Thing, spec.Material);
            if(thing == null)
            {
                ModCore.Error($"Failed to generate a '{spec.Thing.LabelCap}' made out of '{spec.Material?.LabelCap ?? "<nothing>"}'.");
                return null;
            }

            var app = thing as Apparel;
            if(app == null)
            {
                ModCore.Error($"Generated a {thing.LabelCap} but it is not apparel?!?");
                app.Destroy(DestroyMode.Vanish);
                return null;
            }

            if(spec.Style != null)
                thing.SetStyleDef(spec.Style);

            if (spec.Quality != null)
                thing.TryGetComp<CompQuality>()?.SetQuality(spec.Quality.Value, ArtGenerationContext.Outsider);

            if (spec.Color != default)
                thing.SetColor(spec.Color, false);

            var code = thing.TryGetComp<CompBiocodable>();
            if(code != null && code.Biocodable)
            {
                if (code.Biocoded)
                    code.UnCode();
                code.CodeFor(pawn);
            }

            return app;
        }
    
        static HairDef GetForcedHair()
        {
            if (hairs.Count == 0)
                return null;

            hairs.RemoveAll(h => h == null);
            hairs.RemoveDuplicates((a, b) => a == b);
            if (hairs.Count > 0)
                return hairs.RandomElement();
            return null;
        }

        static Color? GetForcedHairColor()
        {
            if (hairColors.Count == 0)
                return null;

            var c = hairColors.RandomElement();
            c.a = 1f;
            return c;
        }
    }
}
