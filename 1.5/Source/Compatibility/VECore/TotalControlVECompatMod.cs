using HarmonyLib;
using Verse;

namespace TotalControlVEPsycastsCompat;

public class TotalControlVECompatMod : Mod
{
    public TotalControlVECompatMod(ModContentPack content)
        : base(content)
    {
        Harmony harmony = new Harmony("co.uk.feldoh.factionloadout.vepsycastscompat");
        harmony.PatchAll();
    }
}
