using FactionLoadout.UISupport;
using FactionLoadout.Util;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class RaidPointsTab : EditTab
{
    public RaidPointsTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_RaidPoints".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawOverride(ui, DefaultKind.combatPower, ref Current.CombatPower, "FactionLoadout_CombatPower".Translate().ToString(), DrawCombatPower, pasteGet: e => e.CombatPower);
        DrawOverride(
            ui,
            DefaultKind.appearsRandomlyInCombatGroups,
            ref Current.AppearsRandomlyInCombatGroups,
            "FactionLoadout_AppearsRandomlyInCombatGroups".Translate().ToString(),
            DrawAppearsRandomlyInCombatGroups,
            pasteGet: e => e.AppearsRandomlyInCombatGroups
        );

        if (!Current.IsGlobal)
            return;

        ui.GapLine();
        Rect descRect = ui.GetRect(120);
        Widgets.Label(descRect, "FactionLoadout_Desc_RaidPoints".Translate());
        ui.GapLine();
        Current.RaidCommonalityFromPointsCurve ??= [];
        Rect overrideButtonRect = ui.GetRect(30);
        if (Widgets.ButtonText(overrideButtonRect, "FactionLoadout_FactionDefault".Translate()))
        {
            Current.RaidCommonalityFromPointsCurve = new SimpleCurve(
                FactionEdit.TryGetOriginal(Current.ParentEdit.Faction.Def.defName)?.raidCommonalityFromPointsCurve?.Points ?? []
            );
        }

        ui.GapLine();
        DrawCurve(ui, ref Current.RaidCommonalityFromPointsCurve, ref curvePointBuffers[curveIndex++]);

        ui.GapLine();
        Rect maxCostDescRect = ui.GetRect(60);
        Widgets.Label(maxCostDescRect, "FactionLoadout_Desc_MaxPawnCost".Translate());
        ui.GapLine();
        Current.MaxPawnCostPerTotalPointsCurve ??= [];
        Rect maxCostButtonRect = ui.GetRect(30);
        if (Widgets.ButtonText(maxCostButtonRect, "FactionLoadout_FactionDefault".Translate()))
        {
            Current.MaxPawnCostPerTotalPointsCurve = new SimpleCurve(
                FactionEdit.TryGetOriginal(Current.ParentEdit.Faction.Def.defName)?.maxPawnCostPerTotalPointsCurve?.Points ?? []
            );
        }

        ui.GapLine();
        DrawCurve(ui, ref Current.MaxPawnCostPerTotalPointsCurve, ref curvePointBuffers[curveIndex++]);
    }

    private void DrawCombatPower(Rect rect, bool active, float def)
    {
        if (active)
        {
            ref string buffer = ref buffers[bufferIndex++];
            float value = Current.CombatPower.GetValueOrDefault(Current.Def.combatPower);
            buffer ??= value.ToString("F0");
            Widgets.TextFieldNumeric(rect, ref value, ref buffer, 0f);
            Current.CombatPower = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.combatPower:F0}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawAppearsRandomlyInCombatGroups(Rect rect, bool active, bool def)
    {
        if (active)
        {
            bool value = Current.AppearsRandomlyInCombatGroups.GetValueOrDefault(Current.Def.appearsRandomlyInCombatGroups);
            Widgets.CheckboxLabeled(rect, "FactionLoadout_AppearsRandomly_Label".Translate(), ref value, placeCheckboxNearText: true);
            Current.AppearsRandomlyInCombatGroups = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.appearsRandomlyInCombatGroups}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }
}
