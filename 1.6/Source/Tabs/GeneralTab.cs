using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class GeneralTab : EditTab
{
    public GeneralTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("General", current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawRename(ui);
        bool isAnimal = DefaultKind.RaceProps.Animal;

        if (!Current.IsGlobal && isAnimal)
            DrawOverride(ui, DefaultKind, ref Current.ReplaceWith, "Replace with...", DrawReplaceWith, pasteGet: e => e.ReplaceWith);

        DrawOverride(ui, DefaultKind.nameMaker ?? DefCache.FakeRulePack, ref Current.NameMaker, "Name Maker...",
            (r, a, d) => DrawNameMakerImpl(r, a, d, female: false), pasteGet: e => e.NameMaker);
        DrawOverride(ui, DefaultKind.nameMakerFemale ?? DefCache.FakeRulePack, ref Current.NameMakerFemale, "Name Maker Female...",
            (r, a, d) => DrawNameMakerImpl(r, a, d, female: true), pasteGet: e => e.NameMakerFemale);

        DrawOverride(ui, Gender.None, ref Current.ForcedGender, "Forced Gender", DrawGender, pasteGet: e => e.ForcedGender);
        DrawOverride(ui, DefaultKind.label, ref Current.Label, "Custom name", DrawCustomName, pasteGet: e => e.Label);
        DrawOverride(ui, DefaultKind.minGenerationAge, ref Current.MinGenerationAge, "Min Generation Age", DrawMinAge, pasteGet: e => e.MinGenerationAge);
        DrawOverride(ui, DefaultKind.maxGenerationAge, ref Current.MaxGenerationAge, "Max Generation Age", DrawMaxAge, pasteGet: e => e.MaxGenerationAge);
        DrawOverride(ui, DefaultKind.itemQuality, ref Current.ItemQuality, "Average Gear Quality", DrawItemQuality, pasteGet: e => e.ItemQuality);

        if (isAnimal)
            return;

        DrawOverride(ui, 0f, ref Current.UnwaveringlyLoyalChance, "Unwaveringly Loyal Chance", DrawUnwaveringlyLoyalChance, pasteGet: e => e.UnwaveringlyLoyalChance);

        if (!Current.IsGlobal)
        {
            DrawOverride(ui, DefaultKind.race, ref Current.Race, "Species", DrawRace, pasteGet: e => e.Race);
        }
    }

    // ==================== Draw methods ====================

    private void DrawRename(Listing_Standard ui)
    {
        Rect renameBox = ui.GetRect(32);
        bool forcedByGlobal = !Current.IsGlobal && (Current.ParentEdit.GetGlobalEditor()?.RenameDef ?? false);
        string label = Current.IsGlobal
            ? "Rename All Pawnkind Defs in this Faction"
            : $"Rename Def [{Current.Def.defName} => {FactionEdit.GetNewNameForPawnKind(Current.Def, Current.ParentEdit.Faction.Def)}]{(
                forcedByGlobal ? " - Forced By Global" : "")}";
        if (forcedByGlobal)
        {
            Widgets.Label(renameBox, label);
        }
        else
        {
            Widgets.CheckboxLabeled(renameBox, label, ref Current.RenameDef, placeCheckboxNearText: true);
        }

        TooltipHandler.TipRegion(
            renameBox,
            "This will give the cloned pawn kind a new name\nThis may have unintended consequences and may break existing pawns spawned for this faction."
        );
        ui.Gap();
    }

    private void DrawNameMakerImpl(Rect rect, bool active, RulePackDef defaultRulePack, bool female)
    {
        DrawDefSelector(
            rect,
            true,
            DefCache.AllRulePackDefs,
            female ? Current.NameMakerFemale : Current.NameMaker,
            defaultRulePack,
            r => { if (female) { Current.NameMakerFemale = r; } else { Current.NameMaker = r; } },
            d => d?.defName ?? "None"
        );
    }

    private void DrawCustomName(Rect rect, bool active, string defaultName)
    {
        if (active)
        {
            float w = Mathf.Max(400, rect.height * 0.5f);
            Rect input = rect;
            input.width = w;
            Current.Label = Widgets.TextEntryLabeled(input, "Custom name:  ", Current.Label);
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {defaultName}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawRace(Rect rect, bool active, ThingDef defaultRace)
    {
        DrawDefSelector(
            rect,
            active,
            DefCache.AllHumanlikeRaces,
            Current.Race,
            DefaultKind.race,
            r => { Current.Race = r; }
        );
    }

    private void DrawReplaceWith(Rect rect, bool active, PawnKindDef defaultKind)
    {
        DrawDefSelector(
            rect,
            active,
            DefCache.AllAnimalKindDefs,
            Current.ReplaceWith,
            DefaultKind,
            r => { Current.ReplaceWith = r; }
        );
    }

    private void DrawItemQuality(Rect rect, bool active, QualityCategory _)
    {
        DrawEnumSelector(rect, active, Current.ItemQuality, Current.Def.itemQuality, q => Current.ItemQuality = q);
    }

    private void DrawGender(Rect rect, bool active, Gender defaultValue)
    {
        DrawEnumSelector(rect, active, Current.ForcedGender, Current.Def.fixedGender ?? defaultValue, q => Current.ForcedGender = q);
    }

    private void DrawMinAge(Rect rect, bool active, int _)
    {
        if (active)
        {
            ref string minAgeBuffer = ref buffers[bufferIndex++];
            int minGenerationAge = Current.MinGenerationAge.GetValueOrDefault(Current.Def.minGenerationAge);
            minAgeBuffer ??= minGenerationAge.ToString();
            Widgets.IntEntry(rect, ref minGenerationAge, ref minAgeBuffer);
            Current.MinGenerationAge = minGenerationAge;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.minGenerationAge}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawMaxAge(Rect rect, bool active, int _)
    {
        if (active)
        {
            ref string maxAgeBuffer = ref buffers[bufferIndex++];
            int maxGenerationAge = Current.MaxGenerationAge.GetValueOrDefault(Current.Def.maxGenerationAge);
            maxAgeBuffer ??= maxGenerationAge.ToString();
            Widgets.IntEntry(rect, ref maxGenerationAge, ref maxAgeBuffer);
            Current.MaxGenerationAge = maxGenerationAge;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.maxGenerationAge}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawUnwaveringlyLoyalChance(Rect rect, bool active, float def)
    {
        DrawChance(ref Current.UnwaveringlyLoyalChance, def, rect, active);
    }
}
