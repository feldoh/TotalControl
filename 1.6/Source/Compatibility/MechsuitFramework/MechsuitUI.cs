using Exosuit;
using FactionLoadout;
using UnityEngine;
using Verse;

namespace TotalControlMechsuitCompat;

/// <summary>
/// UI drawing for the Exosuit Framework tab.
/// </summary>
public static class MechsuitUI
{
    /// <summary>
    /// Read the existing StructurePointRange from a PawnKindDef's ModExtForceApparelGen (if any).
    /// </summary>
    public static FloatRange? ReadDefStructurePointRange(PawnKindDef def)
    {
        ModExtForceApparelGen ext = def?.GetModExtension<ModExtForceApparelGen>();
        return ext?.StructurePointRange;
    }

    public static void DrawExosuitTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        MechsuitData data = MechsuitModule.GetOrCreateData(edit);

        // Read def defaults
        FloatRange? defSPRange = ReadDefStructurePointRange(defaultKind);

        // --- Header ---
        ui.Label("<b>Exosuit Framework</b>");
        ui.GapLine();
        ui.Label("Configure structure point (health) settings for exosuit pawns. " + "Exosuit core apparel is assigned via the Apparel tab as normal.");
        ui.Gap(8);

        // --- Structure Point Range ---
        ui.Label("<b>Structure Point Range</b>");
        ui.Gap(2);
        ui.Label(
            "Controls the health multiplier applied to the exosuit core on spawn. " + "For example, (0.8, 1.0) means spawned suits will have 80\u2013100% of their max health."
        );
        ui.Gap(4);

        bool hasOverride = data.StructurePointRange != null;
        if (hasOverride)
        {
            FloatRange range = data.StructurePointRange.Value;

            Rect row = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(row.LeftPart(0.6f), $"Range: {range.min:P0} \u2013 {range.max:P0}");
            if (Widgets.ButtonText(row.RightPart(0.25f), "Default"))
            {
                data.StructurePointRange = null;
                return;
            }

            ui.Gap(4);

            // Min slider
            Rect minRow = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(minRow.LeftPart(0.2f), "Min:");
            float newMin = Widgets.HorizontalSlider(minRow.RightPart(0.75f), range.min, 0f, 1f, true, $"{range.min:P0}");

            // Max slider
            Rect maxRow = ui.GetRect(Text.LineHeight + 4);
            Widgets.Label(maxRow.LeftPart(0.2f), "Max:");
            float newMax = Widgets.HorizontalSlider(maxRow.RightPart(0.75f), range.max, 0f, 1f, true, $"{range.max:P0}");

            // Clamp min <= max
            if (newMin > newMax)
                newMin = newMax;

            data.StructurePointRange = new FloatRange(newMin, newMax);
        }
        else
        {
            Rect row = ui.GetRect(Text.LineHeight + 4);
            string defaultLabel =
                defSPRange != null ? $"Structure Points: (default: {defSPRange.Value.min:P0} \u2013 {defSPRange.Value.max:P0})" : "Structure Points: (default: 100%)";
            Widgets.Label(row.LeftPart(0.65f), defaultLabel);

            if (Widgets.ButtonText(row.RightPart(0.25f), "Override"))
                data.StructurePointRange = defSPRange ?? new FloatRange(0.9f, 1f);
        }
    }
}
