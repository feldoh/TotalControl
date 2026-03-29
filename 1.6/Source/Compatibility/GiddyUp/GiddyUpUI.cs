using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionLoadout;
using FactionLoadout.UISupport;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// UI drawing for the GiddyUp mounts tab.
/// </summary>
public static class GiddyUpUI
{
    /// <summary>
    /// Read the existing CustomMounts extension from a PawnKindDef (if any).
    /// Returns the mount chance and possible mounts dictionary, or nulls if not present.
    /// </summary>
    public static void ReadDefaults(PawnKindDef def, out int? defMountChance, out Dictionary<PawnKindDef, int> defPossibleMounts)
    {
        defMountChance = null;
        defPossibleMounts = null;

        System.Type customMountsType = GiddyUpReflection.CustomMountsType;
        FieldInfo mountChanceField = GiddyUpReflection.MountChanceField;
        FieldInfo possibleMountsField = GiddyUpReflection.PossibleMountsField;

        if (customMountsType == null || def?.modExtensions == null)
            return;

        DefModExtension ext = def.modExtensions.FirstOrDefault(e => e.GetType() == customMountsType);
        if (ext == null)
            return;

        if (mountChanceField != null)
        {
            int val = (int)mountChanceField.GetValue(ext);
            if (val != 0)
                defMountChance = val;
        }

        if (possibleMountsField == null)
            return;

        defPossibleMounts = possibleMountsField.GetValue(ext) as Dictionary<PawnKindDef, int>;
        if (defPossibleMounts is { Count: 0 })
            defPossibleMounts = null;
    }

    public static void DrawMountsTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        GiddyUpData data = GiddyUpModule.GetOrCreateData(edit);

        // Read existing defaults from the def's CustomMounts extension (if any)
        ReadDefaults(defaultKind, out int? defMountChance, out Dictionary<PawnKindDef, int> defPossibleMounts);

        // --- Disable Mounts checkbox ---
        Rect disableRow = ui.GetRect(Text.LineHeight + 4);
        bool disableMounts = data.DisableMounts ?? false;
        Widgets.CheckboxLabeled(disableRow, "GU_DisableMounts".Translate(), ref disableMounts);
        TooltipHandler.TipRegion(disableRow, "GU_DisableMounts_Tip".Translate().ToString());
        data.DisableMounts = disableMounts ? true : (bool?)null;

        ui.GapLine();

        // --- Mount Chance section (greyed when Disable Mounts is active) ---
        Color prevColor = GUI.color;
        if (disableMounts)
            GUI.color = Color.grey;

        Rect headerRow = ui.GetRect(Text.LineHeight + 4);
        Widgets.Label(headerRow, "<b>" + "GU_MountChance".Translate() + "</b>");

        bool hasChance = data.MountChance != null;
        Rect chanceRow = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = chanceRow.LeftHalf();
        Rect fieldRect = chanceRow.RightHalf();

        if (hasChance)
        {
            Widgets.Label(labelRect, "GU_MountChanceValue".Translate(data.MountChance.Value));

            Rect sliderRect = fieldRect.LeftPart(0.7f);
            Rect clearRect = fieldRect.RightPart(0.25f);
            int val = data.MountChance.Value;

            GUI.enabled = !disableMounts;
            val = (int)Widgets.HorizontalSlider(sliderRect, val, 0, 100, true, val + "%");
            if (!disableMounts)
                data.MountChance = val;
            GUI.enabled = true;

            if (Widgets.ButtonText(clearRect, "FactionLoadout_FactionDefault".Translate()))
                data.MountChance = null;
        }
        else
        {
            string defaultLabel = defMountChance != null
                ? "GU_MountChanceDefault_Known".Translate(defMountChance.Value).ToString()
                : "GU_MountChanceDefault_Unknown".Translate().ToString();
            Widgets.Label(labelRect, defaultLabel);

            GUI.enabled = !disableMounts;
            if (Widgets.ButtonText(fieldRect.LeftPart(0.4f), "FactionLoadout_Override".Translate()))
                data.MountChance = defMountChance ?? 50;
            GUI.enabled = true;
        }

        GUI.color = prevColor;

        // Informational note when slider is at 0 (not an error — 0 inherits faction defaults in GiddyUp)
        if (hasChance && data.MountChance == 0)
        {
            string noteText = "GU_ZeroMountChanceNote".Translate().ToString();
            float noteHeight = Text.CalcHeight(noteText, ui.ColumnWidth) + 4f;
            Rect noteRect = ui.GetRect(noteHeight);
            Widgets.DrawBoxSolid(noteRect, new Color(1f, 0.85f, 0f, 0.12f));
            Color prevCol = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.3f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(noteRect.ContractedBy(2f), noteText);
            Text.Font = GameFont.Small;
            GUI.color = prevCol;
        }

        ui.GapLine();

        // --- Possible Mounts ---
        Widgets.Label(ui.GetRect(Text.LineHeight + 4), "<b>" + "GU_PossibleMounts".Translate() + "</b>");
        ui.Gap(4);

        // Show def defaults if the user hasn't overridden and the def has custom mounts
        if (defPossibleMounts is { Count: > 0 } && (data.PossibleMounts == null || data.PossibleMounts.Count == 0))
        {
            Color prevCol = GUI.color;
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(ui.GetRect(Text.LineHeight), "GU_DefDefaults".Translate());
            Text.Font = GameFont.Small;
            foreach (KeyValuePair<PawnKindDef, int> kvp in defPossibleMounts)
            {
                Rect row = ui.GetRect(Text.LineHeight);
                Widgets.Label(row, "  " + kvp.Key.LabelCap + "  (" + "GU_Weight".Translate(kvp.Value) + ")");
            }
            GUI.color = prevCol;
            ui.Gap(4);
        }

        data.PossibleMounts ??= new Dictionary<string, int>();

        // List existing user-configured mounts
        string toRemove = null;
        foreach (KeyValuePair<string, int> kvp in data.PossibleMounts)
        {
            Rect row = ui.GetRect(Text.LineHeight + 4);
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kvp.Key);
            string label = kindDef?.LabelCap ?? kvp.Key;

            Widgets.Label(row.LeftPart(0.4f), label);

            Rect weightRect = row.RightPart(0.55f).LeftPart(0.5f);
            string buffer = kvp.Value.ToString();
            int weight = kvp.Value;
            Widgets.TextFieldNumeric(weightRect, ref weight, ref buffer, 1, 9999);
            if (weight != kvp.Value)
                data.PossibleMounts[kvp.Key] = weight;

            Rect removeRect = row.RightPart(0.25f);
            if (Widgets.ButtonText(removeRect, "Remove".Translate()))
                toRemove = kvp.Key;
        }

        if (toRemove != null)
            data.PossibleMounts.Remove(toRemove);

        ui.Gap(4);

        // Add mount button
        if (ui.ButtonText("FactionLoadout_AddMount".Translate()))
        {
            var mountItems = CustomFloatMenu.MakeItems(
                DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.RaceProps.Animal && !data.PossibleMounts.ContainsKey(k.defName)),
                k => new MenuItemText(k, k.LabelCap, tooltip: k.description)
            );
            if (mountItems.Count > 0)
            {
                CustomFloatMenu.Open(
                    mountItems,
                    item =>
                    {
                        PawnKindDef k = item.GetPayload<PawnKindDef>();
                        data.PossibleMounts[k.defName] = 100;
                    }
                );
            }
        }

        // Clear all button
        if (data.PossibleMounts.Count > 0 && ui.ButtonText("FactionLoadout_ClearMounts".Translate()))
        {
            data.PossibleMounts.Clear();
        }
    }
}
