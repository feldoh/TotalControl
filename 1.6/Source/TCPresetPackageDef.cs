using Verse;

namespace FactionLoadout
{
    // Other mods place an instance of this Def in their Defs/ folder to register
    // a bundled TC preset file with Total Control.
    //
    // Example in another mod's Defs/TC_Presets.xml:
    //   <FactionLoadout.TCPresetPackageDef>
    //     <defName>MyMod_TCPreset</defName>
    //     <presetPath>TotalControl/my_preset.xml</presetPath>
    //   </FactionLoadout.TCPresetPackageDef>
    //
    // presetPath is relative to the hosting mod's root directory.
    // The preset XML file must be a valid TC preset (same format TC exports).
    // The hosting mod should declare Total Control in its loadAfter list.
    public class TCPresetPackageDef : Def
    {
        // Relative path to the preset XML file from the hosting mod's root.
        // e.g. "TotalControl/MyMod_Presets.xml"
        public string presetPath;
    }
}
