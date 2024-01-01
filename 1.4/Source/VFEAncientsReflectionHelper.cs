using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace FactionLoadout;

public static class VFEAncientsReflectionHelper
{
    public const string VfeAncientsExtensionClassName = "VFEAncients.PawnKindExtension_Powers";

    public static Lazy<bool> ModLoaded = new(() => ModLister.GetActiveModWithIdentifier("VanillaExpanded.VFEA") is not null);
    public static Lazy<Type> VfeAncientsExtensionType = new(() => AccessTools.TypeByName(VfeAncientsExtensionClassName));
    public static Lazy<Type> PowerDefType = new(() => AccessTools.TypeByName("VFEAncients.PowerDef"));
    public static Lazy<FieldInfo> NumRandomSuperpowersField = new(() => VfeAncientsExtensionType.Value?.GetField("numRandomSuperpowers"));
    public static Lazy<FieldInfo> NumRandomWeaknessesField = new(() => VfeAncientsExtensionType.Value?.GetField("numRandomWeaknesses"));
    public static Lazy<FieldInfo> ForcePowersField = new(() => VfeAncientsExtensionType.Value?.GetField("forcePowers"));
    public static Lazy<Type> ClosedDefDatabaseType = new(() => ReflectionHelper.DefDatabaseGenericType.Value?.MakeGenericType(PowerDefType.Value));
    public static Lazy<Type> ClosedPowerListGenericType = new(() => ReflectionHelper.ListGenericType.Value?.MakeGenericType(PowerDefType.Value));
    public static Lazy<MethodInfo> GetPowerDefMethod = new(() => ClosedDefDatabaseType.Value?.GetMethod("GetNamedSilentFail"));
    public static Lazy<PropertyInfo> GetPowerDefsMethod = new(() => ClosedDefDatabaseType.Value?.GetProperty("AllDefsListForReading"));

    [CanBeNull] private static PawnKindDef _lastDef = null;
    [CanBeNull] private static DefModExtension _lastExtension = null;

    [CanBeNull]
    public static DefModExtension FindVEAncientsExtension(PawnKindDef currentDef)
    {
        if (_lastDef == currentDef) return _lastExtension;
        _lastDef = currentDef;
        _lastExtension = currentDef.modExtensions
            ?.Find(me => me.GetType().FullName == VfeAncientsExtensionClassName);
        return _lastExtension;
    }
}
