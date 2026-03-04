using System;
using System.Reflection;
using FactionLoadout;
using HarmonyLib;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Cached reflection references for GiddyUp's internal CustomMounts extension type.
/// </summary>
public static class GiddyUpReflection
{
    public static Type CustomMountsType { get; private set; }
    public static FieldInfo MountChanceField { get; private set; }
    public static FieldInfo PossibleMountsField { get; private set; }

    public static bool IsResolved => CustomMountsType != null && MountChanceField != null && PossibleMountsField != null;

    public static void Resolve()
    {
        CustomMountsType = AccessTools.TypeByName("GiddyUp.CustomMounts");
        if (CustomMountsType == null)
        {
            ModCore.Warn("GiddyUp module: Could not find GiddyUp.CustomMounts type.");
            return;
        }

        MountChanceField = AccessTools.Field(CustomMountsType, "mountChance");
        PossibleMountsField = AccessTools.Field(CustomMountsType, "possibleMounts");

        if (MountChanceField == null || PossibleMountsField == null)
            ModCore.Warn("GiddyUp module: Could not resolve CustomMounts fields via reflection.");
    }
}
