using System.Collections.Generic;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class GeneralTab : EditTab
{
    public GeneralTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_General".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawRename(ui);
        bool isAnimal = DefaultKind.RaceProps.Animal;

        if (!Current.IsGlobal && isAnimal)
            DrawOverride(ui, DefaultKind, ref Current.ReplaceWith, "FactionLoadout_General_ReplaceWith".Translate().ToString(), DrawReplaceWith, pasteGet: e => e.ReplaceWith);

        DrawOverride(
            ui,
            DefaultKind.nameMaker ?? DefCache.FakeRulePack,
            ref Current.NameMaker,
            "FactionLoadout_General_NameMaker".Translate().ToString(),
            (r, a, d) => DrawNameMakerImpl(r, a, d, female: false),
            pasteGet: e => e.NameMaker
        );
        DrawOverride(
            ui,
            DefaultKind.nameMakerFemale ?? DefCache.FakeRulePack,
            ref Current.NameMakerFemale,
            "FactionLoadout_General_NameMakerFemale".Translate().ToString(),
            (r, a, d) => DrawNameMakerImpl(r, a, d, female: true),
            pasteGet: e => e.NameMakerFemale
        );

        DrawOverride(ui, Gender.None, ref Current.ForcedGender, "FactionLoadout_General_ForcedGender".Translate().ToString(), DrawGender, pasteGet: e => e.ForcedGender);

        if (ModsConfig.IdeologyActive && !isAnimal)
        {
            DrawIdeoOverride(ui);
        }

        DrawOverride(ui, DefaultKind.label, ref Current.Label, "FactionLoadout_General_CustomName".Translate().ToString(), DrawCustomName, pasteGet: e => e.Label);
        DrawOverride(
            ui,
            DefaultKind.minGenerationAge,
            ref Current.MinGenerationAge,
            "FactionLoadout_General_MinGenAge".Translate().ToString(),
            DrawMinAge,
            pasteGet: e => e.MinGenerationAge
        );
        DrawOverride(
            ui,
            DefaultKind.maxGenerationAge,
            ref Current.MaxGenerationAge,
            "FactionLoadout_General_MaxGenAge".Translate().ToString(),
            DrawMaxAge,
            pasteGet: e => e.MaxGenerationAge
        );
        DrawOverride(
            ui,
            DefaultKind.itemQuality,
            ref Current.ItemQuality,
            "FactionLoadout_General_AvgGearQuality".Translate().ToString(),
            DrawItemQuality,
            pasteGet: e => e.ItemQuality
        );

        if (isAnimal)
            return;

        DrawOverride(
            ui,
            0f,
            ref Current.UnwaveringlyLoyalChance,
            "FactionLoadout_General_UnwaveringlyLoyal".Translate().ToString(),
            DrawUnwaveringlyLoyalChance,
            pasteGet: e => e.UnwaveringlyLoyalChance
        );

        if (!Current.IsGlobal)
        {
            DrawOverride(ui, DefaultKind.race, ref Current.Race, "FactionLoadout_General_Species".Translate().ToString(), DrawRace, pasteGet: e => e.Race);
        }
    }

    // ==================== Draw methods ====================

    private void DrawRename(Listing_Standard ui)
    {
        Rect renameBox = ui.GetRect(32);
        bool forcedByGlobal = !Current.IsGlobal && (Current.ParentEdit.GetGlobalEditor()?.RenameDef ?? false);
        string label = Current.IsGlobal
            ? "FactionLoadout_General_RenameAll".Translate().ToString()
            : "FactionLoadout_General_RenameDef"
                .Translate(
                    Current.Def.defName,
                    FactionEdit.GetNewNameForPawnKind(Current.Def, Current.ParentEdit.Faction.Def),
                    forcedByGlobal ? "FactionLoadout_General_ForcedByGlobal".Translate().ToString() : ""
                )
                .ToString();
        if (forcedByGlobal)
        {
            Widgets.Label(renameBox, label);
        }
        else
        {
            Widgets.CheckboxLabeled(renameBox, label, ref Current.RenameDef, placeCheckboxNearText: true);
        }

        TooltipHandler.TipRegion(renameBox, "FactionLoadout_General_RenameTooltip".Translate());
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
            r =>
            {
                if (female)
                {
                    Current.NameMakerFemale = r;
                }
                else
                {
                    Current.NameMaker = r;
                }
            },
            d => d?.defName ?? "None".Translate().ToString()
        );
    }

    private void DrawCustomName(Rect rect, bool active, string defaultName)
    {
        if (active)
        {
            float w = Mathf.Max(400, rect.height * 0.5f);
            Rect input = rect;
            input.width = w;
            Current.Label = Widgets.TextEntryLabeled(input, "FactionLoadout_General_CustomName".Translate().ToString() + ":  ", Current.Label);
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
            r =>
            {
                Current.Race = r;
            }
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
            r =>
            {
                Current.ReplaceWith = r;
            }
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

    private void DrawIdeoOverride(Listing_Standard ui)
    {
        Rect headerRect = ui.GetRect(Text.LineHeight);
        Widgets.Label(headerRect, "<b>" + "FactionLoadout_General_ForcedIdeo".Translate() + "</b>");
        TooltipHandler.TipRegion(headerRect, "FactionLoadout_General_ForcedIdeoTooltip".Translate());

        Rect row = ui.GetRect(32);
        bool active = Current.ForcedIdeoName != null;

        Rect toggleRect = new Rect(row.x, row.y, 120, 32);
        string toggleLabel = active ? "FactionLoadout_OverrideOn".Translate().ToString() : "FactionLoadout_OverrideOff".Translate().ToString();
        if (Widgets.ButtonText(toggleRect, toggleLabel))
        {
            if (active)
            {
                Current.ForcedIdeoName = null;
            }
            else
            {
                Current.ForcedIdeoName = "";
            }
            active = !active;
        }

        Rect contentRect = new Rect(row.x + 124, row.y, row.width - 126, 32);

        if (!active)
        {
            string txt = Current.IsGlobal ? "---" : "FactionLoadout_General_FactionDefault".Translate().ToString();
            Widgets.Label(contentRect.GetCentered(txt), txt);
        }
        else
        {
            bool worldLoaded = Verse.Current.Game != null && Find.IdeoManager != null;
            string displayName = string.IsNullOrEmpty(Current.ForcedIdeoName) ? "FactionLoadout_General_IdeoNoneSelected".Translate().ToString() : Current.ForcedIdeoName;

            if (worldLoaded)
            {
                if (Widgets.ButtonText(contentRect, displayName))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (Ideo ideo in Find.IdeoManager.IdeosListForReading)
                    {
                        if (ideo.hidden)
                            continue;
                        Ideo localIdeo = ideo;
                        options.Add(
                            new FloatMenuOption(
                                localIdeo.name,
                                () =>
                                {
                                    Current.ForcedIdeoName = localIdeo.name;
                                }
                            )
                        );
                    }
                    if (options.Count > 0)
                    {
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            }
            else
            {
                Widgets.Label(contentRect, displayName + " " + "FactionLoadout_General_IdeoNoWorld".Translate());
            }
        }

        ui.Gap();
    }
}
