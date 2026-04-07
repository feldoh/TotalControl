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
            TooltipHandler.TipRegion(rect, "FactionLoadout_Inventory_ReplaceDefaultTooltip".Translate());
            Widgets.CheckboxLabeled(rect, "FactionLoadout_Inventory_ReplaceDefault".Translate(), ref Current.ReplaceDefaultInventory, placeCheckboxNearText: true);
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
        string overrideLabel = "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate());
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), overrideLabel))
        {
            Current.Inventory = !active ? new InventoryOptionEdit(Current.Def.inventoryOptions) : null;

            active = !active;
        }

        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);
        GUI.enabled = active;
        ui.CheckboxLabeled("FactionLoadout_Inventory_RemoveFixed".Translate(Current.Def.fixedInventory?.Count ?? 0).ToString(), ref Current.RemoveFixedInventory);

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
            string txt = Current.IsGlobal ? "---" : "FactionLoadout_Inventory_MaxItems".Translate(InventoryOptionEdit.GetSize(Current.Def.inventoryOptions)).ToString();
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
            string delLabel = $" [{("Delete".Translate())}]";
            delRect.width = Mathf.Max(48, Text.CalcSize(delLabel).x + 10);
            if (Widgets.ButtonText(delRect, delLabel))
                delete = true;

            GUI.color = Color.white;
            defRect.xMin += delRect.width + 4;

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
                    Widgets.HorizontalSlider(
                        defRect,
                        ref part.SkipChance,
                        FloatRange.ZeroToOne,
                        "FactionLoadout_Inventory_SkipChance".Translate($"{100f * part.SkipChance:F0}").ToString()
                    );
                if (isChildOfOne)
                    Widgets.HorizontalSlider(
                        defRect,
                        ref part.ChoiceChance,
                        FloatRange.ZeroToOne,
                        "FactionLoadout_Inventory_Weight".Translate($"{100f * part.ChoiceChance:F0}").ToString()
                    );
            }

            clone.x += 220;
            clone.width = 100;
            int min = part.CountRange.min;
            int max = part.CountRange.max;

            part.BufferA ??= min.ToString();
            part.BufferB ??= max.ToString();

            Widgets.TextFieldNumericLabeled(clone, "min".Translate().CapitalizeFirst() + ":  ", ref min, ref part.BufferA, 1);
            clone.x += 110;
            Widgets.TextFieldNumericLabeled(clone, "max".Translate().CapitalizeFirst() + ":  ", ref max, ref part.BufferB, 1);

            part.CountRange = new IntRange(min, max);
        }

        bool hasTakeAll = part.SubOptionsTakeAll?.Count > 0;
        bool hasTakeOne = part.SubOptionsChooseOne?.Count > 0;
        ui.Gap(5);
        Rect addRect = ui.GetRect(20);
        addRect.width = 80;
        if (hasTakeAll)
        {
            ui.Label("FactionLoadout_Inventory_TakeAllHeader".Translate());
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
            ui.Label("FactionLoadout_Inventory_TakeOneHeader".Translate());
            ui.Indent(20);
            for (int i = 0; i < part.SubOptionsChooseOne.Count; i++)
                if (DrawInvPart(ui, part.SubOptionsChooseOne[i], false, true))
                {
                    part.SubOptionsChooseOne.RemoveAt(i);
                    i--;
                }

            ui.Outdent(20);
        }

        if (Widgets.ButtonText(addRect, "FactionLoadout_Inventory_TakeAll".Translate()))
        {
            part.SubOptionsTakeAll ??= [];
            part.SubOptionsTakeAll.Add(new InventoryOptionEdit());
        }

        addRect.x += 90;
        if (Widgets.ButtonText(addRect, "FactionLoadout_Inventory_TakeOne".Translate()))
        {
            part.SubOptionsChooseOne ??= [];
            part.SubOptionsChooseOne.Add(new InventoryOptionEdit());
        }

        ui.GapLine();

        return delete;
    }
}
