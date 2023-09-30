using HarmonyLib;
using Verse;

namespace FactionLoadout
{
    [HarmonyPatch(typeof(ThingIDMaker), "GiveIDTo")]
    public static class ThingIDPatch
    {
        public static bool Active = false;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(Thing t)
        {
            if (Active)
            {
                t.thingIDNumber = 69420;
                return false;
            }

            return true;
        }
    }
}
