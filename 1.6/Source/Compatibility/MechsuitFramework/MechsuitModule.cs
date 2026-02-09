using System.Collections.Generic;
using System.Linq;
using Exosuit;
using FactionLoadout;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalControlMechsuitCompat;

/// <summary>
/// Per-PawnKindEdit data for MechsuitFramework configuration.
/// </summary>
public class MechsuitData
{
    /// <summary>
    /// Override for the structure point (health) range on spawn.
    /// Null means "use the def default or framework default (1, 1)".
    /// </summary>
    public FloatRange? StructurePointRange;
}

/// <summary>
/// Total Control module for MechsuitFramework (Exosuit Framework) compatibility.
///
/// Fixes issue #43: when Total Control forces mechsuit apparel onto a PawnKindDef that doesn't
/// already have a <see cref="ModExtForceApparelGen"/> extension, the framework's own initialization
/// postfix (on PawnGenerator.GenerateGearFor) never fires — leaving the exosuit core with
/// uninitialized health.
///
/// At Apply time, this module detects exosuit core apparel in the edit's SpecificApparel list and
/// adds the required <see cref="ModExtForceApparelGen"/> extension to the cloned PawnKindDef. This
/// lets MechsuitFramework's own patch handle initialization naturally.
///
/// The UI tab lets users configure the Structure Point Range (health multiplier on spawn).
/// </summary>
public class MechsuitModule : ITotalControlModule
{
    private const string MechsuitPackageId = "Aoba.Exosuit.Framework";

    public string ModuleKey => "mechsuit";
    public string ModuleName => "Exosuit Framework";
    public bool IsActive => ModsConfig.IsActive(MechsuitPackageId);

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, MechsuitData> dataStore = new();

    public void Initialize() { }

    public static MechsuitData GetData(PawnKindEdit edit)
    {
        return dataStore.TryGetValue(edit, out MechsuitData data) ? data : null;
    }

    public static MechsuitData GetOrCreateData(PawnKindEdit edit)
    {
        if (!dataStore.TryGetValue(edit, out MechsuitData data))
        {
            data = new MechsuitData();
            dataStore[edit] = data;
        }
        return data;
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        // Only humanlike pawns wear exosuits
        if (defaultKind.RaceProps.Animal)
            return;

        tabs.Add(new Tab("Exosuit", ui => DrawExosuitTab(ui, edit, defaultKind)));
    }

    public void ExposeData(PawnKindEdit edit)
    {
        MechsuitData data = GetOrCreateData(edit);

        // FloatRange isn't nullable, so use sentinel (-1, -1) for "no override".
        float spMin = data.StructurePointRange?.min ?? -1f;
        float spMax = data.StructurePointRange?.max ?? -1f;

        Scribe_Values.Look(ref spMin, "structurePointMin", -1f);
        Scribe_Values.Look(ref spMax, "structurePointMax", -1f);

        data.StructurePointRange = spMin >= 0f && spMax >= 0f
            ? new FloatRange(spMin, spMax)
            : null;

        // Clean up empty data
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (data.StructurePointRange == null)
                dataStore.Remove(edit);
        }
    }

    public void Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global)
    {
        // Collect exosuit core ThingDefs from the edit's SpecificApparel list.
        List<ThingDef> coreApparel = CollectExosuitCores(edit);

        // Also check the global edit if present.
        if (global != null)
        {
            List<ThingDef> globalCores = CollectExosuitCores(global);
            if (globalCores != null)
            {
                coreApparel ??= [];
                coreApparel.AddRange(globalCores);
            }
        }

        if (coreApparel is not { Count: > 0 })
            return;

        coreApparel = coreApparel.Distinct().ToList();

        // Resolve structure point range: specific edit > global > def default > (0.9, 1)
        MechsuitData data = GetData(edit);
        MechsuitData globalData = global != null ? GetData(global) : null;
        FloatRange spRange = data?.StructurePointRange
            ?? globalData?.StructurePointRange
            ?? ReadDefStructurePointRange(def)
            ?? new FloatRange(0.9f, 1f);

        // If the def already has a ModExtForceApparelGen, just update the structure point range.
        ModExtForceApparelGen existing = def.GetModExtension<ModExtForceApparelGen>();
        if (existing != null)
        {
            if (data?.StructurePointRange != null || globalData?.StructurePointRange != null)
                existing.StructurePointRange = spRange;
            return;
        }

        // Build and attach the extension so MechsuitFramework's PawnGenerator_Patch finds it.
        ModExtForceApparelGen ext = new()
        {
            apparels = coreApparel,
            StructurePointRange = spRange
        };

        def.modExtensions ??= [];
        def.modExtensions.Add(ext);

        ModCore.Debug($"Mechsuit module: Added ModExtForceApparelGen to {def.defName} with cores: {coreApparel.Select(d => d.defName).ToCommaList()}, SP range: {spRange}");
    }

    /// <summary>
    /// Scan a PawnKindEdit's SpecificApparel for ThingDefs whose thingClass is or inherits from Exosuit_Core.
    /// </summary>
    private static List<ThingDef> CollectExosuitCores(PawnKindEdit edit)
    {
        if (edit.SpecificApparel is not { Count: > 0 })
            return null;

        List<ThingDef> cores = null;
        foreach (SpecRequirementEdit spec in edit.SpecificApparel)
        {
            if (spec.Thing == null)
                continue;

            if (!typeof(Exosuit_Core).IsAssignableFrom(spec.Thing.thingClass))
                continue;

            cores ??= [];
            cores.Add(spec.Thing);
        }

        return cores;
    }

    /// <summary>
    /// Read the existing StructurePointRange from a PawnKindDef's ModExtForceApparelGen (if any).
    /// </summary>
    private static FloatRange? ReadDefStructurePointRange(PawnKindDef def)
    {
        ModExtForceApparelGen ext = def?.GetModExtension<ModExtForceApparelGen>();
        return ext?.StructurePointRange;
    }

    private void DrawExosuitTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        MechsuitData data = GetOrCreateData(edit);

        // Read def defaults
        FloatRange? defSPRange = ReadDefStructurePointRange(defaultKind);

        // --- Header ---
        ui.Label("<b>Exosuit Framework</b>");
        ui.GapLine();
        ui.Label("Configure structure point (health) settings for exosuit pawns. "
            + "Exosuit core apparel is assigned via the Apparel tab as normal.");
        ui.Gap(8);

        // --- Structure Point Range ---
        ui.Label("<b>Structure Point Range</b>");
        ui.Gap(2);
        ui.Label("Controls the health multiplier applied to the exosuit core on spawn. "
            + "For example, (0.8, 1.0) means spawned suits will have 80\u2013100% of their max health.");
        ui.Gap(4);

        bool hasOverride = data.StructurePointRange != null;
        if (hasOverride)
        {
            FloatRange range = data.StructurePointRange.Value;

            Rect row = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(row.LeftPart(0.6f), $"Range: {range.min:P0} \u2013 {range.max:P0}");
            if (Widgets.ButtonText(row.RightPart(0.25f), "Default"))
            {
                data.StructurePointRange = null;
                return;
            }

            ui.Gap(4);

            // Min slider
            Rect minRow = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(minRow.LeftPart(0.2f), "Min:");
            float newMin = Widgets.HorizontalSlider(minRow.RightPart(0.75f), range.min, 0f, 1f, true, $"{range.min:P0}");

            // Max slider
            Rect maxRow = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(maxRow.LeftPart(0.2f), "Max:");
            float newMax = Widgets.HorizontalSlider(maxRow.RightPart(0.75f), range.max, 0f, 1f, true, $"{range.max:P0}");

            // Clamp min <= max
            if (newMin > newMax)
                newMin = newMax;

            data.StructurePointRange = new FloatRange(newMin, newMax);
        }
        else
        {
            Rect row = ui.GetRect(Text.LineHeight + 4);
            string defaultLabel = defSPRange != null
                ? $"Structure Points: (default: {defSPRange.Value.min:P0} \u2013 {defSPRange.Value.max:P0})"
                : "Structure Points: (default: 100%)";
            Widgets.Label(row.LeftPart(0.65f), defaultLabel);

            if (Widgets.ButtonText(row.RightPart(0.25f), "Override"))
                data.StructurePointRange = defSPRange ?? new FloatRange(0.9f, 1f);
        }
    }
}
