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
/// Edits one faction: a two-panel layout with the pawnkind-edit list on the left and
/// faction-level settings (tech level, xenotypes, spawn groups, module UI) on the right.
/// Extracted from the old <c>FactionEditUI.DoWindowContents</c>, minus the preview block
/// (the live preview lands on the Pawn Edit screen in Phase C).
/// </summary>
[HotSwappable]
public class FactionEditScreen
{
    public readonly TotalControlController Controller;
    public readonly FactionEdit Current;

    private readonly List<PawnKindEdit> bin = [];
    private readonly HashSet<PawnKindDef> tempKinds = [];

    private Vector2 kindsScroll;
    private Vector2 settingsScroll;
    private float settingsContentHeight = 10000f; // measured after first frame; large so nothing clips

    /// <summary>Right-column mode: the faction settings form, or the live roster gallery.</summary>
    public enum RightMode
    {
        Settings,
        Roster,
    }

    private RightMode rightMode = RightMode.Settings;
    private FactionRosterWidget roster;

    public FactionEditScreen(TotalControlController controller, FactionEdit fac)
    {
        Controller = controller;
        Current = fac;
    }

    public void Dispose()
    {
        roster?.Dispose();
        roster = null;
    }

    public void Draw(Rect inRect)
    {
        if (Current == null || Current.DeletedOrClosed)
            return;

        // --- Header block (title + warnings + clipboard toolbar) ---
        Listing_Standard head = new();
        head.Begin(new Rect(inRect.x, inRect.y, inRect.width, 240f));

        Rect titleRect = head.GetRect(44f);
        Widgets.Label(
            titleRect,
            $"<size=30><b>{"FactionLoadout_TC_FactionHeader".Translate()}: <color=#cf9af5>{Current.Faction.Def?.LabelCap ?? "None".Translate()}</color></b></size>"
        );

        if (Current.Faction.IsMissing)
            head.Label($"<color=orange>{"FactionLoadout_FactionMissingEditWarning".Translate()}</color>");
        if (Current.Faction.DefName == Preset.SpecialCreepjoinerFactionDefName)
            head.Label($"<color=yellow>{"FactionLoadout_FactionEdit_ExperimentalCreepjoiner".Translate()}</color>");
        if (Current.Faction.DefName == Preset.SpecialWildManFactionDefName)
            head.Label($"<color=yellow>{"FactionLoadout_FactionEdit_ExperimentalWildMan".Translate()}</color>");
        if (Current.Faction.DefName == Preset.SpecialFactionlessPawnsFactionDefName)
            head.Label($"<color=yellow>{"FactionLoadout_Special_FactionlessWarning".Translate()}</color>");

        if (!Current.Faction.IsMissing)
            DrawFactionClipboardToolbar(head);

        float headH = head.CurHeight;
        head.End();

        // --- Two columns ---
        float colsY = inRect.y + headH + 6f;
        float colsH = inRect.yMax - colsY;
        float leftW = inRect.width * 0.36f;
        const float gap = 10f;
        Rect leftRect = new(inRect.x, colsY, leftW, colsH);
        Rect rightRect = new(inRect.x + leftW + gap, colsY, inRect.width - leftW - gap, colsH);

        DrawKindList(leftRect);
        DrawRightColumn(rightRect);
    }

    // ---------------------------------------------- right column: settings / roster toggle

    private void DrawRightColumn(Rect rect)
    {
        // The roster render needs a concrete faction def to clone + generate against; for
        // missing factions there's nothing to render, so show settings only (no toggle).
        bool canRender = !Current.Faction.IsMissing && Current.Faction.Def != null;
        if (!canRender)
        {
            DrawFactionSettings(rect);
            return;
        }

        Rect toggleRow = new(rect.x, rect.y, rect.width, 30f);
        float half = (toggleRow.width - 6f) / 2f;
        Rect settingsBtn = new(toggleRow.x, toggleRow.y, half, toggleRow.height);
        Rect rosterBtn = new(toggleRow.x + half + 6f, toggleRow.y, half, toggleRow.height);
        DrawModeButton(settingsBtn, RightMode.Settings, "FactionLoadout_FactionEdit_ModeSettings".Translate());
        DrawModeButton(rosterBtn, RightMode.Roster, "FactionLoadout_FactionEdit_ModeRoster".Translate());

        Rect body = new(rect.x, toggleRow.yMax + 6f, rect.width, rect.yMax - (toggleRow.yMax + 6f));
        if (rightMode == RightMode.Settings)
        {
            DrawFactionSettings(body);
        }
        else
        {
            roster ??= new FactionRosterWidget(Current);
            roster.Draw(body, Controller);
        }
    }

    private void DrawModeButton(Rect rect, RightMode mode, string label)
    {
        bool selected = rightMode == mode;
        Color bg = selected ? new Color32(49, 82, 133, 255) : new Color(0.2f, 0.2f, 0.2f, 1f);
        Rect r = rect;
        if (Widgets.CustomButtonText(ref r, $"<b>{label}</b>", bg, Color.white, Color.white))
            rightMode = mode;
    }

    // ----------------------------------------------------------- left: pawnkinds

    private void DrawKindList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(6f);

        Rect titleR = new(rect.x, rect.y, rect.width, 24f);
        Widgets.Label(titleR, $"<b>{"FactionLoadout_FactionEdit_LoadoutOverrides".Translate()}</b>");

        // "Add..." button pinned at the bottom of the panel.
        Rect addBtn = new(rect.x, rect.yMax - 30f, rect.width, 30f);
        float listY = titleR.yMax + 4f;
        Rect scrollOut = new(rect.x, listY, rect.width, addBtn.y - listY - 6f);

        const float rowH = 32f;
        float contentH = Mathf.Max(Current.KindEdits.Count * rowH + 6f, scrollOut.height);
        Rect view = new(0, 0, scrollOut.width - 16f, contentH);

        HashSet<PawnKindDef> orphanedKinds = Current?.GetOrphanedKinds() ?? [];

        Widgets.BeginScrollView(scrollOut, ref kindsScroll, view);
        for (int i = 0; i < Current.KindEdits.Count; i++)
        {
            PawnKindEdit edit = Current.KindEdits[i];
            Rect row = new(0, i * rowH, view.width, rowH - 2f);
            if (i % 2 == 1)
                Widgets.DrawHighlight(row);
            DrawKindRow(row, edit, orphanedKinds);
        }
        Widgets.EndScrollView();

        foreach (PawnKindEdit item in bin)
            Current.KindEdits.Remove(item);
        bin.Clear();

        if (!Current.Faction.IsMissing && Widgets.ButtonText(addBtn, "Add".Translate().CapitalizeFirst() + "..."))
            OpenAddKindMenu();
    }

    private void DrawKindRow(Rect row, PawnKindEdit edit, HashSet<PawnKindDef> orphanedKinds)
    {
        float x = row.x + 2f;
        float y = row.y + 2f;

        string delText = "Delete".Translate();
        float delW = Mathf.Max(38f, Text.CalcSize(delText).x + 10f);
        GUI.color = Color.red;
        if (Widgets.ButtonText(new Rect(x, y, delW, 24f), delText))
        {
            bin.Add(edit);
            edit.DeletedOrClosed = true;
        }
        GUI.color = Color.white;
        x += delW + 4f;

        string editText = "FactionLoadout_Edit".Translate().CapitalizeFirst();
        float editW = Mathf.Max(50f, Text.CalcSize(editText).x + 10f);
        if (Widgets.ButtonText(new Rect(x, y, editW, 24f), editText))
            Controller.OpenKind(edit);
        x += editW + 4f;

        if (Widgets.ButtonImageFitted(new Rect(x, y, 24f, 24f), TexButton.Copy))
            PawnKindClipboard.Copy(edit);
        TooltipHandler.TipRegion(new Rect(x, y, 24f, 24f), "FactionLoadout_Clipboard_CopyTooltip".Translate());
        x += 28f;

        if (PawnKindClipboard.HasData)
        {
            if (Widgets.ButtonImageFitted(new Rect(x, y, 24f, 24f), TexButton.Paste))
                PawnKindClipboard.PasteAll(edit);
            TooltipHandler.TipRegion(new Rect(x, y, 24f, 24f), "FactionLoadout_Clipboard_PasteAllTooltip".Translate(PawnKindClipboard.GetDescription()));
        }
        else
        {
            GUI.color = Color.gray;
            Widgets.DrawTextureFitted(new Rect(x, y, 24f, 24f), TexButton.Paste, 1f);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(x, y, 24f, 24f), "FactionLoadout_Clipboard_Empty".Translate());
        }
        x += 28f;

        bool isOrphaned = !edit.IsGlobal && edit.Def != null && orphanedKinds.Contains(edit.Def);
        if (isOrphaned)
        {
            GUI.color = Color.yellow;
            Widgets.Label(new Rect(x, y, 20f, 24f), "⚠");
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(x, y, 20f, 24f), "FactionLoadout_SpawnGroups_OrphanKindTooltip".Translate());
            x += 22f;
        }

        Rect labelRect = new(x, row.y, row.xMax - x, row.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, $"<b>{(edit.IsGlobal ? $"<color=cyan>{"FactionLoadout_GlobalLabel".Translate()}</color>" : edit.Def.LabelCap.ToString())}</b>");
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // ------------------------------------------------------ right: faction settings

    private void DrawFactionSettings(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(6f);

        float innerH = Mathf.Max(settingsContentHeight + 40f, rect.height);
        Rect view = new(0, 0, rect.width - 16f, innerH);

        Widgets.BeginScrollView(rect, ref settingsScroll, view);
        Listing_Standard ui = new() { ColumnWidth = view.width };
        ui.Begin(view);

        // Tech level.
        if (
            ui.ButtonTextLabeled(
                "FactionLoadout_Faction_Techlevel".Translate(),
                Current.TechLevel?.ToStringHuman() ?? "FactionLoadout_NotOverriden_WithDefault".Translate((Current.Faction?.Def?.techLevel ?? TechLevel.Undefined).ToStringHuman())
            )
        )
        {
            IEnumerable<TechLevel?> enums = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel?>().Append(null);
            FloatMenuUtility.MakeMenu(
                enums,
                e => e?.ToStringHuman() ?? "FactionLoadout_NotOverriden_WithDefault".Translate((Current.Faction?.Def?.techLevel ?? TechLevel.Undefined).ToStringHuman()),
                e => () => Current.TechLevel = e
            );
        }

        // Forced primary ideology (Ideology). Skipped for the synthetic special factions.
        if (
            ModsConfig.IdeologyActive
            && Current.Faction is { IsMissing: false }
            && Current.Faction?.Def != Preset.SpecialWildManFaction
            && Current.Faction?.Def != Preset.SpecialCreepjoinerFaction
            && Current.Faction?.Def != Preset.SpecialFactionlessPawnsFaction
        )
        {
            ui.GapLine();
            string primaryLabel =
                ForcedIdeoRefUI.DisabledByClassicMode ? "FactionLoadout_General_IdeoClassicDisabled".Translate().ToString()
                : string.IsNullOrEmpty(Current.ForcedPrimaryIdeoKey) ? "FactionLoadout_Faction_PrimaryIdeoNotOverridden".Translate().ToString()
                : ForcedIdeoRefUI.DisplayName(Current.ForcedPrimaryIdeoSourceKind, Current.ForcedPrimaryIdeoKey);
            if (
                ui.ButtonTextLabeled("FactionLoadout_Faction_PrimaryIdeo".Translate(), primaryLabel, tooltip: "FactionLoadout_Faction_PrimaryIdeoTooltip".Translate())
                && !ForcedIdeoRefUI.DisabledByClassicMode
            )
            {
                ForcedIdeoRefUI.OpenPicker(
                    includeFactionPrimary: false,
                    (source, key) =>
                    {
                        Current.ForcedPrimaryIdeoSourceKind = source;
                        Current.ForcedPrimaryIdeoKey = key;
                    },
                    onClear: () => Current.ForcedPrimaryIdeoKey = null,
                    clearLabel: "FactionLoadout_Faction_PrimaryIdeoNotOverridden".Translate().ToString()
                );
            }
        }

        // Xenotype spawn rates (Biotech).
        if (
            ModsConfig.BiotechActive
            && Current.Faction is { IsMissing: false }
            && Current.Faction?.Def != Preset.SpecialWildManFaction
            && Current.Faction?.Def != Preset.SpecialFactionlessPawnsFaction
        )
        {
            if (!Current.OverrideFactionXenotypes)
            {
                Current.xenotypeChances.Clear();
                Current.xenotypeChancesByDef.Clear();
            }

            ui.GapLine();
            string xenoState = Current.OverrideFactionXenotypes
                ? "FactionLoadout_Xenotype_ActiveCount".Translate(Current.xenotypeChances.Count)
                : "FactionLoadout_Xenotype_Off".Translate();
            if (ui.ButtonTextLabeled("FactionLoadout_EditXenoSpawnRates".Translate(), xenoState))
                Find.WindowStack.Add(new Dialog_XenotypeEdit(Current));
        }

        // Spawn groups.
        if (Current.Faction is not { IsMissing: true })
        {
            ui.GapLine();

            Rect groupsRow = ui.GetRect(28f);
            const float editBtnW = 160f;
            Rect editGroupsBtn = new(groupsRow.xMax - editBtnW, groupsRow.y, editBtnW, 24f);

            Text.Anchor = TextAnchor.MiddleLeft;
            string groupsSummary;
            if (Current.PawnGroupMakerEdits != null)
                groupsSummary = "FactionLoadout_SpawnGroups_SummaryModified".Translate(
                    Current.PawnGroupMakerEdits.Count,
                    "FactionLoadout_GroupEditor_NewTag".Translate().ToString().ToLower()
                );
            else
                groupsSummary = "FactionLoadout_SpawnGroups_Summary".Translate(Current?.Faction?.Def?.pawnGroupMakers?.Count ?? 0);

            Rect summaryLabelRect = new(groupsRow.x, groupsRow.y, groupsRow.width - editBtnW - 4f, groupsRow.height);
            GUI.color = Color.grey;
            Widgets.Label(summaryLabelRect, "FactionLoadout_SpawnGroups_Label".Translate() + "  " + groupsSummary);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(editGroupsBtn, "FactionLoadout_SpawnGroups_EditButton".Translate()))
                GroupEditorUI.OpenEditor(Current);

            HashSet<PawnKindDef> orphaned = Current?.GetOrphanedKinds() ?? [];
            if (orphaned.Count > 0)
            {
                string names = orphaned.Select(k => k.LabelCap.ToString()).OrderBy(n => n).ToCommaList();
                string warnText = "FactionLoadout_SpawnGroups_OrphanWarning".Translate(names);
                Rect warnRow = ui.GetRect(Text.CalcHeight(warnText, ui.ColumnWidth));
                GUI.color = new Color(1f, 0.6f, 0.1f);
                Widgets.Label(warnRow, warnText);
                GUI.color = Color.white;
            }
        }

        // Active modules contribute faction-level UI here (unchanged contract).
        foreach (ITotalControlModule module in ModuleRegistry.Modules)
        {
            if (!module.IsActive)
                continue;
            try
            {
                module.AddFactionUI(Current, ui);
            }
            catch (Exception e)
            {
                ModCore.Error($"Error drawing faction UI for module '{module.ModuleName}'", e);
            }
        }

        settingsContentHeight = ui.CurHeight;
        ui.End();
        Widgets.EndScrollView();
    }

    // ------------------------------------------------------------------- helpers

    private void DrawFactionClipboardToolbar(Listing_Standard ui)
    {
        Rect toolbar = ui.GetRect(28f);
        float x = toolbar.x;
        float y = toolbar.y;
        const float btnSize = 24f;
        const float gap = 4f;

        if (Widgets.ButtonImageFitted(new Rect(x, y, btnSize, btnSize), TexButton.Copy))
            FactionEditClipboard.Copy(Current);
        TooltipHandler.TipRegion(new Rect(x, y, btnSize, btnSize), "FactionLoadout_FactionClipboard_CopyTooltip".Translate());

        x += btnSize + gap;
        if (FactionEditClipboard.HasData)
        {
            if (Widgets.ButtonImageFitted(new Rect(x, y, btnSize, btnSize), TexButton.Paste))
                FactionEditClipboard.PasteAll(Current);
            TooltipHandler.TipRegion(new Rect(x, y, btnSize, btnSize), "FactionLoadout_FactionClipboard_PasteTooltip".Translate(FactionEditClipboard.GetDescription()));
        }
        else
        {
            GUI.color = Color.gray;
            Widgets.DrawTextureFitted(new Rect(x, y, btnSize, btnSize), TexButton.Paste, 1f);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(x, y, btnSize, btnSize), "FactionLoadout_Clipboard_Empty".Translate());
        }
    }

    private void OpenAddKindMenu()
    {
        IEnumerable<PawnKindDef> MakeKinds()
        {
            tempKinds.Clear();
            if (!Current.HasGlobalEditor())
                tempKinds.Add(null);

            foreach (PawnKindDef kind in Current.GetAllKindDefsForUI())
            {
                if (!Current.HasEditFor(kind))
                    tempKinds.Add(kind);
            }

            if (Current.PawnGroupMakerEdits != null)
            {
                if (Current.Faction.Def?.basicMemberKind != null && !Current.HasEditFor(Current.Faction.Def.basicMemberKind))
                    tempKinds.Add(Current.Faction.Def.basicMemberKind);
                if (Current.Faction.Def?.fixedLeaderKinds != null)
                {
                    foreach (PawnKindDef item in Current.Faction.Def.fixedLeaderKinds)
                    {
                        if (!Current.HasEditFor(item))
                            tempKinds.Add(item);
                    }
                }
            }

            foreach (PawnKindDef item in tempKinds)
                yield return item;

            if (tempKinds.Count(k => k != null) == 0)
            {
                if (Current.Faction.Def == FactionDefOf.Ancients || Current.Faction.Def == FactionDefOf.AncientsHostile)
                {
                    yield return PawnKindDefOf.AncientSoldier;
                    yield return PawnKindDefOf.Slave;
                }
            }

            tempKinds.Clear();
        }

        List<PawnKindDef> kinds = MakeKinds().ToList();
        List<MenuItemBase> items = CustomFloatMenu.MakeItems(
            kinds,
            k =>
                k != null
                    ? new MenuItemText(k, $"{k.LabelCap} ({k.defName})", tooltip: k.description)
                    : new MenuItemText(null, $"<color=cyan><b>{"FactionLoadout_GlobalLabel".Translate()}</b></color>")
        );
        CustomFloatMenu.Open(
            items,
            raw =>
            {
                PawnKindDef k = raw.GetPayload<PawnKindDef>();
                if (k != null)
                {
                    Current.KindEdits.Add(new PawnKindEdit(k));
                }
                else
                {
                    PawnKindDef kind = kinds.FirstOrDefault(pawnKindDef => pawnKindDef != null);
                    ModCore.Log($"Using {kind} as global base.");
                    if (kind != null)
                        Current.KindEdits.Insert(0, new PawnKindEdit(kind) { IsGlobal = true });
                }
            }
        );
    }
}
