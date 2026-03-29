using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Draws the "Required Apparel/Weapon (advanced)" toggle-list.
/// Each entry is a <see cref="SpecRequirementEdit"/> with its own
/// thing, material, style, biocode, quality, color, and selection-mode controls.
/// </summary>
public static class SpecificGearDrawer
{
    public static void Draw(Listing_Standard ui, ref List<SpecRequirementEdit> edits, string label, Func<ThingDef, bool> thingFilter, ThingDef defaultThing, ref Vector2 scroll)
    {
        float height = edits == null ? 32 : 300;

        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = edits != null;

        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
        {
            edits = active ? null : [];
            active = !active;
        }

        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);

        if (active)
        {
            Widgets.BeginScrollView(content, ref scroll, new Rect(0, 0, 100, 152 * edits.Count - 10));
            Listing_Standard tempUI = new();
            tempUI.Begin(new Rect(0, 0, content.width - 20, 152 * edits.Count));
            DrawContent(tempUI, thingFilter, edits);
            tempUI.End();
            Widgets.EndScrollView();

            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, "<b>Add New</b>"))
                edits.Add(new SpecRequirementEdit { Thing = defaultThing });
        }
        else
        {
            string text = "[Default] <i>None</i>";
            GUI.enabled = false;
            Widgets.Label(content.GetCentered(text), text);
            GUI.enabled = true;
        }

        ui.Gap();
    }

    // ==================== Per-item loop ====================

    private static void DrawContent(Listing_Standard ui, Func<ThingDef, bool> thingFilter, List<SpecRequirementEdit> edits)
    {
        for (int i = 0; i < edits.Count; i++)
        {
            SpecRequirementEdit item = edits[i];
            if (item?.Thing == null)
                continue;

            Rect area = ui.GetRect(140);
            Widgets.DrawBoxSolidWithOutline(area, default, Color.white * 0.75f);

            DrawItemFrame(area, item);

            if (DrawItemDeleteButton(area, edits, i))
            {
                i--;
                continue;
            }

            DrawItemThingSelector(area, item, thingFilter);
            DrawItemMaterial(area, item);
            DrawItemStyle(area, item);
            DrawItemBiocode(area, item);
            DrawItemQuality(area, item);
            DrawItemColor(area, item);
            DrawItemSelectionMode(area, item);

            ui.Gap();
        }
    }

    // ==================== Per-item section helpers ====================

    private static void DrawItemFrame(Rect area, SpecRequirementEdit item)
    {
        Rect icon = area;
        icon.width = icon.height = 64;
        Widgets.DefIcon(icon, item.Thing, item.Material, thingStyleDef: item.Style, color: item.Color == default ? null : item.Color);

        Rect label = icon;
        label.x += 70;
        label.y += 14;
        label.width = 225;
        Widgets.LabelFit(label, $"<b>{item.Thing.LabelCap}</b>");
    }

    /// <returns>True if the item was removed from the list.</returns>
    private static bool DrawItemDeleteButton(Rect area, List<SpecRequirementEdit> edits, int index)
    {
        Rect delete = new(area.xMax - 105, area.y + 5, 100, 20);
        GUI.color = Color.red;
        bool removed = Widgets.ButtonText(delete, "<b>REMOVE</b>");
        GUI.color = Color.white;
        if (removed)
            edits.RemoveAt(index);
        return removed;
    }

    private static void DrawItemThingSelector(Rect area, SpecRequirementEdit item, Func<ThingDef, bool> thingFilter)
    {
        Rect defSel = area;
        defSel.x += 8;
        defSel.y += 10;
        defSel.width = 220;
        defSel.height = 50;

        Widgets.DrawHighlightIfMouseover(defSel);
        TooltipHandler.TipRegion(defSel, "FactionLoadout_LeftClickToChange".Translate() + "\n" + "FactionLoadout_RightClickToInspect".Translate());

        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(defSel))
        {
            if (Verse.Current.ProgramState == ProgramState.Playing)
            {
                Find.WindowStack.Add(new Dialog_InfoCard(item.Thing));
            }
            else
            {
                Find.WindowStack.Add(new Dialog_ApparelInfo(item.Thing));
            }
            Event.current.Use();
        }

        if (Widgets.ButtonInvisible(defSel))
        {
            IEnumerable<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading.Where(thingFilter);
            List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                defs,
                d => new MenuItemText(d, d.LabelCap, DefUtils.TryGetIcon(d, out Color c), c, DefUtils.BuildApparelTooltip(d))
            );
            CustomFloatMenu.Open(
                items,
                raw =>
                {
                    item.Thing = raw.GetPayload<ThingDef>();
                    item.Style = null;
                    item.Material = null;
                }
            );
        }
    }

    private static void DrawItemMaterial(Rect area, SpecRequirementEdit item)
    {
        bool canDoStuff = item.Thing?.MadeFromStuff ?? false;

        // Label + current value row
        Rect material = area;
        material.width = 220;
        material.height = 24;
        material.y += 62;
        material.x += 10;

        if (canDoStuff)
        {
            Widgets.Label(material, "<b>Material: </b>");
        }
        else
        {
            item.Material = null;
        }

        material.x += 68;
        if (canDoStuff)
        {
            if (item.Material != null)
            {
                Widgets.DefLabelWithIcon(material, item.Material, 5);
            }
            else
            {
                Widgets.Label(material, "None");
            }
        }

        // Clickable region for the picker
        material.x = area.x + 5;
        material.width = 220;
        if (!canDoStuff)
            return;

        if (item.Material == null)
        {
            FactionDef faction = Find.WindowStack.WindowOfType<FactionEditUI>()?.Current?.Faction?.Def;
            TechLevel techLevel = MySettings.VanillaRestrictions ? faction?.techLevel ?? TechLevel.Undefined : TechLevel.Undefined;
            item.Material = GenStuff.AllowedStuffsFor(item.Thing, techLevel).FirstOrDefault();
        }

        Widgets.DrawHighlightIfMouseover(material);
        if (Widgets.ButtonInvisible(material))
        {
            FactionDef faction = Find.WindowStack.WindowOfType<FactionEditUI>()?.Current?.Faction?.Def;
            TechLevel techLevel = MySettings.VanillaRestrictions ? faction?.techLevel ?? TechLevel.Undefined : TechLevel.Undefined;
            IEnumerable<ThingDef> defs = GenStuff.AllowedStuffsFor(item.Thing, techLevel);
            List<MenuItemBase> stuffItems = CustomFloatMenu.MakeItems(
                defs,
                d => new MenuItemText(d, d.LabelAsStuff.CapitalizeFirst(), DefUtils.TryGetIcon(d, out Color c), c, d.description)
            );
            CustomFloatMenu.Open(stuffItems, raw => item.Material = raw.GetPayload<ThingDef>());
        }
    }

    private static void DrawItemStyle(Rect area, SpecRequirementEdit item)
    {
        // Style row sits just below the material row (area.y + 62 + 24 = area.y + 86)
        Rect style = area;
        style.width = 220;
        style.height = 24;
        style.y += 86;
        style.x += 10;

        Widgets.Label(style, "<b>Style: </b>");
        style.x += 68;

        bool canHaveStyle = item.Thing != null && item.Thing.CanBeStyled();
        if (!canHaveStyle)
            item.Style = null;

        if (item.Style != null)
        {
            Widgets.Label(style, item.Style.Category?.LabelCap ?? "<VALID_STYLE_BUT_MISSING_CAT>");
        }
        else
        {
            Widgets.Label(style, $"None {(canHaveStyle ? "" : "(cannot be styled)")}");
        }

        style.x = area.x + 5;
        Widgets.DrawHighlightIfMouseover(style);
        if (Widgets.ButtonInvisible(style) && canHaveStyle)
        {
            List<MenuItemBase> items = CustomFloatMenu.MakeItems(StyleHelper.GetValidStyles(item.Thing), s => new MenuItemText(s.style, s.name, s.exampleIcon));
            items.Add(new MenuItemText(null, "_ No Style _", null, default, "This item will have no style at all."));
            CustomFloatMenu.Open(items, raw => item.Style = raw.Payload == null ? null : raw.GetPayload<ThingStyleDef>());
        }
    }

    private static void DrawItemBiocode(Rect area, SpecRequirementEdit item)
    {
        // material rect: x=area.x+5, y=area.y+62, width=220, height=24  →  yMax = area.y+86
        // biocode.y = yMax + 26 = area.y + 112 ; biocode.x = area.x+5+4 = area.x+9
        Rect biocode = new(area.x + 9, area.y + 112, 100, 20);

        if (item.Thing != null && item.Thing.HasAssignableCompFrom(typeof(CompBiocodable)))
        {
            Widgets.CheckboxLabeled(biocode, "<b>Biocode: </b>", ref item.Biocode);
        }
        else
        {
            item.Biocode = false;
        }
    }

    private static void DrawItemQuality(Rect area, SpecRequirementEdit item)
    {
        bool canDoQuality = item.Thing?.CompDefForAssignableFrom<CompQuality>() != null;

        // Toggle button at area.x+230, area.y+10
        Rect qualityCheck = area;
        qualityCheck.x += 230;
        qualityCheck.y += 10;
        qualityCheck.width = 150;
        qualityCheck.height = 28;

        if (
            canDoQuality
            && Widgets.ButtonText(qualityCheck, $"<b>Specific quality: </b><color={(item.Quality != null ? "#81f542" : "#ff4d4d")}>{(item.Quality != null ? "Yes" : "No")}</color>")
        )
        {
            if (item.Quality == null)
            {
                item.Quality = QualityCategory.Normal;
            }
            else
            {
                item.Quality = null;
            }
        }
        else if (!canDoQuality)
        {
            item.Quality = null;
        }

        // Picker button below toggle
        Rect quality = qualityCheck;
        quality.y += 34;
        if (canDoQuality && item.Quality != null && Widgets.ButtonText(quality, item.Quality.ToString()))
        {
            IEnumerable<QualityCategory> enums = Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>();
            FloatMenuUtility.MakeMenu(enums, e => e.ToString(), e => () => item.Quality = e);
        }
    }

    private static void DrawItemColor(Rect area, SpecRequirementEdit item)
    {
        bool canDoColor = item.Thing?.CompDefForAssignableFrom<CompColorable>() != null;

        // color rect is below the quality picker: quality.y+34 = (area.y+10+34)+34 = area.y+78
        // quality.x = area.x+230  →  color.x = area.x+232
        Rect color = new(area.x + 232, area.y + 78, 150, 28);

        if (canDoColor)
        {
            Widgets.Label(color, "<b>Color: </b>");
        }

        color.x += 60;
        bool isDefault = item.Color == default;

        if (canDoColor)
        {
            Widgets.DrawBoxSolidWithOutline(color, item.Color, Color.white);
            Widgets.DrawHighlightIfMouseover(color);
            if (Widgets.ButtonInvisible(color))
            {
                if (isDefault)
                    item.Color = Color.white;
                Find.WindowStack.Add(
                    new Window_ColorPicker(
                        item.Color,
                        c =>
                        {
                            c.a = 1f;
                            item.Color = c;
                        }
                    )
                );
            }

            if (isDefault)
            {
                Widgets.Label(color.GetCentered("No color"), "No color");
            }
            else
            {
                Color c = item.Color;
                c.a = 1f;
                item.Color = c;

                color.x += 154;
                color.width = 48;
                if (Widgets.ButtonText(color, "Clear"))
                    item.Color = default;
            }
        }
        else
        {
            item.Color = default;
        }
    }

    private static void DrawItemSelectionMode(Rect area, SpecRequirementEdit item)
    {
        static string ModeToName(ApparelSelectionMode mode) =>
            mode switch
            {
                ApparelSelectionMode.AlwaysTake => "Always picked",
                ApparelSelectionMode.RandomChance => "Random chance to be picked",
                ApparelSelectionMode.FromPool1 => "Part of pool 1",
                ApparelSelectionMode.FromPool2 => "Part of pool 2",
                ApparelSelectionMode.FromPool3 => "Part of pool 3",
                ApparelSelectionMode.FromPool4 => "Part of pool 4",
                _ => mode.ToString(),
            };

        Rect modeBox = area;
        modeBox.xMin += 500;
        modeBox.y += 45;
        modeBox.width = 220;

        Rect modeLabel = modeBox.ExpandedBy(-5);
        modeLabel.height = 30;
        Widgets.Label(modeLabel, "Selection mode:");

        Rect modeButton = modeBox.ExpandedBy(-5);
        modeButton.y += 22;
        modeButton.height = 30;

        if (Widgets.ButtonText(modeButton, ModeToName(item.SelectionMode)))
        {
            IEnumerable<ApparelSelectionMode> values = Enum.GetValues(typeof(ApparelSelectionMode)).Cast<ApparelSelectionMode>();
            FloatMenuUtility.MakeMenu(values, ModeToName, e => () => item.SelectionMode = e);
        }

        Rect chanceRect = modeButton.ExpandedBy(-5);
        chanceRect.y += 34;
        chanceRect.height = 30;

        if (item.SelectionMode != ApparelSelectionMode.AlwaysTake)
        {
            Widgets.HorizontalSlider(
                chanceRect,
                ref item.SelectionChance,
                FloatRange.ZeroToOne,
                $"{(item.SelectionMode == ApparelSelectionMode.RandomChance ? "Chance" : "Weight")}: {item.SelectionChance * 100f:F0}%"
            );
        }
    }
}
