using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionLoadout;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Module data for a single PawnKindEdit's GiddyUp mount configuration.
/// </summary>
public class GiddyUpData
{
    public int? MountChance;
    public Dictionary<string, int> PossibleMounts; // defName -> weight
}

/// <summary>
/// Total Control module that allows configuring GiddyUp mount settings per PawnKindEdit.
/// Users can set mount chance and specify which animals a pawnkind can mount with weighted probabilities.
/// </summary>
public class GiddyUpModule : ITotalControlModule
{
    private const string GiddyUpPackageId = "Owlchemist.GiddyUp";

    public string ModuleKey => "giddyUp";
    public string ModuleName => "GiddyUp Mounts";
    public bool IsActive => ModLister.GetActiveModWithIdentifier(GiddyUpPackageId) is not null;

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, GiddyUpData> dataStore = new();

    // Cached reflection references
    private static Type customMountsType;
    private static FieldInfo mountChanceField;
    private static FieldInfo possibleMountsField;

    public void Initialize()
    {
        // Resolve GiddyUp types via reflection (they are internal)
        customMountsType = AccessTools.TypeByName("GiddyUp.CustomMounts");
        if (customMountsType == null)
        {
            ModCore.Warn("GiddyUp module: Could not find GiddyUp.CustomMounts type.");
            return;
        }

        mountChanceField = AccessTools.Field(customMountsType, "mountChance");
        possibleMountsField = AccessTools.Field(customMountsType, "possibleMounts");

        if (mountChanceField == null || possibleMountsField == null)
            ModCore.Warn("GiddyUp module: Could not resolve CustomMounts fields via reflection.");
    }

    public static GiddyUpData GetData(PawnKindEdit edit)
    {
        return dataStore.TryGetValue(edit, out GiddyUpData data) ? data : null;
    }

    public static GiddyUpData GetOrCreateData(PawnKindEdit edit)
    {
        if (!dataStore.TryGetValue(edit, out GiddyUpData data))
        {
            data = new GiddyUpData();
            dataStore[edit] = data;
        }
        return data;
    }

    public void ExposeData(PawnKindEdit edit)
    {
        GiddyUpData data = GetOrCreateData(edit);

        // Scribe_Values doesn't handle Nullable<int> reliably, so use -1 as sentinel for "no override".
        int mountChanceRaw = data.MountChance ?? -1;
        Dictionary<string, int> mounts = data.PossibleMounts;

        Scribe_Values.Look(ref mountChanceRaw, "mountChance", -1);
        Scribe_Collections.Look(ref mounts, "possibleMounts", LookMode.Value, LookMode.Value);

        data.MountChance = mountChanceRaw >= 0 ? mountChanceRaw : null;
        data.PossibleMounts = mounts;

        // Clean up empty data to avoid cluttering saved XML
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (data.MountChance == null && (data.PossibleMounts == null || data.PossibleMounts.Count == 0))
                dataStore.Remove(edit);
        }
    }

    public void Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global)
    {
        if (customMountsType == null || mountChanceField == null || possibleMountsField == null)
            return;

        GiddyUpData data = GetData(edit);
        GiddyUpData globalData = global != null ? GetData(global) : null;

        // Merge: specific edit overrides global
        int? mountChance = data?.MountChance ?? globalData?.MountChance;
        Dictionary<string, int> possibleMounts = data?.PossibleMounts ?? globalData?.PossibleMounts;

        if (mountChance == null && (possibleMounts == null || possibleMounts.Count == 0))
            return; // Nothing to apply

        def.modExtensions ??= [];

        // Find or create the CustomMounts extension
        DefModExtension extension = def.modExtensions.FirstOrDefault(e => e.GetType() == customMountsType);
        if (extension == null)
        {
            extension = AccessTools.CreateInstance(customMountsType) as DefModExtension;
            if (extension == null)
            {
                ModCore.Warn("GiddyUp module: Failed to create CustomMounts instance.");
                return;
            }
            def.modExtensions.Add(extension);
        }

        if (mountChance != null)
            mountChanceField.SetValue(extension, mountChance.Value);

        if (possibleMounts is { Count: > 0 })
        {
            // Convert defName strings to PawnKindDef keys
            Dictionary<PawnKindDef, int> resolved = new();
            foreach (KeyValuePair<string, int> kvp in possibleMounts)
            {
                PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kvp.Key);
                if (kindDef != null)
                    resolved[kindDef] = kvp.Value;
                else
                    ModCore.Warn($"GiddyUp module: Could not resolve PawnKindDef '{kvp.Key}' for mount.");
            }

            if (resolved.Count > 0)
                possibleMountsField.SetValue(extension, resolved);
        }
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        // Only humanlike pawns can be configured as riders
        if (defaultKind.RaceProps.Animal)
            return;

        tabs.Add(new Tab("GiddyUp_Mounts".Translate(), ui => DrawMountsTab(ui, edit)));
    }

    private void DrawMountsTab(Listing_Standard ui, PawnKindEdit edit)
    {
        GiddyUpData data = GetOrCreateData(edit);

        // Mount Chance
        Rect chanceRect = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = chanceRect.LeftHalf();
        Rect fieldRect = chanceRect.RightHalf();

        bool hasChance = data.MountChance != null;
        string chanceLabel = hasChance
            ? $"Mount Chance: {data.MountChance}%"
            : "Mount Chance: (default)";
        Widgets.Label(labelRect, chanceLabel);

        if (hasChance)
        {
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
            if (Widgets.ButtonText(fieldRect.LeftPart(0.4f), "Override"))
                data.MountChance = 50;
        }

        ui.GapLine();

        // Possible Mounts header
        Widgets.Label(ui.GetRect(Text.LineHeight + 4), "<b>Possible Mounts</b> (animal → weight)");
        ui.Gap(4);

        data.PossibleMounts ??= new Dictionary<string, int>();

        // List existing mounts
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
