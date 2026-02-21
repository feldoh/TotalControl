using System.Collections.Generic;
using FactionLoadout;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using HarmonyLib;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Total Control module that allows configuring GiddyUp mount settings per PawnKindEdit.
/// Users can set mount chance and specify which animals a pawnkind can mount with weighted probabilities.
/// </summary>
public class GiddyUpModule : ITotalControlModule
{
    private const string GiddyUpPackageId = "Owlchemist.GiddyUp";

    public string ModuleKey => "giddyUp";
    public string ModuleName => "GiddyUp Mounts";
    public bool IsActive => ModsConfig.IsActive(GiddyUpPackageId);

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, GiddyUpData> dataStore = new();

    public void Initialize() => GiddyUpReflection.Resolve();

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
        if (!GiddyUpReflection.IsResolved)
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
        DefModExtension extension = def.modExtensions.FirstOrDefault(e => e.GetType() == GiddyUpReflection.CustomMountsType);
        if (extension == null)
        {
            extension = AccessTools.CreateInstance(GiddyUpReflection.CustomMountsType) as DefModExtension;
            if (extension == null)
            {
                ModCore.Warn("GiddyUp module: Failed to create CustomMounts instance.");
                return;
            }

            def.modExtensions.Add(extension);
        }

        if (mountChance != null)
            GiddyUpReflection.MountChanceField.SetValue(extension, mountChance.Value);

        if (possibleMounts is not { Count: > 0 })
            return;

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
            GiddyUpReflection.PossibleMountsField.SetValue(extension, resolved);
    }

    public void CopyData(PawnKindEdit source, PawnKindEdit dest)
    {
        GiddyUpData data = GetData(source);
        if (data == null)
            return;

        GiddyUpData copy = new() { MountChance = data.MountChance, PossibleMounts = data.PossibleMounts != null ? new Dictionary<string, int>(data.PossibleMounts) : null, };
        dataStore[dest] = copy;
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        // Only humanlike pawns can be configured as riders
        if (defaultKind.RaceProps.Animal)
            return;

        tabs.Add(new Tab("GiddyUp_Mounts".Translate(), ui => GiddyUpUI.DrawMountsTab(ui, edit, defaultKind)));
    }
}
