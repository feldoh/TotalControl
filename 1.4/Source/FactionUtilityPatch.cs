using HarmonyLib;
using RimWorld;

namespace FactionLoadout;

[HarmonyPatch(typeof(FactionUtility), "HostileTo")]
public static class FactionUtilityPawnGenPatch
{
    public static bool Active = false;

    [HarmonyPriority(Priority.First)]
    static bool Prefix(ref bool __result)
    {
        if (!Active) return true;
        __result = false;
        return false;

    }
}
