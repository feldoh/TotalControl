using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionLoadout;
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
    public static void ReadDefaults(
        PawnKindDef def,
        out int? defMountChance,
        out Dictionary<PawnKindDef, int> defPossibleMounts)
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

        if (possibleMountsField == null) return;

        defPossibleMounts = possibleMountsField.GetValue(ext) as Dictionary<PawnKindDef, int>;
        if (defPossibleMounts is { Count: 0 })
            defPossibleMounts = null;
    }

    public static void DrawMountsTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        GiddyUpData data = GiddyUpModule.GetOrCreateData(edit);

        // Read existing defaults from the def's CustomMounts extension (if any)
        ReadDefaults(defaultKind, out int? defMountChance, out Dictionary<PawnKindDef, int> defPossibleMounts);

        // --- Mount Chance ---
        Rect chanceRect = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = chanceRect.LeftHalf();
        Rect fieldRect = chanceRect.RightHalf();

        bool hasChance = data.MountChance != null;
        if (hasChance)
        {
            Widgets.Label(labelRect, $"Mount Chance: {data.MountChance}%");

            Rect sliderRect = fieldRect.LeftPart(0.7f);
            Rect clearRect = fieldRect.RightPart(0.25f);
            int val = data.MountChance.Value;
            val = (int)Widgets.HorizontalSlider(sliderRect, val, 0, 100, true, $"{val}%");
            data.MountChance = val;

            if (Widgets.ButtonText(clearRect, "Default"))
                data.MountChance = null;
        }
        else
        {
            string defaultLabel = defMountChance != null
                ? $"Mount Chance: (default: {defMountChance}%)"
                : "Mount Chance: (default)";
            Widgets.Label(labelRect, defaultLabel);

            if (Widgets.ButtonText(fieldRect.LeftPart(0.4f), "Override"))
                data.MountChance = defMountChance ?? 50;
        }

        ui.GapLine();

        // --- Possible Mounts ---
        Widgets.Label(ui.GetRect(Text.LineHeight + 4), "<b>Possible Mounts</b> (animal \u2192 weight)");
        ui.Gap(4);

        // Show def defaults if the user hasn't overridden and the def has custom mounts
        if (defPossibleMounts is { Count: > 0 } && (data.PossibleMounts == null || data.PossibleMounts.Count == 0))
        {
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(ui.GetRect(Text.LineHeight), "Def defaults:");
            Text.Font = GameFont.Small;
            foreach (KeyValuePair<PawnKindDef, int> kvp in defPossibleMounts)
            {
                Rect row = ui.GetRect(Text.LineHeight);
                Widgets.Label(row, $"  {kvp.Key.LabelCap}  (weight {kvp.Value})");
            }
            GUI.color = Color.white;
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
            // Show a float menu of all mountable animal PawnKindDefs
            List<FloatMenuOption> options = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(k => k.RaceProps.Animal && !data.PossibleMounts.ContainsKey(k.defName))
                .OrderBy(k => k.LabelCap.Resolve())
                .Select(k => new FloatMenuOption(k.LabelCap, () =>
                {
                    data.PossibleMounts[k.defName] = 100;
                }))
                .ToList();

            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        // Clear all button
        if (data.PossibleMounts.Count > 0 && ui.ButtonText("FactionLoadout_ClearMounts".Translate()))
        {
            data.PossibleMounts.Clear();
        }
    }
}
