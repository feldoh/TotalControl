using RimWorld;
using Verse.Sound;

namespace FactionLoadout.Util;

/// <summary>
/// Data stored on the clipboard after a copy operation.
/// Holds a deep clone of the source PawnKindEdit and display metadata.
/// </summary>
public class PawnKindClipboardData
{
    /// <summary>
    /// Deep clone of the source edit. Never mutated after creation.
    /// We use a deep clone so that users can copy then tweak before moving to a new pawnkind.
    /// This means they don't have to go back and forth between source and target if they want to copy a base set of items.
    /// </summary>
    public PawnKindEdit Clone;

    /// <summary>Display name for tooltips (e.g. "Pirate Gunner" or "Global").</summary>
    public string SourceLabel;
}

/// <summary>
/// Static clipboard for copy-paste operations on PawnKindEdit.
/// Copy records a deep clone of the source. Paste-All calls <see cref="PawnKindEdit.CopyFrom"/>.
/// Per-field paste is done directly in the UI — each DrawOverride row has a paste button
/// that reads its specific field value from <see cref="Clipboard"/>.Clone.
/// </summary>
public static class PawnKindClipboard
{
    public static PawnKindClipboardData Clipboard { get; set; }
    public static bool HasData => Clipboard != null;

    /// <summary>Copy a PawnKindEdit to the clipboard (deep clone).</summary>
    public static void Copy(PawnKindEdit source)
    {
        PawnKindEdit clone = new() { Def = source.Def, IsGlobal = source.IsGlobal };
        clone.CopyFrom(source);

        Clipboard = new PawnKindClipboardData
        {
            Clone = clone,
            SourceLabel = source.IsGlobal ? "Global" : source.Def?.LabelCap.ToString() ?? "Unknown",
        };
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    /// <summary>Paste all fields from the clipboard into <paramref name="target"/>.</summary>
    public static void PasteAll(PawnKindEdit target)
    {
        if (Clipboard == null)
            return;

        target.CopyFrom(Clipboard.Clone);
        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
    }

    /// <summary>Clipboard description for tooltips.</summary>
    public static string GetDescription() =>
        Clipboard == null ? "Clipboard is empty." : $"Source: {Clipboard.SourceLabel}";
}
