using System.Collections.Generic;
using FactionLoadout.UISupport;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class AppearanceTab : EditTab
{
    public AppearanceTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_Appearance".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawOverride(ui, null, ref Current.CustomBeards, "Forced Beard Styles", DrawBeardStyles, GetHeightFor(Current.CustomBeards), false, pasteGet: e => e.CustomBeards);
        DrawOverride(ui, null, ref Current.CustomHair, "Forced Hair Styles", DrawHairStyles, GetHeightFor(Current.CustomHair), false, pasteGet: e => e.CustomHair);
        DrawOverride(
            ui,
            null,
            ref Current.CustomHairColors,
            "Forced Hair Colors",
            DrawHairColors,
            GetHeightFor(Current.CustomHairColors, 36),
            false,
            pasteGet: e => e.CustomHairColors
        );
        DrawOverride(ui, null, ref Current.BodyTypes, "Allowed Body Types", DrawBodyTypes, GetHeightFor(Current.BodyTypes), false, pasteGet: e => e.BodyTypes);
    }

    private void DrawHairStyles(Rect rect, bool active, List<HairDef> nullList)
    {
        MenuItemBase MakeItem(HairDef def)
        {
            return new MenuItemIcon(def, $"{def.LabelCap} ({def.modContentPack?.Name ?? "<no-mod>"})", def.Icon) { Size = new Vector2(100, 100), BGColor = Color.white };
        }

        CustomFloatMenu sel = DrawDefList(rect, active, ref scrolls[scrollIndex++], Current.CustomHair, nullList, DefDatabase<HairDef>.AllDefsListForReading, true, MakeItem);
        if (sel == null)
            return;
        sel.AllowChangeTint = true;
        sel.Tint = new Color32(245, 212, 78, 255);
        sel.Columns = 4;
    }

    private void DrawBeardStyles(Rect rect, bool active, List<BeardDef> nullList)
    {
        MenuItemBase MakeItem(BeardDef def)
        {
            return new MenuItemIcon(def, $"{def.LabelCap} ({def.modContentPack?.Name ?? "<no-mod>"})", def.Icon) { Size = new Vector2(100, 100), BGColor = Color.white };
        }

        CustomFloatMenu sel = DrawDefList(rect, active, ref scrolls[scrollIndex++], Current.CustomBeards, nullList, DefDatabase<BeardDef>.AllDefsListForReading, true, MakeItem);
        if (sel == null)
            return;
        sel.AllowChangeTint = true;
        sel.Tint = new Color32(245, 212, 78, 255);
        sel.Columns = 4;
    }

    private void DrawHairColors(Rect rect, bool active, List<Color> nullList)
    {
        DrawColorList(rect, active, ref scrolls[scrollIndex++], Current.CustomHairColors, nullList);
    }

    private void DrawBodyTypes(Rect rect, bool active, List<BodyTypeDef> defaultBodyTypes)
    {
        DrawDefList(
            rect,
            active,
            ref scrolls[scrollIndex++],
            Current.BodyTypes,
            defaultBodyTypes,
            DefCache.AllBodyTypes,
            false,
            d => new MenuItemText(d, (string)d.LabelCap ?? d.defName, DefUtils.TryGetIcon(d, out Color c), c, d.description)
        );
    }
}
