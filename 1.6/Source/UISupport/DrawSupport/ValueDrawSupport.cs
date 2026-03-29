using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Static helpers for simple scalar overrides: enum selectors, def selectors,
/// chance sliders, and int/float range pickers.
/// </summary>
public static class ValueDrawSupport
{
    public static void DrawEnumSelector<T>(Rect rect, bool active, bool isGlobal, T? field, T defaultValue, Action<T> apply, Func<T, string> makeName = null)
        where T : struct
    {
        string Name(T? t)
        {
            if (t is { } safeT)
                return makeName == null ? t.ToString() : makeName(safeT);
            return "UNKNOWN";
        }

        IEnumerable<object> MakeEnumerable(IEnumerable normal)
        {
            foreach (object item in normal)
                yield return item;
        }

        if (!Widgets.ButtonText(rect, active ? Name(field) : isGlobal ? "---" : $"[Default] {Name(defaultValue)}"))
            return;

        IEnumerable<object> values = MakeEnumerable(Enum.GetValues(typeof(T)));
        FloatMenuUtility.MakeMenu(values, e => Name((T)e), e => () => apply((T)e));
    }

    public static void DrawDefSelector<T>(Rect rect, bool active, bool isGlobal, IEnumerable<T> defs, T field, T defaultValue, Action<T> apply, Func<T, string> makeName = null)
        where T : Def
    {
        string Name(T t) => makeName == null ? t.LabelCap : makeName(t);

        if (!Widgets.ButtonText(rect, active ? Name(field) : isGlobal ? "---" : $"[Default] {Name(defaultValue)}"))
            return;

        List<MenuItemBase> items = CustomFloatMenu.MakeItems(defs, d => new MenuItemText(d, Name(d), DefUtils.TryGetIcon(d, out Color c), c, d.description));
        CustomFloatMenu.Open(items, raw => apply(raw.GetPayload<T>()));
    }

    public static void DrawChance(Rect rect, bool active, bool isGlobal, ref float? field, float defaultValue)
    {
        if (active)
        {
            float fieldVal = field!.Value;
            Widgets.HorizontalSlider(rect, ref fieldVal, FloatRange.ZeroToOne, $"Chance: {100f * field:F0}% (default: {100f * defaultValue:F0}%)");
            field = fieldVal;
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {100f * defaultValue:F0}%";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    public static void DrawIntRange(Rect rect, bool active, bool isGlobal, ref IntRange? current, IntRange defaultRange, ref string buffer, ref string buffer2)
    {
        if (active)
        {
            int value = current?.min ?? 0;
            Rect left = rect;
            left.width = 220;
            Widgets.IntEntry(left, ref value, ref buffer);
            current = new IntRange(value, current?.max ?? value + 1);

            value = current.Value.max;
            Rect right = new(rect.xMax - 220, rect.y, 220, rect.height);
            Widgets.IntEntry(right, ref value, ref buffer2);
            current = new IntRange(current.Value.min, value);

            string txt = $"{current.Value.TrueMin:F0} to {current.Value.TrueMax:F0}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {defaultRange}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    public static void DrawFloatRange(Rect rect, bool active, bool isGlobal, ref FloatRange? current, FloatRange defaultRange, ref string buffer, ref string buffer2)
    {
        if (active)
        {
            current ??= defaultRange;
            int value = (int)current.Value.min;
            Rect left = rect;
            left.width = 220;
            Widgets.IntEntry(left, ref value, ref buffer);
            current = new FloatRange(value, current.Value.max);

            value = (int)current.Value.max;
            Rect right = new(rect.xMax - 220, rect.y, 220, rect.height);
            Widgets.IntEntry(right, ref value, ref buffer2);
            current = new FloatRange(current.Value.min, value);

            string txt = $"{current.Value.TrueMin:F0} to {current.Value.TrueMax:F0}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {defaultRange}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    public static float GetHeightFor(IList list, float itemHeight = 26)
    {
        if (list == null)
            return 32;
        return Math.Min(36 + itemHeight * 1 + (list.Count - 1) * itemHeight, 120);
    }
}
