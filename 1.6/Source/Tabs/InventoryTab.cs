using System.Collections.Generic;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class InventoryTab : EditTab
{
    public InventoryTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_Inventory".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        if (Current.IsGlobal)
        {
            Rect rect = ui.GetRect(30);
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(
                rect,
                "If true, the override inventory entirely replaces the default one.\nIf false, the override inventory is added to the default inventory."
            );
            Widgets.CheckboxLabeled(rect, "Replace default inventory? ", ref Current.ReplaceDefaultInventory, placeCheckboxNearText: true);
        }

        DrawInventory(ui);
    }

    private void DrawInventory(Listing_Standard ui)
    {
        float height = 32;
        InventoryOptionEdit field = Current.Inventory;

        ui.Label("<b>Inventory</b>");
        Rect rect = ui.GetRect(height);
        bool active = field != null;
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
        {
            Current.Inventory = !active ? new InventoryOptionEdit(Current.Def.inventoryOptions) : null;

            active = !active;
        }

        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);
        GUI.enabled = active;
        ui.CheckboxLabeled($"Remove fixed inventory [Fixed Inventory Size: {Current.Def.fixedInventory?.Count ?? 0}]:", ref Current.RemoveFixedInventory);

        if (Current.Inventory != null)
        {
            // Make the top level just a passthrough for the suboptions
            Current.Inventory.Thing = null;
            Current.Inventory.SkipChance = 0f;
            Current.Inventory.ChoiceChance = 1f;
            DrawInvPart(ui, Current.Inventory, false, false);
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] Max. {InventoryOptionEdit.GetSize(Current.Def.inventoryOptions)} items";
            Widgets.Label(content.GetCentered(txt), txt);
        }

        GUI.enabled = true;
    }

    private bool DrawInvPart(Listing_Standard ui, InventoryOptionEdit part, bool isChildOfAll, bool isChildOfOne)
    {
        Rect defRect = ui.GetRect(28);
        bool delete = false;

        if (part.Thing != null)
        {
            Rect delRect = defRect;
            delRect.width = 48;
            GUI.color = Color.red;
            if (Widgets.ButtonText(delRect, " [DEL]"))
                delete = true;

            GUI.color = Color.white;
            defRect.xMin += 52;

            if (isChildOfAll || isChildOfOne)
                defRect.xMin += 100;
            defRect.width = 240;
            Widgets.DefLabelWithIcon(defRect, part.Thing);
            if (Widgets.ButtonInvisible(defRect))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                    DefCache.AllInvItems,
                    d => new MenuItemText(d, d.LabelCap, DefUtils.TryGetIcon(d, out Color c), c, d.description)
                );
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        part.Thing = raw.GetPayload<ThingDef>();
                    }
                );
            }

            Rect clone = defRect;
            if (isChildOfAll || isChildOfOne)
            {
                defRect.y += 14;
                defRect.xMin -= 100;
                defRect.width = 100;
                defRect.height = 20;

                if (isChildOfAll)
                    Widgets.HorizontalSlider(defRect, ref part.SkipChance, FloatRange.ZeroToOne, $"Skip chance: {100f * part.SkipChance:F0}%");
                if (isChildOfOne)
                    Widgets.HorizontalSlider(defRect, ref part.ChoiceChance, FloatRange.ZeroToOne, $"Weight: {100f * part.ChoiceChance:F0}%");
            }

            clone.x += 220;
            clone.width = 100;
            int min = part.CountRange.min;
            int max = part.CountRange.max;

            part.BufferA ??= min.ToString();
            part.BufferB ??= max.ToString();

            Widgets.TextFieldNumericLabeled(clone, "Min:  ", ref min, ref part.BufferA, 1);
            clone.x += 110;
            Widgets.TextFieldNumericLabeled(clone, "Max:  ", ref max, ref part.BufferB, 1);

            part.CountRange = new IntRange(min, max);
        }

        bool hasTakeAll = part.SubOptionsTakeAll?.Count > 0;
        bool hasTakeOne = part.SubOptionsChooseOne?.Count > 0;
        ui.Gap(5);
        Rect addRect = ui.GetRect(20);
        addRect.width = 80;
        if (hasTakeAll)
        {
            ui.Label("<b>TAKE ALL:</b>");
            ui.Indent(20);
            for (int i = 0; i < part.SubOptionsTakeAll.Count; i++)
                if (DrawInvPart(ui, part.SubOptionsTakeAll[i], true, false))
                {
                    part.SubOptionsTakeAll.RemoveAt(i);
                    i--;
                }

            ui.Outdent(20);
        }

        if (hasTakeOne)
        {
            ui.Label("<b>TAKE ONE:</b>");
            ui.Indent(20);
            for (int i = 0; i < part.SubOptionsChooseOne.Count; i++)
                if (DrawInvPart(ui, part.SubOptionsChooseOne[i], false, true))
                {
                    part.SubOptionsChooseOne.RemoveAt(i);
                    i--;
                }

            ui.Outdent(20);
        }

        if (Widgets.ButtonText(addRect, "+ Take all"))
        {
            part.SubOptionsTakeAll ??= [];
            part.SubOptionsTakeAll.Add(new InventoryOptionEdit());
        }

        addRect.x += 90;
        if (Widgets.ButtonText(addRect, "+ Take one"))
        {
            part.SubOptionsChooseOne ??= [];
            part.SubOptionsChooseOne.Add(new InventoryOptionEdit());
        }

        ui.GapLine();

        return delete;
    }
}
