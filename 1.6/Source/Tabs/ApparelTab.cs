using System.Collections.Generic;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class ApparelTab : EditTab
{
    public ApparelTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_Apparel".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawForceNaked(ui);
        if (Current.ForceNaked)
            return;
        DrawForceOnlySelected(ui);

        DrawOverride(
            ui,
            DefaultKind.apparelMoney,
            ref Current.ApparelMoney,
            "FactionLoadout_ValueLabel".Translate("FactionLoadout_Tab_Apparel".Translate()).ToString(),
            DrawApparelMoney,
            pasteGet: e => e.ApparelMoney
        );
        DrawOverride(
            ui,
            DefaultKind.apparelTags,
            ref Current.ApparelTags,
            "FactionLoadout_AllowedTypes".Translate("FactionLoadout_Tab_Apparel".Translate()).ToString(),
            DrawApparelTags,
            GetHeightFor(Current.ApparelTags),
            true,
            pasteGet: e => e.ApparelTags
        );
        DrawOverride(
            ui,
            DefaultKind.apparelDisallowTags,
            ref Current.ApparelDisallowedTags,
            "FactionLoadout_DisallowedTypes".Translate("FactionLoadout_Tab_Apparel".Translate()).ToString(),
            (Rect rect, bool active, List<string> _) => DrawDisallowedApparelTags(rect, active),
            GetHeightFor(Current.ApparelDisallowedTags),
            true,
            pasteGet: e => e.ApparelDisallowedTags
        );
        DrawOverride(
            ui,
            DefaultKind.apparelColor,
            ref Current.ApparelColor,
            "FactionLoadout_Apparel_Color".Translate().ToString(),
            DrawApparelColor,
            pasteGet: e => e.ApparelColor
        );
        DrawOverride(
            ui,
            (List<DefRef<ThingDef>>)null,
            ref Current.ApparelRequired,
            "FactionLoadout_Apparel_RequiredSimple".Translate().ToString(),
            DrawRequiredApparel,
            GetHeightFor(Current.ApparelRequired),
            true,
            pasteGet: e => e.ApparelRequired
        );
        DrawSpecificGear(ui, ref Current.SpecificApparel, "FactionLoadout_Apparel_RequiredAdvanced".Translate().ToString(), t => t.IsApparel, ThingDefOf.Apparel_Parka);
        DrawOverride(
            ui,
            null,
            ref Current.ApparelBlacklist,
            "FactionLoadout_ApparelBlacklist".Translate(),
            DrawApparelBlacklist,
            GetHeightFor(Current.ApparelBlacklist),
            false,
            pasteGet: e => e.ApparelBlacklist
        );
    }

    private void DrawForceOnlySelected(Listing_Standard ui)
    {
        Rect onlySelectedBox = ui.GetRect(32);
        Widgets.CheckboxLabeled(onlySelectedBox, "FactionLoadout_Apparel_ForceOnlySelected".Translate().ToString(), ref Current.ForceOnlySelected, placeCheckboxNearText: true);
        ui.Gap();
    }

    private void DrawForceNaked(Listing_Standard ui)
    {
        Rect nakedBox = ui.GetRect(32);
        Widgets.CheckboxLabeled(nakedBox, "FactionLoadout_Apparel_ForceNaked".Translate().ToString(), ref Current.ForceNaked, placeCheckboxNearText: true);
        ui.Gap();
    }

    private void DrawApparelColor(Rect rect, bool active, Color def)
    {
        if (active)
        {
            Color currentApparelColor = Current.ApparelColor ?? Color.white;
            Rect label = rect;
            label = label.ExpandedBy(-3);
            label.width = 100;

            Rect picker = rect;
            picker.xMin += 100;
            picker = picker.ExpandedBy(-3);

            Widgets.Label(label, "FactionLoadout_PickColor".Translate());
            if (Mouse.IsOver(picker))
            {
                Color border = Color.white - currentApparelColor;
                border.a = 1;
                border = Color.Lerp(border, currentApparelColor, 0.2f);
                Widgets.DrawBoxSolidWithOutline(picker, currentApparelColor, border, 2);
            }
            else
            {
                Widgets.DrawBoxSolid(picker, currentApparelColor);
            }

            if (Widgets.ButtonInvisible(picker))
                Find.WindowStack.Add(
                    new Window_ColorPicker(
                        currentApparelColor,
                        col =>
                        {
                            col.a = 1f;
                            Current.ApparelColor = col;
                        }
                    )
                    {
                        grayOutIfOtherDialogOpen = false,
                    }
                );
        }
        else
        {
            bool forced = Current.Def.apparelColor != Color.white;
            string txt = "FactionLoadout_ColorLabel".Translate(forced ? "" : "FactionLoadout_NoneSpecified".Translate().ToString()).ToString();
            Rect label = rect;
            label = label.ExpandedBy(-3);
            label.width = 200;
            Rect preview = rect;
            preview.xMin += 100;
            preview = preview.ExpandedBy(-3);
            Widgets.Label(label, txt);
            if (forced)
                Widgets.DrawBoxSolidWithOutline(preview, Current.Def.apparelColor, Color.black, 2);
        }
    }

    private void DrawApparelMoney(Rect rect, bool active, FloatRange defaultRange)
    {
        DrawFloatRange(rect, active, ref Current.ApparelMoney, Current.Def.apparelMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
    }

    private void DrawApparelTags(Rect rect, bool active, System.Collections.Generic.List<string> defaultTags)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ApparelTags, Current.Def.apparelTags, DefCache.AllApparelTags);
    }

    private void DrawDisallowedApparelTags(Rect rect, bool active)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ApparelDisallowedTags, Current.Def.apparelDisallowTags, DefCache.AllApparelTags);
    }

    private void DrawApparelBlacklist(Rect rect, bool active, System.Collections.Generic.List<DefRef<ThingDef>> defaultList)
    {
        DrawDefRefList(rect, active, ref scrolls[scrollIndex++], Current.ApparelBlacklist, null, DefCache.AllApparel);
    }

    private void DrawRequiredApparel(Rect rect, bool active, System.Collections.Generic.List<DefRef<ThingDef>> _)
    {
        DrawDefRefList(rect, active, ref scrolls[scrollIndex++], Current.ApparelRequired, DefaultKind.apparelRequired, DefCache.AllApparel);
    }
}
