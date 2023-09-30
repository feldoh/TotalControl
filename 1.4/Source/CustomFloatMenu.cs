using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionLoadout
{

    public class CustomFloatMenu : Window
    {
        public static CustomFloatMenu Open(List<MenuItemBase> items, Action<MenuItemBase> onSelected)
        {
            var created = new CustomFloatMenu();
            created.Items = items;
            created.OnSelected = onSelected;
            created.closeOnAccept = false;
            created.closeOnCancel = true;
            created.closeOnClickedOutside = true;
            created.layer = WindowLayer.SubSuper;
            Find.WindowStack.Add(created);
            return created;
        }

        public static string SearchMatch(string label, string search, bool highlight)
        {
            int index = label.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            if (!highlight)
                return label;

            return label.Insert(index+search.Length, "</color>").Insert(index, "<color=#57ff57>");
        }

        public static List<MenuItemBase> MakeItems<T>(IEnumerable<T> rawItems, Func<T, MenuItemBase> makeItem)
        {
            var list = new List<MenuItemBase>();
            foreach (var item in rawItems)
            {
                var result = makeItem(item);
                if (result != null)
                    list.Add(result);
            }
            list.Sort();
            return list;
        }

        public List<MenuItemBase> Items;
        public Action<MenuItemBase> OnSelected;
        public bool CloseOnSelected = true;
        public int Columns = 2;
        public string SearchString= "";
        public Color Tint = Color.white;
        public bool AllowChangeTint;

        private readonly List<MenuItemBase> preRenderItems = new List<MenuItemBase>();
        private float lastHeight, lastWidth;
        private Vector2 scroll;

        public override void DoWindowContents(Rect inRect)
        {
            if (Items == null || Items.Count == 0)
            {
                ModCore.Error("CustomFloatMenu tried to draw with no items! Window has been closed.");
                Close();
                return;
            }

            var searchBar = inRect;
            searchBar.height = 28;
            if (AllowChangeTint)
                searchBar.width -= 100;
            SearchString = Widgets.TextField(searchBar, SearchString);
            inRect.yMin += 36;

            Rect colArea = searchBar;
            colArea.xMin = colArea.xMax + 5;
            colArea.width = 90;
            Widgets.DrawBoxSolidWithOutline(colArea, Tint, Color.white, 2);
            Widgets.DrawHighlightIfMouseover(colArea);
            if (Widgets.ButtonInvisible(colArea))
            {
                Find.WindowStack.Add(new Window_ColorPicker(Tint, t => Tint = t)
                {
                    layer = WindowLayer.Super
                });
            }

            // Goal: evenly split items into columns.
            // number of items in each column should be equal.
            preRenderItems.Clear();
            preRenderItems.AddRange(FilteredItems(SearchString));
            int perColumnTarget = Mathf.CeilToInt((float)preRenderItems.Count / Columns);

            float padding = 6;
            float x = 0;

            Widgets.BeginScrollView(inRect, ref scroll, new Rect(0, 0, lastWidth, lastHeight));
            lastWidth = 0;
            lastHeight = 0;

            for (int i = 0; i < Columns; i++)
            {
                float maxItemWidth = 0f;
                float y = 0;
                for (int j = 0; j < perColumnTarget; j++)
                {
                    int index = perColumnTarget * i + j;
                    if (index >= preRenderItems.Count)
                        break;

                    var pos = new Vector2(x, y);
                    var item = preRenderItems[index];
                    if (Tint != Color.white)
                        GUI.color = Tint;
                    var size = item.Draw(pos);
                    GUI.color = Color.white;
                    var area = new Rect(pos, size);
                    Widgets.DrawBox(area);
                    if (Widgets.ButtonInvisible(area))
                    {
                        OnSelected?.Invoke(item);
                        if (CloseOnSelected)
                        {
                            Close();
                            break;
                        }
                    }
                    y += size.y + padding;
                    if (maxItemWidth < size.x)
                        maxItemWidth = size.x;
                    if (y > lastHeight)
                        lastHeight = y;
                }
                x += maxItemWidth + padding * 2;
                if (x > lastWidth)
                    lastWidth = x;
            }

            Widgets.EndScrollView();
        }

        public IEnumerable<MenuItemBase> FilteredItems(string search)
        {
            if (Items == null)
                yield break;

            bool all = string.IsNullOrWhiteSpace(search);
            string newSearch = search?.Trim();

            foreach (var item in Items)
            {
                if (all || item.Matches(newSearch))
                    yield return item;
            }
        }
    }

    public abstract class MenuItemBase : IComparable<MenuItemBase>
    {
        public object Payload { get; set; }

        public T GetPayload<T>() => (T)Payload;

        public abstract bool Matches(string search);
        public abstract int CompareTo(MenuItemBase other);
        public abstract Vector2 Draw(Vector2 pos);
    }

    public class MenuItemText : MenuItemBase
    {
        public string Label;
        public string Tooltip;
        public Texture2D Icon;
        public Color IconColor;
        public Vector2 Size = new Vector2(212, 28);

        private string drawLabel;
        private bool consumedSearch = false;

        public MenuItemText() { }

        public MenuItemText(object payload, string text, Texture2D icon = null, Color iconColor = default, string tooltip = null)
        {
            this.Payload = payload;
            this.Label = text;
            this.Icon = icon;
            this.Tooltip = tooltip;
            this.IconColor = iconColor == default ? Color.white : iconColor;
        }

        public override bool Matches(string search)
        {
            drawLabel = CustomFloatMenu.SearchMatch(Label, search, true);
            consumedSearch = false;
            return drawLabel != null;
        }

        public override int CompareTo(MenuItemBase other)
        {
            if (other is MenuItemText txt)
                return string.Compare(Label, txt.Label, StringComparison.Ordinal);
            return 0;
        }

        public override Vector2 Draw(Vector2 pos)
        {
            Rect area = new Rect(pos, Size);

            string label = Label;

            if (!consumedSearch)
            {
                label = drawLabel;
                consumedSearch = true;
            }

            bool hasIcon = Icon != null;

            if (hasIcon)
            {
                Rect iconArea = area;
                iconArea.width = iconArea.height;
                GUI.color = IconColor;
                Widgets.DrawTextureFitted(iconArea, Icon, 1f);
                GUI.color = Color.white;
            }

            Rect labelArea = area;
            labelArea.y += hasIcon ? 3 : 5;
            if (hasIcon)
                labelArea.xMin += area.height + 2;
            else
                labelArea.xMin += 4;
            
            Widgets.LabelFit(labelArea, label);

            if (Tooltip != null)
                TooltipHandler.TipRegion(area, Tooltip);

            return Size;
        }
    }

    public class MenuItemIcon : MenuItemBase
    {
        public Vector2 Size = new Vector2(64, 64);
        public string Label;
        public Texture2D Icon;
        public Color Color = Color.white;
        public Color BGColor = default;

        public MenuItemIcon() { }

        public MenuItemIcon(object payload, string label, Texture2D icon, Color iconColor = default)
        {
            this.Payload = payload;
            this.Label = label;
            this.Icon = icon;
            this.Color = iconColor == default ? Color.white : iconColor;
        }

        public override bool Matches(string search)
        {
            return Label == null || CustomFloatMenu.SearchMatch(Label, search, false) != null;
        }

        public override int CompareTo(MenuItemBase other)
        {
            return 0; // No order, sort by natural load order (mod).
        }

        public override Vector2 Draw(Vector2 pos)
        {
            if (Icon == null)
                return Size;

            Rect area = new Rect(pos, Size);

            if (BGColor != default)
            {
                Widgets.DrawBoxSolid(area, BGColor);
            }

            var old = GUI.color;
            if(Color != Color.white)
                GUI.color = Color;
            Widgets.DrawTextureFitted(area, Icon, 1f);
            GUI.color = old;

            GUI.color = Color.white;
            TooltipHandler.TipRegion(area, Label);
            GUI.color = old;

            return Size;
        }
    }
}
