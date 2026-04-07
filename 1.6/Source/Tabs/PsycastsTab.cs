using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class PsycastsTab : EditTab
{
    private string vpeGiveRandomAbilitiesBuffer = null;
    private string vpeLevelBuffer = null;

    public PsycastsTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_VEPsycasts".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        if (!VEPsycastsReflectionModule.ModLoaded.Value)
            return;
        DrawOverride(
            ui,
            false,
            ref Current.VEPsycastRandomAbilities,
            "FactionLoadout_Psycasts_GiveRandomAbilities".Translate().ToString(),
            DrawVPERandomAbilities,
            pasteGet: e => e.VEPsycastRandomAbilities
        );
        DrawOverride(ui, 1, ref Current.VEPsycastLevel, "FactionLoadout_Psycasts_Level".Translate().ToString(), DrawVPELevel, pasteGet: e => e.VEPsycastLevel);
        DrawOverride(
            ui,
            IntRange.Zero,
            ref Current.VEPsycastStatPoints,
            "FactionLoadout_Psycasts_StatPoints".Translate().ToString(),
            DrawVPEStats,
            pasteGet: e => e.VEPsycastStatPoints
        );
    }

    // --- Private draw methods ---

    private void DrawVPERandomAbilities(Rect rect, bool active, bool _)
    {
        if (vpeGiveRandomAbilitiesBuffer == null && active)
            vpeGiveRandomAbilitiesBuffer = Current.VEPsycastRandomAbilities?.ToString() ?? "NA";

        if (active)
        {
            bool value =
                Current.VEPsycastRandomAbilities
                ?? (
                    VEPsycastsReflectionModule.FindVEPsycastsExtension(Current.Def) is { } psycastsExtension
                    && VEPsycastsReflectionModule.GiveRandomAbilitiesField.Value?.GetValue(psycastsExtension) is true
                );
            Widgets.CheckboxLabeled(rect, "FactionLoadout_Psycasts_GiveRandomAbilities".Translate(), ref value);
            Current.VEPsycastRandomAbilities = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : "[Default] 1";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawVPELevel(Rect rect, bool active, int _)
    {
        if (vpeLevelBuffer == null && active)
            vpeLevelBuffer = Current.VEPsycastLevel?.ToString() ?? "NA";

        if (active)
        {
            int value =
                Current.VEPsycastLevel
                ?? (
                    VEPsycastsReflectionModule.FindVEPsycastsExtension(Current.Def) is { } psycastsExtension
                    && VEPsycastsReflectionModule.LevelField.Value?.GetValue(psycastsExtension) is int i
                        ? i
                        : 1
                );
            Widgets.IntEntry(rect, ref value, ref vpeLevelBuffer);
            Current.VEPsycastLevel = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : "[Default] 1";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawVPEStats(Rect rect, bool active, IntRange defaultRange)
    {
        if (
            VEPsycastsReflectionModule.FindVEPsycastsExtension(Current.Def) is { } psycastsExtension
            && VEPsycastsReflectionModule.StatUpgradePointsField.Value?.GetValue(psycastsExtension) is IntRange ir
        )
            defaultRange = ir;

        DrawIntRange(rect, active, ref Current.VEPsycastStatPoints, defaultRange, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
    }
}
