using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

[HotSwappable]
public class Window_ColorPicker : Dialog_ColorPickerBase
{
    public Action<Color> selectAction;

    public static Widgets.ColorComponents visibleColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;
    public static Widgets.ColorComponents editableColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;

    private Texture2D _brightnessTex;
    private float _lastTexH = -1f;
    private float _lastTexS = -1f;
    private bool _draggingBrightness;

    // Extra height for the brightness slider row
    private const float SliderRowHeight = 50f;

    public override Vector2 InitialSize => new(600f, 480f + SliderRowHeight);
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

    public override void DoWindowContents(Rect inRect)
    {
        // Give the base class exactly the space it expects (original 480px content area).
        Rect baseRect = new(inRect.x, inRect.y, inRect.width, inRect.height - SliderRowHeight);
        base.DoWindowContents(baseRect);

        Rect sliderRow = new(inRect.x, baseRect.yMax, inRect.width, SliderRowHeight);
        DrawBrightnessSlider(sliderRow);
    }

    private void DrawBrightnessSlider(Rect area)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);

        // Label on the left
        const float labelWidth = 80f;
        const float barHeight = 16f;
        float barY = area.y + (area.height - barHeight) * 0.5f;

        Rect labelRect = new(area.x, area.y + (area.height - Text.LineHeight) * 0.5f, labelWidth, Text.LineHeight);
        Widgets.Label(labelRect, "FactionLoadout_ColorPicker_Brightness".Translate());

        // Gradient bar
        Rect barRect = new(area.x + labelWidth + 6f, barY, area.width - labelWidth - 6f, barHeight);
        EnsureBrightnessTexture(h, s);
        GUI.DrawTexture(barRect, _brightnessTex, ScaleMode.StretchToFill);
        Widgets.DrawBox(barRect);

        // Handle
        float handleX = Mathf.Lerp(barRect.x, barRect.xMax, v);
        Rect handleRect = new(handleX - 4f, barRect.y - 4f, 8f, barRect.height + 8f);
        Widgets.DrawBoxSolid(handleRect, Color.white);
        GUI.color = Color.black;
        Widgets.DrawBox(handleRect);
        GUI.color = Color.white;

        // Input
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && barRect.Contains(evt.mousePosition))
        {
            _draggingBrightness = true;
            SetValueFromMouse(barRect, h, s);
            evt.Use();
        }
        if (_draggingBrightness && evt.type == EventType.MouseDrag)
        {
            SetValueFromMouse(barRect, h, s);
            evt.Use();
        }
        if (evt.type == EventType.MouseUp)
        {
            _draggingBrightness = false;
        }
    }

    private void SetValueFromMouse(Rect barRect, float h, float s)
    {
        float newV = Mathf.Clamp01((Event.current.mousePosition.x - barRect.x) / barRect.width);
        color = Color.HSVToRGB(h, s, newV);
    }

    private void EnsureBrightnessTexture(float h, float s)
    {
        if (_brightnessTex != null && Mathf.Approximately(_lastTexH, h) && Mathf.Approximately(_lastTexS, s))
        {
            return;
        }

        _lastTexH = h;
        _lastTexS = s;

        if (_brightnessTex == null)
        {
            _brightnessTex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            _brightnessTex.wrapMode = TextureWrapMode.Clamp;
            _brightnessTex.filterMode = FilterMode.Bilinear;
        }

        Color[] pixels = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            pixels[i] = Color.HSVToRGB(h, s, i / 255f);
        }
        _brightnessTex.SetPixels(pixels);
        _brightnessTex.Apply();
    }

    public override void PostClose()
    {
        base.PostClose();
        if (_brightnessTex != null)
        {
            UnityEngine.Object.Destroy(_brightnessTex);
            _brightnessTex = null;
        }
    }

    public override void SaveColor(Color newColor)
    {
        selectAction(newColor);
    }
}
