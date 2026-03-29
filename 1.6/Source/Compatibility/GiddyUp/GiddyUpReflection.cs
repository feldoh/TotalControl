using System;
using System.Reflection;
using FactionLoadout;
using HarmonyLib;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Cached reflection references for GiddyUp's internal extension types.
/// </summary>
public static class GiddyUpReflection
{
    // CustomMounts (per-pawnkind)
    public static Type CustomMountsType { get; private set; }
    public static FieldInfo MountChanceField { get; private set; }
    public static FieldInfo PossibleMountsField { get; private set; }

    // FactionRestrictions (per-faction)
    public static Type FactionRestrictionsType { get; private set; }
    public static FieldInfo FactionMountChanceField { get; private set; }
    public static FieldInfo WildAnimalWeightField { get; private set; }
    public static FieldInfo NonWildAnimalWeightField { get; private set; }
    public static FieldInfo AllowedWildAnimalsField { get; private set; }
    public static FieldInfo AllowedNonWildAnimalsField { get; private set; }

    public static bool IsResolved => CustomMountsType != null && MountChanceField != null && PossibleMountsField != null;
    public static bool IsFactionResolved => FactionRestrictionsType != null && FactionMountChanceField != null;

    public static void Resolve()
    {
        // Per-pawnkind
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

        // Per-faction
        FactionRestrictionsType = AccessTools.TypeByName("GiddyUp.FactionRestrictions");
        if (FactionRestrictionsType == null)
        {
            ModCore.Warn("GiddyUp module: Could not find GiddyUp.FactionRestrictions type — faction-level mount settings will be unavailable.");
            return;
        }

        FactionMountChanceField = AccessTools.Field(FactionRestrictionsType, "mountChance");
        WildAnimalWeightField = AccessTools.Field(FactionRestrictionsType, "wildAnimalWeight");
        NonWildAnimalWeightField = AccessTools.Field(FactionRestrictionsType, "nonWildAnimalWeight");
        AllowedWildAnimalsField = AccessTools.Field(FactionRestrictionsType, "allowedWildAnimals");
        AllowedNonWildAnimalsField = AccessTools.Field(FactionRestrictionsType, "allowedNonWildAnimals");

        if (FactionMountChanceField == null)
            ModCore.Warn("GiddyUp module: Could not resolve FactionRestrictions fields via reflection.");
    }
}
