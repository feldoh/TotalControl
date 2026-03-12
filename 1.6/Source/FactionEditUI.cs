using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionLoadout.Patches;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

[HotSwappable]
public class FactionEditUI : Window
{
    public static string BaselinerDefName = "Baseliner";

    public readonly FactionEdit Current;

    private readonly List<PawnKindEdit> bin = new();
    private FactionDef clonedFac;
    private ThingFilterUI.UIState filterState = new();
    private int framesSinceF;
    private readonly List<Pawn> pawns = new();
    private readonly HashSet<PawnKindDef> tempKinds = new();
    private bool _ThingIDPatch = false;
    private bool _previewFailed = false;
    private Vector2 overridesScrollPos;

    public FactionEditUI(FactionEdit fac)
    {
        Current = fac;
        draggable = true;
        resizeable = true;
        doCloseX = true;
        closeOnCancel = true;
        closeOnClickedOutside = false;
    }

    public static void OpenEditor(FactionEdit fac)
    {
        if (fac == null)
            return;

        Find.WindowStack.Add(new FactionEditUI(fac));
    }

    public override void PostOpen()
    {
        base.PostOpen();
        Rect copy = windowRect;
        copy.y = 110;
        copy.x -= copy.width * 0.5f + 15;
        copy.height = 800;
        windowRect = copy;
    }

    public override void PostClose()
    {
        base.PostClose();

        DestroyPawns();
        clonedFac = null;

        Find.WindowStack.WindowOfType<PawnKindEditUI>()?.Close();
    }

    private void DestroyPawns()
    {
        foreach (Pawn pawn in pawns)
        {
            pawn?.Discard(true);
        }

        pawns.Clear();
    }

    public override void DoWindowContents(Rect inRect)
    {
        framesSinceF++;

        if (Current == null || Current.DeletedOrClosed)
        {
            Close();
            return;
        }

        Listing_Standard ui = new();
        ui.Begin(inRect);

        // --- Header (always visible) ---
        Rect r = ui.GetRect(50);
        Widgets.Label(r, $"<size=34><b>Faction: <color=#cf9af5>{Current.Faction.Def?.LabelCap ?? "none"}</color></b></size>");
        if (Current.Faction.IsMissing)
        {
            ui.Label($"<color=orange>{"FactionLoadout_FactionMissingEditWarning".Translate()}</color>");
        }

        if (Current.Faction.DefName == Preset.SpecialCreepjoinerFactionDefName)
            ui.Label("<color=yellow>EXPERIMENTAL! - This is a fake faction used for Creepjoiner editing, use at your own risk.</color>");
        if (Current.Faction.DefName == Preset.SpecialWildManFactionDefName)
            ui.Label("<color=yellow>EXPERIMENTAL! - This is a fake faction used for WildMan editing, use at your own risk.</color>");
        if (Current.Faction.DefName == Preset.SpecialFactionlessPawnsFactionDefName)
            ui.Label($"<color=yellow>{"FactionLoadout_Special_FactionlessWarning".Translate()}</color>");

        // Disabled for now
        // DrawMaterialFilter(ui);

        if (!Current.Faction.IsMissing)
        {
            DrawFactionClipboardToolbar(ui);
        }

        // --- Scrollable overrides ---
        // Cap scroll height so at least 200px remains for the preview section below.
        const float minFooterHeight = 200f;
        float available = Mathf.Max(60f, inRect.height - ui.CurHeight - minFooterHeight);
        float innerW = inRect.width - 16f; // scrollbar width
        float contentH = CalcOverridesContentHeight(innerW);
        float scrollOutHeight = Mathf.Clamp(contentH, 60f, available);
        Rect scrollOutRect = ui.GetRect(scrollOutHeight);
        Rect scrollViewRect = new(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(contentH, scrollOutHeight));

        Widgets.BeginScrollView(scrollOutRect, ref overridesScrollPos, scrollViewRect);
        Listing_Standard inner = new();
        inner.Begin(scrollViewRect);

        if (
            inner.ButtonTextLabeled(
                "FactionLoadout_Faction_Techlevel".Translate(),
                Current.TechLevel?.ToStringHuman() ?? "FactionLoadout_NotOverriden_WithDefault".Translate((Current.Faction?.Def?.techLevel ?? TechLevel.Undefined).ToStringHuman())
            )
        )
        {
            IEnumerable<TechLevel?> enums = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel?>().Append(null);
            FloatMenuUtility.MakeMenu(
                enums,
                e => e?.ToStringHuman() ?? "FactionLoadout_NotOverriden_WithDefault".Translate((Current.Faction?.Def?.techLevel ?? TechLevel.Undefined).ToStringHuman()),
                e =>
                    () =>
                    {
                        Current.TechLevel = e;
                    }
            );
        }

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

            inner.GapLine();
            string xenoState = Current.OverrideFactionXenotypes
                ? "FactionLoadout_Xenotype_ActiveCount".Translate(Current.xenotypeChances.Count)
                : "FactionLoadout_Xenotype_Off".Translate();
            if (inner.ButtonTextLabeled("FactionLoadout_EditXenoSpawnRates".Translate(), xenoState))
                Find.WindowStack.Add(new Dialog_XenotypeEdit(Current));
        }

        // --- Spawn Groups section ---
        if (Current.Faction is not { IsMissing: true })
        {
            inner.GapLine();

            // Summary row: "Spawn Groups: N groups [Edit Spawn Groups]"
            Rect groupsRow = inner.GetRect(28f);
            float editBtnW = 160f;
            Rect editGroupsBtn = new(groupsRow.xMax - editBtnW, groupsRow.y, editBtnW, 24f);

            Text.Anchor = TextAnchor.MiddleLeft;
            string groupsSummary;
            int groupCount;
            if (Current.PawnGroupMakerEdits != null)
            {
                groupCount = Current.PawnGroupMakerEdits.Count;
                groupsSummary = "FactionLoadout_SpawnGroups_SummaryModified".Translate(groupCount, "FactionLoadout_GroupEditor_NewTag".Translate().ToString().ToLower());
            }
            else
            {
                groupCount = Current?.Faction?.Def?.pawnGroupMakers?.Count ?? 0;
                groupsSummary = "FactionLoadout_SpawnGroups_Summary".Translate(groupCount);
            }

            Rect summaryLabelRect = new(groupsRow.x, groupsRow.y, groupsRow.width - editBtnW - 4f, groupsRow.height);
            GUI.color = Color.grey;
            Widgets.Label(summaryLabelRect, "FactionLoadout_SpawnGroups_Label".Translate() + "  " + groupsSummary);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(editGroupsBtn, "FactionLoadout_SpawnGroups_EditButton".Translate()))
                GroupEditorUI.OpenEditor(Current);

            // Orphan warning (conditional) — lists which pawnkinds are missing from groups
            HashSet<PawnKindDef> orphaned = Current?.GetOrphanedKinds() ?? [];
            if (orphaned.Count > 0)
            {
                string names = orphaned.Select(k => k.LabelCap.ToString()).OrderBy(n => n).ToCommaList();
                string warnText = "FactionLoadout_SpawnGroups_OrphanWarning".Translate(names);
                float warnH = Text.CalcHeight(warnText, inner.ColumnWidth);
                Rect warnRow = inner.GetRect(warnH);
                GUI.color = new Color(1f, 0.6f, 0.1f);
                Widgets.Label(warnRow, warnText);
                GUI.color = Color.white;
            }
        }

        inner.GapLine();
        inner.Label("<b>Loadout Overrides:</b>");
        inner.Gap();

        HashSet<PawnKindDef> orphanedKinds = Current?.GetOrphanedKinds() ?? [];
        foreach (PawnKindEdit edit in Current.KindEdits)
        {
            Rect rect = inner.GetRect(30);
            GUI.color = Color.red;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 38, 24), "DEL"))
            {
                bin.Add(edit);
                edit.DeletedOrClosed = true;
            }

            GUI.color = Color.white;
            rect.x += 42;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 50, 24), "EDIT"))
                Find.WindowStack.Add(new PawnKindEditUI(edit));

            rect.x += 54;
            if (Widgets.ButtonImageFitted(new Rect(rect.x, rect.y, 24, 24), TexButton.Copy))
                PawnKindClipboard.Copy(edit);
            TooltipHandler.TipRegion(new Rect(rect.x, rect.y, 24, 24), "FactionLoadout_Clipboard_CopyTooltip".Translate());

            rect.x += 28;
            if (PawnKindClipboard.HasData)
            {
                if (Widgets.ButtonImageFitted(new Rect(rect.x, rect.y, 24, 24), TexButton.Paste))
                    PawnKindClipboard.PasteAll(edit);
                TooltipHandler.TipRegion(new Rect(rect.x, rect.y, 24, 24), "FactionLoadout_Clipboard_PasteAllTooltip".Translate(PawnKindClipboard.GetDescription()));
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.DrawTextureFitted(new Rect(rect.x, rect.y, 24, 24), TexButton.Paste, 1f);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(new Rect(rect.x, rect.y, 24, 24), "FactionLoadout_Clipboard_Empty".Translate());
            }

            rect.x += 28;

            // Orphan warning icon
            bool isOrphaned = !edit.IsGlobal && edit.Def != null && orphanedKinds.Contains(edit.Def);
            if (isOrphaned)
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(rect.x, rect.y, 20f, 24f), "⚠");
                GUI.color = Color.white;
                TooltipHandler.TipRegion(new Rect(rect.x, rect.y, 20f, 24f), "FactionLoadout_SpawnGroups_OrphanKindTooltip".Translate());
                rect.x += 22f;
            }

            Widgets.Label(rect, $"<b>{(edit.IsGlobal ? "<color=cyan>Global (affects all faction pawns)</color>" : edit.Def.LabelCap)}</b>");
        }

        foreach (PawnKindEdit item in bin)
            Current.KindEdits.Remove(item);
        bin.Clear();

        if (!Current.Faction.IsMissing && inner.ButtonText("Add new..."))
        {
            IEnumerable<PawnKindDef> MakeKinds()
            {
                tempKinds.Clear();
                if (!Current.HasGlobalEditor())
                    tempKinds.Add(null);

                // Collect all pawnkinds from group edits (if active) or the
                // live faction def. When PawnGroupMakerEdits is null,
                // GetAllKindDefsForUI delegates to GetAllPawnKinds which already
                // includes basicMemberKind and fixedLeaderKinds.
                foreach (PawnKindDef kind in Current.GetAllKindDefsForUI())
                {
                    if (!Current.HasEditFor(kind))
                        tempKinds.Add(kind);
                }

                // basicMemberKind and fixedLeaderKinds are NOT included in the
                // PawnGroupMaker-based path of GetAllKindDefsForUI (those fields
                // are not part of any group maker entry). When PawnGroupMakerEdits
                // is null the GetAllPawnKinds path already covers them, so only
                // add them here when group edits are active to avoid duplicates.
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
                        : new MenuItemText(null, "<color=cyan><b>Global (affects all faction pawns)</b></color>")
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

        inner.End();
        Widgets.EndScrollView();

        // --- Footer (always visible) ---
        ui.GapLine(26);

        if (Prefs.DevMode && clonedFac != null && ui.ButtonText("DevMode: Debug cloned kinds"))
            foreach (PawnKindDef kind in clonedFac.GetKindDefs())
            {
                ModCore.Log($"Kind: {kind.label} ({kind.defName})");
                ModCore.Log($" - Apparel Money: {kind.apparelMoney}");
                if (kind.apparelRequired == null)
                    continue;
                ModCore.Log(" - Apparel required:");
                foreach (ThingDef item in kind.apparelRequired)
                    ModCore.Log($"  * {item?.LabelCap ?? "<null>"}");
            }

        bool isInGame = Verse.Current.Game != null;

        if (!isInGame)
        {
            ui.Label("<color=yellow>[ERROR] You must load a save game to preview pawns. Sorry!</color>");
        }
        else
        {
            ui.CheckboxLabeled("Thing ID Patch", ref _ThingIDPatch, "Turn on to save thing IDs");
            ui.Gap(20);
            Rect total = ui.GetRect(inRect.height - ui.CurHeight - 32);
            int count = pawns.Count;

            if (count != 0)
            {
                float w = total.width / count;
                for (int i = 0; i < count; i++)
                {
                    Rect pawnArea = new(total.x + i * w, total.y, w, w);
                    Pawn pawn = pawns[i];

                    if (pawn != null)
                        Widgets.ThingIcon(pawnArea, pawn);
                    else
                        Widgets.DrawTextureFitted(pawnArea, Widgets.CheckboxOffTex, 1f);

                    Widgets.DrawHighlightIfMouseover(pawnArea);
                    TooltipHandler.TipRegion(pawnArea, pawn?.KindLabel?.CapitalizeFirst() ?? "<ERROR INVALID PAWN>");
                    if (Mouse.IsOver(pawnArea) && pawn != null)
                    {
                        Pawn p = pawns[i];
                        Rect window = windowRect;
                        window.y += 510;
                        window.x -= 465 - 40;
                        window.height = 550;
                        window.width = 410;
                        Find.WindowStack.ImmediateWindow(
                            90812358,
                            window,
                            WindowLayer.Super,
                            () =>
                            {
                                var list =
                                    typeof(Selector).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Find.Selector) as List<object>
                                    ?? new List<object>();
                                list.Clear();
                                list.Add(p);
                                typeof(ITab_Pawn_Gear).GetMethod("FillTab", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(new ITab_Pawn_Gear(), []);
                                list.Clear();
                            }
                        );
                    }

                    pawnArea.height = 200;
                    pawnArea.y += w + 10;
                    if (pawnArea.width >= 50)
                        Widgets.Label(pawnArea, pawns[i]?.KindLabel.CapitalizeFirst() ?? "<ERROR INVALID PAWN>");
                }
            }
        }

        GUI.enabled = isInGame;
        bool f = Input.GetKeyDown(KeyCode.F);
        if ((ui.ButtonText("Regenerate previews [Hotkey: F]") || (pawns.Count == 0 && !_previewFailed) || (f && framesSinceF > 20)) && isInGame)
        {
            if (f)
                framesSinceF = 0;
            _previewFailed = false;

            FactionDef toClone = FactionEdit.TryGetOriginal(Current.Faction.Def.defName) ?? Current.Faction.Def;
            clonedFac = CloningUtility.Clone(toClone);
            clonedFac.defName = Current.Faction.Def.defName;
            clonedFac.humanlikeFaction = Current.Faction.Def.humanlikeFaction;
            clonedFac.fixedName = $"TEMP FACTION CLONE ({clonedFac.defName})";

            Current.Apply(clonedFac, false);
            DestroyPawns();

            Faction faction = new()
            {
                def = clonedFac,
                loadID = -1,
                colorFromSpectrum = Rand.Range(0f, 1f),
                hidden = true,
                ideos = Find.FactionManager?.FirstFactionOfDef(Current.Faction.Def)?.ideos,
                Name = clonedFac.fixedName,
                relations = Find
                    .FactionManager.AllFactionsVisible.Select(otherFaction => new FactionRelation
                    {
                        other = otherFaction,
                        baseGoodwill = 0,
                        kind = FactionRelationKind.Neutral,
                    })
                    .ToList(),
                temporary = true,
                deactivated = true,
            };

            ThingIDPatch.Active = _ThingIDPatch;
            IdeoUtilityPatch.Active = true;
            FactionUtilityPawnGenPatch.Active = true;

            foreach (PawnKindDef item in FactionEdit.GetAllPawnKinds(clonedFac))
                try
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(
                        new PawnGenerationRequest(item, faction)
                        {
                            ForceGenerateNewPawn = true,
                            AllowDowned = false,
                            AllowDead = false,
                            CanGeneratePawnRelations = false,
                            RelationWithExtraPawnChanceFactor = 0,
                            ColonistRelationChanceFactor = 0,
                            ForceNoIdeo = true,
                        }
                    );
                    pawns.Add(pawn);
                }
                catch (Exception e)
                {
                    ModCore.Error($"Failed to generate pawn of type '{item.LabelCap}':", e);
                    pawns.Add(null);
                }

            Find.FactionManager.Remove(faction);

            ThingIDPatch.Active = false;
            FactionLeaderPatch.Active = false;
            FactionUtilityPawnGenPatch.Active = false;
            IdeoUtilityPatch.Active = false;
        }

        GUI.enabled = true;
        ui.End();
    }

    /// <summary>
    /// Computes the scroll content height from the data model, mirroring the rendering
    /// logic in DoWindowContents. Must be kept in sync with changes to the inner listing.
    /// </summary>
    private float CalcOverridesContentHeight(float contentWidth)
    {
        const float vs = 2f; // Listing_Standard.verticalSpacing added by GetRect
        float h = Text.LineHeight + vs; // Tech Level ButtonTextLabeled

        if (
            ModsConfig.BiotechActive
            && Current.Faction is { IsMissing: false }
            && Current.Faction?.Def != Preset.SpecialWildManFaction
            && Current.Faction?.Def != Preset.SpecialFactionlessPawnsFaction
        )
        {
            h += 12f; // GapLine
            h += Text.LineHeight + vs; // xenotype ButtonTextLabeled
        }

        if (Current.Faction is not { IsMissing: true })
        {
            h += 12f; // GapLine
            h += 28f + vs; // GetRect(28f) for spawn groups summary row

            HashSet<PawnKindDef> orphaned = Current?.GetOrphanedKinds() ?? [];
            if (orphaned.Count > 0)
            {
                string names = orphaned.Select(k => k.LabelCap.ToString()).OrderBy(n => n).ToCommaList();
                string warnText = "FactionLoadout_SpawnGroups_OrphanWarning".Translate(names);
                h += Text.CalcHeight(warnText, contentWidth) + vs;
            }
        }

        h += 12f; // GapLine
        h += Text.LineHeight + vs; // Label "Loadout Overrides:" (single line)
        h += 12f; // Gap()
        h += Current.KindEdits.Count * (30f + vs); // each KindEdit GetRect(30)

        if (!Current.Faction.IsMissing)
            h += Text.LineHeight + vs; // "Add new..." ButtonText

        return h + 20f; // safety margin
    }

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

    private void DrawMaterialFilter(Listing_Standard ui)
    {
        Rect matRect = ui.GetRect(28);
        matRect.width = 300;
        if (Widgets.ButtonText(matRect, $"Use custom apparel materials: {(Current.ApparelStuffFilter == null ? "<color=#ff4d4d>NO</color>" : "<color=#81f542>YES</color>")}"))
        {
            filterState = new ThingFilterUI.UIState();

            if (Current.ApparelStuffFilter != null)
            {
                Current.ApparelStuffFilter = null;
            }
            else
            {
                Current.ApparelStuffFilter = new ThingFilter();
                if (Current.Faction.Def.apparelStuffFilter != null)
                    Current.ApparelStuffFilter.CopyAllowancesFrom(Current.Faction.Def.apparelStuffFilter);
            }
        }

        if (Current.ApparelStuffFilter == null)
            return;
        Rect filter = ui.GetRect(240);
        ThingFilterUI.DoThingFilterConfigWindow(
            filter,
            filterState,
            Current.ApparelStuffFilter,
            forceHideHitPointsConfig: true,
            forceHiddenFilters:
            [
                SpecialThingFilterDefOf.AllowDeadmansApparel,
                SpecialThingFilterDefOf.AllowNonDeadmansApparel,
                SpecialThingFilterDefOf.AllowFresh,
                DefDatabase<SpecialThingFilterDef>.GetNamed("AllowRotten"),
            ]
        );
    }
}

public class Dialog_XenotypeEdit : Window
{
    private readonly FactionEdit _edit;
    private Vector2 _scrollPos;

    public Dialog_XenotypeEdit(FactionEdit edit)
    {
        _edit = edit;
        doCloseX = true;
        closeOnCancel = true;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(450f, 400f);

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard ui = new();
        ui.Begin(inRect);

        ui.CheckboxLabeled($"<b>{"FactionLoadout_EditXenoSpawnRates".Translate()}:</b>", ref _edit.OverrideFactionXenotypes);

        if (_edit.OverrideFactionXenotypes)
        {
            if (_edit.xenotypeChances.NullOrEmpty())
            {
                _edit.xenotypeChances = _edit.Faction?.Def?.xenotypeSet?.xenotypeChances?.ToDictionary(x => x.xenotype.defName, x => x.chance) ?? new Dictionary<string, float>();
                if (!_edit.xenotypeChances.ContainsKey(FactionEditUI.BaselinerDefName))
                    _edit.xenotypeChances.Add(FactionEditUI.BaselinerDefName, _edit.Faction?.Def?.xenotypeSet?.BaselinerChance ?? 1f);
            }

            _edit.xenotypeChances[FactionEditUI.BaselinerDefName] = Math.Max(0f, 1f - _edit.xenotypeChances.Sum(x => x.Key == FactionEditUI.BaselinerDefName ? 0 : x.Value));

            // Reserve space for add buttons at bottom.
            const float addButtonsHeight = 70f;
            float scrollH = Mathf.Max(30f, inRect.height - ui.CurHeight - addButtonsHeight);
            Rect scrollOutRect = ui.GetRect(scrollH);
            // Compute inner height directly from item count — avoids feedback loops where
            // Listing_Standard.CurHeight is clamped to the inner rect, preventing growth.
            const float ItemRowH = 32f; // SliderLabeledWithDelete: GetRect(30) + Gap(2)
            float contentH = _edit.xenotypeChances.Count * ItemRowH;
            Rect innerRect = new(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(contentH, scrollH));

            Widgets.BeginScrollView(scrollOutRect, ref _scrollPos, innerRect);
            Listing_Standard inner = new();
            inner.Begin(innerRect);

            List<string> toDelete = [];
            foreach (string key in _edit.xenotypeChances.Keys.OrderBy(k => DefDatabase<XenotypeDef>.GetNamedSilentFail(k)?.LabelCap.ToString() ?? k).ToList())
                _edit.xenotypeChances[key] = UIHelpers.SliderLabeledWithDelete(
                    inner,
                    $"{DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap ?? key}: {_edit.xenotypeChances[key].ToStringPercent()}",
                    _edit.xenotypeChances[key],
                    0f,
                    1f,
                    deleteAction: delegate
                    {
                        toDelete.Add(key);
                    }
                );

            foreach (string delete in toDelete)
                _edit.xenotypeChances.Remove(delete);

            inner.End();
            Widgets.EndScrollView();

            if (ui.ButtonText("FactionLoadout_AddNewByDefName".Translate()))
            {
                Find.WindowStack.Add(
                    new Dialog_TextEntry(
                        "FactionLoadout_AddNewByDefNameDesc".Translate(),
                        defName =>
                        {
                            if (_edit.xenotypeChances.ContainsKey(defName))
                            {
                                Messages.Message("FactionLoadout_DuplicateListItem".Translate(defName), MessageTypeDefOf.RejectInput);
                                return;
                            }
                            _edit.xenotypeChances[defName] = 0.1f;
                        }
                    )
                );
            }

            if (ModLister.BiotechInstalled && ui.ButtonText("FactionLoadout_AddNew".Translate()))
            {
                var xenoItems = CustomFloatMenu.MakeItems(
                    DefDatabase<XenotypeDef>.AllDefs.Where(def => !_edit.xenotypeChances.ContainsKey(def.defName)),
                    def => new MenuItemText(def, def.LabelCap, def.Icon)
                );
                CustomFloatMenu.Open(
                    xenoItems,
                    item =>
                    {
                        XenotypeDef def = item.GetPayload<XenotypeDef>();
                        _edit.xenotypeChances[def.defName] = 0.1f;
                    }
                );
            }
        }
        else
        {
            _edit.xenotypeChances.Clear();
            _edit.xenotypeChancesByDef.Clear();
        }

        ui.End();
    }
}

public class Dialog_TextEntry : Window
{
    private string message;
    private string input;
    private Action<string> onConfirm;

    public Dialog_TextEntry(string message, Action<string> onConfirm)
    {
        this.message = message;
        this.onConfirm = onConfirm;
        input = string.Empty;
        doCloseX = true;
        closeOnAccept = false;
        closeOnCancel = true;
    }

    public override Vector2 InitialSize
    {
        get
        {
            Vector2 size = Text.CalcSize(message);
            size.x += 100;
            size.y *= 7;
            return size;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
        listingStandard.Label(message);
        input = listingStandard.TextEntry(input);
        if (listingStandard.ButtonText("Accept".Translate()))
        {
            onConfirm?.Invoke(input);
            Close();
        }
        if (listingStandard.ButtonText("Cancel".Translate()))
        {
            Close();
        }
        listingStandard.End();
    }
}
