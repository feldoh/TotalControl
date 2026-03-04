using HarmonyLib;
using RimWorld;

namespace FactionLoadout.Patches;

[HarmonyPatch(typeof(Faction), "TryGenerateNewLeader")]
public static class FactionLeaderPatch
{
    public static bool Active = false;

    [HarmonyPriority(Priority.First)]
    static bool Prefix(ref bool __result)
    {
        if (Active)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
