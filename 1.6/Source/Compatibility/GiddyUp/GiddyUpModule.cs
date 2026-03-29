using System.Collections.Generic;
using FactionLoadout;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Total Control module that allows configuring GiddyUp mount settings per PawnKindEdit.
/// Users can set mount chance and specify which animals a pawnkind can mount with weighted probabilities.
/// </summary>
public class GiddyUpModule : ITotalControlModule
{
    // Support both the Owlchemist original and the MemeGoddess fork (mutually incompatible, so at most one will be active)
    private const string GiddyUpPackageIdOwlchemist = "Owlchemist.GiddyUp";
    private const string GiddyUpPackageIdMemeGoddess = "MemeGoddess.GiddyUp";

    public string ModuleKey => "giddyUp";
    public string ModuleName => "GiddyUp Mounts";
    public bool IsActive => ModsConfig.IsActive(GiddyUpPackageIdOwlchemist) || ModsConfig.IsActive(GiddyUpPackageIdMemeGoddess);

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, GiddyUpData> dataStore = new();

    // Per-FactionEdit data storage
    private static readonly Dictionary<FactionEdit, GiddyUpFactionData> factionDataStore = new();

    public static GiddyUpFactionData GetFactionData(FactionEdit edit)
    {
        return factionDataStore.TryGetValue(edit, out GiddyUpFactionData data) ? data : null;
    }

    public static GiddyUpFactionData GetOrCreateFactionData(FactionEdit edit)
    {
        if (!factionDataStore.TryGetValue(edit, out GiddyUpFactionData data))
        {
            data = new GiddyUpFactionData();
            factionDataStore[edit] = data;
        }

        return data;
    }

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
        bool disableMounts = data.DisableMounts ?? false;

        Scribe_Values.Look(ref mountChanceRaw, "mountChance", -1);
        Scribe_Collections.Look(ref mounts, "possibleMounts", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref disableMounts, "disableMounts", false);

        data.MountChance = mountChanceRaw >= 0 ? mountChanceRaw : null;
        data.PossibleMounts = mounts;
        data.DisableMounts = disableMounts ? true : null;

        // Clean up empty data to avoid cluttering saved XML
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (data.MountChance == null && data.DisableMounts == null && (data.PossibleMounts == null || data.PossibleMounts.Count == 0))
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
        bool? disableMounts = data?.DisableMounts ?? globalData?.DisableMounts;
        int? mountChance = data?.MountChance ?? globalData?.MountChance;
        Dictionary<string, int> possibleMounts = data?.PossibleMounts ?? globalData?.PossibleMounts;

        if (disableMounts == null && mountChance == null && (possibleMounts == null || possibleMounts.Count == 0))
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

        // DisableMounts takes priority: writes -1 so GiddyUp always skips mount generation.
        // MountChance 0 writes 0, which GiddyUp treats as "no pawnkind override" — falls through to faction defaults.
        // MountChance 1-100 writes that value as a direct override.
        if (disableMounts == true)
        {
            GiddyUpReflection.MountChanceField.SetValue(extension, -1);
        }
        else if (mountChance != null)
        {
            GiddyUpReflection.MountChanceField.SetValue(extension, mountChance.Value);
        }

        if (possibleMounts is not { Count: > 0 })
            return;

        // Convert defName strings to PawnKindDef keys
        Dictionary<PawnKindDef, int> resolved = new();
        foreach (KeyValuePair<string, int> kvp in possibleMounts)
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kvp.Key);
            if (kindDef != null)
            {
                resolved[kindDef] = kvp.Value;
            }
            else
            {
                ModCore.Warn($"GiddyUp module: Could not resolve PawnKindDef '{kvp.Key}' for mount.");
            }
        }

        if (resolved.Count > 0)
            GiddyUpReflection.PossibleMountsField.SetValue(extension, resolved);
    }

    public void CopyData(PawnKindEdit source, PawnKindEdit dest)
    {
        GiddyUpData data = GetData(source);
        if (data == null)
            return;

        GiddyUpData copy = new()
        {
            MountChance = data.MountChance,
            DisableMounts = data.DisableMounts,
            PossibleMounts = data.PossibleMounts != null ? new Dictionary<string, int>(data.PossibleMounts) : null,
        };
        dataStore[dest] = copy;
    }

    public void AddFactionUI(FactionEdit edit, Listing_Standard ui)
    {
        ui.GapLine();

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Rect row = ui.GetRect(30f);
        Widgets.Label(row.LeftPartPixels(row.width - 224f), "<b>" + "GU_FactionMounts".Translate() + "</b>");
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonText(row.RightPartPixels(220f), "GU_EditFactionMounts".Translate()))
            Find.WindowStack.Add(new GiddyUpFactionDialog(edit));
    }

    public void ExposeFactionData(FactionEdit edit)
    {
        GiddyUpFactionData data = GetOrCreateFactionData(edit);

        int mountChanceRaw = data.MountChance ?? -1;
        int wildWeightRaw = data.WildAnimalWeight ?? -1;
        int nonWildWeightRaw = data.NonWildAnimalWeight ?? -1;
        List<string> wildAnimals = data.AllowedWildAnimals;
        List<string> nonWildAnimals = data.AllowedNonWildAnimals;

        Scribe_Values.Look(ref mountChanceRaw, "mountChance", -1);
        Scribe_Values.Look(ref wildWeightRaw, "wildAnimalWeight", -1);
        Scribe_Values.Look(ref nonWildWeightRaw, "nonWildAnimalWeight", -1);
        Scribe_Collections.Look(ref wildAnimals, "allowedWildAnimals", LookMode.Value);
        Scribe_Collections.Look(ref nonWildAnimals, "allowedNonWildAnimals", LookMode.Value);

        data.MountChance = mountChanceRaw >= 0 ? mountChanceRaw : null;
        data.WildAnimalWeight = wildWeightRaw >= 0 ? wildWeightRaw : null;
        data.NonWildAnimalWeight = nonWildWeightRaw >= 0 ? nonWildWeightRaw : null;
        data.AllowedWildAnimals = wildAnimals;
        data.AllowedNonWildAnimals = nonWildAnimals;

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            bool isEmpty =
                data.MountChance == null
                && data.WildAnimalWeight == null
                && data.NonWildAnimalWeight == null
                && (data.AllowedWildAnimals == null || data.AllowedWildAnimals.Count == 0)
                && (data.AllowedNonWildAnimals == null || data.AllowedNonWildAnimals.Count == 0);

            if (isEmpty)
                factionDataStore.Remove(edit);
        }
    }

    public void ApplyFaction(FactionEdit edit, FactionDef def)
    {
        if (!GiddyUpReflection.IsFactionResolved)
            return;

        GiddyUpFactionData data = GetFactionData(edit);
        if (data == null)
            return;

        bool hasData =
            data.MountChance != null
            || data.WildAnimalWeight != null
            || data.NonWildAnimalWeight != null
            || (data.AllowedWildAnimals != null && data.AllowedWildAnimals.Count > 0)
            || (data.AllowedNonWildAnimals != null && data.AllowedNonWildAnimals.Count > 0);

        if (!hasData)
            return;

        def.modExtensions ??= [];

        DefModExtension extension = def.modExtensions.FirstOrDefault(e => e.GetType() == GiddyUpReflection.FactionRestrictionsType);
        if (extension == null)
        {
            extension = AccessTools.CreateInstance(GiddyUpReflection.FactionRestrictionsType) as DefModExtension;
            if (extension == null)
            {
                ModCore.Warn("GiddyUp module: Failed to create FactionRestrictions instance.");
                return;
            }

            def.modExtensions.Add(extension);
        }

        if (data.MountChance != null)
            GiddyUpReflection.FactionMountChanceField.SetValue(extension, data.MountChance.Value);

        if (data.WildAnimalWeight != null && GiddyUpReflection.WildAnimalWeightField != null)
            GiddyUpReflection.WildAnimalWeightField.SetValue(extension, data.WildAnimalWeight.Value);

        if (data.NonWildAnimalWeight != null && GiddyUpReflection.NonWildAnimalWeightField != null)
            GiddyUpReflection.NonWildAnimalWeightField.SetValue(extension, data.NonWildAnimalWeight.Value);

        if (data.AllowedWildAnimals is { Count: > 0 } && GiddyUpReflection.AllowedWildAnimalsField != null)
        {
            List<PawnKindDef> resolved = ResolveAnimalList(data.AllowedWildAnimals);
            if (resolved.Count > 0)
                GiddyUpReflection.AllowedWildAnimalsField.SetValue(extension, resolved);
        }

        if (data.AllowedNonWildAnimals is { Count: > 0 } && GiddyUpReflection.AllowedNonWildAnimalsField != null)
        {
            List<PawnKindDef> resolved = ResolveAnimalList(data.AllowedNonWildAnimals);
            if (resolved.Count > 0)
                GiddyUpReflection.AllowedNonWildAnimalsField.SetValue(extension, resolved);
        }
    }

    private static List<PawnKindDef> ResolveAnimalList(List<string> defNames)
    {
        List<PawnKindDef> result = [];
        foreach (string defName in defNames)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            if (kind != null)
            {
                result.Add(kind);
            }
            else
            {
                ModCore.Warn($"GiddyUp module: Could not resolve PawnKindDef '{defName}' for faction animal whitelist.");
            }
        }

        return result;
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        // Only humanlike pawns can be configured as riders
        if (defaultKind.RaceProps.Animal)
            return;

        tabs.Add(new Tab("GiddyUp_Mounts".Translate(), ui => GiddyUpUI.DrawMountsTab(ui, edit, defaultKind)));
    }
}
