using System;
using System.Collections.Generic;
using FactionLoadout;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

public class UIHelpers
{
    /// <summary>Standard row height used by the override-style field helpers.</summary>
    public const float OverrideRowH = 28f;

    public static float SliderLabeledWithDelete(
        Listing_Standard ls,
        string label,
        float val,
        float min,
        float max,
        float labelPct = 0.5f,
        string tooltip = null,
        Action deleteAction = null
    )
    {
        Rect rect = ls.GetRect(30f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect.LeftPart(labelPct), label);
        if (tooltip != null)
            TooltipHandler.TipRegion(rect.LeftPart(labelPct), tooltip);

        Text.Anchor = TextAnchor.UpperLeft;
        Rect sliderRect = rect.RightPart(1f - labelPct);
        if (deleteAction != null)
            sliderRect.width -= 32;

        float result = Widgets.HorizontalSlider(sliderRect, val, min, max, true);
        if (deleteAction != null)
        {
            Rect deleteButton = new Rect(sliderRect.xMax + 5, sliderRect.y, 24, 24);
            if (Widgets.ButtonImage(deleteButton, TexButton.Delete))
                deleteAction();
        }

        ls.Gap(ls.verticalSpacing);
        return result;
    }

    /// <summary>
    /// Draws a nullable <see cref="FloatRange"/> override row.
    /// Shows min/max text fields when overridden, or a hint + Override button when not.
    /// </summary>
    public static void DrawFloatRangeRow(Listing_Standard ui, string label, ref FloatRange? field, float minLimit, float maxLimit, FloatRange defaultSeed)
    {
        bool hasOverride = field.HasValue;
        Rect row = ui.GetRect(OverrideRowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            FloatRange current = field.Value;
            float min = current.min;
            float max = current.max;
            string minBuf = min.ToString("F0");
            string maxBuf = max.ToString("F0");

            Rect minRect = fieldRect.LeftPart(0.28f);
            Rect dashRect = fieldRect.LeftPart(0.5f).RightPart(0.12f);
            Rect maxRect = fieldRect.LeftPart(0.68f).RightPart(0.28f);
            Rect clearRect = fieldRect.RightPart(0.28f);

            Widgets.TextFieldNumeric(minRect, ref min, ref minBuf, minLimit, maxLimit);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(dashRect, "–");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(maxRect, ref max, ref maxBuf, minLimit, maxLimit);
            field = new FloatRange(min, Mathf.Max(min, max));

            if (Widgets.ButtonText(clearRect, "FactionLoadout_Clear".Translate()))
            {
                field = null;
            }
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), "(–)");
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                field = defaultSeed;
            }
        }
    }

    /// <summary>
    /// Draws a nullable float slider override row.
    /// Shows a horizontal slider when overridden, or a hint + Override button when not.
    /// </summary>
    public static void DrawFloatSliderRow(Listing_Standard ui, string label, ref float? field, float minLimit, float maxLimit, float defaultSeed, bool asPercent = false)
    {
        bool hasOverride = field.HasValue;
        Rect row = ui.GetRect(OverrideRowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            float val = field.Value;
            Rect sliderRect = fieldRect.LeftPart(0.7f);
            Rect clearRect = fieldRect.RightPart(0.27f);
            string sliderLabel = asPercent ? $"{val * 100f:F0}%" : $"{val:F2}";
            val = Widgets.HorizontalSlider(sliderRect, val, minLimit, maxLimit, true, sliderLabel);
            field = val;

            if (Widgets.ButtonText(clearRect, "FactionLoadout_Clear".Translate()))
            {
                field = null;
            }
        }
        else
        {
            string hint = asPercent ? $"({defaultSeed * 100f:F0}%)" : $"({defaultSeed:F2})";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                field = defaultSeed;
            }
        }
    }

    /// <summary>
    /// Draws a list of string tags with per-item remove buttons and an "Add tag…" button
    /// that opens a <see cref="Dialog_TextEntry"/> for input.
    /// The <paramref name="list"/> must be non-null; initialize with <c>??= []</c> before calling.
    /// </summary>
    public static void DrawStringListSection(Listing_Standard ui, List<string> list, bool indent = false)
    {
        string prefix = indent ? "    " : "  ";
        int toRemove = -1;

        for (int i = 0; i < list.Count; i++)
        {
            Rect row = ui.GetRect(OverrideRowH);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.LeftPart(0.75f), prefix + list[i]);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(row.RightPart(0.22f), "FactionLoadout_Clear".Translate()))
            {
                toRemove = i;
            }
        }

        if (toRemove >= 0)
        {
            list.RemoveAt(toRemove);
        }

        // Capture reference so the async dialog callback can add to it
        List<string> captured = list;
        Rect addRow = ui.GetRect(OverrideRowH);
        if (Widgets.ButtonText(addRow.LeftPart(0.45f), "FactionLoadout_AddTag".Translate()))
        {
            Find.WindowStack.Add(
                new Dialog_TextEntry(
                    "FactionLoadout_AddTagDesc".Translate(),
                    newTag =>
                    {
                        if (!string.IsNullOrWhiteSpace(newTag))
                        {
                            captured.Add(newTag.Trim());
                        }
                    }
                )
            );
        }
    }
}
