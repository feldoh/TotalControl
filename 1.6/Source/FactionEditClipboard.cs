using RimWorld;
using Verse;
using Verse.Sound;

namespace FactionLoadout;

/// <summary>
/// Static clipboard for copy-paste of top-level <see cref="FactionEdit"/> fields
/// (tech level, xenotype overrides). KindEdits are not copied.
/// </summary>
public static class FactionEditClipboard
{
    public static FactionEdit Clipboard { get; set; }
    public static bool HasData => Clipboard != null;

    public static void Copy(FactionEdit source)
    {
        FactionEdit clone = new();
        clone.CopyFrom(source);
        Clipboard = clone;
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    public static void PasteAll(FactionEdit target)
    {
        if (Clipboard == null)
            return;
        target.CopyFrom(Clipboard);
        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
    }

    public static string GetDescription() =>
        Clipboard == null
            ? "FactionLoadout_Clipboard_Empty".Translate()
            : "FactionLoadout_FactionClipboard_Description".Translate(
                (TaggedString)(Clipboard.TechLevel?.ToStringHuman() ?? "FactionLoadout_NotOverriden_WithDefault".Translate("-")),
                Clipboard.OverrideFactionXenotypes.ToString()
            );
}
