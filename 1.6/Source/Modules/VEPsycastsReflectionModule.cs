using System;
using System.Reflection;
using FactionLoadout.Util;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace FactionLoadout.Modules;

public static class VEPsycastsReflectionModule
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

    public static void ApplyVEPsycastsEdits(PawnKindEdit edit, PawnKindDef def)
    {
        if (!ModLoaded.Value)
            return;
        if (edit.VEPsycastLevel == null && edit.VEPsycastStatPoints == null && edit.VEPsycastRandomAbilities == null)
            return;

        def.modExtensions ??= [];
        DefModExtension vePsycastExtension = FindVEPsycastsExtension(def);
        if (vePsycastExtension == null)
        {
            vePsycastExtension = AccessTools.CreateInstance(VpeExtensionType.Value) as DefModExtension;
            ImplantDefField.Value?.SetValue(vePsycastExtension, DefDatabase<HediffDef>.GetNamed("VPE_PsycastAbilityImplant"));
            UnlockedPathsField.Value?.SetValue(vePsycastExtension, AccessTools.CreateInstance(ClosedUnlockedPathsListGenericType.Value));
            def.modExtensions.Add(vePsycastExtension);
        }

        if (edit.VEPsycastLevel != null)
            LevelField.Value?.SetValue(vePsycastExtension, edit.VEPsycastLevel);
        if (edit.VEPsycastStatPoints != null)
            StatUpgradePointsField.Value?.SetValue(vePsycastExtension, edit.VEPsycastStatPoints);
        if (edit.VEPsycastRandomAbilities != null)
            GiveRandomAbilitiesField.Value?.SetValue(vePsycastExtension, edit.VEPsycastRandomAbilities);
    }
}
