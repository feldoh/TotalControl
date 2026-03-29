using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Modules;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Applies a <see cref="PawnKindEdit"/> to a cloned <see cref="PawnKindDef"/>.
/// All mutation logic lives here so that <see cref="PawnKindEdit"/> remains a
/// focused data-and-serialization class.
/// </summary>
public static class PawnKindApplicator
{
    public static PawnKindDef Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global, bool addToEdits = true)
    {
        if (def == null)
            return null;

        if (addToEdits)
        {
            PawnKindEdit.AddActiveEdit(def, edit);
            DefCache.BuildBlacklistCaches(edit, def, global);
        }

        if (edit.ReplaceWith != null)
            return edit.ReplaceWith;

        // Only human-likes can have race replaced.
        if (def.RaceProps.Animal)
            edit.Race = null;

        ReplaceUtils.ReplaceMaybe(ref def.itemQuality, edit.ItemQuality);
        ReplaceUtils.ReplaceMaybe(ref def.biocodeWeaponChance, edit.BiocodeWeaponChance);
        ReplaceUtils.ReplaceMaybe(ref def.techHediffsChance, edit.TechHediffChance);
        ReplaceUtils.ReplaceMaybe(ref def.techHediffsMaxAmount, edit.TechHediffsMaxAmount);
        ReplaceUtils.ReplaceMaybe(ref def.apparelMoney, edit.ApparelMoney);
        ReplaceUtils.ReplaceMaybe(ref def.techHediffsMoney, edit.TechMoney);
        ReplaceUtils.ReplaceMaybe(ref def.weaponMoney, edit.WeaponMoney);
        ReplaceUtils.ReplaceMaybe(ref def.minGenerationAge, edit.MinGenerationAge);
        ReplaceUtils.ReplaceMaybe(ref def.maxGenerationAge, edit.MaxGenerationAge);
        ReplaceUtils.ReplaceMaybe(ref def.inventoryOptions, edit.Inventory, edit, global);
        ReplaceUtils.ReplaceMaybe(ref def.forceWeaponQuality, edit.ForcedWeaponQuality);
        ReplaceUtils.ReplaceMaybe(ref def.label, edit.Label);
        ReplaceUtils.ReplaceMaybe(ref def.race, edit.Race);
        ReplaceUtils.ReplaceMaybe(ref def.fixedGender, edit.ForcedGender);
        ReplaceUtils.ReplaceMaybe(ref def.nameMaker, edit.NameMaker);
        ReplaceUtils.ReplaceMaybe(ref def.nameMakerFemale, edit.NameMakerFemale);
        ReplaceUtils.ReplaceMaybe(ref def.combatPower, edit.CombatPower);
        ReplaceUtils.ReplaceMaybe(ref def.appearsRandomlyInCombatGroups, edit.AppearsRandomlyInCombatGroups);

        ReplaceUtils.ReplaceMaybeList(ref def.techHediffsTags, edit.TechHediffTags, global?.TechHediffTags != null);
        ReplaceUtils.ReplaceMaybeList(ref def.techHediffsDisallowTags, edit.TechHediffDisallowedTags, global?.TechHediffDisallowedTags != null);
        ReplaceUtils.ReplaceMaybeList(ref def.weaponTags, edit.WeaponTags, global?.WeaponTags != null);
        ReplaceUtils.ReplaceMaybeList(ref def.apparelTags, edit.ApparelTags, global?.ApparelTags != null);
        ReplaceUtils.ReplaceMaybeList(ref def.apparelDisallowTags, edit.ApparelDisallowedTags, global?.ApparelDisallowedTags != null);
        ReplaceUtils.ReplaceMaybeDefRefList(ref def.apparelRequired, edit.ApparelRequired, global?.ApparelRequired != null);
        ReplaceUtils.ReplaceMaybeDefRefList(ref def.techHediffsRequired, edit.TechRequired, global?.TechRequired != null);

        // Backstory filters override — BackstoryFilter extends BackstoryCategoryFilter, so cast directly.
        if (edit.BackstoryFiltersOverride is { Count: > 0 })
            def.backstoryFiltersOverride = [.. edit.BackstoryFiltersOverride];

        ReplaceUtils.ReplaceMaybe(ref def.backstoryCryptosleepCommonality, edit.BackstoryCryptosleepCommonality);

        // Fixed backstories — resolve DefRefs to actual defs, skipping missing ones.
        ReplaceUtils.ReplaceMaybeDefRefList(ref def.fixedChildBackstories, edit.FixedChildBackstories, global?.FixedChildBackstories != null);
        ReplaceUtils.ReplaceMaybeDefRefList(ref def.fixedAdultBackstories, edit.FixedAdultBackstories, global?.FixedAdultBackstories != null);

        // Backstory exclusions: inject category excludes and resolve fixed lists.
        ApplyBackstoryExclusions(edit, def);

        bool removeFixedInventory = edit.RemoveFixedInventory || global?.RemoveFixedInventory == true;
        if (removeFixedInventory)
            def.fixedInventory = [];

        bool removeSpecific = edit.ApparelRequired != null || edit.SpecificApparel != null;
        if (removeSpecific)
            def.specificApparelRequirements = null;

        // Can't be done like this. Disabled for now.
        if (edit.Race != null)
        {
            PawnKindDef realKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k != def && k.defName != def.defName && k.race == edit.Race);
            if (realKind != null)
                def.lifeStages = realKind.lifeStages;
        }

        // Colour: pure white would be ignored by RimWorld — use near-white instead.
        Color? color = edit.ApparelColor;
        if (color != null && color == Color.white)
            color = new Color(0.995f, 0.995f, 0.995f, 1f);
        ReplaceUtils.ReplaceMaybe(ref def.apparelColor, color);

        if (edit.ForcedTraitsDef is { Count: > 0 })
        {
            def.forcedTraits ??= [];
            foreach (ForcedTrait t in edit.ForcedTraitsDef)
            {
                if (t.TraitDef == null)
                    continue;
                if (!def.forcedTraits.Any(e => e.def == t.TraitDef && e.degree.GetValueOrDefault() == t.degree))
                    def.forcedTraits.Add(new TraitRequirement { def = t.TraitDef, degree = t.degree });
            }
        }

        def.modExtensions ??= [];

        ForcedExtrasModExtension extrasExtension = null;
        if (edit.ForcedHediffs is { Count: > 0 })
        {
            extrasExtension = def.GetModExtension<ForcedExtrasModExtension>() ?? def.GetModExtension<ForcedHediffModExtension>();
            if (extrasExtension == null)
            {
                extrasExtension = new ForcedExtrasModExtension();
                def.modExtensions.Add(extrasExtension);
            }

            extrasExtension.forcedHediffs.AddRange(edit.ForcedHediffs);
            ModCore.Debug($"Adding forced hediffs {extrasExtension.forcedHediffs?.Select(h => h.HediffDef?.defName).ToCommaList() ?? "None"} to {def.defName}");
        }

        if (edit.ForcedGenes is { Count: > 0 })
        {
            extrasExtension ??= def.GetModExtension<ForcedExtrasModExtension>();
            if (extrasExtension == null)
            {
                extrasExtension = new ForcedExtrasModExtension();
                def.modExtensions.Add(extrasExtension);
            }

            extrasExtension.forcedGenes.AddRange(edit.ForcedGenes);
            ModCore.Debug($"Adding forced genes {extrasExtension.forcedGenes?.Select(h => h.GeneDef?.defName).ToCommaList() ?? "None"} to {def.defName}");
        }

        if (edit.ForcedTraits is { Count: > 0 })
        {
            extrasExtension ??= def.GetModExtension<ForcedExtrasModExtension>();
            if (extrasExtension == null)
            {
                extrasExtension = new ForcedExtrasModExtension();
                def.modExtensions.Add(extrasExtension);
            }

            extrasExtension.forcedTraits.AddRange(edit.ForcedTraits);
        }

        if (ModsConfig.BiotechActive && def.RaceProps.Humanlike && edit.ForceSpecificXenos && (edit.ForcedXenotypeChanceDefs?.Count ?? 0) >= 1)
        {
            def.useFactionXenotypes = false;
            def.xenotypeSet ??= new XenotypeSet();
            def.xenotypeSet.xenotypeChances ??= [];
            def.xenotypeSet.xenotypeChances.Clear();
            foreach (KeyValuePair<XenotypeDef, float> rate in edit.ForcedXenotypeChanceDefs ?? [])
                def.xenotypeSet.xenotypeChances.Add(new XenotypeChance(rate.Key, rate.Value));
        }

        if (def.RaceProps.Animal)
            return def; // Animals can't have powers.

        VFEAncientsReflectionModule.ApplyVFEAncientsEdits(edit, def);
        VEPsycastsReflectionModule.ApplyVEPsycastsEdits(edit, def);

        // Delegate to registered modules.
        foreach (ITotalControlModule module in ModuleRegistry.Modules)
        {
            if (!module.IsActive)
                continue;
            try
            {
                module.Apply(edit, def, global);
            }
            catch (Exception e)
            {
                ModCore.Error($"Error applying module '{module.ModuleName}' (key: {module.ModuleKey}) to {def.defName}", e);
            }
        }

        return def;
    }

    // ==================== Backstory exclusions ====================

    private static void ApplyBackstoryExclusions(PawnKindEdit edit, PawnKindDef def)
    {
        bool hasExcludedCategories = edit.ExcludedBackstoryCategories is { Count: > 0 };
        bool hasExcludedDefs = edit.ExcludedBackstories is { Count: > 0 };

        // Inject excluded categories into all existing filters as exclude entries.
        if (hasExcludedCategories)
        {
            void InjectExcludes(List<BackstoryCategoryFilter> filters)
            {
                if (filters == null)
                    return;

                foreach (BackstoryCategoryFilter filter in filters)
                {
                    filter.exclude ??= [];
                    foreach (string cat in edit.ExcludedBackstoryCategories)
                    {
                        if (!filter.exclude.Contains(cat))
                            filter.exclude.Add(cat);
                    }
                }
            }

            InjectExcludes(def.backstoryFiltersOverride);
            InjectExcludes(def.backstoryFilters);
        }

        if (!hasExcludedDefs)
            return;

        // Partition excluded defs by slot so we only resolve categories for affected slots.
        HashSet<BackstoryDef> excludedChild = [];
        HashSet<BackstoryDef> excludedAdult = [];
        foreach (DefRef<BackstoryDef> defRef in edit.ExcludedBackstories)
        {
            switch (defRef?.Def?.slot)
            {
                case BackstorySlot.Childhood:
                    excludedChild.Add(defRef.Def);
                    break;
                case BackstorySlot.Adulthood:
                    excludedAdult.Add(defRef.Def);
                    break;
                default:
                    continue;
            }
        }

        // Always remove excluded defs from existing fixed lists regardless.
        if (excludedChild.Count > 0)
            def.fixedChildBackstories?.RemoveAll(b => excludedChild.Contains(b));
        if (excludedAdult.Count > 0)
            def.fixedAdultBackstories?.RemoveAll(b => excludedAdult.Contains(b));

        // Collect active categories per slot from the def's filters.
        HashSet<string> childCategories = [];
        HashSet<string> adultCategories = [];
        List<BackstoryCategoryFilter> activeFilters = def.backstoryFiltersOverride ?? def.backstoryFilters;
        if (activeFilters != null)
        {
            foreach (BackstoryCategoryFilter filter in activeFilters)
            {
                if (!filter.categories.NullOrEmpty())
                {
                    foreach (string cat in filter.categories)
                    {
                        childCategories.Add(cat);
                        adultCategories.Add(cat);
                    }
                }

                if (!filter.categoriesChildhood.NullOrEmpty())
                {
                    foreach (string cat in filter.categoriesChildhood)
                        childCategories.Add(cat);
                }

                if (!filter.categoriesAdulthood.NullOrEmpty())
                {
                    foreach (string cat in filter.categoriesAdulthood)
                        adultCategories.Add(cat);
                }
            }
        }

        // For each slot with excluded defs, resolve categories into concrete defs and
        // populate the fixed backstory list so vanilla picks from it directly.
        if (excludedChild.Count > 0)
            ResolveBackstoryCategories(def, BackstorySlot.Childhood, childCategories, excludedChild);
        if (excludedAdult.Count > 0)
            ResolveBackstoryCategories(def, BackstorySlot.Adulthood, adultCategories, excludedAdult);
    }

    /// <summary>
    /// Resolves backstory categories into concrete <see cref="BackstoryDef"/> entries for a given slot,
    /// excluding any defs in <paramref name="excluded"/>, then writes them into the corresponding
    /// fixed backstory list on the def. Existing entries in the fixed list are preserved.
    /// </summary>
    private static void ResolveBackstoryCategories(PawnKindDef def, BackstorySlot slot, HashSet<string> categories, HashSet<BackstoryDef> excluded)
    {
        List<BackstoryDef> fixedList = slot == BackstorySlot.Childhood ? def.fixedChildBackstories : def.fixedAdultBackstories;
        HashSet<BackstoryDef> existing = fixedList != null ? [.. fixedList] : [];

        List<BackstoryDef> resolved =
        [
            .. from bs in DefDatabase<BackstoryDef>.AllDefsListForReading
            where bs.slot == slot && bs.shuffleable && !excluded.Contains(bs) && !existing.Contains(bs)
            where bs.spawnCategories != null
            where categories.Count <= 0 || bs.spawnCategories.Any(categories.Contains)
            select bs,
        ];

        switch (slot)
        {
            case BackstorySlot.Childhood:
                def.fixedChildBackstories ??= [];
                def.fixedChildBackstories.AddRange(resolved);
                break;
            case BackstorySlot.Adulthood:
                def.fixedAdultBackstories ??= [];
                def.fixedAdultBackstories.AddRange(resolved);
                break;
            default:
                return;
        }

        ModCore.Debug($"Backstory exclusions for {def.defName} ({slot}): {excluded.Count} excluded, {resolved.Count} resolved from categories into fixed list.");
    }
}
