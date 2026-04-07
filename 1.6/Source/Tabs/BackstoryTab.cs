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
        DrawForcedTraitsDef(ui);
        DrawForcedTraitsChance(ui);
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
        if (
            Widgets.ButtonText(
                new Rect(rect.x, rect.y, 120, 32),
                "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate())
            )
        )
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

    // ==================== Forced traits ====================

    private void DrawForcedTraitsDef(Listing_Standard ui)
    {
        List<ForcedTrait> traits = Current.ForcedTraitsDef;
        float height = traits == null ? 32 : 38 * traits.Count + 66;

        ui.Label($"<b>{"FactionLoadout_Traits_ForcedTraitsDef".Translate()}</b>");
        TooltipHandler.TipRegion(ui.GetRect(0), "FactionLoadout_Traits_ForcedTraitsDefTooltip".Translate());
        Rect rect = ui.GetRect(height);
        bool active = traits != null;

        if (
            Widgets.ButtonText(
                new Rect(rect.x, rect.y, 120, 32),
                "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate())
            )
        )
        {
            if (active)
            {
                Current.ForcedTraitsDef = null;
            }
            else
            {
                Current.ForcedTraitsDef =
                    DefaultKind.forcedTraits?.Where(t => t.def != null).Select(t => new ForcedTrait { TraitDef = t.def, degree = t.degree.GetValueOrDefault() }).ToList() ?? [];
            }

            active = !active;
            traits = Current.ForcedTraitsDef;
        }

        float contentH = active ? Mathf.Max(4f, rect.height - 33f) : rect.height;
        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, contentH);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);

        ref Vector2 scroll = ref scrolls[scrollIndex++];
        if (active)
        {
            DrawTraitList(content, ref scroll, traits, showChance: false);
            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, $"<b>{"FactionLoadout_Traits_AddTraitAlways".Translate()}</b>"))
            {
                (TraitDef def, int deg) = DefCache.AllTraitDegrees.FirstOrDefault();
                if (def != null)
                    traits.Add(
                        new ForcedTrait
                        {
                            TraitDef = def,
                            degree = deg,
                            chance = 1f,
                        }
                    );
            }
        }
        else
        {
            List<TraitRequirement> defTraits = DefaultKind.forcedTraits;
            string txt;
            if (Current.IsGlobal)
            {
                txt = "---";
            }
            else if (defTraits.NullOrEmpty())
            {
                txt = $"[Default] <i>{"FactionLoadout_None".Translate()}</i>";
            }
            else
            {
                txt = $"[Default] {defTraits.Count} {"FactionLoadout_Traits_TraitCount".Translate(defTraits.Count)}";
            }

            GUI.enabled = false;
            Widgets.Label(content.GetCentered(txt), txt);
            GUI.enabled = true;
        }

        ui.Gap();
    }

    private void DrawForcedTraitsChance(Listing_Standard ui)
    {
        List<ForcedTrait> traits = Current.ForcedTraits;
        float height = traits == null ? 32 : 38 * traits.Count + 66;

        ui.Label($"<b>{"FactionLoadout_Traits_ForcedTraits".Translate()}</b>");
        TooltipHandler.TipRegion(ui.GetRect(0), "FactionLoadout_Traits_ForcedTraitsTooltip".Translate());
        Rect rect = ui.GetRect(height);
        bool active = traits != null;

        if (
            Widgets.ButtonText(
                new Rect(rect.x, rect.y, 120, 32),
                "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate())
            )
        )
        {
            Current.ForcedTraits = active ? null : [];
            active = !active;
            traits = Current.ForcedTraits;
        }

        float contentH = active ? Mathf.Max(4f, rect.height - 33f) : rect.height;
        Rect content = new(rect.x + 122, rect.y, ui.ColumnWidth - 124, contentH);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);

        ref Vector2 scroll = ref scrolls[scrollIndex++];
        if (active)
        {
            DrawTraitList(content, ref scroll, traits, showChance: true);
            content.y += content.height + 5;
            content.height = 28;
            content.width = 250;
            if (Widgets.ButtonText(content, $"<b>{"FactionLoadout_Traits_AddTraitChance".Translate()}</b>"))
            {
                (TraitDef def, int deg) = DefCache.AllTraitDegrees.FirstOrDefault();
                if (def != null)
                    traits.Add(
                        new ForcedTrait
                        {
                            TraitDef = def,
                            degree = deg,
                            chance = 1f,
                        }
                    );
            }
        }
        else
        {
            string txt = Current.IsGlobal ? "---" : $"[Default] <i>{"FactionLoadout_None".Translate()}</i>";
            GUI.enabled = false;
            Widgets.Label(content.GetCentered(txt), txt);
            GUI.enabled = true;
        }

        ui.Gap();
    }

    private void DrawTraitList(Rect rect, ref Vector2 scroll, List<ForcedTrait> traits, bool showChance)
    {
        float innerHeight = 38f * traits.Count;
        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 20, Mathf.Max(innerHeight, rect.height)));

        ForcedTrait toRemove = null;
        float y = 0;
        float itemW = rect.width - 20;

        for (int i = 0; i < traits.Count; i++)
        {
            ForcedTrait item = traits[i];
            Rect itemRect = new(0, y, itemW, 36);
            bool conflict = TraitHasConflictInList(traits, item);
            Color outlineColor = conflict ? Color.yellow * 0.8f : Color.white * 0.2f;
            Widgets.DrawBoxSolidWithOutline(itemRect, Color.black * 0.3f, outlineColor);

            if (conflict)
            {
                TooltipHandler.TipRegion(itemRect, "FactionLoadout_Traits_ConflictWarning".Translate());
            }

            // Trait selector button — left side, same row as delete button
            float selectorW = showChance ? (itemW - 38) * 0.55f : itemW - 38;
            Rect selectorBtn = new(itemRect.x + 4, itemRect.y + 4, selectorW, 28);
            string traitLabel = TraitLabel(item.TraitDef, item.degree);
            if (item.TraitDef == null)
            {
                GUI.color = Color.grey;
            }
            if (Widgets.ButtonText(selectorBtn, traitLabel))
            {
                ForcedTrait captured = item;
                List<MenuItemBase> menuItems = CustomFloatMenu.MakeItems(
                    DefCache.AllTraitDegrees,
                    td => new MenuItemText(td, TraitMenuLabel(td.def, td.degree), tooltip: TraitMenuTooltip(td.def, td.degree)) { Size = new Vector2(440, 28) }
                );
                CustomFloatMenu.Open(
                    menuItems,
                    raw =>
                    {
                        (TraitDef def, int deg) = raw.GetPayload<(TraitDef, int)>();
                        captured.TraitDef = def;
                        captured.degree = deg;
                    },
                    columns: 1
                );
            }
            GUI.color = Color.white;

            // Chance slider (only for probabilistic section) — between selector and delete button
            if (showChance)
            {
                float afterSelector = selectorBtn.xMax + 4;
                float remainingW = itemRect.xMax - 30 - afterSelector - 4;
                Rect chanceLabel = new(afterSelector, itemRect.y + 4, 60, 28);
                Widgets.Label(chanceLabel, "FactionLoadout_Traits_Chance".Translate());
                Rect chanceSlider = new(afterSelector + 64, itemRect.y + 8, remainingW - 64, 20);
                item.chance = Widgets.HorizontalSlider(chanceSlider, item.chance, 0f, 1f, middleAlignment: true, $"{item.chance:P0}");
            }

            // Delete button — right side, same row as selector
            Rect deleteBtn = new(itemRect.xMax - 28, itemRect.y + 4, 24, 28);
            GUI.color = Color.red;
            if (Widgets.ButtonText(deleteBtn, "X"))
            {
                toRemove = item;
            }
            GUI.color = Color.white;

            y += 38;
        }

        Widgets.EndScrollView();

        if (toRemove != null)
        {
            traits.Remove(toRemove);
        }
    }

    private static bool TraitHasConflictInList(List<ForcedTrait> traits, ForcedTrait item)
    {
        if (item.TraitDef == null)
        {
            return false;
        }

        foreach (ForcedTrait other in traits)
        {
            if (other == item || other.TraitDef == null)
            {
                continue;
            }

            if (TraitsConflict(item.TraitDef, other.TraitDef))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TraitsConflict(TraitDef a, TraitDef b)
    {
        if (a == b)
        {
            return true; // same TraitDef at any degree conflicts — pawn can only have one degree of a trait
        }

        if (a.conflictingTraits != null && a.conflictingTraits.Contains(b))
        {
            return true;
        }

        if (b.conflictingTraits != null && b.conflictingTraits.Contains(a))
        {
            return true;
        }

        if (a.exclusionTags != null && b.exclusionTags != null)
        {
            foreach (string tag in a.exclusionTags)
            {
                if (b.exclusionTags.Contains(tag))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string TraitLabel(TraitDef def, int degree)
    {
        if (def == null)
        {
            return "<i>None</i>";
        }

        // Degree data label is the primary display name for all traits.
        // TraitDef.label / LabelCap is often null — labels live in degreeDatas.
        TraitDegreeData data = def.DataAtDegree(degree);
        string degLabel = data?.label;
        string displayName;
        if (!degLabel.NullOrEmpty())
        {
            displayName = degLabel.CapitalizeFirst();
        }
        else
        {
            string defLabel = def.label;
            displayName = defLabel.NullOrEmpty() ? def.defName : defLabel.CapitalizeFirst();
        }

        return $"{displayName} [{def.defName}, {degree}]";
    }

    private static string TraitMenuLabel(TraitDef def, int degree)
    {
        TraitDegreeData data = def.DataAtDegree(degree);
        string degLabel = data?.label;
        string displayName;
        if (!degLabel.NullOrEmpty())
        {
            displayName = degLabel.CapitalizeFirst();
        }
        else
        {
            string defLabel = def.label;
            displayName = defLabel.NullOrEmpty() ? def.defName : defLabel.CapitalizeFirst();
        }

        return $"{displayName} [{def.defName}, {degree}]";
    }

    private static string TraitMenuTooltip(TraitDef def, int degree)
    {
        TraitDegreeData data = def.DataAtDegree(degree);
        return data?.description ?? def.description ?? string.Empty;
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
