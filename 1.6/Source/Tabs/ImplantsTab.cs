using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class ImplantsTab : EditTab
{
    private string maxTechBuffer = null;

    public ImplantsTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_ImplantsAndBionics".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawOverride(ui, DefaultKind.techHediffsMoney, ref Current.TechMoney, "Implants & Bionics Value", DrawTechMoney, pasteGet: e => e.TechMoney);
        DrawOverride(
            ui,
            DefaultKind.techHediffsTags,
            ref Current.TechHediffTags,
            "Allowed Implants & Bionics Types",
            DrawTechTags,
            GetHeightFor(Current.TechHediffTags),
            true,
            pasteGet: e => e.TechHediffTags
        );
        DrawOverride(
            ui,
            DefaultKind.techHediffsDisallowTags,
            ref Current.TechHediffDisallowedTags,
            "Disallowed Implants & Bionics Types",
            DrawDisallowedTechTags,
            GetHeightFor(Current.TechHediffDisallowedTags),
            true,
            pasteGet: e => e.TechHediffDisallowedTags
        );
        DrawOverride(
            ui,
            (List<DefRef<ThingDef>>)null,
            ref Current.TechRequired,
            "Required Implants & Bionics",
            DrawRequiredTech,
            GetHeightFor(Current.TechRequired),
            true,
            pasteGet: e => e.TechRequired
        );
        DrawOverride(ui, DefaultKind.techHediffsChance, ref Current.TechHediffChance, "Implants & Bionics Chance", DrawTechChance, pasteGet: e => e.TechHediffChance);
        DrawOverride(ui, DefaultKind.techHediffsMaxAmount, ref Current.TechHediffsMaxAmount, "Max # of Implants & Bionics", DrawMaxTech, pasteGet: e => e.TechHediffsMaxAmount);
        DrawSpecificHediffs(ui, ref Current.ForcedHediffs, "Required Hediffs (advanced)", _ => true, HediffDefOf.Scaria);
    }

    // --- Private draw methods ---

    private void DrawSpecificHediffs(Listing_Standard ui, ref List<ForcedHediff> edits, string label, Func<HediffDef, bool> hediffFilter, HediffDef defaultHediffDef)
    {
        float height = edits == null ? 32 : 340;

        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = edits != null;
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
        {
            edits = active ? null : [];
            active = !active;
        }

        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height - 30);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);

        ref Vector2 scroll = ref scrolls[scrollIndex++];
        if (active)
        {
            Widgets.BeginScrollView(content, ref scroll, new Rect(0, 0, 100, 320 * edits.Count - 10));
            Listing_Standard tempUI = new();
            tempUI.Begin(new Rect(0, 0, content.width - 20, 320 * edits.Count));
            DrawSpecificHediffContent(tempUI, hediffFilter, edits);
            tempUI.End();
            Widgets.EndScrollView();
            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, "<b>Add New</b>"))
                edits.Add(new ForcedHediff { HediffDef = defaultHediffDef });
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

    private void DrawSpecificHediffContent(Listing_Standard tempUI, Func<HediffDef, bool> hediffFilter, List<ForcedHediff> edits)
    {
        bool active = edits != null;
        if (!active)
            return;

        for (int i = 0; i < edits.Count; i++)
        {
            ForcedHediff item = edits[i];

            if (item?.HediffDef == null)
                continue;

            Rect area = tempUI.GetRect(270);
            Widgets.DrawBoxSolidWithOutline(area, default, Color.white * 0.75f);

            Rect delete = new(area.xMax - 105, area.y + 5, 100, 20);
            GUI.color = Color.red;
            if (Widgets.ButtonText(delete, "<b>REMOVE</b>"))
            {
                edits.RemoveAt(i);
                i--;
            }

            GUI.color = Color.white;

            tempUI.Gap(2);
            Rect hediffRect = new(area.x + 5, area.y + 5, 250, 25);
            if (Widgets.ButtonText(hediffRect, item.HediffDef.LabelCap))
            {
                IEnumerable<HediffDef> defs = DefDatabase<HediffDef>.AllDefsListForReading.Where(hediffFilter);
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(defs, d => new MenuItemText(d, d.LabelCap, DefUtils.TryGetIcon(d, out Color c), c, d.description));
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        HediffDef a = raw.GetPayload<HediffDef>();
                        item.HediffDef = a;
                    }
                );
            }

            Rect hediffMaxPartsRect = new(area.x + 10, area.y + 32, (area.width - 100) * 0.8f, 20);
            Widgets.Label(hediffMaxPartsRect.LeftPart(0.15f), "Max parts to hit");
            Widgets.IntEntry(hediffMaxPartsRect.RightPart(0.75f), ref item.maxParts, ref buffers[bufferIndex++]);
            Rect hediffMaxPartsRangeRect = new(area.x + 10, area.y + 60, area.width - 10, 30);
            Widgets.Label(hediffMaxPartsRangeRect.LeftPart(0.15f), "Parts to Hit");
            Widgets.IntRange(hediffMaxPartsRangeRect.RightPart(0.75f), (int)hediffMaxPartsRangeRect.y, ref item.maxPartsRange, 0, 10);
            Rect hediffChanceRect = new(area.x + 10, area.y + 90, area.width - 10, 30);
            Widgets.Label(hediffChanceRect.LeftPart(0.7f), $"Chance to Apply Any: ({item.chance.ToStringPercent()})");
            Widgets.TextFieldPercent(hediffChanceRect.RightPart(0.29f), ref item.chance, ref buffers[bufferIndex++]);
            Rect partsLabelRect = new(area.x + 10, area.y + 130, area.width - 10, 30);
            Widgets.Label(partsLabelRect, "Body Parts to Hit (None if should not target specific parts)");
            Rect validPartsRect = new(area.x, area.y + 160, (area.width) * 0.5f, area.height - 170);
            IEnumerable<BodyPartDef> bodyPartDefs = (Current.Race?.race ?? Current.Def.RaceProps).body.AllParts.Select(bpr => bpr.def).Distinct().ToList();
            item.parts ??= [];
            DrawDefRefList(validPartsRect, true, ref scrolls[scrollIndex++], item.parts, null, bodyPartDefs);
            tempUI.Gap(3);
        }
    }

    private void DrawTechMoney(Rect rect, bool active, FloatRange defaultRange)
    {
        DrawFloatRange(rect, active, ref Current.TechMoney, Current.Def.techHediffsMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
    }

    private void DrawTechTags(Rect rect, bool active, List<string> defaultTags)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.TechHediffTags, Current.Def.techHediffsTags, DefCache.AllTechHediffTags);
    }

    private void DrawDisallowedTechTags(Rect rect, bool active, List<string> defaultTags)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.TechHediffDisallowedTags, Current.Def.techHediffsDisallowTags, DefCache.AllTechHediffTags);
    }

    private void DrawRequiredTech(Rect rect, bool active, List<DefRef<ThingDef>> _)
    {
        DrawDefRefList(rect, active, ref scrolls[scrollIndex++], Current.TechRequired, DefaultKind.techHediffsRequired, DefCache.AllTech);
    }

    private void DrawTechChance(Rect rect, bool active, float def)
    {
        DrawChance(ref Current.TechHediffChance, def, rect, active);
    }

    private void DrawMaxTech(Rect rect, bool active, int _)
    {
        int currentTechHediffsMaxAmount = Current.TechHediffsMaxAmount ?? 1;
        if (maxTechBuffer == null && active)
            maxTechBuffer = currentTechHediffsMaxAmount.ToString();

        if (active)
        {
            int value = currentTechHediffsMaxAmount;
            Widgets.IntEntry(rect, ref value, ref maxTechBuffer);
            Current.TechHediffsMaxAmount = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.techHediffsMaxAmount}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }
}
