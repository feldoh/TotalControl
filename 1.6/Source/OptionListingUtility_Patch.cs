using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace FactionLoadout;

[HarmonyPatch(typeof(OptionListingUtility))]
public static class OptionListingUtility_Patch
{
    [HarmonyPatch(nameof(OptionListingUtility.DrawOptionListing))]
    [HarmonyPrefix]
    public static void DrawOptionListing_Patch(ref List<ListableOption> optList)
    {
        if (optList.Any(opt => opt is ListableOption_WebLink))
        {
            optList.Add(
                new ListableOption_WebLink(
                    "FactionLoadout_SettingName".Translate(),
                    delegate
                    {
                        Find.WindowStack.Add(new Dialog_FactionLoadout());
                    },
                    Textures.TC_Link
                )
            );
        }
    }
}
