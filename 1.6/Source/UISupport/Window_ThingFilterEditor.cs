using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>
/// Simple dialog that wraps RimWorld's <see cref="ThingFilterUI"/> so users can
/// configure a <see cref="ThingFilter"/> interactively (e.g. for material overrides).
/// </summary>
public class Window_ThingFilterEditor : Window
{
    private readonly ThingFilter filter;
    private readonly ThingFilterUI.UIState filterState = new();

    public override Vector2 InitialSize => new(400f, 600f);

    public Window_ThingFilterEditor(ThingFilter filter)
    {
        this.filter = filter;
        doCloseButton = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Rect filterRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - CloseButSize.y - 4f);
        ThingFilterUI.DoThingFilterConfigWindow(filterRect, filterState, filter);
    }
}
