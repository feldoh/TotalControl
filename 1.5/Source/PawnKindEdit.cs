using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class PawnKindEdit : IExposable
{
    public static Dictionary<PawnKindDef, List<PawnKindEdit>> activeEdits = new();
    public static Dictionary<PawnKindDef, PawnKindDef> replacementToOriginal = new();
    public static RulePackDef FakeRulePack = new() { defName = "NONE" };

    public static void RecordReplacement(PawnKindDef original, PawnKindDef replacement) => replacementToOriginal.SetOrAdd(replacement, original);

    public static List<PawnKindEdit> RemoveActiveEdits(PawnKindDef pawnKindDef)
    {
        List<PawnKindEdit> currentEdits = activeEdits.TryGetValue(pawnKindDef, null);
        activeEdits.Remove(pawnKindDef);
        return currentEdits;
    }

    public static void SetActiveEdits(PawnKindDef pawnKindDef, List<PawnKindEdit> edits)
    {
        activeEdits.SetOrAdd(pawnKindDef, edits);
    }

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
        {
            return fallbackFactionDef;
        }

        return def == PawnKindDefOf.WildMan ? Preset.SpecialWildManFaction : fallbackFactionDef;
    }

    private static void AddActiveEdit(PawnKindDef def, PawnKindEdit edit)
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

    public PawnKindDef Def;
    public bool IsGlobal = false;

    public FactionEdit ParentEdit
    {
        get { return Preset.LoadedPresets.SelectMany(preset => preset.factionChanges).FirstOrDefault(change => change.KindEdits.Contains(this)); }
    }

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
    public bool DeletedOrClosed;
    public List<ForcedHediff> ForcedHediffs = null;
    public Dictionary<string, float> ForcedXenotypeChances = new();
    public Dictionary<XenotypeDef, float> ForcedXenotypeChanceDefs = new();
    public Gender? ForcedGender = null;
    public SimpleCurve RaidCommonalityFromPointsCurve = null;
    public SimpleCurve RaidLootValueFromPointsCurve = null;
    public RulePackDef NameMaker = null;
    public RulePackDef NameMakerFemale = null;
    public float? UnwaveringlyLoyalChance = null;

    // VFE Ancients
    public int? NumVFEAncientsSuperPowers = null;
    public int? NumVFEAncientsSuperWeaknesses = null;
    public List<string> ForcedVFEAncientsItems = null;

    // VPE
    public int? VEPsycastLevel = null;
    public IntRange? VEPsycastStatPoints = null;
    public bool? VEPsycastRandomAbilities = null;

    /**
     * public List<AbilityDef> giveAbilities
     * public HediffDef implantDef
     * List<PathUnlockData> unlockedPaths
     * |- public PsycasterPathDef path
     * |- public IntRange unlockedAbilityLevelRange
     * |- public IntRange unlockedAbilityCount
     */
    private PawnKindEdit globalEdit = null;

    public PawnKindEdit() { }

    public PawnKindEdit(PawnKindDef def)
    {
        Def = def;
    }

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
        Scribe_Collections.Look(ref ForcedXenotypeChances, "forcedXenotypeChances", LookMode.Value, LookMode.Value);
        Scribe_Deep.Look(ref RaidLootValueFromPointsCurve, "raidLootValueFromPointsCurve");
        Scribe_Deep.Look(ref RaidCommonalityFromPointsCurve, "raidCommonalityFromPointsCurve");
        Scribe_Values.Look(ref UnwaveringlyLoyalChance, "unwaveringlyLoyalChance");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ForcedXenotypeChanceDefs = ForcedXenotypeChances
                .Select(kvp => new Pair<XenotypeDef, float>(DefDatabase<XenotypeDef>.GetNamedSilentFail(kvp.Key), kvp.Value))
                .Where(c => c.first != null)
                .ToDictionary(kvp => kvp.first, kvp => kvp.second);
        }

        bool isFake = NameMaker == FakeRulePack;
        if (isFake)
            NameMaker = null;
        Scribe_Defs.Look(ref NameMaker, "nameMaker");
        if (isFake)
            NameMaker = FakeRulePack;
        isFake = NameMakerFemale == FakeRulePack;
        if (isFake)
            NameMakerFemale = null;
        Scribe_Defs.Look(ref NameMakerFemale, "nameMakerFemale");
        if (isFake)
            NameMakerFemale = FakeRulePack;

        // VFEAncients Compatibility
        Scribe_Values.Look(ref NumVFEAncientsSuperPowers, "numVFEAncientsSuperPowers");
        Scribe_Values.Look(ref NumVFEAncientsSuperWeaknesses, "numVFEAncientsSuperWeaknesses");
        Scribe_Collections.Look(ref ForcedVFEAncientsItems, "forcedVFEAncientsEffects");

        // VPE
        Scribe_Values.Look(ref VEPsycastLevel, "vePsycastLevel");
        Scribe_Values.Look(ref VEPsycastStatPoints, "vePsycastStatPoints");
        Scribe_Values.Look(ref VEPsycastRandomAbilities, "vePsycastRandomAbilities");
    }

    private void ReplaceMaybe<T>(ref T field, T maybe)
        where T : class
    {
        if (maybe == null)
            return;

        field = maybe;
    }

    private void ReplaceMaybe<T>(ref T field, T? maybe)
        where T : struct
    {
        if (maybe == null)
            return;

        field = maybe.Value;
    }

    private void ReplaceMaybe<T>(ref T? field, T? maybe)
        where T : struct
    {
        if (maybe == null)
            return;

        field = maybe.Value;
    }

    private void ReplaceMaybeList<T>(ref T field, T maybe, bool tryAdd)
        where T : IList, new()
    {
        if (maybe == null)
            return;

        if (tryAdd && field != null)
        {
            foreach (object value in maybe)
                if (!field.Contains(value))
                    field.Add(value);
        }
        else
        {
            field = [];
            foreach (object value in maybe)
                field.Add(value);
        }
    }

    private void ReplaceMaybe(ref PawnInventoryOption inv, InventoryOptionEdit maybe)
    {
        if (maybe == null)
            return;

        if (globalEdit?.Inventory != null || (IsGlobal && !ReplaceDefaultInventory))
        {
            if (inv == null)
            {
                inv = maybe.ConvertToVanilla();
            }
            else
            {
                PawnInventoryOption vanilla = maybe.ConvertToVanilla();
                if (vanilla.subOptionsTakeAll != null)
                    inv.subOptionsTakeAll.AddRange(vanilla.subOptionsTakeAll);
                if (vanilla.subOptionsChooseOne != null)
                    inv.subOptionsChooseOne.AddRange(vanilla.subOptionsChooseOne);
            }
        }
        else
        {
            inv = maybe.ConvertToVanilla();
        }
    }

    public PawnKindDef Apply(PawnKindDef def, PawnKindEdit global, bool addToEdits = true)
    {
        if (def == null)
            return null;

        if (addToEdits)
            AddActiveEdit(def, this);

        if (ReplaceWith != null)
            return ReplaceWith;

        // Only human-likes can have race replaced.
        if (def.RaceProps.Animal)
            Race = null;

        globalEdit = global;

        ReplaceMaybe(ref def.itemQuality, ItemQuality);
        ReplaceMaybe(ref def.biocodeWeaponChance, BiocodeWeaponChance);
        ReplaceMaybe(ref def.techHediffsChance, TechHediffChance);
        ReplaceMaybe(ref def.techHediffsMaxAmount, TechHediffsMaxAmount);
        ReplaceMaybe(ref def.apparelMoney, ApparelMoney);
        ReplaceMaybe(ref def.techHediffsMoney, TechMoney);
        ReplaceMaybe(ref def.weaponMoney, WeaponMoney);
        ReplaceMaybe(ref def.minGenerationAge, MinGenerationAge);
        ReplaceMaybe(ref def.maxGenerationAge, MaxGenerationAge);
        ReplaceMaybe(ref def.inventoryOptions, Inventory);
        ReplaceMaybe(ref def.forceWeaponQuality, ForcedWeaponQuality);
        ReplaceMaybe(ref def.label, Label);
        ReplaceMaybe(ref def.race, Race);
        ReplaceMaybe(ref def.fixedGender, ForcedGender);
        ReplaceMaybe(ref def.nameMaker, NameMaker);
        ReplaceMaybe(ref def.nameMakerFemale, NameMakerFemale);

        ReplaceMaybeList(ref def.techHediffsTags, TechHediffTags, global?.TechHediffTags != null);
        ReplaceMaybeList(ref def.techHediffsDisallowTags, TechHediffDisallowedTags, global?.TechHediffDisallowedTags != null);
        ReplaceMaybeList(ref def.weaponTags, WeaponTags, global?.WeaponTags != null);
        ReplaceMaybeList(ref def.apparelTags, ApparelTags, global?.ApparelTags != null);
        ReplaceMaybeList(ref def.apparelDisallowTags, ApparelDisallowedTags, global?.ApparelDisallowedTags != null);
        ReplaceMaybeList(ref def.apparelRequired, ApparelRequired, global?.ApparelRequired != null);
        ReplaceMaybeList(ref def.techHediffsRequired, TechRequired, global?.TechRequired != null);

        bool removeFixedInventory = RemoveFixedInventory || global?.RemoveFixedInventory == true;
        if (removeFixedInventory)
            def.fixedInventory = [];

        bool removeSpecific = ApparelRequired != null || SpecificApparel != null;
        if (removeSpecific)
            def.specificApparelRequirements = null;

        // Can't be done like this. Disabled for now.
        if (Race != null)
        {
            // Try find life stages of new race.
            PawnKindDef realKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k != def && k.defName != def.defName && k.race == Race);
            if (realKind != null)
                def.lifeStages = realKind.lifeStages;
        }

        // Special case: color cannot be pure white, because Rimworld will then ignore it.
        // If color is not null and is pure white, change it to a slightly off-white.
        Color? color = ApparelColor;
        if (color != null && color == Color.white)
            color = new Color(0.995f, 0.995f, 0.995f, 1f);
        ReplaceMaybe(ref def.apparelColor, color);

        def.modExtensions ??= [];

        if (ForcedHediffs is { Count: > 0 })
        {
            ForcedHediffModExtension hediffExtension = def.GetModExtension<ForcedHediffModExtension>();
            if (hediffExtension == null)
            {
                hediffExtension = new ForcedHediffModExtension();
                def.modExtensions.Add(hediffExtension);
            }

            hediffExtension.forcedHediffs.AddRange(ForcedHediffs);
            ModCore.Debug($"Adding forced hediffs {hediffExtension.forcedHediffs?.Select(h => h.HediffDef?.defName).ToCommaList() ?? "None"} to {def.defName}");
        }

        if (ModsConfig.BiotechActive && def.RaceProps.Humanlike && ForceSpecificXenos && (ForcedXenotypeChanceDefs?.Count ?? 0) >= 1)
        {
            def.useFactionXenotypes = false;
            def.xenotypeSet ??= new XenotypeSet();
            def.xenotypeSet.xenotypeChances ??= [];
            def.xenotypeSet.xenotypeChances.Clear();
            foreach (KeyValuePair<XenotypeDef, float> rate in ForcedXenotypeChanceDefs ?? [])
                def.xenotypeSet.xenotypeChances.Add(new XenotypeChance(rate.Key, rate.Value));
        }

        if (def.RaceProps.Animal)
            return def; // Animals can't have powers
        ApplyVFEAncientsEdits(def);
        ApplyVEPsycastsEdits(def);

        globalEdit = null;
        return def;
    }

    public virtual void ApplyVEPsycastsEdits(PawnKindDef def)
    {
        if (!VEPsycastsReflectionHelper.ModLoaded.Value)
            return;
        if (VEPsycastLevel == null && VEPsycastStatPoints == null && VEPsycastRandomAbilities == null)
            return;
        def.modExtensions ??= [];
        DefModExtension vePsycastExtension = VEPsycastsReflectionHelper.FindVEPsycastsExtension(def);
        if (vePsycastExtension == null)
        {
            vePsycastExtension = AccessTools.CreateInstance(VEPsycastsReflectionHelper.VpeExtensionType.Value) as DefModExtension;
            VEPsycastsReflectionHelper.ImplantDefField.Value?.SetValue(vePsycastExtension, DefDatabase<HediffDef>.GetNamed("VPE_PsycastAbilityImplant"));
            VEPsycastsReflectionHelper.UnlockedPathsField.Value?.SetValue(
                vePsycastExtension,
                AccessTools.CreateInstance(VEPsycastsReflectionHelper.ClosedUnlockedPathsListGenericType.Value)
            );
            def.modExtensions.Add(vePsycastExtension);
        }

        // Set the field values
        if (VEPsycastLevel != null)
            VEPsycastsReflectionHelper.LevelField.Value?.SetValue(vePsycastExtension, VEPsycastLevel);
        if (VEPsycastStatPoints != null)
            VEPsycastsReflectionHelper.StatUpgradePointsField.Value?.SetValue(vePsycastExtension, VEPsycastStatPoints);
        if (VEPsycastRandomAbilities != null)
            VEPsycastsReflectionHelper.GiveRandomAbilitiesField.Value?.SetValue(vePsycastExtension, VEPsycastRandomAbilities);
    }

    public virtual void ApplyVFEAncientsEdits(PawnKindDef def)
    {
        if (!VFEAncientsReflectionHelper.ModLoaded.Value)
            return;
        if (NumVFEAncientsSuperPowers == null && NumVFEAncientsSuperWeaknesses == null && ForcedVFEAncientsItems == null)
            return;
        def.modExtensions ??= [];
        DefModExtension ancientsExtension = VFEAncientsReflectionHelper.FindVEAncientsExtension(def);
        if (ancientsExtension == null)
        {
            ancientsExtension = AccessTools.CreateInstance(VFEAncientsReflectionHelper.VfeAncientsExtensionType.Value) as DefModExtension;
            def.modExtensions.Add(ancientsExtension);
        }

        // Set the field values
        if (NumVFEAncientsSuperPowers != null)
            VFEAncientsReflectionHelper.NumRandomSuperpowersField.Value?.SetValue(ancientsExtension, NumVFEAncientsSuperPowers);
        if (NumVFEAncientsSuperWeaknesses != null)
            VFEAncientsReflectionHelper.NumRandomWeaknessesField.Value?.SetValue(ancientsExtension, NumVFEAncientsSuperWeaknesses);
        if (ForcedVFEAncientsItems == null)
            return;
        object powers = VFEAncientsReflectionHelper.ForcePowersField.Value?.GetValue(ancientsExtension);
        if (powers == null)
        {
            powers = AccessTools.CreateInstance(VFEAncientsReflectionHelper.ClosedPowerListGenericType.Value);
            VFEAncientsReflectionHelper.ForcePowersField.Value?.SetValue(ancientsExtension, powers);
        }

        if (powers is not IList powerList)
            return;
        powerList.Clear();
        ForcedVFEAncientsItems
            .Select(i => VFEAncientsReflectionHelper.GetPowerDefMethod.Value.Invoke(null, new object[] { i }))
            .Where(p => p != null)
            .DoIf(p => !powerList.Contains(p), p => powerList.Add(p));
    }

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

public static class ReflectionHelper
{
    public static Lazy<Type> DefDatabaseGenericType = new(() => typeof(DefDatabase<>));
    public static Lazy<Type> ListGenericType = new(() => typeof(List<>));

    public static Lazy<MethodInfo> GetCompGenericMethod = new(() => AccessTools.Method(typeof(Pawn), "GetComp"));
}
