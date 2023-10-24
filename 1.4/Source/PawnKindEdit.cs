﻿using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public class PawnKindEdit : IExposable
    {
        private static Dictionary<PawnKindDef, List<PawnKindEdit>> activeEdits = new();

        public static IEnumerable<PawnKindEdit> GetEditsFor(PawnKindDef def)
        {
            if (def == null)
                yield break;

            if (!activeEdits.TryGetValue(def, out var list)) yield break;
            foreach (PawnKindEdit item in list)
                yield return item;
        }

        private static void AddActiveEdit(PawnKindDef def, PawnKindEdit edit)
        {
            if (def == null || edit == null)
                return;

            if (!activeEdits.TryGetValue(def, out var list))
            {
                list = new List<PawnKindEdit>();
                activeEdits.Add(def, list);
            }

            if (!list.Contains(edit))
                list.Add(edit);
        }

        public PawnKindDef Def;
        public bool IsGlobal = false;

        public FactionEdit ParentEdit
        {
            get
            {
                return Preset.LoadedPresets
                    .SelectMany(preset => preset.factionChanges)
                    .FirstOrDefault(change => change.KindEdits.Contains(this));
            }
        }

        public PawnKindDef ReplaceWith = null;
        public bool ForceNaked = false;
        public QualityCategory? ItemQuality = null;
        public float? BiocodeWeaponChance = null;
        public float? TechHediffChance = null;
        public int? TechHediffsMaxAmount = null;
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
        public QualityCategory? ForcedWeaponQuality = null;
        public Color? ApparelColor = null;
        public string Label = null;
        public ThingDef Race = null;
        public List<HairDef> CustomHair = null;
        public List<Color> CustomHairColors = null;
        public bool DeletedOrClosed;

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

        public PawnKindEdit()
        {
        }

        public PawnKindEdit(PawnKindDef def)
        {
            Def = def;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref Def, "def");
            Scribe_Defs.Look(ref ReplaceWith, "replaceWith");
            Scribe_Values.Look(ref ForceNaked, "forceNaked");
            Scribe_Values.Look(ref ItemQuality, "itemQuality");
            Scribe_Values.Look(ref TechHediffChance, "techHediffChance");
            Scribe_Values.Look(ref TechHediffsMaxAmount, "techHediffsMaxAmount");
            Scribe_Collections.Look(ref TechHediffDisallowedTags, "techHediffDisallowedTags");
            Scribe_Collections.Look(ref TechHediffTags, "techHediffTags");
            Scribe_Values.Look(ref BiocodeWeaponChance, "biocodeWeaponChance");
            Scribe_Values.Look(ref ApparelMoney, "apparelMoney");
            Scribe_Values.Look(ref TechMoney, "techMoney");
            Scribe_Values.Look(ref WeaponMoney, "weaponMoney");
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
            Scribe_Collections.Look(ref CustomHair, "customHair", LookMode.Def);
            Scribe_Collections.Look(ref CustomHairColors, "customHairColors");

            // VFEAncients Compatibility
            Scribe_Values.Look(ref NumVFEAncientsSuperPowers, "numVFEAncientsSuperPowers");
            Scribe_Values.Look(ref NumVFEAncientsSuperWeaknesses, "numVFEAncientsSuperWeaknesses");
            Scribe_Collections.Look(ref ForcedVFEAncientsItems, "forcedVFEAncientsEffects");

            // VPE
            Scribe_Values.Look(ref VEPsycastLevel, "vePsycastLevel");
            Scribe_Values.Look(ref VEPsycastStatPoints, "vePsycastStatPoints");
            Scribe_Values.Look(ref VEPsycastRandomAbilities, "vePsycastRandomAbilities");
        }

        private void ReplaceMaybe<T>(ref T field, T maybe) where T : class
        {
            if (maybe == null)
                return;

            field = maybe;
        }

        private void ReplaceMaybe<T>(ref T field, T? maybe) where T : struct
        {
            if (maybe == null)
                return;

            field = maybe.Value;
        }

        private void ReplaceMaybe<T>(ref T? field, T? maybe) where T : struct
        {
            if (maybe == null)
                return;

            field = maybe.Value;
        }

        private void ReplaceMaybeList<T>(ref T field, T maybe, bool tryAdd) where T : IList, new()
        {
            if (maybe == null)
                return;

            if (tryAdd && field != null)
            {
                foreach (var value in maybe)
                {
                    if (!field.Contains(value))
                        field.Add(value);
                }
            }
            else
            {
                field = new T();
                foreach (var value in maybe)
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
            ReplaceMaybe(ref def.inventoryOptions, Inventory);
            ReplaceMaybe(ref def.forceWeaponQuality, ForcedWeaponQuality);
            ReplaceMaybe(ref def.label, Label);
            ReplaceMaybe(ref def.race, Race);

            ReplaceMaybeList(ref def.techHediffsTags, TechHediffTags, global?.TechHediffTags != null);
            ReplaceMaybeList(ref def.techHediffsDisallowTags, TechHediffDisallowedTags, global?.TechHediffDisallowedTags != null);
            ReplaceMaybeList(ref def.weaponTags, WeaponTags, global?.WeaponTags != null);
            ReplaceMaybeList(ref def.apparelTags, ApparelTags, global?.ApparelTags != null);
            ReplaceMaybeList(ref def.apparelDisallowTags, ApparelDisallowedTags, global?.ApparelDisallowedTags != null);
            ReplaceMaybeList(ref def.apparelRequired, ApparelRequired, global?.ApparelRequired != null);
            ReplaceMaybeList(ref def.techHediffsRequired, TechRequired, global?.TechRequired != null);

            var removeSpecific = ApparelRequired != null || SpecificApparel != null;
            if (removeSpecific)
                def.specificApparelRequirements = null;

            // Can't be done like this. Disabled for now.
            if (Race != null)
            {
                // Try find life stages of new race.
                PawnKindDef realKind = DefDatabase<PawnKindDef>.AllDefsListForReading
                    .FirstOrDefault(k => k != def && k.defName != def.defName && k.race == Race);
                if (realKind != null)
                    def.lifeStages = realKind.lifeStages;
            }

            // Special case: color cannot be pure white, because Rimworld will then ignore it.
            // If color is not null and is pure white, change it to a slightly off-white.
            var color = ApparelColor;
            if (color != null && color == Color.white)
                color = new Color(0.995f, 0.995f, 0.995f, 1f);
            ReplaceMaybe(ref def.apparelColor, color);

            if (def.RaceProps.Animal) return def; // Animals can't have powers
            ApplyVFEAncientsEdits(def);
            ApplyVEPsycastsEdits(def);

            globalEdit = null;
            return def;
        }

        public virtual void ApplyVEPsycastsEdits(PawnKindDef def)
        {
            if (ModLister.GetActiveModWithIdentifier("vanillaexpanded.vpsycastse") is null) return;
            if (VEPsycastLevel == null && VEPsycastStatPoints == null && VEPsycastRandomAbilities == null) return;
            def.modExtensions ??= new List<DefModExtension>();
            DefModExtension vePsycastExtension = def.modExtensions.Find(me => me.GetType().FullName == VEPsycastsReflectionHelper.VpeExtensionClassName);
            if (vePsycastExtension == null)
            {
                vePsycastExtension = AccessTools.CreateInstance(VEPsycastsReflectionHelper.VpeExtensionType.Value) as DefModExtension;
                VEPsycastsReflectionHelper.ImplantDefField.Value?.SetValue(vePsycastExtension, DefDatabase<HediffDef>.GetNamed("VPE_PsycastAbilityImplant"));
                VEPsycastsReflectionHelper.UnlockedPathsField.Value?.SetValue(vePsycastExtension,
                    AccessTools.CreateInstance(VEPsycastsReflectionHelper.ClosedUnlockedPathsListGenericType.Value));
                def.modExtensions.Add(vePsycastExtension);
            }

            // Set the field values
            if (VEPsycastLevel != null) VEPsycastsReflectionHelper.LevelField.Value?.SetValue(vePsycastExtension, VEPsycastLevel);
            if (VEPsycastStatPoints != null) VEPsycastsReflectionHelper.StatUpgradePointsField.Value?.SetValue(vePsycastExtension, VEPsycastStatPoints);
            if (VEPsycastRandomAbilities != null) VEPsycastsReflectionHelper.GiveRandomAbilitiesField.Value?.SetValue(vePsycastExtension, VEPsycastRandomAbilities);
        }

        public virtual void ApplyVFEAncientsEdits(PawnKindDef def)
        {
            if (ModLister.GetActiveModWithIdentifier("vanillaexpanded.vfea") is null) return;
            if (NumVFEAncientsSuperPowers == null && NumVFEAncientsSuperWeaknesses == null && ForcedVFEAncientsItems == null) return;
            def.modExtensions ??= new List<DefModExtension>();
            DefModExtension ancientsExtension = def.modExtensions.Find(me => me.GetType().FullName == VFEAncientsReflectionHelper.VfeAncientsExtensionClassName);
            if (ancientsExtension == null)
            {
                ancientsExtension = AccessTools.CreateInstance(VFEAncientsReflectionHelper.VfeAncientsExtensionType.Value) as DefModExtension;
                def.modExtensions.Add(ancientsExtension);
            }

            // Set the field values
            if (NumVFEAncientsSuperPowers != null) VFEAncientsReflectionHelper.NumRandomSuperpowersField.Value?.SetValue(ancientsExtension, NumVFEAncientsSuperPowers);
            if (NumVFEAncientsSuperWeaknesses != null) VFEAncientsReflectionHelper.NumRandomWeaknessesField.Value?.SetValue(ancientsExtension, NumVFEAncientsSuperWeaknesses);
            if (ForcedVFEAncientsItems != null)
            {
                var powers = VFEAncientsReflectionHelper.ForcePowersField.Value?.GetValue(ancientsExtension);
                if (powers == null)
                {
                    powers = AccessTools.CreateInstance(VFEAncientsReflectionHelper.ClosedPowerListGenericType.Value);
                    VFEAncientsReflectionHelper.ForcePowersField.Value?.SetValue(ancientsExtension, powers);
                }

                if (powers is not IList powerList) return;
                powerList.Clear();
                ForcedVFEAncientsItems.Select(i => VFEAncientsReflectionHelper.GetPowerDefMethod.Value.Invoke(null, new object[] { i }))
                    .Where(p => p != null)
                    .DoIf(p => !powerList.Contains(p), p => powerList.Add(p));
            }
        }

        public bool AppliesTo(PawnKindDef def)
        {
            return def != null && Def.defName == def.defName;
        }
    }

    public static class ReflectionHelper
    {
        public static Lazy<Type> DefDatabaseGenericType = new(() => typeof(DefDatabase<>));
        public static Lazy<Type> ListGenericType = new(() => typeof(List<>));

        public static Lazy<MethodInfo> GetCompGenericMethod = new(() => AccessTools.Method(typeof(Pawn), "GetComp"));
    }

    public static class VEPsycastsReflectionHelper
    {
        public const string VpeExtensionClassName = "VanillaPsycastsExpanded.PawnKindAbilityExtension_Psycasts";
        public const string VEAbilityExtensionClassName = "VanillaPsycastsExpanded.AbilityExtension_Psycast";
        public static Lazy<Type> VpeExtensionType = new(() => AccessTools.TypeByName(VpeExtensionClassName));
        public static Lazy<Type> PathUnlockDataType = new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PathUnlockData"));
        public static Lazy<Type> ClosedUnlockedPathsListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PathUnlockDataType.Value));
        public static Lazy<FieldInfo> StatUpgradePointsField = new(() => VpeExtensionType.Value?.GetField("statUpgradePoints"));
        public static Lazy<FieldInfo> LevelField = new(() => VpeExtensionType.Value?.GetField("initialLevel"));
        public static Lazy<FieldInfo> GiveRandomAbilitiesField = new(() => VpeExtensionType.Value?.GetField("giveRandomAbilities"));
        public static Lazy<FieldInfo> ImplantDefField = new(() => VpeExtensionType.Value?.GetField("implantDef"));
        public static Lazy<FieldInfo> UnlockedPathsField = new(() => VpeExtensionType.Value?.GetField("unlockedPaths"));
        public static Lazy<Type> VpeHediff_PsycastAbilities = new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.Hediff_PsycastAbilities"));
        public static Lazy<Type> PsycasterPathDefType = new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PsycasterPathDef"));
        public static Lazy<Type> AbilityExtension_PsycastType = new(() => AccessTools.TypeByName(VEAbilityExtensionClassName));
        // public static Lazy<MethodInfo> VpeHediff_PsycastAbilities_UnlockPathMethod = new(() => VpeHediff_PsycastAbilities.Value?.GetMethod("UnlockPath"));
        public static Lazy<Type> ClosedDefDatabasePsycasterPathType = new(() => ReflectionHelper.DefDatabaseGenericType.Value?.MakeGenericType(PsycasterPathDefType.Value));
        // public static Lazy<PropertyInfo> GetPsycasterPathDefsMethod = new(() => ClosedDefDatabasePsycasterPathType.Value?.GetProperty("AllDefsListForReading"));
        // public static Lazy<Type> ClosedPsycastPathListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PsycasterPathDefType.Value));
        // public static Lazy<MethodInfo> VEAbilityExtension_PrereqsCompletedMethod = new(() => AbilityExtension_PsycastType.Value?.GetMethod("PrereqsCompleted"));

        // Core
        public static Lazy<Type> VpeAbilityType = new(() => AccessTools.TypeByName("VFECore.Abilities.Ability"));
        // public static Lazy<Type> ClosedAbilityListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(VpeAbilityType.Value));
        public static Lazy<Type> VpeAbilitiesComp = new(() => AccessTools.TypeByName("VFECore.Abilities.CompAbilities"));
        // public static Lazy<MethodInfo> ClosedAbilityGetCompGenericMethod = new(() => ReflectionHelper.GetCompGenericMethod.Value?.MakeGenericMethod(VpeAbilitiesComp.Value));

        [CanBeNull] private static PawnKindDef _lastDef = null;
        [CanBeNull] private static DefModExtension _lastExtension = null;

        [CanBeNull]
        public static DefModExtension FindVEPsycastsExtension(PawnKindDef currentDef)
        {
            if (_lastDef == currentDef) return _lastExtension;
            _lastDef = currentDef;
            _lastExtension = currentDef.modExtensions
                ?.Find(me => me.GetType().FullName == VpeExtensionClassName);
            return _lastExtension;
        }
    }

    public static class VFEAncientsReflectionHelper
    {
        public const string VfeAncientsExtensionClassName = "VFEAncients.PawnKindExtension_Powers";
        public static Lazy<Type> VfeAncientsExtensionType = new(() => AccessTools.TypeByName("VFEAncients.PawnKindExtension_Powers"));
        public static Lazy<Type> PowerDefType = new(() => AccessTools.TypeByName("VFEAncients.PowerDef"));
        public static Lazy<FieldInfo> NumRandomSuperpowersField = new(() => VfeAncientsExtensionType.Value?.GetField("numRandomSuperpowers"));
        public static Lazy<FieldInfo> NumRandomWeaknessesField = new(() => VfeAncientsExtensionType.Value?.GetField("numRandomWeaknesses"));
        public static Lazy<FieldInfo> ForcePowersField = new(() => VfeAncientsExtensionType.Value?.GetField("forcePowers"));
        public static Lazy<Type> ClosedDefDatabaseType = new(() => ReflectionHelper.DefDatabaseGenericType.Value?.MakeGenericType(PowerDefType.Value));
        public static Lazy<Type> ClosedPowerListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PowerDefType.Value));
        public static Lazy<TypeConverter> PowerDefConverter = new(() => TypeDescriptor.GetConverter(PowerDefType));
        public static Lazy<MethodInfo> GetPowerDefMethod = new(() => ClosedDefDatabaseType.Value?.GetMethod("GetNamedSilentFail"));
        public static Lazy<PropertyInfo> GetPowerDefsMethod = new(() => ClosedDefDatabaseType.Value?.GetProperty("AllDefsListForReading"));

        [CanBeNull] private static PawnKindDef _lastDef = null;
        [CanBeNull] private static DefModExtension _lastExtension = null;

        [CanBeNull]
        public static DefModExtension FindVEPsycastsExtension(PawnKindDef currentDef)
        {
            if (_lastDef == currentDef) return _lastExtension;
            _lastDef = currentDef;
            _lastExtension = currentDef.modExtensions
                ?.Find(me => me.GetType().FullName == VfeAncientsExtensionClassName);
            return _lastExtension;
        }
    }
}
