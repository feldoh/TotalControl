using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class BackstoryTab : EditTab
{
    public BackstoryTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Backstory_Section".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawOverride(
            ui,
            DefaultKind.backstoryCryptosleepCommonality,
            ref Current.BackstoryCryptosleepCommonality,
            "FactionLoadout_Backstory_CryptosleepChance".Translate(),
            DrawCryptosleepCommonality,
            pasteGet: e => e.BackstoryCryptosleepCommonality
        );
        DrawBackstoryFiltersOverride(ui);
        DrawOverride(
            ui,
            null,
            ref Current.FixedChildBackstories,
            "FactionLoadout_Backstory_FixedChildhood".Translate(),
            (r, a, d) => DrawFixedBackstories(r, a, d, child: true),
            GetHeightFor(Current.FixedChildBackstories),
            false,
            pasteGet: e => e.FixedChildBackstories
        );
        DrawOverride(
            ui,
            null,
            ref Current.FixedAdultBackstories,
            "FactionLoadout_Backstory_FixedAdulthood".Translate(),
            (r, a, d) => DrawFixedBackstories(r, a, d, child: false),
            GetHeightFor(Current.FixedAdultBackstories),
            false,
            pasteGet: e => e.FixedAdultBackstories
        );
        DrawOverride(
            ui,
            null,
            ref Current.ExcludedBackstoryCategories,
            "FactionLoadout_Backstory_ExcludedCategories".Translate(),
            DrawExcludedBackstoryCategories,
            GetHeightFor(Current.ExcludedBackstoryCategories),
            false,
            pasteGet: e => e.ExcludedBackstoryCategories
        );
        DrawOverride(
            ui,
            null,
            ref Current.ExcludedBackstories,
            "FactionLoadout_Backstory_Excluded".Translate(),
            DrawExcludedBackstories,
            GetHeightFor(Current.ExcludedBackstories),
            false,
            pasteGet: e => e.ExcludedBackstories
        );
    }

    // ==================== Draw methods ====================

    private void DrawCryptosleepCommonality(Rect rect, bool active, float defaultValue)
    {
        if (active)
        {
            float value = Current.BackstoryCryptosleepCommonality ?? defaultValue;
            value = Widgets.HorizontalSlider(rect, value, 0f, 1f, middleAlignment: true, $"{value:P0}");
            Current.BackstoryCryptosleepCommonality = value;
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] {defaultValue:P0}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawBackstoryFiltersOverride(Listing_Standard ui)
    {
        List<BackstoryFilter> filters = Current.BackstoryFiltersOverride;
        float height = filters == null ? 32 : 80 * filters.Count + 33;

        ui.Label($"<b>{"FactionLoadout_Backstory_FiltersOverride".Translate()}</b>");
        TooltipHandler.TipRegion(ui.GetRect(0), "FactionLoadout_Backstory_FiltersOverrideTooltip".Translate());
        Rect rect = ui.GetRect(height);
        bool active = filters != null;
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
        {
            if (active)
            {
                Current.BackstoryFiltersOverride = null;
            }
            else
            {
                Current.BackstoryFiltersOverride = [];
                if (!DefaultKind.backstoryFiltersOverride.NullOrEmpty())
                {
                    foreach (BackstoryCategoryFilter f in DefaultKind.backstoryFiltersOverride)
                        Current.BackstoryFiltersOverride.Add(new BackstoryFilter(f));
                }
                else if (!DefaultKind.backstoryFilters.NullOrEmpty())
                {
                    foreach (BackstoryCategoryFilter f in DefaultKind.backstoryFilters)
                        Current.BackstoryFiltersOverride.Add(new BackstoryFilter(f));
                }
            }

            active = !active;
        }

        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);

        ref Vector2 scroll = ref scrolls[scrollIndex++];
        if (active)
        {
            filters = Current.BackstoryFiltersOverride;
            DrawBackstoryFilterList(content, ref scroll, filters);
            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, $"<b>{"FactionLoadout_Backstory_AddFilter".Translate()}</b>"))
            {
                string defaultCat = DefCache.AllBackstoryCategories.FirstOrDefault() ?? "Civil";
                filters.Add(new BackstoryFilter { categories = [defaultCat], commonality = 1f });
            }
        }
        else
        {
            string txt;
            if (Current.IsGlobal)
            {
                txt = "---";
            }
            else
            {
                List<BackstoryCategoryFilter> defFilters = DefaultKind.backstoryFiltersOverride ?? DefaultKind.backstoryFilters;
                if (defFilters.NullOrEmpty())
                {
                    txt = $"[Default] <i>{"FactionLoadout_None".Translate()}</i>";
                }
                else
                {
                    txt = "FactionLoadout_Backstory_FilterCount".Translate(defFilters.Count);
                }
            }

            GUI.enabled = false;
            Widgets.Label(content.GetCentered(txt), txt);
            GUI.enabled = true;
        }

        ui.Gap();
    }

    private void DrawBackstoryFilterList(Rect rect, ref Vector2 scroll, List<BackstoryFilter> filters)
    {
        string noneLabel = $"<i>{"FactionLoadout_None".Translate()}</i>";
        float itemHeight = 76;
        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 20, itemHeight * filters.Count));

        BackstoryFilter toRemove = null;
        float y = 0;
        foreach (BackstoryFilter filter in filters)
        {
            Rect itemRect = new(0, y, rect.width - 20, itemHeight - 4);
            Widgets.DrawBoxSolidWithOutline(itemRect, Color.black * 0.3f, Color.white * 0.2f);
            itemRect = itemRect.ContractedBy(4);

            // Delete button
            Rect deleteBtn = new(itemRect.xMax - 22, itemRect.y, 20, 20);
            GUI.color = Color.red;
            if (Widgets.ButtonText(deleteBtn, "X"))
                toRemove = filter;
            GUI.color = Color.white;

            // Categories row
            Rect catLabel = new(itemRect.x, itemRect.y, 80, 24);
            Widgets.Label(catLabel, "FactionLoadout_Backstory_Categories".Translate());
            Rect catValue = new(itemRect.x + 82, itemRect.y, itemRect.width - 110, 24);
            string catStr = filter.categories.NullOrEmpty() ? noneLabel : string.Join(", ", filter.categories);
            if (Widgets.ButtonText(catValue, catStr, drawBackground: false))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(DefCache.AllBackstoryCategories, t => new MenuItemText(t, t));
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        string cat = raw.GetPayload<string>();
                        filter.categories ??= [];
                        if (!filter.categories.Contains(cat))
                            filter.categories.Add(cat);
                    }
                );
            }

            // Exclude row
            Rect exLabel = new(itemRect.x, itemRect.y + 24, 80, 24);
            Widgets.Label(exLabel, "FactionLoadout_Backstory_Exclude".Translate());
            Rect exValue = new(itemRect.x + 82, itemRect.y + 24, itemRect.width - 110, 24);
            string exStr = filter.exclude.NullOrEmpty() ? noneLabel : string.Join(", ", filter.exclude);
            if (Widgets.ButtonText(exValue, exStr, drawBackground: false))
            {
                List<MenuItemBase> items = CustomFloatMenu.MakeItems(DefCache.AllBackstoryCategories, t => new MenuItemText(t, t));
                CustomFloatMenu.Open(
                    items,
                    raw =>
                    {
                        string cat = raw.GetPayload<string>();
                        filter.exclude ??= [];
                        if (!filter.exclude.Contains(cat))
                            filter.exclude.Add(cat);
                    }
                );
            }

            // Commonality slider
            Rect comLabel = new(itemRect.x, itemRect.y + 48, 80, 20);
            Widgets.Label(comLabel, "FactionLoadout_Backstory_Weight".Translate());
            Rect comSlider = new(itemRect.x + 82, itemRect.y + 48, itemRect.width - 110, 20);
            filter.commonality = Widgets.HorizontalSlider(comSlider, filter.commonality, 0f, 5f, middleAlignment: true, $"{filter.commonality:F1}");

            y += itemHeight;
        }

        Widgets.EndScrollView();

        if (toRemove != null)
            filters.Remove(toRemove);
    }

    /// <param name="child">True = childhood backstories; false = adulthood.</param>
    private void DrawFixedBackstories(Rect rect, bool active, List<DefRef<BackstoryDef>> defaultList, bool child)
    {
        DrawDefRefList(
            rect,
            active,
            ref scrolls[scrollIndex++],
            child ? Current.FixedChildBackstories : Current.FixedAdultBackstories,
            child ? DefaultKind.fixedChildBackstories : DefaultKind.fixedAdultBackstories,
            child ? DefCache.AllChildhoodBackstories : DefCache.AllAdulthoodBackstories,
            MakeBackstoryMenuItem,
            BackstoryLabel
        );
    }

    private void DrawExcludedBackstoryCategories(Rect rect, bool active, List<string> defaultList)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ExcludedBackstoryCategories, null, DefCache.AllBackstoryCategories);
    }

    private void DrawExcludedBackstories(Rect rect, bool active, List<DefRef<BackstoryDef>> defaultList)
    {
        DrawDefRefList(rect, active, ref scrolls[scrollIndex++], Current.ExcludedBackstories, null, DefCache.AllBackstoryDefs, MakeBackstoryMenuItem, BackstoryLabel);
    }

    // ==================== Static helpers (used by DefCache for sorting) ====================

    public static MenuItemBase MakeBackstoryMenuItem(BackstoryDef def)
    {
        string slotStr = def.slot == BackstorySlot.Childhood ? "FactionLoadout_Backstory_SlotChild".Translate() : "FactionLoadout_Backstory_SlotAdult".Translate();
        string title = def.title.NullOrEmpty() ? def.defName : def.title;
        return new MenuItemText(def, $"{slotStr} {title} ({def.modContentPack?.Name ?? "<no-mod>"})", tooltip: def.baseDesc);
    }

    public static string BackstoryLabel(BackstoryDef def)
    {
        return def.title.NullOrEmpty() ? (string)def.LabelCap ?? def.defName : def.title;
    }
}
