using System.Collections.Generic;
using FactionLoadout.UISupport;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout.Patches;

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
                        // The in-game ESC menu (MainTabWindow_Menu) sits at WindowLayer.Super,
                        // above TC's Dialog layer - leaving it open would draw it over TC. Close
                        // it so the fullscreen editor isn't obscured.
                        Find.WindowStack.WindowOfType<MainTabWindow_Menu>()?.Close();
                        Find.WindowStack.Add(new Dialog_TotalControl());
                    },
                    Textures.TC_Link
                )
            );
        }
    }
}
