using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace FactionLoadout;

public static class VEPsycastsReflectionHelper
{
    public const string VpeExtensionClassName = "VanillaPsycastsExpanded.PawnKindAbilityExtension_Psycasts";
    public const string VEAbilityExtensionClassName = "VanillaPsycastsExpanded.AbilityExtension_Psycast";

    public static Lazy<bool> ModLoaded = new(() => ModLister.GetActiveModWithIdentifier("vanillaexpanded.vpsycastse") is not null);
    public static Lazy<Type> VpeExtensionType = new(() => AccessTools.TypeByName(VpeExtensionClassName));
    public static Lazy<Type> PathUnlockDataType = new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PathUnlockData"));
    public static Lazy<Type> ClosedUnlockedPathsListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PathUnlockDataType.Value));
    public static Lazy<FieldInfo> StatUpgradePointsField = new(() => VpeExtensionType.Value?.GetField("statUpgradePoints"));
    public static Lazy<FieldInfo> LevelField = new(() => VpeExtensionType.Value?.GetField("initialLevel"));
    public static Lazy<FieldInfo> GiveRandomAbilitiesField = new(() => VpeExtensionType.Value?.GetField("giveRandomAbilities"));
    public static Lazy<FieldInfo> ImplantDefField = new(() => VpeExtensionType.Value?.GetField("implantDef"));
    public static Lazy<FieldInfo> UnlockedPathsField = new(() => VpeExtensionType.Value?.GetField("unlockedPaths"));
    public static Lazy<Type> PsycasterPathDefType = new(() => AccessTools.TypeByName("VanillaPsycastsExpanded.PsycasterPathDef"));

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
        _lastExtension = currentDef.modExtensions?.Find(me => me.GetType().FullName == VpeExtensionClassName);
        return _lastExtension;
    }
}
