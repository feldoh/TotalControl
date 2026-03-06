using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TotalControlCECompat;

/// <summary>Data for AttachmentOption from CE.</summary>
public class AttachmentData : IExposable
{
    /// <summary>Count range for attachments. Null = no override.</summary>
    public FloatRange? AttachmentCount;

    /// <summary>Attachment tags. Null = no override.</summary>
    public List<string> AttachmentTags;

    public bool IsEmpty => AttachmentCount == null && (AttachmentTags == null || AttachmentTags.Count == 0);

    public void ExposeData()
    {
        float min = AttachmentCount?.min ?? -1f;
        float max = AttachmentCount?.max ?? -1f;
        Scribe_Values.Look(ref min, "countMin", -1f);
        Scribe_Values.Look(ref max, "countMax", -1f);
        AttachmentCount = min >= 0f ? new FloatRange(min, max) : null;

        Scribe_Collections.Look(ref AttachmentTags, "tags", LookMode.Value);
    }

    public AttachmentData DeepClone() => new() { AttachmentCount = AttachmentCount, AttachmentTags = AttachmentTags != null ? [.. AttachmentTags] : null };
}

/// <summary>Data for SidearmOption from CE.</summary>
public class SidearmData : IExposable
{
    /// <summary>Money budget for sidearm weapon. Null = no override.</summary>
    public FloatRange? SidearmMoney;

    /// <summary>Magazine count range. Null = no override.</summary>
    public FloatRange? MagazineCount;

    /// <summary>Weapon tags. Null = no override (CE selects randomly).</summary>
    public List<string> WeaponTags;

    /// <summary>Chance to generate this sidearm (0–1). Null = no override (CE default: 1).</summary>
    public float? GenerateChance;

    /// <summary>Attachment configuration. Null = no override.</summary>
    public AttachmentData Attachments;

    public void ExposeData()
    {
        float moneyMin = SidearmMoney?.min ?? -1f;
        float moneyMax = SidearmMoney?.max ?? -1f;
        Scribe_Values.Look(ref moneyMin, "moneyMin", -1f);
        Scribe_Values.Look(ref moneyMax, "moneyMax", -1f);
        SidearmMoney = moneyMin >= 0f ? new FloatRange(moneyMin, moneyMax) : null;

        float magMin = MagazineCount?.min ?? -1f;
        float magMax = MagazineCount?.max ?? -1f;
        Scribe_Values.Look(ref magMin, "magMin", -1f);
        Scribe_Values.Look(ref magMax, "magMax", -1f);
        MagazineCount = magMin >= 0f ? new FloatRange(magMin, magMax) : null;

        Scribe_Collections.Look(ref WeaponTags, "weaponTags", LookMode.Value);

        float chance = GenerateChance ?? -1f;
        Scribe_Values.Look(ref chance, "generateChance", -1f);
        GenerateChance = chance >= 0f ? chance : null;

        AttachmentData att = Attachments;
        bool hasAttachments = att != null;
        Scribe_Values.Look(ref hasAttachments, "hasAttachments", false);
        if (hasAttachments)
        {
            att ??= new AttachmentData();
            Scribe_Deep.Look(ref att, "attachments");
            Attachments = att;
        }
        else
        {
            Attachments = null;
        }
    }

    public SidearmData DeepClone() =>
        new SidearmData
        {
            SidearmMoney = SidearmMoney,
            MagazineCount = MagazineCount,
            WeaponTags = WeaponTags != null ? [.. WeaponTags] : null,
            GenerateChance = GenerateChance,
            Attachments = Attachments?.DeepClone(),
        };
}

/// <summary>
/// Maps a specific weapon (by ThingDef defName or weapon tag) to a weighted list of ammo
/// categories. Used by the per-weapon ammo mapping feature to select ammo conditionally based
/// on which weapon a pawn was actually given at generation time.
/// </summary>
public class WeaponAmmoMapEntry : IExposable
{
    /// <summary>ThingDef.defName of the weapon, or a weapon tag string.</summary>
    public string WeaponKey;

    /// <summary>If true, WeaponKey is a weapon tag; if false it is a ThingDef.defName.</summary>
    public bool IsTag;

    /// <summary>
    /// Weighted ammo category choices. Reuses WeightedAmmoCategoryData so the UI and
    /// serialization patterns are identical to the global weighted ammo section.
    /// A single forced category is stored as a list with one entry.
    /// </summary>
    public List<WeightedAmmoCategoryData> Choices;

    public void ExposeData()
    {
        Scribe_Values.Look(ref WeaponKey, "weaponKey", null);
        Scribe_Values.Look(ref IsTag, "isTag", false);
        Scribe_Collections.Look(ref Choices, "choices", LookMode.Deep);
    }

    public WeaponAmmoMapEntry DeepClone() =>
        new()
        {
            WeaponKey = WeaponKey,
            IsTag = IsTag,
            Choices = Choices?.Select(c => c.DeepClone()).ToList(),
        };
}

/// <summary>Data for WeightedAmmoCategory from CE.</summary>
public class WeightedAmmoCategoryData : IExposable
{
    /// <summary>DefName of the AmmoCategoryDef.</summary>
    public string AmmoCategoryDefName;

    /// <summary>Weight/chance for this ammo category.</summary>
    public float Chance;

    public void ExposeData()
    {
        Scribe_Values.Look(ref AmmoCategoryDefName, "ammoCategory", null);
        Scribe_Values.Look(ref Chance, "chance", 0f);
    }

    public WeightedAmmoCategoryData DeepClone() => new() { AmmoCategoryDefName = AmmoCategoryDefName, Chance = Chance };
}

/// <summary>
/// Module data for a single PawnKindEdit's Combat Extended loadout configuration.
/// </summary>
public class CEData
{
    // --- Ammo ---

    /// <summary>DefName of an AmmoCategoryDef to force. Null = no override (CE picks randomly).</summary>
    public string ForcedAmmoCategoryDefName;

    /// <summary>How many extra primary magazines the pawn carries. Null = no override.</summary>
    public FloatRange? PrimaryMagazineCount;

    /// <summary>Minimum ammo count. Null = no override.</summary>
    public int? MinAmmoCount;

    /// <summary>Weighted list of ammo categories to choose from. Null = no override.</summary>
    public List<WeightedAmmoCategoryData> WeightedAmmoCategories;

    // --- Shield ---

    /// <summary>Money budget for shield generation. Null = no override.</summary>
    public FloatRange? ShieldMoney;

    /// <summary>Chance the pawn spawns with a shield (0–1). Null = no override.</summary>
    public float? ShieldChance;

    /// <summary>Whether to force a specific shield material. Null = no override.</summary>
    public bool? ForceShieldMaterial;

    /// <summary>Tags used for shield selection. Null = no override.</summary>
    public List<string> ShieldTags;

    /// <summary>Material filter for shield generation. Null = no override.</summary>
    public ThingFilter ShieldMaterialFilter;

    // --- Sidearms ---

    /// <summary>A forced sidearm configuration. Null = no override.</summary>
    public SidearmData ForcedSidearm;

    /// <summary>List of possible sidearm configurations. Null = no override.</summary>
    public List<SidearmData> Sidearms;

    // --- Primary Attachments ---

    /// <summary>Attachment configuration for the primary weapon. Null = no override.</summary>
    public AttachmentData PrimaryAttachments;

    // --- Per-Weapon Ammo Mappings ---

    /// <summary>
    /// Maps specific weapons or weapon tags to weighted ammo category choices.
    /// Applied at pawn-generation time via a Harmony patch on GenerateLoadoutFor,
    /// overriding the global ForcedAmmoCategory/WeightedAmmoCategories for that pawn.
    /// </summary>
    public List<WeaponAmmoMapEntry> WeaponAmmoMappings;

    public bool IsEmpty =>
        ForcedAmmoCategoryDefName == null
        && PrimaryMagazineCount == null
        && MinAmmoCount == null
        && (WeightedAmmoCategories == null || WeightedAmmoCategories.Count == 0)
        && ShieldMoney == null
        && ShieldChance == null
        && ForceShieldMaterial == null
        && (ShieldTags == null || ShieldTags.Count == 0)
        && ShieldMaterialFilter == null
        && ForcedSidearm == null
        && (Sidearms == null || Sidearms.Count == 0)
        && PrimaryAttachments == null
        && (WeaponAmmoMappings == null || WeaponAmmoMappings.Count == 0);
}
