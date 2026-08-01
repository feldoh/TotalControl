using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport.Screens;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

public enum TCScreen
{
    FactionView,
    FactionEdit,
    PawnEdit,
}

/// <summary>
/// Owns the navigation state (which screen + the selected preset/faction/pawnkind) and
/// renders the shared chrome (title bar, top tab strip, breadcrumb) before dispatching
/// to the active screen. Screen objects hold their own scroll/buffer state so it persists
/// across frames; the controller rebuilds a screen only when its subject changes.
/// </summary>
[HotSwappable]
public class TotalControlController
{
    public readonly Dialog_TotalControl Owner;

    public TCScreen Screen { get; private set; } = TCScreen.FactionView;
    public Preset SelectedPreset;
    public FactionEdit SelectedFaction { get; private set; }
    public PawnKindEdit SelectedKind { get; private set; }

    private readonly FactionViewScreen factionView;
    private FactionEditScreen factionEdit;
    private PawnEditScreen pawnEdit;

    public TotalControlController(Dialog_TotalControl owner, Preset initialPreset)
    {
        Owner = owner;
        SelectedPreset = ResolveInitialPreset(initialPreset);
        factionView = new FactionViewScreen(this);
    }

    public static Preset ResolveInitialPreset(Preset given)
    {
        if (given != null)
            return given;

        if (!MySettings.ActivePreset.NullOrEmpty())
        {
            Preset active = Preset.LoadedPresets.FirstOrDefault(p => p.GUID == MySettings.ActivePreset);
            if (active != null)
                return active;
        }

        return Preset.LoadedPresets.FirstOrDefault();
    }

    // ---------------------------------------------------------------- navigation

    public void SelectPreset(Preset p)
    {
        SelectedPreset = p;
        SelectedFaction = null;
        SelectedKind = null;
        DisposeFactionEdit();
        DisposePawnEdit();
        TCEditContext.CurrentFaction = null;
        Screen = TCScreen.FactionView;
    }

    public void OpenFaction(FactionEdit fe)
    {
        if (fe == null)
            return;

        if (SelectedFaction != fe)
        {
            SelectedFaction = fe;
            SelectedKind = null;
            DisposeFactionEdit();
            factionEdit = new FactionEditScreen(this, fe);
            DisposePawnEdit();
        }

        TCEditContext.CurrentFaction = fe;
        Screen = TCScreen.FactionEdit;
    }

    public void OpenKind(PawnKindEdit pke)
    {
        if (pke == null)
            return;

        if (SelectedKind != pke)
        {
            DisposePawnEdit();
            SelectedKind = pke;
            pawnEdit = new PawnEditScreen(this, SelectedFaction, pke);
        }

        Screen = TCScreen.PawnEdit;
    }

    public void GoTo(TCScreen s)
    {
        if (IsEnabled(s))
            Screen = s;
    }

    public bool IsEnabled(TCScreen s) =>
        s switch
        {
            TCScreen.FactionView => true,
            TCScreen.FactionEdit => SelectedFaction is { DeletedOrClosed: false },
            TCScreen.PawnEdit => SelectedKind is { DeletedOrClosed: false },
            _ => false,
        };

    public bool HandleEscape()
    {
        switch (Screen)
        {
            case TCScreen.PawnEdit:
                Screen = TCScreen.FactionEdit;
                return true;
            case TCScreen.FactionEdit:
                Screen = TCScreen.FactionView;
                return true;
            default:
                return false; // at Home -> let the window close
        }
    }

    public void Dispose()
    {
        DisposeFactionEdit();
        DisposePawnEdit();
        TCEditContext.CurrentFaction = null;
    }

    private void DisposePawnEdit()
    {
        pawnEdit?.Dispose();
        pawnEdit = null;
    }

    private void DisposeFactionEdit()
    {
        factionEdit?.Dispose();
        factionEdit = null;
    }

    // ------------------------------------------------------------------- drawing

    public void Draw(Rect inRect)
    {
        // Validate the selection context; fall back if something was deleted underneath us.
        if (Screen == TCScreen.PawnEdit && SelectedKind is not { DeletedOrClosed: false })
        {
            SelectedKind = null;
            DisposePawnEdit();
            Screen = IsEnabled(TCScreen.FactionEdit) ? TCScreen.FactionEdit : TCScreen.FactionView;
        }
        if (Screen == TCScreen.FactionEdit && SelectedFaction is not { DeletedOrClosed: false })
        {
            SelectedFaction = null;
            SelectedKind = null;
            DisposeFactionEdit();
            DisposePawnEdit();
            Screen = TCScreen.FactionView;
        }

        Rect titleBar = new(inRect.x, inRect.y, inRect.width, 38f);
        Rect tabStrip = new(inRect.x, titleBar.yMax + 2f, inRect.width, 32f);
        Rect crumb = new(inRect.x, tabStrip.yMax + 4f, inRect.width, 20f);
        Rect content = new(inRect.x, crumb.yMax + 6f, inRect.width, inRect.yMax - (crumb.yMax + 6f));

        DrawTitleBar(titleBar);
        DrawTabStrip(tabStrip);
        DrawBreadcrumb(crumb);

        switch (Screen)
        {
            case TCScreen.FactionView:
                factionView.Draw(content);
                break;
            case TCScreen.FactionEdit:
                factionEdit?.Draw(content);
                break;
            case TCScreen.PawnEdit:
                pawnEdit?.Draw(content);
                break;
        }
    }

    private void DrawTitleBar(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Rect titleRect = new(rect.x, rect.y, 360f, rect.height);
        Widgets.Label(titleRect, "FactionLoadout_TC_Title".Translate());
        Text.Font = GameFont.Small;

        // Active-preset dropdown. Leave ~34px on the right for the window's close X.
        const float dropdownW = 300f;
        Rect dropdown = new(rect.xMax - dropdownW - 34f, rect.y + 4f, dropdownW, 30f);
        string presetLabel = SelectedPreset != null ? "FactionLoadout_TC_PresetLabel".Translate(SelectedPreset.Name) : "FactionLoadout_TC_NoPreset".Translate();
        if (Widgets.ButtonText(dropdown, presetLabel))
            OpenPresetDropdown();
    }

    private void OpenPresetDropdown()
    {
        List<FloatMenuOption> options = new();

        // "None" clears the viewed preset (the Faction View then offers to create one).
        options.Add(new FloatMenuOption("FactionLoadout_TC_PresetNone".Translate(), () => SelectPreset(null)));

        foreach (Preset p in Preset.LoadedPresets)
        {
            Preset captured = p;
            bool active = MySettings.ActivePreset == p.GUID;
            string label = active ? $"{p.Name}  <color=#81f542>({"FactionLoadout_Active".Translate()})</color>" : p.Name;
            options.Add(new FloatMenuOption(label, () => SelectPreset(captured)));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void DrawTabStrip(Rect rect)
    {
        const float tabW = 170f;
        const float gap = 4f;
        DrawTab(new Rect(rect.x, rect.y, tabW, rect.height), TCScreen.FactionView, "FactionLoadout_Tab_FactionView".Translate());
        DrawTab(new Rect(rect.x + (tabW + gap), rect.y, tabW, rect.height), TCScreen.FactionEdit, "FactionLoadout_Tab_FactionEdit".Translate());
        DrawTab(new Rect(rect.x + (tabW + gap) * 2f, rect.y, tabW, rect.height), TCScreen.PawnEdit, "FactionLoadout_Tab_PawnEdit".Translate());
        Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
    }

    private void DrawTab(Rect rect, TCScreen screen, string label)
    {
        bool selected = Screen == screen;
        Color bg = selected ? new Color32(49, 82, 133, 255) : new Color(0.2f, 0.2f, 0.2f, 1f);

        if (!IsEnabled(screen))
        {
            Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"<b>{label}</b>");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        Rect r = rect;
        if (Widgets.CustomButtonText(ref r, $"<b>{label}</b>", bg, Color.white, Color.white))
            GoTo(screen);
    }

    private void DrawBreadcrumb(Rect rect)
    {
        Text.Font = GameFont.Tiny;
        float x = rect.x;

        // Home is always clickable (unless already there).
        DrawCrumbSegment(ref x, rect, "FactionLoadout_Breadcrumb_Home".Translate(), Screen != TCScreen.FactionView, () => GoTo(TCScreen.FactionView));

        if (SelectedFaction is { DeletedOrClosed: false })
        {
            DrawCrumbSeparator(ref x, rect);
            DrawCrumbSegment(ref x, rect, SelectedFaction.Faction.LabelCap.ToString(), Screen != TCScreen.FactionEdit, () => GoTo(TCScreen.FactionEdit));

            if (SelectedKind is { DeletedOrClosed: false })
            {
                DrawCrumbSeparator(ref x, rect);
                string kindName = SelectedKind.IsGlobal ? "FactionLoadout_GlobalLabel".Translate().ToString() : SelectedKind.Def?.LabelCap.ToString();
                DrawCrumbSegment(ref x, rect, kindName, clickable: false, onClick: null);
            }
        }

        Text.Font = GameFont.Small;
    }

    private static void DrawCrumbSegment(ref float x, Rect bar, string text, bool clickable, Action onClick)
    {
        float w = Text.CalcSize(text).x;
        Rect r = new(x, bar.y, w, bar.height);
        GUI.color = clickable ? new Color(0.6f, 0.8f, 1f) : Color.white;
        Widgets.Label(r, text);
        if (clickable)
        {
            Widgets.DrawHighlightIfMouseover(r);
            if (Widgets.ButtonInvisible(r))
                onClick?.Invoke();
        }
        GUI.color = Color.white;
        x += w;
    }

    private static void DrawCrumbSeparator(ref float x, Rect bar)
    {
        const string sep = "   ›   ";
        float w = Text.CalcSize(sep).x;
        GUI.color = new Color(0.6f, 0.6f, 0.6f);
        Widgets.Label(new Rect(x, bar.y, w, bar.height), sep);
        GUI.color = Color.white;
        x += w;
    }
}
