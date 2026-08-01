using Verse;

namespace FactionLoadout;

public class MySettings : ModSettings
{
    public static string ActivePreset = null;
    public static bool VanillaRestrictions = true;
    public static bool VerboseLogging = false;
    public static bool PatchKindInRequests = false;
    public static bool IgnorePriceLimits = false;
    public static bool OverrideForcedIdeos = false;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref ActivePreset, "activePreset", null);
        Scribe_Values.Look(ref VanillaRestrictions, "vanillaRestrictions", true);
        Scribe_Values.Look(ref VerboseLogging, "verboseLogging", false);
        Scribe_Values.Look(ref PatchKindInRequests, "patchKindInRequests", false);
        Scribe_Values.Look(ref IgnorePriceLimits, "ignorePriceLimits", false);
        Scribe_Values.Look(ref OverrideForcedIdeos, "overrideForcedIdeos", false);
    }
}
