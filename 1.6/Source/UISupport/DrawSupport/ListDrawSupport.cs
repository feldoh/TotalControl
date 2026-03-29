using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Static helpers for drawing editable lists of defs, strings, and colors.
/// Each method takes an <paramref name="isGlobal"/> flag (from <c>PawnKindEdit.IsGlobal</c>)
/// to show the appropriate placeholder text when the field is inactive.
/// </summary>
public static class ListDrawSupport
{
    public static CustomFloatMenu DrawDefRefList<T>(
        Rect rect,
        bool active,
        ref Vector2 scroll,
        IList<DefRef<T>> current,
        IList<T> defaults,
        IEnumerable<T> allDefs,
        bool isGlobal,
        Func<T, MenuItemBase> makeItem = null,
        Func<T, string> labelFunc = null
    )
        where T : Def, new()
    {
        string GetLabel(T def)
        {
            if (labelFunc != null)
                return labelFunc(def);
            return (string)def.LabelCap ?? def.defName;
        }

        string MakeDefaultString(IList<T> list)
        {
            if (list == null || list.Count == 0)
                return $"<i>{"FactionLoadout_None".Translate()}</i>";
            string raw = string.Join(", ", list.Select(GetLabel));
            if (raw.Length > 43)
                raw = raw.Substring(0, 40) + "...";
            return raw;
        }

        if (active)
        {
            CustomFloatMenu toReturn = null;
            if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "FactionLoadout_AddNew".Translate()))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(allDefs, makeItem ?? (d => new MenuItemText(d, GetLabel(d))));
                toReturn = CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        T def = raw.GetPayload<T>();
                        if (current.All(r => r.DefName != def.defName))
                            current.Add(new DefRef<T>(def));
                    }
                );
            }

            rect.yMin += 30;

            Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 26 * current.Count));
            Rect curr = new(26, 3, 1000, 30);
            Rect currButton = new(3, 3, 20, 20);

            DefRef<T> toRemove = null;
            foreach (DefRef<T> defRef in current)
            {
                GUI.color = Color.red;
                if (Widgets.ButtonText(currButton, " X"))
                    toRemove = defRef;
                GUI.color = Color.white;

                if (defRef.IsMissing)
                {
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    Widgets.Label(curr, "FactionLoadout_DefRef_Missing".Translate(defRef.DefName, defRef.ModName ?? "FactionLoadout_DefRef_UnknownMod".Translate()));
                    GUI.color = Color.white;
                }
                else if (defRef.HasValue)
                {
                    Widgets.Label(curr, GetLabel(defRef.Def));
                }

                curr.y += 26;
                currButton.y += 26;
            }

            Widgets.EndScrollView();

            if (toRemove != null)
                current.Remove(toRemove);

            return toReturn;
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {MakeDefaultString(defaults)}";
            Widgets.Label(rect.GetCentered(txt), txt);
            return null;
        }
    }

    public static CustomFloatMenu DrawDefList<T>(
        Rect rect,
        bool active,
        ref Vector2 scroll,
        IList<T> current,
        IList<T> defaultThings,
        IEnumerable<T> allThings,
        bool allowDupes,
        bool isGlobal,
        Func<T, MenuItemBase> makeItems = null
    )
        where T : Def
    {
        string MakeString(IList<T> list)
        {
            if (list == null || list.Count == 0)
                return "<i>None</i>";
            string raw = string.Join(", ", list);
            if (raw.Length > 43)
                raw = raw.Substring(0, 40) + "...";
            return raw;
        }

        if (active)
        {
            CustomFloatMenu toReturn = null;
            if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                    allThings,
                    makeItems ?? (d => new MenuItemText(d, d.LabelCap, DefUtils.TryGetIcon(d, out Color c), c, d.description))
                );
                toReturn = CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        T t = raw.GetPayload<T>();
                        if (allowDupes || !current.Contains(t))
                            current.Add(t);
                    }
                );
            }

            rect.yMin += 30;

            Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 26 * current.Count));
            Rect curr = new(26, 3, 1000, 30);
            Rect currButton = new(3, 3, 20, 20);

            T toRemove = null;
            foreach (T thing in current)
            {
                GUI.color = Color.red;
                if (Widgets.ButtonText(currButton, " X"))
                    toRemove = thing;
                GUI.color = Color.white;

                if (thing is BodyTypeDef)
                {
                    GUI.color = Color.white;
                    Widgets.Label(curr, (string)thing.LabelCap ?? thing.defName);
                }
                else if (thing != null && thing is not StyleItemDef)
                {
                    Widgets.DefLabelWithIcon(curr, thing);
                }
                else if (thing is StyleItemDef si)
                {
                    Rect label = curr;
                    label.xMin += 34;
                    Rect icon = curr;
                    icon.width = icon.height;
                    Widgets.DrawTextureFitted(icon, si.Icon, 1f);
                    Widgets.Label(label, si.LabelCap);
                }

                curr.y += 26;
                currButton.y += 26;
            }

            Widgets.EndScrollView();

            if (toRemove != null)
                current.Remove(toRemove);

            return toReturn;
        }

        string defaultTxt = isGlobal ? "---" : $"[Default] {MakeString(defaultThings)}";
        Widgets.Label(rect.GetCentered(defaultTxt), defaultTxt);
        return null;
    }

    public static void DrawColorList(Rect rect, bool active, ref Vector2 scroll, IList<Color> current, IList<Color> defaultColors, bool isGlobal)
    {
        string MakeString(IList<Color> list)
        {
            if (list == null || list.Count == 0)
                return "<i>None</i>";
            string raw = string.Join(", ", list);
            if (raw.Length > 73)
                raw = raw.Substring(0, 70) + "...";
            return raw;
        }

        if (active)
        {
            if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
            {
                Find.WindowStack.Add(
                    new Window_ColorPicker(
                        new Color32(240, 216, 122, 255),
                        selected =>
                        {
                            selected.a = 1f;
                            current.Add(selected);
                        }
                    )
                );
            }

            rect.yMin += 30;

            Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 38 * current.Count));
            Rect curr = new(26, 3, rect.width, 36);
            Rect currButton = new(3, 3, 20, 20);

            for (int i = 0; i < current.Count; i++)
            {
                Color color = current[i];
                int currentPosition = i;

                GUI.color = Color.red;
                if (Widgets.ButtonText(currButton, " X"))
                {
                    current.RemoveAt(i);
                    i--;
                    continue;
                }

                GUI.color = Color.white;

                Rect real = curr.ExpandedBy(-4, -2);
                Widgets.DrawBoxSolid(real, color);
                Widgets.DrawHighlightIfMouseover(real);
                if (Widgets.ButtonInvisible(real))
                {
                    Find.WindowStack.Add(
                        new Window_ColorPicker(
                            color,
                            selected =>
                            {
                                selected.a = 1f;
                                current[currentPosition] = selected;
                            }
                        )
                    );
                }

                curr.y += 38;
                currButton.y += 38;
            }

            Widgets.EndScrollView();
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {MakeString(defaultColors)}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    public static void DrawStringList(Rect rect, bool active, ref Vector2 scroll, IList<string> current, IList<string> defaultTags, IEnumerable<string> allTags, bool isGlobal)
    {
        string MakeString(IList<string> list)
        {
            if (list == null || list.Count == 0)
                return "<i>None</i>";
            string raw = string.Join(", ", list);
            if (raw.Length > 73)
                raw = raw.Substring(0, 70) + "...";
            return raw;
        }

        if (active)
        {
            if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(allTags, t => new MenuItemText(t, t));
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        string t = raw.GetPayload<string>();
                        if (!current.Contains(t))
                            current.Add(t);
                    }
                );
            }

            rect.yMin += 30;

            Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 26 * current.Count));
            Rect curr = new(26, 3, 1000, 30);
            Rect currButton = new(3, 3, 20, 20);

            string toRemove = null;
            foreach (string tag in current)
            {
                GUI.color = Color.red;
                if (Widgets.ButtonText(currButton, " X"))
                    toRemove = tag;
                GUI.color = Color.white;
                Widgets.Label(curr, tag);

                curr.y += 26;
                currButton.y += 26;
            }

            Widgets.EndScrollView();

            if (toRemove != null)
                current.Remove(toRemove);
        }
        else
        {
            string txt = isGlobal ? "---" : $"[Default] {MakeString(defaultTags)}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }
}
