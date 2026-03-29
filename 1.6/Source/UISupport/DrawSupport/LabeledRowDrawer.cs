using System;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Generic helpers for drawing a left-side label paired with a value, button, or
/// editable float field.  All methods advance the <see cref="Listing_Standard"/>
/// by exactly one 28-pixel row.
/// </summary>
public static class LabeledRowDrawer
{
    public const float DefaultLabelWidth = 160f;

    /// <summary>Draws a read-only label + value row.</summary>
    public static void DrawLabeledText(Listing_Standard ui, string label, string value, float labelW = DefaultLabelWidth)
    {
        Rect row = ui.GetRect(28f);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.grey;
        Widgets.Label(new Rect(row.x, row.y, labelW, row.height), label);
        Widgets.Label(new Rect(row.x + labelW, row.y, row.width - labelW, row.height), value);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    /// <summary>Draws a label + clickable button row.</summary>
    public static void DrawLabeledButton(Listing_Standard ui, string label, string tooltip, string value, Action onClick, float labelW = DefaultLabelWidth)
    {
        Rect row = ui.GetRect(28f);
        Rect labelRect = new(row.x, row.y, labelW, row.height);
        Rect btnRect = new(row.x + labelW, row.y, row.width - labelW - 4f, 24f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(labelRect, tooltip);

        if (Widgets.ButtonText(btnRect, value))
            onClick?.Invoke();
    }

    /// <summary>
    /// Draws a label + float text field row.
    /// The caller owns <paramref name="buf"/>; it is updated when the user types.
    /// Returns the parsed value (clamped to <paramref name="min"/>), or the unchanged
    /// <paramref name="value"/> when the buffer contains an unparseable string.
    /// </summary>
    public static float DrawLabeledFloat(Listing_Standard ui, string label, string tooltip, ref string buf, float value, float min, float labelW = DefaultLabelWidth)
    {
        const float fieldW = 90f;

        Rect row = ui.GetRect(28f);
        Rect labelRect = new(row.x, row.y, labelW, row.height);
        Rect fieldRect = new(row.x + labelW, row.y + 2f, fieldW, 24f);
        Rect tipRect = new(row.x + labelW + fieldW + 4f, row.y, 20f, row.height);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(labelRect, tooltip);

        buf = Widgets.TextField(fieldRect, buf);

        GUI.color = Color.grey;
        Widgets.Label(tipRect, "(?)");
        GUI.color = Color.white;
        TooltipHandler.TipRegion(tipRect, tooltip);

        return float.TryParse(buf, out float parsed) ? Mathf.Max(min, parsed) : value;
    }
}
