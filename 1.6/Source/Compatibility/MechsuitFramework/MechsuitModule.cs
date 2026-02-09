using System.Collections.Generic;
using System.Linq;
using Exosuit;
using FactionLoadout;
using RimWorld;
using Verse;

namespace TotalControlMechsuitCompat;

/// <summary>
/// Total Control module for MechsuitFramework (Exosuit Framework) compatibility.
///
/// Fixes issue #43: when Total Control forces mechsuit apparel onto a PawnKindDef that doesn't
/// already have a <see cref="ModExtForceApparelGen"/> extension, the framework's own initialization
/// postfix (on PawnGenerator.GenerateGearFor) never fires — leaving the exosuit core with
/// uninitialized health.
///
/// At Apply time, this module detects exosuit core apparel in the edit's SpecificApparel list and
/// adds the required <see cref="ModExtForceApparelGen"/> extension to the cloned PawnKindDef.
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
        if (defaultKind.RaceProps.Animal)
            return;

        tabs.Add(new Tab("Exosuit", ui => MechsuitUI.DrawExosuitTab(ui, edit, defaultKind)));
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
        List<ThingDef> coreApparel = CollectExosuitCores(edit);

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
            ?? MechsuitUI.ReadDefStructurePointRange(def)
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

        ModCore.Debug($"Mechsuit module: Added ModExtForceApparelGen to {def.defName} with cores: "
            + $"{coreApparel.Select(d => d.defName).ToCommaList()}, SP range: {spRange}");
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
}
