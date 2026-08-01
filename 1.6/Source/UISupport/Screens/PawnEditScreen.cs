using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Modules;
using FactionLoadout.UISupport.DrawSupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.Screens;

/// <summary>
/// Edits one pawnkind in three columns: a big live preview (left), a vertical category
/// nav (middle), and the selected category's option content (right). Reuses the existing
/// <see cref="Tab"/> machinery unchanged — the only structural changes vs. the old
/// <c>PawnKindEditUI</c> are the vertical nav (instead of cramped tab buttons) and a
/// per-category scroll position (fixes the old shared-scroll bug).
///
/// The preview column is a placeholder in Phase B; Phase C plugs the live portrait in
/// at <see cref="DrawPreviewColumn"/>.
/// </summary>
[HotSwappable]
public class PawnEditScreen
{
    public readonly TotalControlController Controller;
    public readonly FactionEdit ParentFaction;
    public readonly PawnKindEdit Current;
    public readonly PreviewPawnController Preview;
    private readonly PawnPreviewWidget previewWidget = new();

    private List<Tab> tabs;
    private int selectedCategory;
    private readonly Dictionary<Tab, Vector2> categoryScroll = new();
    private readonly Dictionary<Tab, float> tabHeights = new();
    private Vector2 navScroll;

    public PawnEditScreen(TotalControlController controller, FactionEdit parentFaction, PawnKindEdit kind)
    {
        Controller = controller;
        ParentFaction = parentFaction;
        Current = kind;
        Preview = new PreviewPawnController(parentFaction, kind?.Def);
        DefCache.ScanDefs();
    }

    public void Dispose() => Preview.Dispose();

    /// <summary>The unmodified base def, used to show "default" values next to overrides.</summary>
    public PawnKindDef DefaultKind
    {
        get
        {
            if (Current.DeletedOrClosed)
                return Current.Def;

            FactionDef found = FactionEdit.TryGetOriginal(ParentFaction.Faction.DefName);
            if (found == null)
                return Current.Def;
            PawnKindDef found2 = found.GetKindDefs().FirstOrDefault(k => k.defName == Current.Def.defName);
            return found2 ?? Current.Def;
        }
    }

    public void Draw(Rect inRect)
    {
        if (Current == null || Current.DeletedOrClosed)
            return;

        Preview.Tick();

        Text.Font = GameFont.Small;
        if (tabs == null)
            BuildTabs();

        // Header.
        Rect header = new(inRect.x, inRect.y, inRect.width, 32f);
        Text.Font = GameFont.Medium;
        string kindLabel = Current.IsGlobal ? "FactionLoadout_GlobalLabel".Translate().ToString() : Current.Def.LabelCap.ToString();
        Widgets.Label(header, "FactionLoadout_TC_PawnHeader".Translate(kindLabel));
        Text.Font = GameFont.Small;

        float colsY = inRect.y + 36f;
        float colsH = inRect.yMax - colsY;

        if ((tabs?.Count ?? 0) == 0)
        {
            Widgets.Label(new Rect(inRect.x, colsY, inRect.width, 40f), "FactionLoadout_NoEditableProperties".Translate());
            return;
        }

        selectedCategory = Mathf.Clamp(selectedCategory, 0, tabs.Count - 1);

        // Three columns: preview | category nav | options.
        float previewW = inRect.width * 0.38f;
        float navW = inRect.width * 0.16f;
        const float gap = 8f;
        Rect previewRect = new(inRect.x, colsY, previewW, colsH);
        Rect navRect = new(previewRect.xMax + gap, colsY, navW, colsH);
        Rect optsRect = new(navRect.xMax + gap, colsY, inRect.xMax - (navRect.xMax + gap), colsH);

        DrawPreviewColumn(previewRect);
        DrawCategoryNav(navRect);
        DrawOptions(optsRect);
    }

    private void DrawPreviewColumn(Rect rect)
    {
        previewWidget.Draw(rect, Preview);
    }

    private void DrawCategoryNav(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(4f);

        const float rowH = 34f;
        float contentH = tabs.Count * rowH;
        bool needsScroll = contentH > rect.height;
        Rect view = new(0, 0, rect.width - (needsScroll ? 16f : 0f), Mathf.Max(contentH, rect.height));

        Widgets.BeginScrollView(rect, ref navScroll, view);
        for (int i = 0; i < tabs.Count; i++)
        {
            Rect btn = new(0, i * rowH, view.width, rowH - 2f);
            Color bg = selectedCategory == i ? new Color32(49, 82, 133, 255) : new Color(0.2f, 0.2f, 0.2f, 1f);
            Rect r = btn;
            if (Widgets.CustomButtonText(ref r, $"<b>{tabs[i].Name}</b>", bg, Color.white, Color.white))
                selectedCategory = i;
        }
        Widgets.EndScrollView();
    }

    private void DrawOptions(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(6f);

        // Clipboard toolbar (copy / paste-all). Wired identically to the old editor:
        // the reset callback clears the active tab's text/scroll buffers after a paste.
        Rect toolbarRect = new(rect.x, rect.y, rect.width, 28f);
        ClipboardToolbar.Draw(
            toolbarRect,
            Current,
            () =>
            {
                if (selectedCategory >= 0 && selectedCategory < tabs.Count && tabs[selectedCategory] is EditTab et)
                    et.ResetBuffers();
            }
        );

        // Selected category content, with its OWN scroll position (per-category bug fix).
        Tab tab = tabs[selectedCategory];
        Rect scrollOut = new(rect.x, toolbarRect.yMax + 4f, rect.width, rect.yMax - (toolbarRect.yMax + 4f));
        float innerW = scrollOut.width - 24f;
        float contentH = tabHeights.TryGetValue(tab, out float storedH) ? Mathf.Max(storedH, scrollOut.height) : scrollOut.height;

        Vector2 scroll = categoryScroll.TryGetValue(tab, out Vector2 s) ? s : Vector2.zero;
        Widgets.BeginScrollView(scrollOut, ref scroll, new Rect(0, 0, innerW, contentH));

        Listing_Standard ui = new() { ColumnWidth = innerW };
        ui.Begin(new Rect(0, 0, innerW, 1000000));

        // Detect inline option changes (checkboxes/sliders/text) to auto-refresh the
        // preview. Float-menu pickers apply on a later frame and are covered by the
        // explicit Regenerate button.
        bool prevChanged = GUI.changed;
        GUI.changed = false;
        tab.Draw(ui);
        if (GUI.changed)
            Preview.NotifyEditChanged();
        GUI.changed = prevChanged || GUI.changed;

        tabHeights[tab] = ui.CurHeight;
        ui.End();

        Widgets.EndScrollView();
        categoryScroll[tab] = scroll;
    }

    private void BuildTabs()
    {
        PawnKindDef dk = DefaultKind;
        tabs = [new GeneralTab(Current, dk)];

        bool isAnimal = dk.RaceProps.Animal;
        if (!isAnimal)
        {
            tabs.AddRange([
                new BackstoryTab(Current, dk),
                new AppearanceTab(Current, dk),
                new ApparelTab(Current, dk),
                new WeaponTab(Current, dk),
                new ImplantsTab(Current, dk),
                new InventoryTab(Current, dk),
                new RaidPointsTab(Current, dk),
                new RaidLootTab(Current, dk),
            ]);
            if (VFEAncientsReflectionModule.ModLoaded.Value)
                tabs.Add(new AncientsTab(Current, dk));
            if (VEPsycastsReflectionModule.ModLoaded.Value)
                tabs.Add(new PsycastsTab(Current, dk));
            if (ModsConfig.BiotechActive)
                tabs.Add(new XenotypeTab(Current, dk));

            foreach (ITotalControlModule module in ModuleRegistry.Modules)
            {
                if (module.IsActive)
                    module.AddTabs(Current, dk, tabs);
            }
        }
    }
}
