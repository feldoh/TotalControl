using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Window that lets users view, add, remove and edit spawn groups
/// (PawnGroupMakers) for a faction.
/// </summary>
[HotSwappable]
public class GroupEditorUI : Window
{
    private readonly FactionEdit _edit;

    /// <summary>Indices (into GroupEdits list) that are currently expanded.</summary>
    private readonly HashSet<int> _expanded = [];

    private Vector2 _scrollPos;

    // Cached list of RaidStrategyDefs for the block-strategies dropdown.
    private static List<RaidStrategyDef> _allRaidStrategies;

    // Cached sorted list of PawnGroupKindDefs for the add-group menu.
    private static List<PawnGroupKindDef> _allGroupKinds;

    // Buffers for numeric text fields.  Key = (groupIndex, fieldId).
    private readonly Dictionary<(int, string), string> _numBuffers = new();

    // Index of a group pending deletion (set by confirm dialog callback).
    private int _pendingDeleteIndex = -1;

    public static void OpenEditor(FactionEdit edit)
    {
        if (edit == null)
            return;
        Find.WindowStack.Add(new GroupEditorUI(edit));
    }

    public GroupEditorUI(FactionEdit edit)
    {
        _edit = edit;
        draggable = true;
        resizeable = true;
        doCloseX = true;
        closeOnCancel = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(720f, 640f);

    public override void DoWindowContents(Rect inRect)
    {
        if (_edit == null || _edit.DeletedOrClosed)
        {
            Close();
            return;
        }

        // Ensure GroupEdits is initialized so we always have a list to work with.
        List<PawnGroupMakerEdit> groups = _edit.GetOrInitPawnGroupMakerEdits();

        Listing_Standard ui = new();
        ui.Begin(inRect);

        // Title
        Rect titleRect = ui.GetRect(36f);
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, "FactionLoadout_GroupEditor_Title".Translate(_edit.Faction.Def?.LabelCap ?? _edit.Faction.DefName));
        Text.Font = GameFont.Small;

        // Help text
        GUI.color = Color.grey;
        ui.Label("<i>" + "FactionLoadout_GroupEditor_HelpText".Translate() + "</i>");
        GUI.color = Color.white;

        ui.GapLine();

        // Toolbar
        Rect toolbarRow = ui.GetRect(28f);
        float btnW = 160f;
        GUI.color = Color.green;
        if (Widgets.ButtonText(new Rect(toolbarRow.x, toolbarRow.y, btnW, 24f), "FactionLoadout_GroupEditor_AddGroup".Translate()))
        {
            OpenAddGroupMenu();
        }

        GUI.color = Color.white;

        bool hasEdits = _edit.PawnGroupMakerEdits != null;
        if (!hasEdits)
            GUI.color = Color.grey;
        if (Widgets.ButtonText(new Rect(toolbarRow.xMax - 200f, toolbarRow.y, 200f, 24f), "FactionLoadout_GroupEditor_ResetButton".Translate()))
        {
            if (hasEdits)
                OpenResetConfirm();
        }

        GUI.color = Color.white;

        // Scroll view — inner rect sized from data model to avoid feedback loops
        float scrollOutH = Mathf.Max(60f, inRect.height - ui.CurHeight - 8f);
        Rect scrollOutRect = ui.GetRect(scrollOutH);
        float contentH = CalcTotalContentHeight(groups) + 20f; // 20f safety margin
        Rect scrollViewRect = new(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(contentH, scrollOutH));

        Widgets.BeginScrollView(scrollOutRect, ref _scrollPos, scrollViewRect);
        Listing_Standard inner = new();
        inner.Begin(scrollViewRect);

        if (groups.Count == 0)
        {
            GUI.color = Color.grey;
            inner.Label("<i>(" + "FactionLoadout_GroupEditor_NoPawns".Translate() + ")</i>");
            GUI.color = Color.white;
        }
        else
        {
            DrawGroupList(inner, groups);
        }

        inner.End();
        Widgets.EndScrollView();

        ui.End();
    }

    // ── Height calculation (drives scroll inner rect sizing) ──────────────

    private float CalcTotalContentHeight(List<PawnGroupMakerEdit> groups)
    {
        if (groups.Count == 0)
            return 30f; // "(none)" label

        float h = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            h += 28f; // header row
            if (_expanded.Contains(i))
            {
                h += CalcExpandedGroupHeight(groups[i]);
                h += 12f; // GapLine after body
            }
        }

        return h;
    }

    private static float CalcExpandedGroupHeight(PawnGroupMakerEdit group)
    {
        float h = 0f;
        h += 28f; // group type
        h += 28f; // commonality
        h += 28f; // max points
        h += 28f; // block strategies
        h += 12f; // GapLine
        h += CalcPawnListHeight(group.Options);
        h += CalcPawnListHeight(group.Guards);
        h += CalcPawnListHeight(group.Traders);
        h += CalcPawnListHeight(group.Carriers);
        h += 4f; // Gap(4f)
        return h;
    }

    private static float CalcPawnListHeight(List<PawnGenOptionEdit> list)
    {
        float h = 24f; // section header
        h += 2f; // Gap(2f)
        if (list.Count == 0)
        {
            h += 24f; // "(none)" label (~Text.LineHeight + verticalSpacing)
        }
        else
        {
            h += list.Count * 24f; // each pawn row
        }

        h += 12f; // GapLine
        return h;
    }

    private void DrawGroupList(Listing_Standard ui, List<PawnGroupMakerEdit> groups)
    {
        // Apply any pending deletion from the previous frame's confirm callback.
        if (_pendingDeleteIndex >= 0 && _pendingDeleteIndex < groups.Count)
        {
            int idx = _pendingDeleteIndex;
            groups.RemoveAt(idx);
            _expanded.Remove(idx);
            List<int> above = _expanded.Where(x => x > idx).ToList();
            foreach (int a in above)
            {
                _expanded.Remove(a);
                _expanded.Add(a - 1);
            }

            _numBuffers.Clear(); // indices have shifted, drop stale buffers
        }

        _pendingDeleteIndex = -1;

        for (int i = 0; i < groups.Count; i++)
        {
            PawnGroupMakerEdit group = groups[i];
            bool expanded = _expanded.Contains(i);

            DrawGroupHeader(ui, group, i, expanded);
            if (expanded)
            {
                DrawGroupBody(ui, group, i);
            }
        }
    }

    private void DrawGroupHeader(Listing_Standard ui, PawnGroupMakerEdit group, int index, bool expanded)
    {
        Rect row = ui.GetRect(28f);
        Widgets.DrawHighlightIfMouseover(row);

        // Expand toggle (▶ / ▼)
        string toggleLabel = expanded ? "▼" : "▶";
        Rect toggleRect = new(row.x, row.y + 2f, 20f, 24f);
        if (Widgets.ButtonText(toggleRect, toggleLabel, drawBackground: false))
        {
            if (expanded)
            {
                _expanded.Remove(index);
            }
            else
            {
                _expanded.Add(index);
            }
        }

        // Delete button (right side)
        int capturedIndex = index;
        Rect delRect = new(row.xMax - 28f, row.y + 2f, 24f, 24f);
        GUI.color = Color.red;
        if (Widgets.ButtonText(delRect, "×"))
        {
            Find.WindowStack.Add(
                Dialog_MessageBox.CreateConfirmation(
                    "FactionLoadout_GroupEditor_DeleteConfirmBody".Translate(),
                    () => _pendingDeleteIndex = capturedIndex,
                    destructive: true,
                    title: "FactionLoadout_GroupEditor_DeleteConfirmTitle".Translate()
                )
            );
        }

        GUI.color = Color.white;

        // Label area (between toggle and delete)
        Rect labelRect = new(row.x + 24f, row.y, row.width - 56f, row.height);

        // Build label string
        string kindLabel = group.KindDef?.label ?? group.KindDefName;
        if (string.IsNullOrEmpty(kindLabel))
            kindLabel = "?";

        string newTag = group.IsNew ? $" <color=#ffd700>{"FactionLoadout_GroupEditor_NewTag".Translate()}</color>" : "";

        string pawnCount = "FactionLoadout_GroupEditor_PawnCount".Translate(group.TotalKindCount);
        string emptyWarn = group.TotalKindCount == 0 ? $"  <color=#ff9900>{"FactionLoadout_GroupEditor_EmptyWarning".Translate()}</color>" : "";

        string maxStr = group.MaxTotalPoints >= 9999999f ? "" : $"  max {group.MaxTotalPoints:0}";
        string summary = $"<b>{kindLabel}</b>{newTag}  <color=grey>commonality {group.Commonality:0}{maxStr}  {pawnCount}</color>{emptyWarn}";

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, summary);
        Text.Anchor = TextAnchor.UpperLeft;

        if (group.TotalKindCount == 0)
            TooltipHandler.TipRegion(labelRect, "FactionLoadout_GroupEditor_EmptyGroupTooltip".Translate());

        // Clicking the row (except buttons) toggles expand
        Rect clickable = new(row.x + 24f, row.y, row.width - 56f, row.height);
        if (Widgets.ButtonInvisible(clickable))
        {
            if (expanded)
            {
                _expanded.Remove(index);
            }
            else
            {
                _expanded.Add(index);
            }
        }
    }

    private void DrawGroupBody(Listing_Standard ui, PawnGroupMakerEdit group, int groupIndex)
    {
        // Indented background
        Rect bodyBg = ui.GetRect(0f); // zero-height placeholder to get current y
        float startY = bodyBg.y;
        const float indent = 16f;

        Listing_Standard body = new();
        Rect bodyArea = new(ui.curX + indent, startY, ui.ColumnWidth - indent, 9999f);
        body.Begin(bodyArea);

        // ── Group Type ───────────────────────────────────────────────────────
        DrawLabeledButton(
            body,
            "FactionLoadout_GroupEditor_GroupType".Translate(),
            "FactionLoadout_GroupEditor_GroupTypeTooltip".Translate(),
            group.KindDef?.label ?? group.KindDefName,
            () =>
            {
                EnsureGroupKinds();
                FloatMenuUtility.MakeMenu(_allGroupKinds, gk => $"{gk.label} ({gk.defName})", gk => () => group.KindDefName = gk.defName);
            }
        );

        // ── Commonality ──────────────────────────────────────────────────────
        group.Commonality = DrawLabeledFloat(
            body,
            groupIndex,
            "commonality",
            "FactionLoadout_GroupEditor_Commonality".Translate(),
            "FactionLoadout_GroupEditor_CommonalityTooltip".Translate(),
            group.Commonality,
            0f
        );

        // ── Max Group Points ─────────────────────────────────────────────────
        group.MaxTotalPoints = DrawLabeledFloat(
            body,
            groupIndex,
            "maxPoints",
            "FactionLoadout_GroupEditor_MaxPoints".Translate(),
            "FactionLoadout_GroupEditor_MaxPointsTooltip".Translate(),
            group.MaxTotalPoints,
            0f
        );

        // ── Block Strategies ─────────────────────────────────────────────────
        string stratLabel =
            group.DisallowedStrategyDefNames == null || group.DisallowedStrategyDefNames.Count == 0
                ? "FactionLoadout_GroupEditor_BlockStrategiesNone".Translate().ToString()
                : string.Join(", ", group.DisallowedStrategyDefNames.Select(n => DefDatabase<RaidStrategyDef>.GetNamedSilentFail(n)?.label ?? n));
        DrawLabeledButton(
            body,
            "FactionLoadout_GroupEditor_BlockStrategies".Translate(),
            "FactionLoadout_GroupEditor_BlockStrategiesTooltip".Translate(),
            stratLabel,
            () => OpenStrategyMenu(group)
        );

        body.GapLine();

        // ── Pawn sub-lists ───────────────────────────────────────────────────
        DrawPawnList(
            body,
            groupIndex,
            "options",
            "FactionLoadout_GroupEditor_CombatPawns".Translate(),
            "FactionLoadout_GroupEditor_CombatPawnsTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddCombatPawn".Translate(),
            group.Options
        );

        DrawPawnList(
            body,
            groupIndex,
            "guards",
            "FactionLoadout_GroupEditor_Guards".Translate(),
            "FactionLoadout_GroupEditor_GuardsTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddGuard".Translate(),
            group.Guards
        );

        DrawPawnList(
            body,
            groupIndex,
            "traders",
            "FactionLoadout_GroupEditor_Traders".Translate(),
            "FactionLoadout_GroupEditor_TradersTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddTrader".Translate(),
            group.Traders
        );

        DrawPawnList(
            body,
            groupIndex,
            "carriers",
            "FactionLoadout_GroupEditor_Carriers".Translate(),
            "FactionLoadout_GroupEditor_CarriersTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddCarrier".Translate(),
            group.Carriers
        );

        body.Gap(4f);
        float bodyHeight = body.CurHeight;
        body.End();

        // Draw a subtle background tint under the expanded area
        Widgets.DrawBoxSolid(new Rect(ui.curX + indent, startY, ui.ColumnWidth - indent, bodyHeight), new Color(1f, 1f, 1f, 0.04f));

        // Advance the parent listing by the body height
        ui.GetRect(bodyHeight);
        ui.GapLine();
    }

    // ── Field helpers ──────────────────────────────────────────────────────

    private void DrawLabeledButton(Listing_Standard ui, string label, string tooltip, string value, Action onClick)
    {
        const float labelW = 160f;
        Rect row = ui.GetRect(28f);
        Rect labelRect = new(row.x, row.y, labelW, row.height);
        Rect btnRect = new(row.x + labelW, row.y, row.width - labelW - 4f, 24f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(labelRect, tooltip);

        if (Widgets.ButtonText(btnRect, value))
            onClick?.Invoke();
    }

    private float DrawLabeledFloat(Listing_Standard ui, int groupIndex, string fieldId, string label, string tooltip, float value, float min)
    {
        const float labelW = 160f;
        const float fieldW = 90f;

        Rect row = ui.GetRect(28f);
        Rect labelRect = new(row.x, row.y, labelW, row.height);
        Rect fieldRect = new(row.x + labelW, row.y + 2f, fieldW, 24f);
        Rect tipRect = new(row.x + labelW + fieldW + 4f, row.y, 20f, row.height);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(labelRect, tooltip);

        // Get or init buffer
        if (!_numBuffers.TryGetValue((groupIndex, fieldId), out string buf))
            buf = value.ToString("0.##");

        string newBuf = Widgets.TextField(fieldRect, buf);
        _numBuffers[(groupIndex, fieldId)] = newBuf;

        // "(?) " tooltip hint
        GUI.color = Color.grey;
        Widgets.Label(tipRect, "(?)");
        GUI.color = Color.white;
        TooltipHandler.TipRegion(tipRect, tooltip);

        return float.TryParse(newBuf, out float parsed) ? Mathf.Max(min, parsed) : value;
    }

    private void DrawPawnList(Listing_Standard ui, int groupIndex, string listId, string sectionLabel, string sectionTooltip, string addButtonLabel, List<PawnGenOptionEdit> list)
    {
        // Section header row
        Rect headerRow = ui.GetRect(24f);
        float addBtnW = Mathf.Max(120f, Text.CalcSize(addButtonLabel).x + 16f);
        Rect headerLabel = new(headerRow.x, headerRow.y, headerRow.width - addBtnW - 4f, headerRow.height);
        Rect addBtn = new(headerRow.xMax - addBtnW, headerRow.y, addBtnW, 22f);

        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        Widgets.Label(headerLabel, $"<b>{sectionLabel}</b>");
        Text.Anchor = TextAnchor.UpperLeft;

        // "(?) " tooltip after header
        Rect tipRect = new(headerLabel.xMax, headerRow.y, 20f, headerRow.height);
        GUI.color = Color.grey;
        Widgets.Label(tipRect, "(?)");
        GUI.color = Color.white;
        TooltipHandler.TipRegion(tipRect, sectionTooltip);

        if (Widgets.ButtonText(addBtn, addButtonLabel))
        {
            Find.WindowStack.Add(
                new Dialog_PawnKindPicker(
                    sectionLabel,
                    list,
                    defName =>
                    {
                        list.Add(new PawnGenOptionEdit { KindDefName = defName, SelectionWeight = 1f });
                    }
                )
            );
        }

        ui.Gap(2f);

        if (list.Count == 0)
        {
            GUI.color = Color.grey;
            ui.Label("<i>" + "FactionLoadout_GroupEditor_NoPawns".Translate() + "</i>");
            GUI.color = Color.white;
        }
        else
        {
            List<PawnGenOptionEdit> toRemove = [];
            for (int i = 0; i < list.Count; i++)
            {
                PawnGenOptionEdit entry = list[i];
                string entryBufKey = $"{groupIndex}_{listId}_{i}";

                Rect row = ui.GetRect(24f);
                Widgets.DrawHighlightIfMouseover(row);

                // Name label
                string kindLabel = entry.KindDef?.LabelCap ?? entry.KindDefName;
                if (string.IsNullOrEmpty(kindLabel))
                    kindLabel = "<unknown>";
                bool missing = entry.KindDef == null;
                if (missing)
                    GUI.color = Color.grey;

                Rect nameLbl = new(row.x, row.y, row.width - 148f, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameLbl, missing ? $"<color=grey>{kindLabel} (missing)</color>" : kindLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                // "weight" label
                Rect weightLbl = new(row.xMax - 146f, row.y, 48f, row.height);
                GUI.color = Color.grey;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(weightLbl, "FactionLoadout_GroupEditor_WeightLabel".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                TooltipHandler.TipRegion(weightLbl, "FactionLoadout_GroupEditor_WeightTooltip".Translate());

                // Weight field
                Rect weightField = new(row.xMax - 86f, row.y + 1f, 56f, 22f);
                if (!_numBuffers.TryGetValue((groupIndex, entryBufKey), out string wbuf))
                    wbuf = entry.SelectionWeight.ToString("0.##");

                string newWbuf = Widgets.TextField(weightField, wbuf);
                _numBuffers[(groupIndex, entryBufKey)] = newWbuf;
                if (float.TryParse(newWbuf, out float parsedW))
                    entry.SelectionWeight = Mathf.Max(0.01f, parsedW);

                // Remove button
                Rect delBtn = new(row.xMax - 26f, row.y + 2f, 22f, 22f);
                GUI.color = Color.red;
                if (Widgets.ButtonText(delBtn, "×"))
                    toRemove.Add(entry);
                GUI.color = Color.white;
            }

            foreach (PawnGenOptionEdit e in toRemove)
                list.Remove(e);
        }

        ui.GapLine();
    }

    // ── Add group menu ─────────────────────────────────────────────────────

    private void OpenAddGroupMenu()
    {
        EnsureGroupKinds();
        FloatMenuUtility.MakeMenu(
            _allGroupKinds,
            gk => $"{gk.label} ({gk.defName})",
            gk =>
                () =>
                {
                    List<PawnGroupMakerEdit> groups = _edit.GetOrInitPawnGroupMakerEdits();
                    PawnGroupMakerEdit newGroup = new() { IsUserAdded = true, KindDefName = gk.defName };
                    groups.Add(newGroup);
                    _expanded.Add(groups.Count - 1);
                }
        );
    }

    // --- Strategy multi-select menu ---

    private void OpenStrategyMenu(PawnGroupMakerEdit group)
    {
        EnsureRaidStrategies();
        group.DisallowedStrategyDefNames ??= [];

        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (RaidStrategyDef strat in _allRaidStrategies)
        {
            bool current = group.DisallowedStrategyDefNames.Contains(strat.defName);
            string mark = current ? "✓ " : "   ";
            string stratDef = strat.defName; // capture for closure
            options.Add(
                new FloatMenuOption(
                    $"{mark}{strat.label ?? strat.defName}",
                    () =>
                    {
                        if (group.DisallowedStrategyDefNames.Contains(stratDef))
                            group.DisallowedStrategyDefNames.Remove(stratDef);
                        else
                            group.DisallowedStrategyDefNames.Add(stratDef);
                    }
                )
            );
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    // ── Reset confirm ──────────────────────────────────────────────────────

    private void OpenResetConfirm()
    {
        Find.WindowStack.Add(new Dialog_ResetGroupsConfirm(_edit));
    }

    // ── Static cache helpers ───────────────────────────────────────────────

    private static void EnsureGroupKinds()
    {
        if (_allGroupKinds != null)
            return;
        _allGroupKinds = DefDatabase<PawnGroupKindDef>.AllDefsListForReading.OrderBy(gk => gk.label ?? gk.defName).ToList();
    }

    private static void EnsureRaidStrategies()
    {
        if (_allRaidStrategies != null)
            return;
        _allRaidStrategies = DefDatabase<RaidStrategyDef>.AllDefsListForReading.OrderBy(rs => rs.label ?? rs.defName).ToList();
    }
}

// ── Dialog_PawnKindPicker ──────────────────────────────────────────────────

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

        // Search field
        _search = ui.TextEntry(_search);
        ui.Gap(4f);

        // Scroll list
        float scrollH = Mathf.Max(60f, inRect.height - ui.CurHeight - 70f);
        Rect scrollOut = ui.GetRect(scrollH);
        List<PawnKindDef> filtered = string.IsNullOrWhiteSpace(_search)
            ? _allKinds
            : _allKinds
                .Where(k => k.LabelCap.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || k.defName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
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

// ── Dialog_ResetGroupsConfirm ──────────────────────────────────────────────

/// <summary>
/// Confirmation dialog shown before resetting all group edits.
/// Lists any pawnkinds that were added by the user and will become orphaned.
/// </summary>
public class Dialog_ResetGroupsConfirm : Window
{
    private readonly FactionEdit _edit;
    private List<string> _addedKindNames;

    public Dialog_ResetGroupsConfirm(FactionEdit edit)
    {
        _edit = edit;
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
        draggable = false;

        // Find pawnkinds that are in GroupEdits but NOT in the original faction def.
        _addedKindNames = new List<string>();
        if (edit.PawnGroupMakerEdits != null)
        {
            var original = new HashSet<string>(
                FactionEdit.GetAllPawnKinds(FactionEdit.TryGetOriginal(edit.Faction.DefName) ?? edit.Faction.Def ?? new FactionDef()).Select(k => k.defName)
            );

            foreach (PawnGroupMakerEdit g in edit.PawnGroupMakerEdits)
            {
                foreach (PawnKindDef k in g.GetAllKinds())
                {
                    if (!original.Contains(k.defName) && !_addedKindNames.Contains(k.LabelCap.ToString()))
                        _addedKindNames.Add(k.LabelCap);
                }
            }
        }
    }

    public override Vector2 InitialSize => new(480f, _addedKindNames.Count > 0 ? 300f : 180f);

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard ui = new();
        ui.Begin(inRect);

        Text.Font = GameFont.Medium;
        ui.Label("FactionLoadout_GroupEditor_ResetConfirmTitle".Translate());
        Text.Font = GameFont.Small;

        ui.GapLine();
        ui.Label("FactionLoadout_GroupEditor_ResetConfirmBody".Translate());

        if (_addedKindNames.Count > 0)
        {
            ui.Gap(6f);
            GUI.color = new Color(1f, 0.7f, 0.2f);
            ui.Label("FactionLoadout_GroupEditor_ResetConfirmOrphans".Translate(_addedKindNames.Count));
            GUI.color = Color.white;
            foreach (string name in _addedKindNames)
                ui.Label($"  · {name}");
            ui.Gap(4f);
            GUI.color = Color.grey;
            ui.Label("<i>" + "FactionLoadout_GroupEditor_ResetConfirmOrphanNote".Translate() + "</i>");
            GUI.color = Color.white;
        }

        ui.GapLine();

        Rect btnRow = ui.GetRect(28f);
        if (Widgets.ButtonText(new Rect(btnRow.x, btnRow.y, 100f, 24f), "Cancel".Translate()))
            Close();

        GUI.color = Color.red;
        if (Widgets.ButtonText(new Rect(btnRow.xMax - 120f, btnRow.y, 120f, 24f), "FactionLoadout_GroupEditor_ResetConfirmButton".Translate()))
        {
            _edit.ResetGroupEdits();
            Close();
        }

        GUI.color = Color.white;
        ui.End();
    }
}
