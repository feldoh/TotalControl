using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport.DrawSupport;
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

    // Cached read-only view of vanilla groups, computed once when PawnGroupMakerEdits is null.
    private List<PawnGroupMakerEdit> _cachedVanillaGroups;

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

        // Read current edit state without triggering initialization.
        // When PawnGroupMakerEdits is null the faction is using vanilla groups; we build
        // a cached read-only view so users can see and expand groups before choosing to
        // customize.  Initialization only happens when the user clicks Customize Groups
        // or Add Group, so opening the editor has no side-effects on the preset data.
        List<PawnGroupMakerEdit> groups = _edit.PawnGroupMakerEdits;
        bool isReadOnly = groups == null;
        List<PawnGroupMakerEdit> displayGroups;
        if (isReadOnly)
        {
            if (_cachedVanillaGroups == null)
            {
                FactionDef srcDef = FactionEdit.TryGetOriginal(_edit.Faction.DefName) ?? _edit.Faction.Def;
                _cachedVanillaGroups = srcDef?.pawnGroupMakers?.Select(PawnGroupMakerEdit.FromPawnGroupMaker).ToList();
            }
            displayGroups = _cachedVanillaGroups;
        }
        else
        {
            _cachedVanillaGroups = null; // invalidate cache once editing starts
            displayGroups = groups;
        }

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
        if (isReadOnly)
        {
            if (Widgets.ButtonText(new Rect(toolbarRow.x, toolbarRow.y, btnW, 24f), "FactionLoadout_GroupEditor_CustomizeGroups".Translate()))
                _edit.GetOrInitPawnGroupMakerEdits();
        }
        else
        {
            if (Widgets.ButtonText(new Rect(toolbarRow.x, toolbarRow.y, btnW, 24f), "FactionLoadout_GroupEditor_AddGroup".Translate()))
                OpenAddGroupMenu();
        }

        GUI.color = isReadOnly ? Color.grey : Color.white;
        if (Widgets.ButtonText(new Rect(toolbarRow.xMax - 200f, toolbarRow.y, 200f, 24f), "FactionLoadout_GroupEditor_ResetButton".Translate()))
        {
            if (!isReadOnly)
                OpenResetConfirm();
        }

        GUI.color = Color.white;

        // Scroll view — inner rect sized from data model to avoid feedback loops
        float scrollOutH = Mathf.Max(60f, inRect.height - ui.CurHeight - 8f);
        Rect scrollOutRect = ui.GetRect(scrollOutH);
        float contentH = CalcTotalContentHeight(displayGroups) + 20f; // 20f safety margin
        Rect scrollViewRect = new(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(contentH, scrollOutH));

        Widgets.BeginScrollView(scrollOutRect, ref _scrollPos, scrollViewRect);
        Listing_Standard inner = new();
        inner.Begin(scrollViewRect);

        if (displayGroups == null || displayGroups.Count == 0)
        {
            GUI.color = Color.grey;
            inner.Label(isReadOnly ? "<i>" + "FactionLoadout_GroupEditor_VanillaGroups".Translate() + "</i>" : "<i>(" + "FactionLoadout_GroupEditor_NoPawns".Translate() + ")</i>");
            GUI.color = Color.white;
        }
        else
        {
            if (isReadOnly)
            {
                GUI.color = Color.grey;
                inner.Label("<i>" + "FactionLoadout_GroupEditor_VanillaGroups".Translate() + "</i>");
                GUI.color = Color.white;
                inner.Gap(4f);
            }
            DrawGroupList(inner, displayGroups, isReadOnly);
        }

        inner.End();
        Widgets.EndScrollView();

        ui.End();
    }

    // --- Height calculation (drives scroll inner rect sizing) ---

    private float CalcTotalContentHeight(List<PawnGroupMakerEdit> groups)
    {
        if (groups == null || groups.Count == 0)
            return 30f; // placeholder / "(none)" label

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
        h += PawnListDrawer.CalcHeight(group.Options);
        h += PawnListDrawer.CalcHeight(group.Guards);
        h += PawnListDrawer.CalcHeight(group.Traders);
        h += PawnListDrawer.CalcHeight(group.Carriers);
        h += 4f; // Gap(4f)
        return h;
    }

    // --- Group list ---

    private void DrawGroupList(Listing_Standard ui, List<PawnGroupMakerEdit> groups, bool readOnly)
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

            DrawGroupHeader(ui, group, i, expanded, readOnly);
            if (expanded)
                DrawGroupBody(ui, group, i, readOnly);
        }
    }

    private void DrawGroupHeader(Listing_Standard ui, PawnGroupMakerEdit group, int index, bool expanded, bool readOnly)
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

        // Delete button (right side) — edit mode only
        if (!readOnly)
        {
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
        }

        // Label area (between toggle and delete / end of row)
        float rightPad = readOnly ? 4f : 32f;
        Rect labelRect = new(row.x + 24f, row.y, row.width - 24f - rightPad, row.height);

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
        Rect clickable = new(row.x + 24f, row.y, row.width - 24f - rightPad, row.height);
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

    private void DrawGroupBody(Listing_Standard ui, PawnGroupMakerEdit group, int groupIndex, bool readOnly)
    {
        // Indented background
        Rect bodyBg = ui.GetRect(0f); // zero-height placeholder to get current y
        float startY = bodyBg.y;
        const float indent = 16f;

        Listing_Standard body = new();
        Rect bodyArea = new(ui.curX + indent, startY, ui.ColumnWidth - indent, 9999f);
        body.Begin(bodyArea);

        // --- Group Type ---
        if (readOnly)
        {
            LabeledRowDrawer.DrawLabeledText(body, "FactionLoadout_GroupEditor_GroupType".Translate(), group.KindDef?.label ?? group.KindDefName);
        }
        else
        {
            LabeledRowDrawer.DrawLabeledButton(
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
        }

        // --- Commonality ---
        if (readOnly)
        {
            LabeledRowDrawer.DrawLabeledText(body, "FactionLoadout_GroupEditor_Commonality".Translate(), group.Commonality.ToString("0.##"));
        }
        else
        {
            group.Commonality = DrawLabeledFloat(
                body,
                groupIndex,
                "commonality",
                "FactionLoadout_GroupEditor_Commonality".Translate(),
                "FactionLoadout_GroupEditor_CommonalityTooltip".Translate(),
                group.Commonality,
                0f
            );
        }

        // --- Max Group Points ---
        if (readOnly)
        {
            string maxStr = group.MaxTotalPoints >= 9999999f ? "∞" : group.MaxTotalPoints.ToString("0");
            LabeledRowDrawer.DrawLabeledText(body, "FactionLoadout_GroupEditor_MaxPoints".Translate(), maxStr);
        }
        else
        {
            group.MaxTotalPoints = DrawLabeledFloat(
                body,
                groupIndex,
                "maxPoints",
                "FactionLoadout_GroupEditor_MaxPoints".Translate(),
                "FactionLoadout_GroupEditor_MaxPointsTooltip".Translate(),
                group.MaxTotalPoints,
                0f
            );
        }

        // --- Block Strategies ---
        string stratLabel =
            group.DisallowedStrategyDefNames == null || group.DisallowedStrategyDefNames.Count == 0
                ? "FactionLoadout_GroupEditor_BlockStrategiesNone".Translate().ToString()
                : string.Join(", ", group.DisallowedStrategyDefNames.Select(n => DefDatabase<RaidStrategyDef>.GetNamedSilentFail(n)?.label ?? n));
        if (readOnly)
        {
            LabeledRowDrawer.DrawLabeledText(body, "FactionLoadout_GroupEditor_BlockStrategies".Translate(), stratLabel);
        }
        else
        {
            LabeledRowDrawer.DrawLabeledButton(
                body,
                "FactionLoadout_GroupEditor_BlockStrategies".Translate(),
                "FactionLoadout_GroupEditor_BlockStrategiesTooltip".Translate(),
                stratLabel,
                () => OpenStrategyMenu(group)
            );
        }

        body.GapLine();

        // --- Pawn sub-lists ---
        PawnListDrawer.Draw(
            body,
            groupIndex,
            "options",
            "FactionLoadout_GroupEditor_CombatPawns".Translate(),
            "FactionLoadout_GroupEditor_CombatPawnsTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddCombatPawn".Translate(),
            group.Options,
            readOnly,
            _numBuffers
        );
        PawnListDrawer.Draw(
            body,
            groupIndex,
            "guards",
            "FactionLoadout_GroupEditor_Guards".Translate(),
            "FactionLoadout_GroupEditor_GuardsTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddGuard".Translate(),
            group.Guards,
            readOnly,
            _numBuffers
        );
        PawnListDrawer.Draw(
            body,
            groupIndex,
            "traders",
            "FactionLoadout_GroupEditor_Traders".Translate(),
            "FactionLoadout_GroupEditor_TradersTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddTrader".Translate(),
            group.Traders,
            readOnly,
            _numBuffers
        );
        PawnListDrawer.Draw(
            body,
            groupIndex,
            "carriers",
            "FactionLoadout_GroupEditor_Carriers".Translate(),
            "FactionLoadout_GroupEditor_CarriersTooltip".Translate(),
            "FactionLoadout_GroupEditor_AddCarrier".Translate(),
            group.Carriers,
            readOnly,
            _numBuffers
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

    // --- Buffer-aware float field (delegates rendering to LabeledRowDrawer) ---

    private float DrawLabeledFloat(Listing_Standard ui, int groupIndex, string fieldId, string label, string tooltip, float value, float min)
    {
        if (!_numBuffers.TryGetValue((groupIndex, fieldId), out string buf))
            buf = value.ToString("0.##");
        float result = LabeledRowDrawer.DrawLabeledFloat(ui, label, tooltip, ref buf, value, min);
        _numBuffers[(groupIndex, fieldId)] = buf;
        return result;
    }

    // --- Add group menu ---

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

        List<FloatMenuOption> options = [];
        options.AddRange(
            from strat in _allRaidStrategies
            let current = @group.DisallowedStrategyDefNames.Contains(strat.defName)
            let mark = current ? "✓ " : "   "
            let stratDef = strat.defName
            select new FloatMenuOption(
                $"{mark}{strat.label ?? strat.defName}",
                () =>
                {
                    if (@group.DisallowedStrategyDefNames.Contains(stratDef))
                        @group.DisallowedStrategyDefNames.Remove(stratDef);
                    else
                        @group.DisallowedStrategyDefNames.Add(stratDef);
                }
            )
        );

        Find.WindowStack.Add(new FloatMenu(options));
    }

    // --- Reset confirm ---

    private void OpenResetConfirm()
    {
        Find.WindowStack.Add(new Dialog_ResetGroupsConfirm(_edit));
    }

    // --- Static cache helpers ---

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
