using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Draws the pawn sub-lists (Options / Guards / Traders / Carriers) inside an
/// expanded <see cref="PawnGroupMakerEdit"/> body, and provides the matching
/// height calculation used to size the scroll inner rect.
/// </summary>
public static class PawnListDrawer
{
    /// <summary>
    /// Returns the height that <see cref="Draw"/> will consume for <paramref name="list"/>.
    /// </summary>
    public static float CalcHeight(List<PawnGenOptionEdit> list)
    {
        float h = 24f; // section header row
        h += 2f; // Gap(2f) after header
        h += (list.Count == 0 ? 1 : list.Count) * 24f; // "(none)" label or item rows
        h += 12f; // GapLine at end
        return h;
    }

    /// <summary>
    /// Draws a titled, scrollable pawn-list section.
    /// In read-only mode each entry is shown as a non-interactive label.
    /// In edit mode each entry has a weight text field and a remove button,
    /// and a picker button lets the user add new entries.
    /// </summary>
    public static void Draw(
        Listing_Standard ui,
        int groupIndex,
        string listId,
        string sectionLabel,
        string sectionTooltip,
        string addButtonLabel,
        List<PawnGenOptionEdit> list,
        bool readOnly,
        Dictionary<(int, string), string> numBuffers
    )
    {
        // Section header row
        Rect headerRow = ui.GetRect(24f);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;

        if (readOnly)
        {
            Widgets.Label(new Rect(headerRow.x, headerRow.y, headerRow.width, headerRow.height), $"<b>{sectionLabel}</b>");
        }
        else
        {
            float addBtnW = Mathf.Max(120f, Text.CalcSize(addButtonLabel).x + 16f);
            Rect headerLabel = new(headerRow.x, headerRow.y, headerRow.width - addBtnW - 4f, headerRow.height);
            Rect addBtn = new(headerRow.xMax - addBtnW, headerRow.y, addBtnW, 22f);

            Widgets.Label(headerLabel, $"<b>{sectionLabel}</b>");

            // "(?) " tooltip after header
            Rect tipRect = new(headerLabel.xMax, headerRow.y, 20f, headerRow.height);
            GUI.color = Color.grey;
            Widgets.Label(tipRect, "(?)");
            GUI.color = Color.white;
            TooltipHandler.TipRegion(tipRect, sectionTooltip);

            if (Widgets.ButtonText(addBtn, addButtonLabel))
            {
                Find.WindowStack.Add(new Dialog_PawnKindPicker(sectionLabel, list, defName => list.Add(new PawnGenOptionEdit { KindDefName = defName, SelectionWeight = 1f })));
            }
        }

        Text.Anchor = TextAnchor.UpperLeft;
        ui.Gap(2f);

        if (list.Count == 0)
        {
            GUI.color = Color.grey;
            ui.Label("<i>" + "FactionLoadout_GroupEditor_NoPawns".Translate() + "</i>");
            GUI.color = Color.white;
        }
        else if (readOnly)
        {
            foreach (PawnGenOptionEdit entry in list)
            {
                string kindLabel = entry.KindDef?.LabelCap ?? entry.KindDefName;
                if (string.IsNullOrEmpty(kindLabel))
                    kindLabel = "FactionLoadout_GroupEditor_UnknownKind".Translate();
                Rect row = ui.GetRect(24f);
                GUI.color = entry.KindDef == null ? Color.grey : Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 4f, row.height), $"{kindLabel}  <color=grey>(weight: {entry.SelectionWeight:0.##})</color>");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
        else
        {
            List<PawnGenOptionEdit> toRemove = [];
            for (int i = 0; i < list.Count; i++)
            {
                PawnGenOptionEdit entry = list[i];
                string entryBufKey = $"{groupIndex}_{listId}_{i}";

                Rect row = ui.GetRect(24f);
                Widgets.DrawHighlightIfMouseover(row);

                // Name label
                string kindLabel = entry.KindDef?.LabelCap ?? entry.KindDefName;
                if (string.IsNullOrEmpty(kindLabel))
                    kindLabel = "FactionLoadout_GroupEditor_UnknownKind".Translate();
                bool missing = entry.KindDef == null;
                if (missing)
                    GUI.color = Color.grey;

                Rect nameLbl = new(row.x, row.y, row.width - 148f, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameLbl, missing ? $"<color=grey>{kindLabel} {"FactionLoadout_Missing".Translate()}</color>" : kindLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                // "weight" label
                Rect weightLbl = new(row.xMax - 146f, row.y, 48f, row.height);
                GUI.color = Color.grey;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(weightLbl, "FactionLoadout_GroupEditor_WeightLabel".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                TooltipHandler.TipRegion(weightLbl, "FactionLoadout_GroupEditor_WeightTooltip".Translate());

                // Weight field
                Rect weightField = new(row.xMax - 86f, row.y + 1f, 56f, 22f);
                if (!numBuffers.TryGetValue((groupIndex, entryBufKey), out string wbuf))
                    wbuf = entry.SelectionWeight.ToString("0.##");

                string newWbuf = Widgets.TextField(weightField, wbuf);
                numBuffers[(groupIndex, entryBufKey)] = newWbuf;
                if (float.TryParse(newWbuf, out float parsedW))
                    entry.SelectionWeight = Mathf.Max(0.01f, parsedW);

                // Remove button
                Rect delBtn = new(row.xMax - 26f, row.y + 2f, 22f, 22f);
                GUI.color = Color.red;
                if (Widgets.ButtonText(delBtn, "×"))
                    toRemove.Add(entry);
                GUI.color = Color.white;
            }

            foreach (PawnGenOptionEdit e in toRemove)
                list.Remove(e);
        }

        ui.GapLine();
    }
}
