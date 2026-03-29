using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Searchable pawnkind picker dialog.  Any PawnKindDef in the game can be
/// selected; already-added entries are greyed but still selectable.
/// A manual defName entry at the bottom handles modded content that is not
/// currently loaded.
/// </summary>
[HotSwappable]
public class Dialog_PawnKindPicker : Window
{
    private readonly string _roleName;
    private readonly List<PawnGenOptionEdit> _existingList;
    private readonly Action<string> _onPick;

    private string _search = "";
    private Vector2 _scrollPos;
    private float _contentHeight = 0f;
    private string _manualEntry = "";

    // Cached sorted pawnkind list.
    private static List<PawnKindDef> _allKinds;

    public Dialog_PawnKindPicker(string roleName, List<PawnGenOptionEdit> existingList, Action<string> onPick)
    {
        _roleName = roleName;
        _existingList = existingList;
        _onPick = onPick;
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
        draggable = true;
    }

    public override Vector2 InitialSize => new(420f, 440f);

    public override void DoWindowContents(Rect inRect)
    {
        EnsureKinds();

        Listing_Standard ui = new();
        ui.Begin(inRect);

        // Title
        ui.Label("<b>" + "FactionLoadout_GroupEditor_PickerTitle".Translate(_roleName) + "</b>");
        ui.Gap(4f);

        // Search field — reset scroll when query changes so results are always visible at top
        string newSearch = ui.TextEntry(_search);
        if (newSearch != _search)
        {
            _search = newSearch;
            _scrollPos = Vector2.zero;
        }

        ui.Gap(4f);

        // Scroll list
        float scrollH = Mathf.Max(60f, inRect.height - ui.CurHeight - 70f);
        Rect scrollOut = ui.GetRect(scrollH);
        List<PawnKindDef> filtered = string.IsNullOrWhiteSpace(_search)
            ? _allKinds
            : _allKinds
                .Where(k =>
                    k.LabelCap.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || (k.defName ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                )
                .ToList();

        float itemH = 24f;
        Rect innerRect = new(0f, 0f, scrollOut.width - 16f, Mathf.Max(_contentHeight, filtered.Count * (itemH + 2f)));
        Widgets.BeginScrollView(scrollOut, ref _scrollPos, innerRect);

        float y = 0f;
        bool anyShown = false;
        foreach (PawnKindDef kind in filtered)
        {
            bool alreadyAdded = _existingList.Any(e => e.KindDefName == kind.defName);
            Rect row = new(0f, y, innerRect.width, itemH);

            Widgets.DrawHighlightIfMouseover(row);
            if (alreadyAdded)
                GUI.color = Color.grey;

            Widgets.Label(
                new Rect(4f, y + 2f, innerRect.width - 8f, itemH - 4f),
                alreadyAdded
                    ? $"{kind.LabelCap} <color=grey>({kind.defName}) — {"FactionLoadout_GroupEditor_PickerAlreadyAdded".Translate()}</color>"
                    : $"{kind.LabelCap} <color=grey>({kind.defName})</color>"
            );

            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(row))
            {
                _onPick?.Invoke(kind.defName);
                Close();
                return;
            }

            y += itemH + 2f;
            anyShown = true;
        }

        if (!anyShown)
        {
            GUI.color = Color.grey;
            Widgets.Label(new Rect(4f, 4f, innerRect.width - 8f, 24f), "FactionLoadout_GroupEditor_PickerNoResults".Translate().ToString());
            GUI.color = Color.white;
        }

        _contentHeight = y;
        Widgets.EndScrollView();

        ui.Gap(4f);
        ui.GapLine();

        // Manual defName entry
        ui.Label("FactionLoadout_GroupEditor_PickerManualLabel".Translate());
        Rect manualRow = ui.GetRect(28f);
        float addBtnW = 60f;
        _manualEntry = Widgets.TextField(new Rect(manualRow.x, manualRow.y, manualRow.width - addBtnW - 4f, 24f), _manualEntry);
        if (Widgets.ButtonText(new Rect(manualRow.xMax - addBtnW, manualRow.y, addBtnW, 24f), "FactionLoadout_GroupEditor_PickerManualAdd".Translate()))
        {
            if (!string.IsNullOrWhiteSpace(_manualEntry))
            {
                _onPick?.Invoke(_manualEntry.Trim());
                Close();
            }
        }

        ui.End();
    }

    private static void EnsureKinds()
    {
        if (_allKinds != null)
            return;
        _allKinds = DefDatabase<PawnKindDef>.AllDefsListForReading.OrderBy(k => k.LabelCap.ToString()).ToList();
    }
}
