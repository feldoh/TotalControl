using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private bool _ThingIDPatch = true;

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

        Rect r = ui.GetRect(50);
        Widgets.Label(r, $"<size=34><b>Faction: <color=#cf9af5>{Current.Faction.Def?.LabelCap ?? "none"}</color></b></size>");
        if (Current.Faction.IsMissing)
        {
            ui.Label($"<color=red>Missing faction! Could not find '{Current.Faction}', probably because it's in an unloaded mod.</color>");
            ui.End();
            return;
        }

        if (Current.Faction.DefName == Preset.SpecialCreepjoinerFactionDefName)
        {
            ui.Label("<color=yellow>EXPERIMENTAL! - This is a fake faction used for Creepjoiner editing, use at your own risk.</color>");
        }
        if (Current.Faction.DefName == Preset.SpecialWildManFactionDefName)
        {
            ui.Label("<color=yellow>EXPERIMENTAL! - This is a fake faction used for WildMan editing, use at your own risk.</color>");
        }

        // Disabled for now
        // DrawMaterialFilter(ui);

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
                e =>
                    () =>
                    {
                        Current.TechLevel = e;
                    }
            );
        }

        if (ModsConfig.BiotechActive && Current.Faction?.Def != Preset.SpecialWildManFaction)
        {
            ui.GapLine();
            ui.CheckboxLabeled($"<b>{"FactionLoadout_EditXenoSpawnRates".Translate()}:</b>", ref Current.OverrideFactionXenotypes);
            if (Current.OverrideFactionXenotypes)
            {
                List<string> toDelete = [];
                if (Current.xenotypeChances.NullOrEmpty() && Current.OverrideFactionXenotypes)
                {
                    Current.xenotypeChances =
                        Current.Faction?.Def?.xenotypeSet?.xenotypeChances?.ToDictionary(x => x.xenotype.defName, x => x.chance) ?? new Dictionary<string, float>();
                    if (!Current.xenotypeChances.ContainsKey(BaselinerDefName))
                        Current.xenotypeChances.Add(BaselinerDefName, Current.Faction?.Def?.xenotypeSet?.BaselinerChance ?? 1f);
                }

                Current.xenotypeChances[BaselinerDefName] = Math.Max(0f, 1f - Current.xenotypeChances.Sum(x => x.Key == BaselinerDefName ? 0 : x.Value));

                foreach (string key in Current.xenotypeChances.Keys.ToList())
                    Current.xenotypeChances[key] = UIHelpers.SliderLabeledWithDelete(
                        ui,
                        $"{DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap ?? key}: {Current.xenotypeChances[key].ToStringPercent()}",
                        Current.xenotypeChances[key],
                        0f,
                        1f,
                        deleteAction: delegate
                        {
                            toDelete.Add(key);
                        }
                    );

                foreach (string delete in toDelete)
                    Current.xenotypeChances.Remove(delete);

                if (ui.ButtonText("FactionLoadout_AddNewByDefName".Translate()))
                {
                    // Add a new xenotype by def name only, allows adding options without the mod loaded.
                    Find.WindowStack.Add(
                        new Dialog_TextEntry(
                            "FactionLoadout_AddNewByDefNameDesc".Translate(),
                            defName =>
                            {
                                if (Current.xenotypeChances.ContainsKey(defName))
                                {
                                    Messages.Message("FactionLoadout_DuplicateListItem".Translate(defName), MessageTypeDefOf.RejectInput);
                                    return;
                                }
                                Current.xenotypeChances[defName] = 0.1f;
                            }
                        )
                    );
                }
                // Show a list of xenotypes that can be added, much nicer than going by name but requires biotech
                if (ModLister.BiotechInstalled && ui.ButtonText("FactionLoadout_AddNew".Translate()))
                {
                    List<FloatMenuOption> floatMenuList = [];
                    foreach (XenotypeDef def in DefDatabase<XenotypeDef>.AllDefs)
                        if (!Current.xenotypeChances.ContainsKey(def.defName))
                            floatMenuList.Add(
                                new FloatMenuOption(
                                    def.LabelCap,
                                    delegate
                                    {
                                        Current.xenotypeChances[def.defName] = 0.1f;
                                    }
                                )
                            );

                    Find.WindowStack.Add(new FloatMenu(floatMenuList));
                }
            }
            else
            {
                Current.xenotypeChances.Clear();
                Current.xenotypeChancesByDef.Clear();
            }
        }

        ui.GapLine();
        ui.Label("<b>Loadout Overrides:</b>");

        ui.Gap();

        foreach (PawnKindEdit edit in Current.KindEdits)
        {
            Rect rect = ui.GetRect(30);
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
            Widgets.Label(rect, $"<b>{(edit.IsGlobal ? "<color=cyan>Global (affects all faction pawns)</color>" : edit.Def.LabelCap)}</b>");
        }

        foreach (PawnKindEdit item in bin)
            Current.KindEdits.Remove(item);

        bin.Clear();

        if (ui.ButtonText("Add new..."))
        {
            IEnumerable<PawnKindDef> MakeKinds()
            {
                tempKinds.Clear();
                if (!Current.HasGlobalEditor())
                    tempKinds.Add(null);

                void Register(List<PawnGenOption> list)
                {
                    if (list == null)
                        return;

                    foreach (PawnGenOption thing in list)
                        if (!Current.HasEditFor(thing.kind))
                            tempKinds.Add(thing.kind);
                }

                foreach (PawnGroupMaker maker in Current.Faction.Def.pawnGroupMakers ?? Enumerable.Empty<PawnGroupMaker>())
                {
                    Register(maker.options);
                    Register(maker.guards);
                    Register(maker.traders);
                    Register(maker.carriers);
                }

                if (Current.Faction.Def.basicMemberKind != null)
                    tempKinds.Add(Current.Faction.Def.basicMemberKind);
                if (Current.Faction.Def.fixedLeaderKinds != null)
                    foreach (PawnKindDef item in Current.Faction.Def.fixedLeaderKinds)
                        tempKinds.Add(item);

                foreach (PawnKindDef item in tempKinds)
                    yield return item;

                // If there's no kinds to edit, other than the global add some for special cases.
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

            var kinds = MakeKinds().ToList();
            var items = CustomFloatMenu.MakeItems(
                kinds,
                k => k != null ? new MenuItemText(k, k.LabelCap, tooltip: k.description) : new MenuItemText(null, "<color=cyan><b>Global (affects all faction pawns)</b></color>")
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

        var isInGame = Verse.Current.Game != null;

        if (!isInGame)
        {
            ui.Label("<color=yellow>[ERROR] You must load a save game to preview pawns. Sorry!</color>");
        }
        else
        {
            ui.CheckboxLabeled("Thing ID Patch", ref _ThingIDPatch);
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
        if ((ui.ButtonText("Regenerate previews [Hotkey: F]") || pawns.Count == 0 || (f && framesSinceF > 20)) && isInGame)
        {
            if (f)
                framesSinceF = 0;

            FactionDef toClone = FactionEdit.TryGetOriginal(Current.Faction.Def.defName) ?? Current.Faction.Def;
            clonedFac = CloningUtility.Clone(toClone);
            clonedFac.defName = Current.Faction.Def.defName;
            clonedFac.humanlikeFaction = Current.Faction.Def.humanlikeFaction;
            clonedFac.fixedName = $"TEMP FACTION CLONE ({clonedFac.defName})";

            Current.Apply(clonedFac, false);
            DestroyPawns();

            Faction faction =
                new()
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
                            kind = FactionRelationKind.Neutral
                        })
                        .ToList(),
                    temporary = true,
                    deactivated = true
                };

            ThingIDPatch.Active = _ThingIDPatch;
            IdeoUtilityPatch.Active = true;
            FactionUtilityPawnGenPatch.Active = true;
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction != other)
                    faction.TryMakeInitialRelationsWith(other);
            }

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
                            ForceNoIdeo = true
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
                DefDatabase<SpecialThingFilterDef>.GetNamed("AllowRotten")
            ]
        );
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
