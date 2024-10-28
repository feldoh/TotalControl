using HarmonyLib;
using RimWorld;

namespace FactionLoadout;

/**
 * This method checks every faction's relation to our faction. If there are none it spams errors.
 * So to avoid polluting main faction relations with our custom factions, we need to disable this method.
 */
[HarmonyPatch(typeof(IdeoUtility), nameof(IdeoUtility.IdeoChangeToWeight))]
public static class IdeoUtilityPatch
{
    public static bool Active = false;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(ref float __result)
    {
        if (Active)
        {
            __result = 0;
            return false;
        }

        return true;
    }
}
