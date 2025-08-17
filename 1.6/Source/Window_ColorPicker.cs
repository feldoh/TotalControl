using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

[HotSwappable]
public class Window_ColorPicker : Dialog_ColorPickerBase
{
    public Action<Color> selectAction;

    public static Widgets.ColorComponents visibleColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;
    public static Widgets.ColorComponents editableColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;

    public override Vector2 InitialSize => new(600f, 480f);
    public override bool ShowDarklight => false;
    public override Color DefaultColor => color;
    public override List<Color> PickableColors => Dialog_GlowerColorPicker.colors;
    public override float ForcedColorValue => ExtractColorValue(color);
    public override bool ShowColorTemperatureBar => true;

    public static float ExtractColorValue(Color color)
    {
        Color.RGBToHSV(color, out _, out _, out float value);
        return value;
    }

    public Window_ColorPicker(Color currentColor, Action<Color> selectAction)
        : base(visibleColorTextfields, editableColorTextfields)
    {
        doCloseX = true;
        this.selectAction = selectAction;
        color = currentColor;
        oldColor = color;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        closeOnAccept = false;
    }

    public override void SaveColor(Color newColor)
    {
        selectAction(newColor);
    }
}
