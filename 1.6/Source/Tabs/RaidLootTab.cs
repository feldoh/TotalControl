using FactionLoadout.UISupport;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class RaidLootTab : EditTab
{
    public RaidLootTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_RaidLoot".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        if (!Current.IsGlobal)
        {
            Rect rect = ui.GetRect(30);
            Widgets.Label(rect, "FactionLoadout_GlobalOnly".Translate());
            return;
        }

        Rect descRect = ui.GetRect(120);
        Widgets.Label(descRect, "FactionLoadout_Desc_RaidLoot".Translate());
        ui.GapLine();
        Current.RaidLootValueFromPointsCurve ??= [];
        Rect overrideButtonRect = ui.GetRect(30);
        if (Widgets.ButtonText(overrideButtonRect, "FactionLoadout_FactionDefault".Translate()))
        {
            Current.RaidLootValueFromPointsCurve = new SimpleCurve(FactionEdit.TryGetOriginal(Current.ParentEdit.Faction.Def.defName)?.raidLootValueFromPointsCurve?.Points ?? []);
        }

        Current.RaidLootValueFromPointsCurve ??= [];
        ui.GapLine();
        DrawCurve(ui, ref Current.RaidLootValueFromPointsCurve, ref curvePointBuffers[curveIndex++]);
    }
}
