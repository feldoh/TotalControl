using System;
using System.Collections;
using System.Collections.Generic;
using FactionLoadout.UISupport.DrawSupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>
/// Abstract base for built-in PawnKindEdit editor tabs.
/// Holds per-instance buffer/scroll state and thin wrappers around the
/// static draw helpers in <see cref="DrawSupport"/>. Concrete tabs override
/// <see cref="DrawContents"/> to provide tab-specific UI.
/// </summary>
public abstract class EditTab : Tab
{
    public readonly PawnKindEdit Current;
    public readonly PawnKindDef DefaultKind;

    // Per-instance buffer state — each tab keeps its own, so switching tabs preserves state.
    protected Vector2[] scrolls = new Vector2[64];
    protected string[] buffers = new string[64];
    protected List<(string x, string y)>[] curvePointBuffers = new List<(string x, string y)>[64];
    protected int scrollIndex;
    protected int bufferIndex;
    protected int curveIndex;

    protected EditTab(string name, PawnKindEdit current, PawnKindDef defaultKind)
        : base(name, null)
    {
        Current = current;
        DefaultKind = defaultKind;
    }

    public override void Draw(Listing_Standard ui)
    {
        scrollIndex = 0;
        bufferIndex = 0;
        curveIndex = 0;
        DrawRegionTitle(ui, Name);
        DrawContents(ui);
    }

    protected abstract void DrawContents(Listing_Standard ui);

    public void ResetBuffers()
    {
        buffers = new string[64];
        scrolls = new Vector2[64];
        curvePointBuffers = new List<(string x, string y)>[64];
    }

    // ==================== DrawOverride wrappers ====================

    public void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T? field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height = 32,
        Func<PawnKindEdit, T?> pasteGet = null
    )
        where T : struct => OverrideDrawSupport.DrawOverride(ui, defaultValue, ref field, label, drawContent, height, pasteGet, ResetBuffers);

    public void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height = 32,
        Func<PawnKindEdit, T> pasteGet = null
    )
        where T : class => OverrideDrawSupport.DrawOverride(ui, defaultValue, ref field, label, drawContent, height, pasteGet, ResetBuffers);

    public void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height = 32,
        bool cloneDefault = true,
        Func<PawnKindEdit, T> pasteGet = null
    )
        where T : IList => OverrideDrawSupport.DrawOverride(ui, defaultValue, ref field, label, drawContent, height, cloneDefault, pasteGet, ResetBuffers);

    // ==================== List drawer wrappers ====================

    protected CustomFloatMenu DrawDefRefList<T>(
        Rect rect,
        bool active,
        ref Vector2 scroll,
        IList<DefRef<T>> current,
        IList<T> defaults,
        IEnumerable<T> allDefs,
        Func<T, MenuItemBase> makeItem = null,
        Func<T, string> labelFunc = null,
        Func<T, string> warningFunc = null
    )
        where T : Def, new() => ListDrawSupport.DrawDefRefList(rect, active, ref scroll, current, defaults, allDefs, Current.IsGlobal, makeItem, labelFunc, warningFunc);

    protected CustomFloatMenu DrawDefList<T>(
        Rect rect,
        bool active,
        ref Vector2 scroll,
        IList<T> current,
        IList<T> defaultThings,
        IEnumerable<T> allThings,
        bool allowDupes,
        Func<T, MenuItemBase> makeItems = null
    )
        where T : Def => ListDrawSupport.DrawDefList(rect, active, ref scroll, current, defaultThings, allThings, allowDupes, Current.IsGlobal, makeItems);

    protected void DrawColorList(Rect rect, bool active, ref Vector2 scroll, IList<Color> current, IList<Color> defaultColors) =>
        ListDrawSupport.DrawColorList(rect, active, ref scroll, current, defaultColors, Current.IsGlobal);

    protected void DrawStringList(Rect rect, bool active, ref Vector2 scroll, IList<string> current, IList<string> defaultTags, IEnumerable<string> allTags) =>
        ListDrawSupport.DrawStringList(rect, active, ref scroll, current, defaultTags, allTags, Current.IsGlobal);

    // ==================== Value drawer wrappers ====================

    protected void DrawEnumSelector<T>(Rect rect, bool active, T? field, T defaultValue, Action<T> apply, Func<T, string> makeName = null)
        where T : struct => ValueDrawSupport.DrawEnumSelector(rect, active, Current.IsGlobal, field, defaultValue, apply, makeName);

    protected void DrawDefSelector<T>(Rect rect, bool active, IEnumerable<T> defs, T field, T defaultValue, Action<T> apply, Func<T, string> makeName = null)
        where T : Def => ValueDrawSupport.DrawDefSelector(rect, active, Current.IsGlobal, defs, field, defaultValue, apply, makeName);

    protected void DrawChance(ref float? field, float defaultValue, Rect rect, bool active) => ValueDrawSupport.DrawChance(rect, active, Current.IsGlobal, ref field, defaultValue);

    protected void DrawIntRange(Rect rect, bool active, ref IntRange? current, IntRange defaultRange, ref string buffer, ref string buffer2) =>
        ValueDrawSupport.DrawIntRange(rect, active, Current.IsGlobal, ref current, defaultRange, ref buffer, ref buffer2);

    protected void DrawFloatRange(Rect rect, bool active, ref FloatRange? current, FloatRange defaultRange, ref string buffer, ref string buffer2) =>
        ValueDrawSupport.DrawFloatRange(rect, active, Current.IsGlobal, ref current, defaultRange, ref buffer, ref buffer2);

    protected float GetHeightFor(IList list, float itemHeight = 26) => ValueDrawSupport.GetHeightFor(list, itemHeight);

    // ==================== Complex drawer wrappers ====================

    protected void DrawSpecificGear(Listing_Standard ui, ref List<SpecRequirementEdit> edits, string label, Func<ThingDef, bool> thingFilter, ThingDef defaultThing) =>
        SpecificGearDrawer.Draw(ui, ref edits, label, thingFilter, defaultThing, ref scrolls[scrollIndex++]);

    /// <summary>
    /// Draws the allowlist/blocklist mode toggle for a material list at the top of <paramref name="rect"/>
    /// and returns the remaining rect below it for the def-list picker.
    /// </summary>
    protected Rect DrawMaterialModeToggle(Rect rect, ref bool isBlocklist)
    {
        string label = isBlocklist ? "FactionLoadout_Materials_ModeBlacklist".Translate() : "FactionLoadout_Materials_ModeWhitelist".Translate();
        Rect modeRow = rect;
        modeRow.height = 24f;
        modeRow.width = Mathf.Min(rect.width, Mathf.Max(180f, Text.CalcSize(label).x + 24f));
        if (Widgets.ButtonText(modeRow, label))
            isBlocklist = !isBlocklist;

        Rect listRect = rect;
        listRect.yMin += 26f;
        return listRect;
    }

    /// <summary>
    /// Draws a one-line "Allowed: Leathery 13, Metallic 4, …" summary of what a material list+mode permits.
    /// No-op when the field is inactive (null list) or nothing is permitted.
    /// </summary>
    protected void DrawMaterialSummary(Listing_Standard ui, List<DefRef<ThingDef>> materials, bool isBlocklist)
    {
        if (materials == null)
            return;
        string summary = DefCache.MaterialCategorySummary(materials, isBlocklist);
        if (string.IsNullOrEmpty(summary))
            return;
        GUI.color = new Color(0.62f, 0.78f, 1f);
        ui.Label("FactionLoadout_Materials_AllowedSummary".Translate(summary));
        GUI.color = Color.white;
    }

    public void DrawCurve(Listing_Standard listing, ref SimpleCurve curve, ref List<(string x, string y)> curvePointBuffer) =>
        CurveDrawer.DrawCurve(listing, ref curve, ref curvePointBuffer);
}
