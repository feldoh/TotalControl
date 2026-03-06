using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using FactionLoadout;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using Verse;

namespace TotalControlCECompat;

/// <summary>
/// Total Control module that allows configuring Combat Extended loadout settings per PawnKindEdit.
/// Exposes all public fields of LoadoutPropertiesExtension: ammo, shields, sidearms, and attachments.
/// </summary>
public class CEModule : ITotalControlModule
{
    public string ModuleKey => "combatExtended";
    public string ModuleName => "Combat Extended";
    public bool IsActive => ModsConfig.IsActive("CETeam.CombatExtended");

    // Per-PawnKindEdit data storage
    private static readonly Dictionary<PawnKindEdit, CEData> dataStore = new();

    /// <summary>
    /// Pre-resolved per-weapon ammo mappings keyed by cloned PawnKindDef.
    /// Built during Apply() so the Harmony patch has zero def-lookup cost at generation time.
    ///
    /// Although the key type is PawnKindDef, this is effectively a (faction × kindDef) map:
    /// TC clones each PawnKindDef once per faction (clone name: {original}_TCCln_{faction}),
    /// so every key in this dictionary is a distinct object. pawn.kindDef at generation time
    /// returns the same clone that Apply() registered, making the lookup inherently faction-aware.
    /// </summary>
    public static readonly Dictionary<PawnKindDef, ResolvedWeaponAmmoEntry[]> KindDefMappings = new();

    /// <summary>
    /// A single pre-resolved weapon→ammo mapping entry.
    /// WeaponKey is either a ThingDef.defName (IsTag=false) or a weapon tag (IsTag=true).
    /// Choices are CE's own WeightedAmmoCategory instances, fully resolved at Apply() time.
    /// </summary>
    public struct ResolvedWeaponAmmoEntry
    {
        public string WeaponKey;
        public bool IsTag;
        public List<WeightedAmmoCategory> Choices;
    }

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

        // --- Ammo ---
        string ammoCategory = data.ForcedAmmoCategoryDefName;
        float magMin = data.PrimaryMagazineCount?.min ?? -1f;
        float magMax = data.PrimaryMagazineCount?.max ?? -1f;
        int minAmmo = data.MinAmmoCount ?? -1;
        List<WeightedAmmoCategoryData> weightedAmmo = data.WeightedAmmoCategories;

        Scribe_Values.Look(ref ammoCategory, "forcedAmmoCategory", null);
        Scribe_Values.Look(ref magMin, "magCountMin", -1f);
        Scribe_Values.Look(ref magMax, "magCountMax", -1f);
        Scribe_Values.Look(ref minAmmo, "minAmmoCount", -1);
        Scribe_Collections.Look(ref weightedAmmo, "weightedAmmoCategories", LookMode.Deep);

        data.ForcedAmmoCategoryDefName = ammoCategory;
        data.PrimaryMagazineCount = magMin >= 0f ? new FloatRange(magMin, magMax) : null;
        data.MinAmmoCount = minAmmo >= 0 ? minAmmo : null;
        data.WeightedAmmoCategories = weightedAmmo?.Count > 0 ? weightedAmmo : null;

        // --- Shield ---
        float shieldMoneyMin = data.ShieldMoney?.min ?? -1f;
        float shieldMoneyMax = data.ShieldMoney?.max ?? -1f;
        float shieldChance = data.ShieldChance ?? -1f;
        // bool? stored as int: -1=unset, 0=false, 1=true
        int forceShieldMat = data.ForceShieldMaterial.HasValue ? (data.ForceShieldMaterial.Value ? 1 : 0) : -1;
        List<string> shieldTags = data.ShieldTags;
        ThingFilter shieldFilter = data.ShieldMaterialFilter;

        Scribe_Values.Look(ref shieldMoneyMin, "shieldMoneyMin", -1f);
        Scribe_Values.Look(ref shieldMoneyMax, "shieldMoneyMax", -1f);
        Scribe_Values.Look(ref shieldChance, "shieldChance", -1f);
        Scribe_Values.Look(ref forceShieldMat, "forceShieldMaterial", -1);
        Scribe_Collections.Look(ref shieldTags, "shieldTags", LookMode.Value);
        Scribe_Deep.Look(ref shieldFilter, "shieldMaterialFilter");

        data.ShieldMoney = shieldMoneyMin >= 0f ? new FloatRange(shieldMoneyMin, shieldMoneyMax) : null;
        data.ShieldChance = shieldChance >= 0f ? shieldChance : null;
        data.ForceShieldMaterial = forceShieldMat >= 0 ? forceShieldMat > 0 : null;
        data.ShieldTags = shieldTags?.Count > 0 ? shieldTags : null;
        data.ShieldMaterialFilter = shieldFilter;

        // --- Forced Sidearm ---
        SidearmData forcedSidearm = data.ForcedSidearm;
        bool hasForcedSidearm = forcedSidearm != null;
        Scribe_Values.Look(ref hasForcedSidearm, "hasForcedSidearm", false);
        if (hasForcedSidearm)
        {
            forcedSidearm ??= new SidearmData();
            Scribe_Deep.Look(ref forcedSidearm, "forcedSidearm");
            data.ForcedSidearm = forcedSidearm;
        }
        else
        {
            data.ForcedSidearm = null;
        }

        // --- Sidearms List ---
        List<SidearmData> sidearms = data.Sidearms;
        Scribe_Collections.Look(ref sidearms, "sidearms", LookMode.Deep);
        data.Sidearms = sidearms?.Count > 0 ? sidearms : null;

        // --- Primary Attachments ---
        AttachmentData primaryAttachments = data.PrimaryAttachments;
        bool hasPrimaryAttachments = primaryAttachments != null;
        Scribe_Values.Look(ref hasPrimaryAttachments, "hasPrimaryAttachments", false);
        if (hasPrimaryAttachments)
        {
            primaryAttachments ??= new AttachmentData();
            Scribe_Deep.Look(ref primaryAttachments, "primaryAttachments");
            data.PrimaryAttachments = primaryAttachments;
        }
        else
        {
            data.PrimaryAttachments = null;
        }

        // --- Per-Weapon Ammo Mappings ---
        List<WeaponAmmoMapEntry> weaponAmmoMappings = data.WeaponAmmoMappings;
        Scribe_Collections.Look(ref weaponAmmoMappings, "weaponAmmoMappings", LookMode.Deep);
        data.WeaponAmmoMappings = weaponAmmoMappings?.Count > 0 ? weaponAmmoMappings : null;

        // Remove the entry if everything is cleared
        if (Scribe.mode == LoadSaveMode.PostLoadInit && data.IsEmpty)
        {
            dataStore.Remove(edit);
        }
    }

    public void Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global)
    {
        CEData data = GetData(edit);
        CEData globalData = global != null ? GetData(global) : null;

        // Merge: specific edit overrides global for each field independently
        string ammoCategoryName = data?.ForcedAmmoCategoryDefName ?? globalData?.ForcedAmmoCategoryDefName;
        FloatRange? magCount = data?.PrimaryMagazineCount ?? globalData?.PrimaryMagazineCount;
        int? minAmmo = data?.MinAmmoCount ?? globalData?.MinAmmoCount;
        List<WeightedAmmoCategoryData> weightedAmmo = HasEntries(data?.WeightedAmmoCategories)
            ? data?.WeightedAmmoCategories
            : HasEntries(globalData?.WeightedAmmoCategories)
                ? globalData?.WeightedAmmoCategories
                : null;

        FloatRange? shieldMoney = data?.ShieldMoney ?? globalData?.ShieldMoney;
        float? shieldChance = data?.ShieldChance ?? globalData?.ShieldChance;
        bool? forceShieldMaterial = data?.ForceShieldMaterial ?? globalData?.ForceShieldMaterial;
        List<string> shieldTags = HasEntries(data?.ShieldTags)
            ? data?.ShieldTags
            : HasEntries(globalData?.ShieldTags)
                ? globalData?.ShieldTags
                : null;
        ThingFilter shieldFilter = data?.ShieldMaterialFilter ?? globalData?.ShieldMaterialFilter;

        SidearmData forcedSidearm = data?.ForcedSidearm ?? globalData?.ForcedSidearm;
        List<SidearmData> sidearms = HasEntries(data?.Sidearms)
            ? data.Sidearms
            : HasEntries(globalData?.Sidearms)
                ? globalData.Sidearms
                : null;
        AttachmentData primaryAttachments = data?.PrimaryAttachments ?? globalData?.PrimaryAttachments;
        List<WeaponAmmoMapEntry> weaponAmmoMappings = MergeMappings(
            data?.WeaponAmmoMappings, globalData?.WeaponAmmoMappings);

        bool hasAnything =
            ammoCategoryName != null
            || magCount != null
            || minAmmo != null
            || weightedAmmo != null
            || shieldMoney != null
            || shieldChance != null
            || forceShieldMaterial != null
            || shieldTags != null
            || shieldFilter != null
            || forcedSidearm != null
            || sidearms != null
            || primaryAttachments != null
            || HasEntries(weaponAmmoMappings);

        if (!hasAnything)
        {
            KindDefMappings.Remove(def);
            return;
        }

        // Resolve forced ammo category def
        AmmoCategoryDef ammoCategory = null;
        if (ammoCategoryName != null)
        {
            ammoCategory = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(ammoCategoryName);
            if (ammoCategory == null)
            {
                ModCore.Warn($"CE module: Could not resolve AmmoCategoryDef '{ammoCategoryName}'.");
            }
        }

        def.modExtensions ??= [];

        LoadoutPropertiesExtension ext = def.GetModExtension<LoadoutPropertiesExtension>();
        if (ext == null)
        {
            ext = new LoadoutPropertiesExtension();
            def.modExtensions.Add(ext);
        }

        if (ammoCategory != null) ext.forcedAmmoCategory = ammoCategory;
        if (magCount.HasValue) ext.primaryMagazineCount = magCount.Value;
        if (minAmmo.HasValue) ext.minAmmoCount = minAmmo.Value;
        if (weightedAmmo != null)
        {
            List<WeightedAmmoCategory> converted = weightedAmmo
                .Select(ConvertWeightedAmmo)
                .Where(w => w != null)
                .ToList();
            if (converted.Count > 0) ext.weightedAmmoCategories = converted;
        }

        if (shieldMoney.HasValue) ext.shieldMoney = shieldMoney.Value;
        if (shieldChance.HasValue) ext.shieldChance = shieldChance.Value;
        if (forceShieldMaterial.HasValue) ext.forceShieldMaterial = forceShieldMaterial.Value;
        if (shieldTags != null) ext.shieldTags = [..shieldTags];
        if (shieldFilter != null) ext.shieldMaterialFilter = shieldFilter;
        if (forcedSidearm != null) ext.forcedSidearm = ConvertSidearm(forcedSidearm);
        if (sidearms != null) ext.sidearms = sidearms.Select(ConvertSidearm).ToList();
        if (primaryAttachments is { IsEmpty: false }) ext.primaryAttachments = ConvertAttachment(primaryAttachments);

        // Build per-weapon ammo lookup cache (all def resolution happens here, not at gen time)
        if (HasEntries(weaponAmmoMappings))
        {
            ResolvedWeaponAmmoEntry[] resolved = weaponAmmoMappings
                .Select(m => new ResolvedWeaponAmmoEntry
                {
                    WeaponKey = m.WeaponKey,
                    IsTag = m.IsTag,
                    Choices = m.Choices
                        ?.Select(ConvertWeightedAmmo)
                        .Where(c => c != null)
                        .ToList(),
                })
                .Where(r => r.WeaponKey != null && r.Choices?.Count > 0)
                .ToArray();

            if (resolved.Length > 0)
            {
                KindDefMappings[def] = resolved;
            }
            else
            {
                KindDefMappings.Remove(def);
            }
        }
        else
        {
            KindDefMappings.Remove(def);
        }
    }

    public void CopyData(PawnKindEdit source, PawnKindEdit dest)
    {
        CEData data = GetData(source);
        if (data == null)
        {
            return;
        }

        ThingFilter shieldFilterCopy = null;
        if (data.ShieldMaterialFilter != null)
        {
            shieldFilterCopy = new ThingFilter();
            shieldFilterCopy.CopyAllowancesFrom(data.ShieldMaterialFilter);
        }

        dataStore[dest] = new CEData
        {
            ForcedAmmoCategoryDefName = data.ForcedAmmoCategoryDefName,
            PrimaryMagazineCount = data.PrimaryMagazineCount,
            MinAmmoCount = data.MinAmmoCount,
            WeightedAmmoCategories = data.WeightedAmmoCategories?.Select(w => w.DeepClone()).ToList(),
            ShieldMoney = data.ShieldMoney,
            ShieldChance = data.ShieldChance,
            ForceShieldMaterial = data.ForceShieldMaterial,
            ShieldTags = data.ShieldTags != null ? [..data.ShieldTags] : null,
            ShieldMaterialFilter = shieldFilterCopy,
            ForcedSidearm = data.ForcedSidearm?.DeepClone(),
            Sidearms = data.Sidearms?.Select(s => s.DeepClone()).ToList(),
            PrimaryAttachments = data.PrimaryAttachments?.DeepClone(),
            WeaponAmmoMappings = data.WeaponAmmoMappings?.Select(e => e.DeepClone()).ToList(),
        };
    }

    public void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs)
    {
        tabs.Add(new Tab("FactionLoadout_Tab_CE".Translate(), ui => CEUI.DrawTab(ui, edit, defaultKind)));
    }

    // --- Conversion helpers ---

    private static SidearmOption ConvertSidearm(SidearmData d) =>
        new()
        {
            sidearmMoney = d.SidearmMoney ?? default,
            magazineCount = d.MagazineCount ?? default,
            weaponTags = d.WeaponTags != null ? [..d.WeaponTags] : null,
            generateChance = d.GenerateChance ?? 1f,
            attachments = d.Attachments is { IsEmpty: false } ? ConvertAttachment(d.Attachments) : null,
        };

    private static AttachmentOption ConvertAttachment(AttachmentData d) =>
        new()
        {
            attachmentCount = d.AttachmentCount ?? default,
            attachmentTags = d.AttachmentTags != null ? [..d.AttachmentTags] : null,
        };

    private static WeightedAmmoCategory ConvertWeightedAmmo(WeightedAmmoCategoryData d)
    {
        AmmoCategoryDef def = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(d.AmmoCategoryDefName);
        if (def == null)
        {
            ModCore.Warn($"CE module: Could not resolve AmmoCategoryDef '{d.AmmoCategoryDefName}' for weighted ammo.");
            return null;
        }

        WeightedAmmoCategory w = new() { ammoCategory = def, chance = d.Chance };
        return w;
    }

    private static bool HasEntries<T>(List<T> list) => list is { Count: > 0 };

    /// <summary>
    /// Merges per-weapon ammo mappings from a specific edit and a global edit.
    /// Specific-edit entries take precedence over global entries for the same WeaponKey.
    /// Entries from global that are not overridden are appended.
    /// </summary>
    private static List<WeaponAmmoMapEntry> MergeMappings(
        List<WeaponAmmoMapEntry> specific, List<WeaponAmmoMapEntry> global)
    {
        if (!HasEntries(specific) && !HasEntries(global)) { return null; }
        if (!HasEntries(global)) { return specific; }
        if (!HasEntries(specific)) { return global; }

        // Specific entries win; add global entries whose key isn't already covered
        List<WeaponAmmoMapEntry> merged = [..specific];
        foreach (WeaponAmmoMapEntry g in global)
        {
            bool overridden = false;
            foreach (WeaponAmmoMapEntry s in specific)
            {
                if (s.WeaponKey == g.WeaponKey && s.IsTag == g.IsTag)
                {
                    overridden = true;
                    break;
                }
            }
            if (!overridden) { merged.Add(g); }
        }
        return merged;
    }
}
