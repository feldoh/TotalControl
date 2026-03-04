using System.Collections.Generic;
using CombatExtended;
using FactionLoadout;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using Verse;

namespace TotalControlCECompat;

/// <summary>
/// Total Control module that allows configuring Combat Extended loadout settings per PawnKindEdit.
/// Users can force a specific ammo category, override primary magazine count, and set minimum ammo.
/// </summary>
public class CEModule : ITotalControlModule
{
    public string ModuleKey => "combatExtended";
    public string ModuleName => "Combat Extended";
    public bool IsActive => ModsConfig.IsActive("CETeam.CombatExtended");

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, CEData> dataStore = new();

    public void Initialize() { }

    public static CEData GetData(PawnKindEdit edit)
    {
        return dataStore.TryGetValue(edit, out CEData data) ? data : null;
    }

    public static CEData GetOrCreateData(PawnKindEdit edit)
    {
        if (!dataStore.TryGetValue(edit, out CEData data))
        {
            data = new CEData();
            dataStore[edit] = data;
        }

        return data;
    }

    public void ExposeData(PawnKindEdit edit)
    {
        CEData data = GetOrCreateData(edit);

        string ammoCategory = data.ForcedAmmoCategoryDefName;
        float magMin = data.PrimaryMagazineCount?.min ?? -1f;
        float magMax = data.PrimaryMagazineCount?.max ?? -1f;
        int minAmmo = data.MinAmmoCount ?? -1;

        Scribe_Values.Look(ref ammoCategory, "forcedAmmoCategory", null);
        Scribe_Values.Look(ref magMin, "magCountMin", -1f);
        Scribe_Values.Look(ref magMax, "magCountMax", -1f);
        Scribe_Values.Look(ref minAmmo, "minAmmoCount", -1);

        data.ForcedAmmoCategoryDefName = ammoCategory;
        data.PrimaryMagazineCount = magMin >= 0f ? new FloatRange(magMin, magMax) : null;
        data.MinAmmoCount = minAmmo >= 0 ? minAmmo : null;

        // Clean up empty data
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (data.ForcedAmmoCategoryDefName == null && data.PrimaryMagazineCount == null && data.MinAmmoCount == null)
                dataStore.Remove(edit);
        }
    }

    public void Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global)
    {
        CEData data = GetData(edit);
        CEData globalData = global != null ? GetData(global) : null;

        // Merge: specific edit overrides global
        string ammoCategoryName = data?.ForcedAmmoCategoryDefName ?? globalData?.ForcedAmmoCategoryDefName;
        FloatRange? magCount = data?.PrimaryMagazineCount ?? globalData?.PrimaryMagazineCount;
        int? minAmmo = data?.MinAmmoCount ?? globalData?.MinAmmoCount;

        if (ammoCategoryName == null && magCount == null && minAmmo == null)
            return;

        AmmoCategoryDef ammoCategory = null;
        if (ammoCategoryName != null)
        {
            ammoCategory = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(ammoCategoryName);
            if (ammoCategory == null)
                ModCore.Warn($"CE module: Could not resolve AmmoCategoryDef '{ammoCategoryName}'.");
        }

        if (ammoCategory == null && magCount == null && minAmmo == null)
            return;

        def.modExtensions ??= [];

        LoadoutPropertiesExtension ext = def.GetModExtension<LoadoutPropertiesExtension>();
        if (ext == null)
        {
            ext = new LoadoutPropertiesExtension();
            def.modExtensions.Add(ext);
        }

        if (ammoCategory != null)
            ext.forcedAmmoCategory = ammoCategory;
        if (magCount.HasValue)
            ext.primaryMagazineCount = magCount.Value;
        if (minAmmo.HasValue)
            ext.minAmmoCount = minAmmo.Value;
    }

    public void CopyData(PawnKindEdit source, PawnKindEdit dest)
    {
        CEData data = GetData(source);
        if (data == null)
            return;

        dataStore[dest] = new CEData
        {
            ForcedAmmoCategoryDefName = data.ForcedAmmoCategoryDefName,
            PrimaryMagazineCount = data.PrimaryMagazineCount,
            MinAmmoCount = data.MinAmmoCount,
        };
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        tabs.Add(new Tab("FactionLoadout_Tab_CE".Translate(), ui => CEUI.DrawTab(ui, edit, defaultKind)));
    }
}
