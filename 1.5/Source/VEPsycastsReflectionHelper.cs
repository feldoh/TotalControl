using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace FactionLoadout;

public static class GiddyUpReflectionHelper
{
    public static Lazy<bool> ModLoaded = new(() => ModsConfig.IsActive("Owlchemist.GiddyUp"));
    
    public static string CustomMountExtensionClassName = "GiddyUp.CustomMounts";
    public static Lazy<Type> CustomMountExtensionType = new(() => AccessTools.TypeByName(CustomMountExtensionClassName));
    public static Lazy<FieldInfo> MountChanceField =
        new(() => CustomMountExtensionType.Value?.GetField("mountChance"));
    public static Lazy<FieldInfo> PossibleMountsField =
        new(() => CustomMountExtensionType.Value?.GetField("possibleMounts"));
            
    public static string CustomStatsExtensionClassName = "GiddyUp.CustomStats";
    public static Lazy<Type> CustomStatsExtensionType = new(() => AccessTools.TypeByName(CustomStatsExtensionClassName));
    public static Lazy<FieldInfo> SpeedModifierField =
        new(() => CustomStatsExtensionType.Value?.GetField("speedModifier"));
    public static Lazy<FieldInfo> ArmorModifierField =
        new(() => CustomStatsExtensionType.Value?.GetField("armorModifier"));
    public static Lazy<FieldInfo> UseMetalArmorField =
        new(() => CustomStatsExtensionType.Value?.GetField("useMetalArmor"));
    
    [CanBeNull]
    private static PawnKindDef _lastDef = null;

    [CanBeNull]
    private static DefModExtension _lastMountExtension = null;
    [CanBeNull]
    private static DefModExtension _lastStatExtension = null;
    
    public static void SetMountChance(PawnKindDef currentDef, int mountChance)
    {
        var extension = FindMountExtension(currentDef, true);
        MountChanceField.Value.SetValue(extension, mountChance);
    }
    
    public static int? GetMountChance(PawnKindDef currentDef)
    {
        var extension = FindMountExtension(currentDef);
        return extension == null ? null : (int?) MountChanceField.Value?.GetValue(extension);
    }

    public static void SetCustomMounts(PawnKindDef currentDef, Dictionary<PawnKindDef, int> mounts)
    {
        var extension = FindMountExtension(currentDef, true);
        PossibleMountsField.Value.SetValue(extension, mounts);
    }

    public static Dictionary<PawnKindDef, int> GetCustomMounts(PawnKindDef currentDef)
    {
        var extension = FindMountExtension(currentDef);
        return extension == null ? null : (Dictionary<PawnKindDef, int>) PossibleMountsField.Value?.GetValue(extension);
    }

    public static void SetSpeedModifier(PawnKindDef currentDef, float speedModifier)
    {
        var extension = FindStatsExtension(currentDef, true);
        SpeedModifierField.Value.SetValue(extension, speedModifier);
    }
    
    public static float? GetSpeedModifier(PawnKindDef currentDef)
    {
        var extension = FindStatsExtension(currentDef);
        return extension == null ? null : (float?) SpeedModifierField.Value?.GetValue(extension);
    }
    
    public static void SetArmorModifier(PawnKindDef currentDef, float armorModifier)
    {
        var extension = FindStatsExtension(currentDef, true);
        ArmorModifierField.Value.SetValue(extension, armorModifier);
    }
    
    public static float? GetArmorModifier(PawnKindDef currentDef)
    {
        var extension = FindStatsExtension(currentDef);
        return extension == null ? null : (float?) ArmorModifierField.Value?.GetValue(extension);
    }
    
    public static void SetUseMetalArmor(PawnKindDef currentDef, bool useMetalArmor)
    {
        var extension = FindStatsExtension(currentDef, true);
        UseMetalArmorField.Value.SetValue(extension, useMetalArmor);
    }

    public static bool? GetUseMetalArmor(PawnKindDef currentDef)
    {
        var extension = FindStatsExtension(currentDef);
        return extension == null ? null : (bool?) UseMetalArmorField.Value?.GetValue(extension);
    }
    
    [CanBeNull]
    public static DefModExtension FindMountExtension(PawnKindDef currentDef, bool createIfAbsent = false)
    {
        if (_lastDef == currentDef)
            return _lastMountExtension;
        _lastDef = currentDef;
        FindModExtensions(currentDef);
        if (_lastMountExtension == null && createIfAbsent)
        {
            _lastMountExtension = AccessTools.CreateInstance(CustomMountExtensionType.Value) as DefModExtension;
            currentDef.modExtensions.Add(_lastMountExtension);
        }
        return _lastMountExtension;
    }
    
    [CanBeNull]
    public static DefModExtension FindStatsExtension(PawnKindDef currentDef, bool createIfAbsent = false)
    {
        if (_lastDef == currentDef)
            return _lastStatExtension;
        _lastDef = currentDef;
        FindModExtensions(currentDef);
        if (_lastStatExtension == null && createIfAbsent)
        {
            _lastStatExtension = AccessTools.CreateInstance(CustomStatsExtensionType.Value) as DefModExtension;
            currentDef.modExtensions.Add(_lastStatExtension);
        }
        return _lastStatExtension;
    }

    public static void FindModExtensions(PawnKindDef currentDef)
    {
        currentDef.modExtensions ??= [];
        _lastMountExtension = currentDef.modExtensions?.Find(me =>
            me.GetType().FullName == CustomMountExtensionClassName
        );
        _lastStatExtension = currentDef.modExtensions?.Find(me =>
            me.GetType().FullName == CustomStatsExtensionClassName
        );
    }

}
public static class VEPsycastsReflectionHelper
{
    public const string VpeExtensionClassName =
        "VanillaPsycastsExpanded.PawnKindAbilityExtension_Psycasts";
    public const string VEAbilityExtensionClassName =
        "VanillaPsycastsExpanded.AbilityExtension_Psycast";

    public static Lazy<bool> ModLoaded =
        new(() => ModsConfig.IsActive("vanillaexpanded.vpsycastse"));
    public static Lazy<Type> VpeExtensionType =
        new(() => AccessTools.TypeByName(VpeExtensionClassName));
    public static Lazy<Type> PathUnlockDataType =
        new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PathUnlockData"));
    public static Lazy<Type> ClosedUnlockedPathsListGenericType =
        new(
            () => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PathUnlockDataType.Value)
        );
    public static Lazy<FieldInfo> StatUpgradePointsField =
        new(() => VpeExtensionType.Value?.GetField("statUpgradePoints"));
    public static Lazy<FieldInfo> LevelField =
        new(() => VpeExtensionType.Value?.GetField("initialLevel"));
    public static Lazy<FieldInfo> GiveRandomAbilitiesField =
        new(() => VpeExtensionType.Value?.GetField("giveRandomAbilities"));
    public static Lazy<FieldInfo> ImplantDefField =
        new(() => VpeExtensionType.Value?.GetField("implantDef"));
    public static Lazy<FieldInfo> UnlockedPathsField =
        new(() => VpeExtensionType.Value?.GetField("unlockedPaths"));
    public static Lazy<Type> PsycasterPathDefType =
        new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PsycasterPathDef"));

    [CanBeNull]
    private static PawnKindDef _lastDef = null;

    [CanBeNull]
    private static DefModExtension _lastExtension = null;

    [CanBeNull]
    public static DefModExtension FindVEPsycastsExtension(PawnKindDef currentDef)
    {
        if (_lastDef == currentDef)
            return _lastExtension;
        _lastDef = currentDef;
        _lastExtension = currentDef.modExtensions?.Find(me =>
            me.GetType().FullName == VpeExtensionClassName
        );
        return _lastExtension;
    }
}
