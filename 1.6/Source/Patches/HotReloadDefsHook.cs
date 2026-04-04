using HarmonyLib;
using Verse;

namespace FactionLoadout.Patches;

[HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.HotReloadDefs))]
public static class HotReloadDefsHook
{
    public static void Postfix()
    {
        // HotReloadDefs queues a long event; queue our reapply immediately after
        // so it runs once that event completes.
        LongEventHandler.QueueLongEvent(ModCore.ReapplyAfterHotReload, "FactionLoadout_ReapplyingLoadingText", false, null);
    }
}
