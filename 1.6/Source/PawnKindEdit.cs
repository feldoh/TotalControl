using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using FactionLoadout.Modules;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class PawnKindEdit : IExposable
{
    // ==================== Static registry ====================

    public static Dictionary<PawnKindDef, List<PawnKindEdit>> activeEdits = new();
    public static Dictionary<PawnKindDef, PawnKindDef> replacementToOriginal = new();

    public static void RecordReplacement(PawnKindDef original, PawnKindDef replacement) => replacementToOriginal.SetOrAdd(replacement, original);

    public static List<PawnKindEdit> RemoveActiveEdits(PawnKindDef pawnKindDef)
    {
        List<PawnKindEdit> currentEdits = activeEdits.TryGetValue(pawnKindDef, null);
        activeEdits.Remove(pawnKindDef);
        return currentEdits;
    }

    public static void SetActiveEdits(PawnKindDef pawnKindDef, List<PawnKindEdit> edits) => activeEdits.SetOrAdd(pawnKindDef, edits);

    public static PawnKindDef NormaliseDef(PawnKindDef def) => replacementToOriginal.TryGetValue(def, def);

    public static IEnumerable<PawnKindEdit> GetEditsFor(PawnKindDef def, FactionDef factionDef)
    {
        if (def == null)
            yield break;
        factionDef = ForcedFactionForEditing(def, factionDef);

        if (!activeEdits.TryGetValue(def, out List<PawnKindEdit> list))
            yield break;
        foreach (PawnKindEdit item in list)
            if (
                factionDef == null
                || item.ParentEdit.Faction.Def == factionDef
                || FactionEdit.TryGetOriginal(factionDef.defName) == item.ParentEdit.Faction.Def
                || (factionDef.fixedName?.StartsWith("TEMP FACTION CLONE") ?? false)
            )
                yield return item;
    }

    public static FactionDef ForcedFactionForEditing(PawnKindDef def, FactionDef fallbackFactionDef)
    {
        if (def == null)
            return fallbackFactionDef;
        if (def == PawnKindDefOf.WildMan)
            return Preset.SpecialWildManFaction;
        if (def is CreepJoinerFormKindDef)
            return Preset.SpecialCreepjoinerFaction;
        if (fallbackFactionDef == null && Preset.FactionlessPawnKindsSet.Contains(def))
            return Preset.SpecialFactionlessPawnsFaction;
        return fallbackFactionDef;
    }

    public static void AddActiveEdit(PawnKindDef def, PawnKindEdit edit)
    {
        if (def == null || edit == null)
            return;

        if (!activeEdits.TryGetValue(def, out List<PawnKindEdit> list))
        {
            list = [];
            activeEdits.Add(def, list);
        }

        if (!list.Contains(edit))
            list.Add(edit);
    }

    // ==================== Instance fields ====================

    public FactionEdit ParentEdit => Preset.LoadedPresets.SelectMany(preset => preset.factionChanges).FirstOrDefault(change => change.KindEdits.Contains(this));

    [NoCopy]
    public PawnKindDef Def;

    [NoCopy]
    public bool IsGlobal = false;

    [NoCopy]
    public bool DeletedOrClosed;

    /// <summary>Link to faction level global edit for easy access</summary>
    [NoCopy]
    public PawnKindEdit globalEdit = null;

    /// <summary>
    /// Raw InnerXml for module sub-nodes whose module is not currently registered/active.
    /// Preserved across save/load so users don't lose module config when a dependency is absent.
    /// Marked [NoCopy] because CopyModuleData handles this explicitly.
    /// </summary>
    [NoCopy]
    public Dictionary<string, string> preservedModuleXml;

    public PawnKindDef ReplaceWith = null;
    public bool RenameDef = false;
    public bool ForceNaked = false;
    public bool ForceOnlySelected = false;
    public bool ForceSpecificXenos = false;
    public QualityCategory? ItemQuality = null;
    public float? BiocodeWeaponChance = null;
    public float? TechHediffChance = null;
    public int? TechHediffsMaxAmount = null;
    public int? MinGenerationAge = null;
    public int? MaxGenerationAge = null;
    public List<string> TechHediffTags = null;
    public List<string> TechHediffDisallowedTags = null;
    public List<string> WeaponTags = null;
    public List<string> ApparelTags = null;
    public List<string> ApparelDisallowedTags = null;
    public List<DefRef<ThingDef>> ApparelBlacklist = null;
    public List<DefRef<ThingDef>> WeaponBlacklist = null;
    public List<ThingDef> ApparelRequired = null;
    public List<ThingDef> TechRequired = null;
    public List<SpecRequirementEdit> SpecificApparel = null;
    public List<SpecRequirementEdit> SpecificWeapons = null;
    public FloatRange? ApparelMoney = null;
    public FloatRange? TechMoney = null;
    public FloatRange? WeaponMoney = null;
    public InventoryOptionEdit Inventory = null;
    public bool ReplaceDefaultInventory = true;
    public bool RemoveFixedInventory;
    public QualityCategory? ForcedWeaponQuality = null;
    public Color? ApparelColor = null;
    public string Label = null;
    public ThingDef Race = null;
    public List<HairDef> CustomHair = null;
    public List<BeardDef> CustomBeards = null;
    public List<BodyTypeDef> BodyTypes = null;
    public List<Color> CustomHairColors = null;
    public List<ForcedHediff> ForcedHediffs = null;
    public List<ForcedGene> ForcedGenes = null;
    public Dictionary<string, float> ForcedXenotypeChances = new();
    public Dictionary<XenotypeDef, float> ForcedXenotypeChanceDefs = new();
    public Gender? ForcedGender = null;
    public SimpleCurve RaidCommonalityFromPointsCurve = null;
    public SimpleCurve RaidLootValueFromPointsCurve = null;
    public SimpleCurve MaxPawnCostPerTotalPointsCurve = null;
    public RulePackDef NameMaker = null;
    public RulePackDef NameMakerFemale = null;
    public float? UnwaveringlyLoyalChance = null;
    public float? CombatPower = null;
    public bool? AppearsRandomlyInCombatGroups = null;

    // Backstory
    public List<BackstoryFilter> BackstoryFiltersOverride = null;
    public List<DefRef<BackstoryDef>> FixedChildBackstories = null;
    public List<DefRef<BackstoryDef>> FixedAdultBackstories = null;
    public List<string> ExcludedBackstoryCategories = null;
    public List<DefRef<BackstoryDef>> ExcludedBackstories = null;
    public float? BackstoryCryptosleepCommonality = null;

    // VFE Ancients
    public int? NumVFEAncientsSuperPowers = null;
    public int? NumVFEAncientsSuperWeaknesses = null;
    public List<string> ForcedVFEAncientsItems = null;

    // VPE
    public int? VEPsycastLevel = null;
    public IntRange? VEPsycastStatPoints = null;
    public bool? VEPsycastRandomAbilities = null;

    // ==================== Constructors ====================

    public PawnKindEdit() { }

    public PawnKindEdit(PawnKindDef def)
    {
        Def = def;
    }

    // ==================== Apply ====================

    /// <summary>
    /// Applies this edit to <paramref name="def"/> via <see cref="PawnKindApplicator"/>.
    /// <paramref name="global"/> is set for the duration so modules and external code can inspect it.
    /// </summary>
    public PawnKindDef Apply(PawnKindDef def, PawnKindEdit global, bool addToEdits = true)
    {
        globalEdit = global;
        try
        {
            return PawnKindApplicator.Apply(this, def, global, addToEdits);
        }
        finally
        {
            globalEdit = null;
        }
    }

    // ==================== Serialization ====================

    public void ExposeData()
    {
        Scribe_Defs.Look(ref Def, "def");
        Scribe_Defs.Look(ref ReplaceWith, "replaceWith");
        Scribe_Values.Look(ref RemoveFixedInventory, "removeFixedInventory");
        Scribe_Values.Look(ref ForceNaked, "forceNaked");
        Scribe_Values.Look(ref RenameDef, "renameDef");
        Scribe_Values.Look(ref ForceOnlySelected, "forceOnlySelected");
        Scribe_Values.Look(ref ForceSpecificXenos, "forceSpecificXenos");
        Scribe_Values.Look(ref ItemQuality, "itemQuality");
        Scribe_Values.Look(ref TechHediffChance, "techHediffChance");
        Scribe_Values.Look(ref TechHediffsMaxAmount, "techHediffsMaxAmount");
        Scribe_Collections.Look(ref TechHediffDisallowedTags, "techHediffDisallowedTags");
        Scribe_Collections.Look(ref TechHediffTags, "techHediffTags");
        Scribe_Values.Look(ref BiocodeWeaponChance, "biocodeWeaponChance");
        Scribe_Values.Look(ref ApparelMoney, "apparelMoney");
        Scribe_Values.Look(ref TechMoney, "techMoney");
        Scribe_Values.Look(ref WeaponMoney, "weaponMoney");
        Scribe_Values.Look(ref MinGenerationAge, "minGenerationAge");
        Scribe_Values.Look(ref MaxGenerationAge, "maxGenerationAge");
        Scribe_Collections.Look(ref WeaponTags, "weaponTags");
        Scribe_Collections.Look(ref ApparelTags, "apparelTags");
        Scribe_Collections.Look(ref ApparelDisallowedTags, "apparelDisallowedTags");
        Scribe_Collections.Look(ref ApparelBlacklist, "apparelBlacklist", LookMode.Deep);
        Scribe_Collections.Look(ref WeaponBlacklist, "weaponBlacklist", LookMode.Deep);
        Scribe_Collections.Look(ref ApparelRequired, "apparelRequired", LookMode.Def);
        Scribe_Collections.Look(ref TechRequired, "techRequired", LookMode.Def);
        Scribe_Collections.Look(ref SpecificApparel, "specificApparel", LookMode.Deep);
        Scribe_Collections.Look(ref SpecificWeapons, "specificWeapons", LookMode.Deep);
        Scribe_Deep.Look(ref Inventory, "inventory");
        Scribe_Values.Look(ref IsGlobal, "isGlobal");
        Scribe_Values.Look(ref ReplaceDefaultInventory, "replaceDefaultInventory");
        Scribe_Values.Look(ref ForcedWeaponQuality, "forcedWeaponQuality");
        Scribe_Values.Look(ref ApparelColor, "apparelColor");
        Scribe_Values.Look(ref Label, "label");
        Scribe_Defs.Look(ref Race, "race");
        Scribe_Values.Look(ref ForcedGender, "forcedGender");
        Scribe_Collections.Look(ref BodyTypes, "bodyTypes", LookMode.Def);
        Scribe_Collections.Look(ref CustomBeards, "customBeards", LookMode.Def);
        Scribe_Collections.Look(ref CustomHair, "customHair", LookMode.Def);
        Scribe_Collections.Look(ref CustomHairColors, "customHairColors");
        Scribe_Collections.Look(ref ForcedHediffs, "forcedHediffs", LookMode.Deep);
        Scribe_Collections.Look(ref ForcedGenes, "forcedGenes", LookMode.Deep);
        Scribe_Collections.Look(ref ForcedXenotypeChances, "forcedXenotypeChances", LookMode.Value, LookMode.Value);
        Scribe_Deep.Look(ref RaidLootValueFromPointsCurve, "raidLootValueFromPointsCurve");
        Scribe_Deep.Look(ref RaidCommonalityFromPointsCurve, "raidCommonalityFromPointsCurve");
        Scribe_Deep.Look(ref MaxPawnCostPerTotalPointsCurve, "maxPawnCostPerTotalPointsCurve");
        Scribe_Values.Look(ref UnwaveringlyLoyalChance, "unwaveringlyLoyalChance");
        Scribe_Values.Look(ref CombatPower, "combatPower");
        Scribe_Values.Look(ref AppearsRandomlyInCombatGroups, "appearsRandomlyInCombatGroups");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ForcedXenotypeChanceDefs = ForcedXenotypeChances
                .Select(kvp => (Def: DefDatabase<XenotypeDef>.GetNamedSilentFail(kvp.Key), Value: kvp.Value))
                .Where(c => c.Def != null)
                .ToDictionary(c => c.Def, c => c.Value);
        }

        bool isFake = NameMaker == DefCache.FakeRulePack;
        if (isFake)
            NameMaker = null;
        Scribe_Defs.Look(ref NameMaker, "nameMaker");
        if (isFake)
            NameMaker = DefCache.FakeRulePack;
        isFake = NameMakerFemale == DefCache.FakeRulePack;
        if (isFake)
            NameMakerFemale = null;
        Scribe_Defs.Look(ref NameMakerFemale, "nameMakerFemale");
        if (isFake)
            NameMakerFemale = DefCache.FakeRulePack;

        // Backstory
        Scribe_Collections.Look(ref BackstoryFiltersOverride, "backstoryFiltersOverride", LookMode.Deep);
        Scribe_Collections.Look(ref FixedChildBackstories, "fixedChildBackstories", LookMode.Deep);
        Scribe_Collections.Look(ref FixedAdultBackstories, "fixedAdultBackstories", LookMode.Deep);
        Scribe_Collections.Look(ref ExcludedBackstoryCategories, "excludedBackstoryCategories");
        Scribe_Collections.Look(ref ExcludedBackstories, "excludedBackstories", LookMode.Deep);
        Scribe_Values.Look(ref BackstoryCryptosleepCommonality, "backstoryCryptosleepCommonality");

        // VFEAncients Compatibility
        Scribe_Values.Look(ref NumVFEAncientsSuperPowers, "numVFEAncientsSuperPowers");
        Scribe_Values.Look(ref NumVFEAncientsSuperWeaknesses, "numVFEAncientsSuperWeaknesses");
        Scribe_Collections.Look(ref ForcedVFEAncientsItems, "forcedVFEAncientsEffects");

        // VPE
        Scribe_Values.Look(ref VEPsycastLevel, "vePsycastLevel");
        Scribe_Values.Look(ref VEPsycastStatPoints, "vePsycastStatPoints");
        Scribe_Values.Look(ref VEPsycastRandomAbilities, "vePsycastRandomAbilities");

        // Module system
        ExposeModuleData();
    }

    /// <summary>
    /// Delegates serialization to registered modules and preserves XML for absent modules.
    /// Each module gets its own named child node inside a &lt;modules&gt; element.
    /// Unrecognized child nodes (from modules not currently loaded) are stored as raw XML
    /// and written back on save to prevent data loss.
    /// </summary>
    private void ExposeModuleData()
    {
        IReadOnlyList<ITotalControlModule> modules = ModuleRegistry.Modules;

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            XmlNode modulesNode = Scribe.loader.curXmlParent?["modules"];
            if (modulesNode == null)
                return;

            foreach (XmlNode child in modulesNode.ChildNodes)
            {
                ITotalControlModule module = ModuleRegistry.GetModule(child.Name);
                if (module is { IsActive: true })
                {
                    // Position the Scribe cursor inside this module's node and let it deserialize.
                    XmlNode previousParent = Scribe.loader.curXmlParent;
                    Scribe.loader.curXmlParent = child;
                    try
                    {
                        module.ExposeData(this);
                    }
                    catch (Exception e)
                    {
                        ModCore.Error($"Error loading module data for '{module.ModuleName}' (key: {module.ModuleKey})", e);
                    }

                    Scribe.loader.curXmlParent = previousParent;
                }
                else
                {
                    // Module not registered or not active — preserve the raw XML for re-saving.
                    preservedModuleXml ??= new Dictionary<string, string>();
                    preservedModuleXml[child.Name] = child.InnerXml;
                    ModCore.Debug($"Preserving module data for absent module '{child.Name}'");
                }
            }
        }
        else if (Scribe.mode == LoadSaveMode.Saving)
        {
            bool hasActiveModules = modules.Any(m => m.IsActive);
            bool hasPreserved = preservedModuleXml is { Count: > 0 };

            if (!hasActiveModules && !hasPreserved)
                return;

            Scribe.saver.EnterNode("modules");
            try
            {
                foreach (ITotalControlModule module in modules)
                {
                    if (!module.IsActive)
                        continue;
                    Scribe.saver.EnterNode(module.ModuleKey);
                    try
                    {
                        module.ExposeData(this);
                    }
                    catch (Exception e)
                    {
                        ModCore.Error($"Error saving module data for '{module.ModuleName}' (key: {module.ModuleKey})", e);
                    }

                    Scribe.saver.ExitNode();
                }

                // Write back preserved XML for absent modules.
                if (preservedModuleXml != null)
                {
                    HashSet<string> activeKeys = new(modules.Where(m => m.IsActive).Select(m => m.ModuleKey));
                    foreach (KeyValuePair<string, string> kvp in preservedModuleXml)
                    {
                        if (activeKeys.Contains(kvp.Key))
                            continue; // Module is now active and wrote its own data above.
                        Scribe.saver.writer.WriteStartElement(kvp.Key);
                        Scribe.saver.writer.WriteRaw(kvp.Value);
                        Scribe.saver.writer.WriteEndElement();
                    }
                }
            }
            finally
            {
                Scribe.saver.ExitNode();
            }
        }
        else if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            foreach (ITotalControlModule module in modules)
            {
                if (!module.IsActive)
                    continue;
                try
                {
                    module.ExposeData(this);
                }
                catch (Exception e)
                {
                    ModCore.Error($"Error in post-load init for module '{module.ModuleName}' (key: {module.ModuleKey})", e);
                }
            }
        }
    }

    // ==================== Deep copy ====================

    /// <summary>Reflection-based field enumeration cached at static init time.</summary>
    public static readonly FieldInfo[] CopyableFields = typeof(PawnKindEdit)
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.GetCustomAttribute<NoCopyAttribute>() == null)
        .ToArray();

    /// <summary>
    /// Copies all user-configured data fields from <paramref name="source"/> into this edit.
    /// Identity fields (<see cref="Def"/>, <see cref="IsGlobal"/>, etc.) are left untouched.
    /// Module data is also copied.
    /// </summary>
    public void CopyFrom(PawnKindEdit source)
    {
        foreach (FieldInfo field in CopyableFields)
            field.SetValue(this, DeepCopy.Value(field.GetValue(source), field.FieldType));

        CopyModuleData(source, this);
    }

    /// <summary>
    /// Copies module data from <paramref name="source"/> into <paramref name="dest"/>.
    /// Handles preserved XML for inactive modules and delegates to each active module's
    /// <see cref="ITotalControlModule.CopyData"/> for its own per-edit state.
    /// </summary>
    public static void CopyModuleData(PawnKindEdit source, PawnKindEdit dest)
    {
        dest.preservedModuleXml = source.preservedModuleXml != null ? new Dictionary<string, string>(source.preservedModuleXml) : null;

        foreach (ITotalControlModule module in ModuleRegistry.Modules)
        {
            if (!module.IsActive)
                continue;
            try
            {
                module.CopyData(source, dest);
            }
            catch (Exception e)
            {
                ModCore.Error($"Error copying module data for '{module.ModuleName}' (key: {module.ModuleKey})", e);
            }
        }
    }

    // ==================== Queries ====================

    public bool AppliesTo(PawnKindDef def)
    {
        try
        {
            if (Def == null)
                return false;
            return def != null && (Def.defName == def.defName || def.defName == NormaliseDef(Def).defName);
        }
        catch (Exception)
        {
            Log.Message($"Something was null when checking if edit for {Def?.defName ?? "UNKNOWN"} applies to {def?.defName ?? "UNKNOWN"}");
            throw;
        }
    }
}
