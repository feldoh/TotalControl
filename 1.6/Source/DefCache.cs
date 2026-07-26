using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Modules;
using RimWorld;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Lazily populated caches of def lists used throughout the editor UI.
/// Call <see cref="ScanDefs"/> once (guarded by a null-check) before accessing any list.
/// that are needed by multiple draw-support classes.
/// </summary>
public static class DefCache
{
    public static List<string> AllTechHediffTags;
    public static List<string> AllApparelTags;
    public static List<string> AllWeaponsTags;
    public static List<BodyTypeDef> AllBodyTypes;
    public static List<ThingDef> AllApparel;
    public static List<ThingDef> AllWeapons;
    public static List<ThingDef> AllTech;
    public static List<ThingDef> AllInvItems;
    public static List<ThingDef> AllHumanlikeRaces;
    public static List<PawnKindDef> AllAnimalKindDefs;
    public static List<RulePackDef> AllRulePackDefs;
    public static List<GeneDef> AllGeneDefs;

    public static List<string> AllBackstoryCategories;
    public static List<BackstoryDef> AllChildhoodBackstories;
    public static List<BackstoryDef> AllAdulthoodBackstories;
    public static List<BackstoryDef> AllBackstoryDefs;

    public static List<(TraitDef def, int degree)> AllTraitDegrees;

    public static List<string> AllPowerDefs;

    public static void ScanDefs()
    {
        if (AllTechHediffTags != null)
            return;

        HashSet<string> techTags = new(128);
        HashSet<string> apparelTags = new(128);
        HashSet<string> weaponTags = new(128);
        HashSet<ThingDef> apparel = new(256);
        HashSet<ThingDef> allHumanlikeRaces = new(256);
        HashSet<ThingDef> weapons = new(256);
        HashSet<ThingDef> allTech = new(128);
        HashSet<ThingDef> allInv = new(1024);
        HashSet<PawnKindDef> allAnimalKindDefs = new(1024);
        HashSet<RulePackDef> allRulePackDefs = new(1024);
        HashSet<BodyTypeDef> allBodyTypeDefs = new(32);
        HashSet<GeneDef> allGeneDefs = new(1024);

        foreach (PawnKindDef def in DefDatabase<PawnKindDef>.AllDefsListForReading)
        {
            if (def.RaceProps is { Animal: true, packAnimal: true })
                allAnimalKindDefs.Add(def);
        }

        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.race is { Animal: false })
                allHumanlikeRaces.Add(def);

            if (def.isTechHediff && !def.IsNaturalOrgan)
            {
                if (def.techHediffsTags != null)
                {
                    foreach (string item in def.techHediffsTags)
                    {
                        if (item != null)
                            techTags.Add(item);
                    }
                }

                allTech.Add(def);
            }

            if (def.IsApparel)
            {
                if (def.apparel?.tags != null)
                {
                    foreach (string item in def.apparel.tags)
                    {
                        if (item != null)
                            apparelTags.Add(item);
                    }
                }

                apparel.Add(def);
            }

            if (def.IsWeapon)
            {
                if (def.weaponTags != null)
                {
                    foreach (string item in def.weaponTags)
                    {
                        if (item != null)
                            weaponTags.Add(item);
                    }
                }

                weapons.Add(def);
            }

            if (def.category == ThingCategory.Item)
                allInv.Add(def);
        }

        allBodyTypeDefs.AddRange(DefDatabase<BodyTypeDef>.AllDefsListForReading);
        allRulePackDefs.AddRange(DefDatabase<RulePackDef>.AllDefsListForReading);
        allGeneDefs.AddRange(DefDatabase<GeneDef>.AllDefsListForReading);

        AllTechHediffTags = [.. techTags];
        AllTechHediffTags.Sort();

        apparelTags.Add("UNUSED");
        AllApparelTags = [.. apparelTags];
        AllApparelTags.Sort();

        AllWeaponsTags = [.. weaponTags];
        AllWeaponsTags.Sort();

        AllApparel = [.. apparel];
        AllApparel.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllWeapons = [.. weapons];
        AllWeapons.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllTech = [.. allTech];
        AllTech.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllInvItems = [.. allInv];
        AllInvItems.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllHumanlikeRaces = [.. allHumanlikeRaces];
        AllHumanlikeRaces.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllAnimalKindDefs = [.. allAnimalKindDefs];
        AllAnimalKindDefs.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

        AllBodyTypes = [.. allBodyTypeDefs];
        AllBodyTypes.Sort((a, b) => string.Compare((string)a.LabelCap ?? a.defName, (string)b.LabelCap ?? b.defName, StringComparison.InvariantCulture));

        AllRulePackDefs = [.. allRulePackDefs];
        AllRulePackDefs.Sort((a, b) => string.Compare(a.defName, b.defName, StringComparison.InvariantCulture));

        AllGeneDefs = [.. allGeneDefs];
        AllGeneDefs.Sort((a, b) => string.Compare((string)a.LabelCap ?? a.defName, (string)b.LabelCap ?? b.defName, StringComparison.InvariantCulture));

        // Backstory categories and defs — discovered from DefDatabase so modded content is included.
        HashSet<string> backstoryCategories = new(64);
        List<BackstoryDef> childBackstories = new(256);
        List<BackstoryDef> adultBackstories = new(256);
        foreach (BackstoryDef bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
        {
            if (bs.spawnCategories != null)
            {
                foreach (string cat in bs.spawnCategories)
                {
                    if (cat != null)
                        backstoryCategories.Add(cat);
                }
            }

            if (bs.slot == BackstorySlot.Childhood)
            {
                childBackstories.Add(bs);
            }
            else
            {
                adultBackstories.Add(bs);
            }
        }

        AllBackstoryCategories = [.. backstoryCategories];
        AllBackstoryCategories.Sort();

        childBackstories.Sort((a, b) => string.Compare(BackstoryTab.BackstoryLabel(a), BackstoryTab.BackstoryLabel(b), StringComparison.InvariantCulture));
        adultBackstories.Sort((a, b) => string.Compare(BackstoryTab.BackstoryLabel(a), BackstoryTab.BackstoryLabel(b), StringComparison.InvariantCulture));
        AllChildhoodBackstories = childBackstories;
        AllAdulthoodBackstories = adultBackstories;
        AllBackstoryDefs = [.. childBackstories];
        AllBackstoryDefs.AddRange(adultBackstories);

        AllTraitDegrees = DefDatabase<TraitDef>
            .AllDefsListForReading.SelectMany(t => t.degreeDatas.Select(d => (t, d.degree)))
            .OrderBy(x => x.t.LabelCap.ToString())
            .ThenBy(x => x.degree)
            .ToList();

        PopulateVFEAncientsObjects();
    }

    private static void PopulateVFEAncientsObjects()
    {
        if (!VFEAncientsReflectionModule.ModLoaded.Value)
            return;
        if (VFEAncientsReflectionModule.GetPowerDefsMethod.Value?.GetValue(null) is not IList powerList)
            return;

        AllPowerDefs = [];
        foreach (object power in powerList)
        {
            if (power is Def pd)
                AllPowerDefs.Add(pd.defName);
        }

        AllPowerDefs.Sort();
    }

    /// <summary>
    /// Pre-cached blacklists built at apply time: cloned PawnKindDef → blacklisted ThingDefs.
    /// Includes both global and specific edits merged. Keyed by cloned def for O(1) lookup at generation time.
    /// </summary>
    public static Dictionary<PawnKindDef, HashSet<ThingDef>> ApparelBlacklistCache = new();

    public static Dictionary<PawnKindDef, HashSet<ThingDef>> WeaponBlacklistCache = new();

    /// <summary>
    /// Pre-cached material rules built at apply time: cloned PawnKindDef → (stuff set, isBlocklist).
    /// Allowlist (isBlocklist false) allows only the set; blocklist bans the set. Specific edit's list
    /// (and its mode) wins; otherwise the faction's global edit's. Absent (or empty) key = no restriction.
    /// </summary>
    public static Dictionary<PawnKindDef, (HashSet<ThingDef> defs, bool blocklist)> ApparelMaterialCache = new();

    public static Dictionary<PawnKindDef, (HashSet<ThingDef> defs, bool blocklist)> WeaponMaterialCache = new();
    public static RulePackDef FakeRulePack = new() { defName = "NONE" };

    /// <summary>True if the kind's apparel material rule permits <paramref name="stuff"/> (no rule / null stuff = allowed).</summary>
    public static bool ApparelMaterialAllows(PawnKindDef kind, ThingDef stuff) => MaterialAllows(ApparelMaterialCache, kind, stuff);

    /// <summary>True if the kind's weapon material rule permits <paramref name="stuff"/> (no rule / null stuff = allowed).</summary>
    public static bool WeaponMaterialAllows(PawnKindDef kind, ThingDef stuff) => MaterialAllows(WeaponMaterialCache, kind, stuff);

    public static bool MaterialAllows(Dictionary<PawnKindDef, (HashSet<ThingDef> defs, bool blocklist)> cache, PawnKindDef kind, ThingDef stuff)
    {
        if (stuff == null || !cache.TryGetValue(kind, out (HashSet<ThingDef> defs, bool blocklist) rule))
            return true;
        bool listed = rule.defs.Contains(stuff);
        return rule.blocklist ? !listed : listed;
    }

    /// <summary>
    /// Builds a per-stuff-category count of the materials a list+mode actually permits
    /// (e.g. "Leathery: 13   Metallic: 4"), for the editor summary. Returns null if nothing is permitted.
    /// </summary>
    public static string MaterialCategorySummary(List<DefRef<ThingDef>> materials, bool isBlocklist)
    {
        IEnumerable<ThingDef> allowed;
        if (isBlocklist)
        {
            // Blocklist: allowed = every stuff except the banned ones, so we need the set + full scan.
            HashSet<ThingDef> banned = [];
            if (materials != null)
            {
                foreach (DefRef<ThingDef> r in materials)
                {
                    if (r.HasValue)
                        banned.Add(r.Def);
                }
            }
            allowed = GenStuff.StuffDefs.Where(s => !banned.Contains(s));
        }
        else
        {
            // Allowlist: allowed = exactly the selected materials — no set or full scan needed.
            allowed = (materials ?? Enumerable.Empty<DefRef<ThingDef>>()).Where(r => r.HasValue).Select(r => r.Def);
        }

        Dictionary<StuffCategoryDef, int> counts = new();
        foreach (ThingDef s in allowed)
        {
            if (s?.stuffProps?.categories == null)
                continue;
            foreach (StuffCategoryDef cat in s.stuffProps.categories)
            {
                counts.TryGetValue(cat, out int c);
                counts[cat] = c + 1;
            }
        }

        return counts.Count == 0 ? null : string.Join("   ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key.LabelCap}: {kv.Value}"));
    }

    public static void BuildBlacklistCaches(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global)
    {
        HashSet<ThingDef> apparelBl = (global?.ApparelBlacklist ?? Enumerable.Empty<DefRef<ThingDef>>())
            .ConcatIfNotNull(edit.ApparelBlacklist)
            .Where(r => r.HasValue)
            .Select(r => r.Def)
            .ToHashSet();

        if (apparelBl.Count > 0)
        {
            ApparelBlacklistCache[def] = apparelBl;
        }
        else
        {
            ApparelBlacklistCache.Remove(def);
        }

        HashSet<ThingDef> weaponBl = (global?.WeaponBlacklist ?? Enumerable.Empty<DefRef<ThingDef>>())
            .ConcatIfNotNull(edit.WeaponBlacklist)
            .Where(r => r.HasValue)
            .Select(r => r.Def)
            .ToHashSet();

        if (weaponBl.Count > 0)
        {
            WeaponBlacklistCache[def] = weaponBl;
        }
        else
        {
            WeaponBlacklistCache.Remove(def);
        }

        // Material rules: specific edit's list (and its mode) wins, else the faction's global edit's.
        // Resolve to currently-present stuff defs; missing-mod entries stay in the saved DefRef list
        // but simply don't constrain generation until their mod returns.
        List<DefRef<ThingDef>> apparelMatList = edit.ApparelMaterials ?? global?.ApparelMaterials;
        bool apparelMatBlocklist = edit.ApparelMaterials != null ? edit.ApparelMaterialsBlocklist : global?.ApparelMaterialsBlocklist ?? false;
        HashSet<ThingDef> apparelMat = apparelMatList?.Where(r => r.HasValue).Select(r => r.Def).ToHashSet();
        if (apparelMat is { Count: > 0 })
        {
            ApparelMaterialCache[def] = (apparelMat, apparelMatBlocklist);
        }
        else
        {
            ApparelMaterialCache.Remove(def);
        }

        List<DefRef<ThingDef>> weaponMatList = edit.WeaponMaterials ?? global?.WeaponMaterials;
        bool weaponMatBlocklist = edit.WeaponMaterials != null ? edit.WeaponMaterialsBlocklist : global?.WeaponMaterialsBlocklist ?? false;
        HashSet<ThingDef> weaponMat = weaponMatList?.Where(r => r.HasValue).Select(r => r.Def).ToHashSet();
        if (weaponMat is { Count: > 0 })
        {
            WeaponMaterialCache[def] = (weaponMat, weaponMatBlocklist);
        }
        else
        {
            WeaponMaterialCache.Remove(def);
        }
    }
}
