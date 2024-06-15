using System;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class UIHelpers
{
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
}
