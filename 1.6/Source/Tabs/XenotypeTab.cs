using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class XenotypeTab : EditTab
{
    public XenotypeTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_Xenotypes".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawSpecificGenes(ui, ref Current.ForcedGenes, "FactionLoadout_Xenotype_RequiredAdvanced".Translate().ToString(), _ => true, DefCache.AllGeneDefs.First());

        DrawForceSpecificXenos(ui);
        if (!Current.ForceSpecificXenos)
            return;

        ui.Label("<b>Xenotype spawn rates:</b>");
        List<string> toDelete = [];
        if (Current.ForcedXenotypeChances.NullOrEmpty())
        {
            Current.ForcedXenotypeChances = Current.Def?.xenotypeSet?.xenotypeChances?.ToDictionary(x => x.xenotype.defName, x => x.chance) ?? new Dictionary<string, float>();
            if (!Current.ForcedXenotypeChances.ContainsKey(FactionEditUI.BaselinerDefName))
                Current.ForcedXenotypeChances.Add(FactionEditUI.BaselinerDefName, Current.Def?.xenotypeSet?.BaselinerChance ?? 1f);
        }

        foreach (string key in Current.ForcedXenotypeChances.Keys.ToList())
            Current.ForcedXenotypeChances[key] = UIHelpers.SliderLabeledWithDelete(
                ui,
                $"{DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap ?? key}: {Current.ForcedXenotypeChances[key].ToStringPercent()}",
                Current.ForcedXenotypeChances[key],
                0f,
                1f,
                deleteAction: delegate
                {
                    toDelete.Add(key);
                }
            );

        foreach (string delete in toDelete)
            Current.ForcedXenotypeChances.Remove(delete);

        if (!ui.ButtonText("Add".Translate().CapitalizeFirst() + "..."))
            return;
        var xenoItems = CustomFloatMenu.MakeItems(
            DefDatabase<XenotypeDef>.AllDefs.Where(def => !Current.ForcedXenotypeChances.ContainsKey(def.defName)),
            def => new MenuItemText(def, def.LabelCap, def.Icon)
        );
        CustomFloatMenu.Open(
            xenoItems,
            item =>
            {
                XenotypeDef def = item.GetPayload<XenotypeDef>();
                Current.ForcedXenotypeChances[def.defName] = 0.1f;
            }
        );
    }

    // --- Private draw methods ---

    private void DrawForceSpecificXenos(Listing_Standard ui)
    {
        Rect xenoBox = ui.GetRect(32);
        Widgets.CheckboxLabeled(xenoBox, "FactionLoadout_Xenotype_ForceSpecific".Translate(), ref Current.ForceSpecificXenos, placeCheckboxNearText: true);
        ui.Gap();
    }

    private void DrawSpecificGenes(Listing_Standard ui, ref List<ForcedGene> edits, string label, Func<GeneDef, bool> geneFilter, GeneDef defaultGeneDef)
    {
        float height = edits == null ? 32 : 340;

        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = edits != null;
        string overrideLabel = "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate());
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), overrideLabel))
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
            DrawSpecificGeneContent(tempUI, geneFilter, edits);
            tempUI.End();
            Widgets.EndScrollView();
            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, "<b>" + "Add".Translate().CapitalizeFirst() + "</b>"))
                edits.Add(new ForcedGene { GeneDef = defaultGeneDef });
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

    private void DrawSpecificGeneContent(Listing_Standard tempUI, Func<GeneDef, bool> geneFilter, List<ForcedGene> edits)
    {
        bool active = edits != null;
        if (!active)
            return;

        for (int i = 0; i < edits.Count; i++)
        {
            ForcedGene item = edits[i];

            if (item?.GeneDef == null)
                continue;

            Rect area = tempUI.GetRect(150);
            Widgets.DrawBoxSolidWithOutline(area, default, Color.white * 0.75f);

            Rect delete = new(area.xMax - 105, area.y + 5, 100, 20);
            GUI.color = Color.red;
            if (Widgets.ButtonText(delete, "<b>" + "Remove".Translate().ToString().ToUpper() + "</b>"))
            {
                edits.RemoveAt(i);
                i--;
            }

            GUI.color = Color.white;

            tempUI.Gap(2);
            Rect geneRect = new(area.x + 5, area.y + 5, 250, 25);
            if (Widgets.ButtonText(geneRect, item.GeneDef.LabelCap))
            {
                IEnumerable<GeneDef> defs = DefDatabase<GeneDef>.AllDefsListForReading.Where(geneFilter);
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                    defs,
                    d => new MenuItemText(d, $"{d.LabelCap} ({d.defName})", DefUtils.TryGetIcon(d, out Color c), c, d.description)
                );
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        GeneDef a = raw.GetPayload<GeneDef>();
                        item.GeneDef = a;
                    }
                );
            }

            Rect xenogeneRect = new(area.x + 10, area.y + 40, area.width - 10, 30);
            Widgets.CheckboxLabeled(xenogeneRect, "FactionLoadout_Xenotype_Xenogene".Translate(), ref item.xenogene);
            Rect forceActiveRect = new(area.x + 10, area.y + 70, area.width - 10, 30);
            Widgets.CheckboxLabeled(forceActiveRect, "FactionLoadout_ForceActive".Translate(), ref item.forceActive);
            Rect geneChanceRect = new(area.x + 10, area.y + 100, area.width - 10, 30);
            Widgets.Label(geneChanceRect.LeftPart(0.7f), "FactionLoadout_ChanceToApply".Translate(item.chance.ToStringPercent()));
            Widgets.TextFieldPercent(geneChanceRect.RightPart(0.29f), ref item.chance, ref buffers[bufferIndex++]);
            tempUI.Gap(3);
        }
    }
}
