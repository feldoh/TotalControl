using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    [HotSwappable]
    public class FactionEditUI : Window
    {
        public static void OpenEditor(FactionEdit fac)
        {
            if (fac == null)
                return;

            Find.WindowStack.Add(new FactionEditUI(fac));
        }

        public readonly FactionEdit Current;

        private List<PawnKindEdit> bin = new List<PawnKindEdit>();
        private HashSet<PawnKindDef> tempKinds = new HashSet<PawnKindDef>();
        private List<Pawn> pawns = new List<Pawn>();
        private FactionDef clonedFac;
        private ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();
        private int framesSinceF = 0;

        public FactionEditUI(FactionEdit fac)
        {
            Current = fac;
            draggable = true;
            resizeable = true;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            var copy = windowRect;
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
            foreach(var pawn in pawns)
            {
                pawn?.Destroy();
            }
            pawns.Clear();
        }

        public static float SliderLabeledWithDelete(Listing_Standard ls, string label, float val, float min, float max, 
            float labelPct = 0.5f, string tooltip = null, Action deleteAction = null)
        {
            Rect rect = ls.GetRect(30f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(labelPct), label);
            if (tooltip != null)
            {
                TooltipHandler.TipRegion(rect.LeftPart(labelPct), tooltip);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            var sliderRect = rect.RightPart(1f - labelPct);
            if (deleteAction != null)
            {
                sliderRect.width -= 32;
            }
            float result = Widgets.HorizontalSlider_NewTemp(sliderRect, val, min, max, middleAlignment: true);
            if (deleteAction != null)
            {
                var deleteButton = new Rect(sliderRect.xMax + 5, sliderRect.y, 24, 24);
                if (Widgets.ButtonImage(deleteButton, TexButton.DeleteX))
                {
                    deleteAction();
                }
            }
            ls.Gap(ls.verticalSpacing);
            return result;
        }

        public override void DoWindowContents(Rect inRect)
        {
            framesSinceF++;

            if(Current == null || Current.DeletedOrClosed)
            {
                Close();
                return;
            }

            var ui = new Listing_Standard();
            ui.Begin(inRect);

            var r = ui.GetRect(50);
            Widgets.Label(r, $"<size=34><b>Faction: <color=#cf9af5>{Current.Faction.Def?.LabelCap ?? "none"}</color></b></size>");
            if (Current.Faction.IsMissing)
            {
                ui.Label($"<color=red>Missing faction! Could not find '{Current.Faction}', probably because it's in an unloaded mod.</color>");
                ui.End();
                return;
            }

            // Disabled for now
            // DrawMaterialFilter(ui);

            if (ModsConfig.BiotechActive)
            {
                ui.GapLine();
                ui.Label("<b>Xenotype spawn rates:</b>");
                var toDelete = new List<XenotypeDef>();
                if (Current.xenotypeChances is null)
                {
                    Current.xenotypeChances = Current.Faction.Def.xenotypeSet.xenotypeChances.ToDictionary(x => x.xenotype, x => x.chance);
                    if (Current.xenotypeChances.ContainsKey(XenotypeDefOf.Baseliner) is false)
                    {
                        Current.xenotypeChances[XenotypeDefOf.Baseliner] = Current.Faction.Def.xenotypeSet.BaselinerChance;
                    }
                }

                foreach (var key in Current.xenotypeChances.Keys.ToList())
                {
                    Current.xenotypeChances[key] = SliderLabeledWithDelete(ui, key.LabelCap + ": " 
                        + Current.xenotypeChances[key].ToStringPercent(), Current.xenotypeChances[key], 0f, 1f, deleteAction: delegate
                        {
                            toDelete.Add(key);
                        });
                };

                foreach (var delete in toDelete)
                {
                    Current.xenotypeChances.Remove(delete);
                }
                if (ui.ButtonText("Add new..."))
                {
                    var floatMenuList = new List<FloatMenuOption>();
                    foreach (var def in DefDatabase<XenotypeDef>.AllDefs)
                    {
                        if (!Current.xenotypeChances.ContainsKey(def))
                        {
                            floatMenuList.Add(new FloatMenuOption(def.LabelCap, delegate
                            {
                                Current.xenotypeChances[def] =  0.1f;
                            }));
                        }
                    }
                    Find.WindowStack.Add(new FloatMenu(floatMenuList));
                }
            }


            ui.GapLine();
            ui.Label("<b>Loadout Overrides:</b>");            

            ui.Gap();

            foreach (var edit in Current.KindEdits)
            {             
                var rect = ui.GetRect(30);
                GUI.color = Color.red;
                if(Widgets.ButtonText(new Rect(rect.x, rect.y, 38, 24), "DEL"))
                {
                    bin.Add(edit);
                    edit.DeletedOrClosed = true;
                }
                GUI.color = Color.white;
                rect.x += 42;
                if(Widgets.ButtonText(new Rect(rect.x, rect.y, 50, 24), "EDIT"))
                {
                    Find.WindowStack.Add(new PawnKindEditUI(edit));
                }
                rect.x += 54;
                Widgets.Label(rect, $"<b>{(edit.IsGlobal ? "<color=cyan>Global (affects all faction pawns)</color>" : edit.Def.LabelCap)}</b>");
            }

            foreach(var item in bin)
            {
                Current.KindEdits.Remove(item);
            }
            bin.Clear();

            if (ui.ButtonText("Add new..."))
            {
                IEnumerable<PawnKindDef> MakeKinds()
                {
                    tempKinds.Clear();
                    if (!Current.HasGlobalEditor())
                        tempKinds.Add(null);

                    if (Current.Faction.Def.pawnGroupMakers == null)
                        yield break;

                    void Register(List<PawnGenOption> list)
                    {
                        if (list == null)
                            return;

                        foreach (var thing in list)
                            if (!Current.HasEditFor(thing.kind))
                                tempKinds.Add(thing.kind);
                    }

                    foreach (var maker in Current.Faction.Def.pawnGroupMakers)
                    {
                        Register(maker.options);
                        Register(maker.guards);
                        Register(maker.traders);
                        Register(maker.carriers);
                    }
                    if (Current.Faction.Def.basicMemberKind != null)
                        tempKinds.Add(Current.Faction.Def.basicMemberKind);
                    if (Current.Faction.Def.fixedLeaderKinds != null)
                        foreach (var item in Current.Faction.Def.fixedLeaderKinds)
                            tempKinds.Add(item);

                    foreach (var item in tempKinds)
                    {
                        yield return item;
                    }

                    tempKinds.Clear();
                }
                var kinds = MakeKinds();
                var items = CustomFloatMenu.MakeItems(kinds, k =>
                {
                    if(k != null)
                        return new MenuItemText(k, k.LabelCap, tooltip: k.description);
                    return new MenuItemText(null, "<color=cyan><b>Global (affects all faction pawns)</b></color>");
                });
                CustomFloatMenu.Open(items, raw =>
                {
                    var k = raw.GetPayload<PawnKindDef>();

                    if (k != null)
                    {
                        Current.KindEdits.Add(new PawnKindEdit(k));
                        //if(k.RaceProps.Animal)
                        //    Messages.Message($"<color=yellow>[WARNING]</color> Editing this {k.LabelCap} affects all {k.GetLabelPlural()}, not just {Current.Faction.LabelCap}'s {k.GetLabelPlural()}!", MessageTypeDefOf.NegativeEvent, false);
                    }
                    else
                    {
                        var kind = kinds.FirstOrDefault(k => k != null);
                        ModCore.Log($"Using {kind} as global base.");
                        if (kind != null)
                            Current.KindEdits.Insert(0, new PawnKindEdit(kind) { IsGlobal = true });
                    }
                });
            }

            ui.GapLine(26);


            if (Prefs.DevMode && clonedFac != null && ui.ButtonText("DevMode: Debug cloned kinds"))
            {
                foreach(var kind in clonedFac.GetKindDefs())
                {
                    ModCore.Log($"Kind: {kind.label} ({kind.defName})");
                    ModCore.Log($" - Apparel Money: {kind.apparelMoney}");
                    if(kind.apparelRequired != null)
                    {
                        ModCore.Log(" - Apparel required:");
                        foreach(var item in kind.apparelRequired)
                        {
                            ModCore.Log($"  * {item?.LabelCap ?? "<null>"}");
                        }
                    }
                }
            }

            bool isInGame = Verse.Current.Game != null;            

            if (!isInGame)
            {
                ui.Label("<color=yellow>[ERROR] You must load a save game to preview pawns. Sorry!</color>");
            }
            else
            {
                ui.Gap(20);
                var total = ui.GetRect(inRect.height - ui.CurHeight - 32);
                int count = pawns.Count;

                if (count != 0)
                {
                    float w = total.width / count;
                    for (int i = 0; i < count; i++)
                    {
                        Rect pawnArea = new Rect(total.x + i * w, total.y, w, w);

                        var pawn = pawns[i];

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
                            Find.WindowStack.ImmediateWindow(90812358, window, WindowLayer.Super, () =>
                            {
                                var list = typeof(Selector).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Find.Selector) as List<object>;
                                list.Clear();
                                list.Add(p);
                                typeof(ITab_Pawn_Gear).GetMethod("FillTab", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(new ITab_Pawn_Gear(), new object[] {});
                                list.Clear();
                            }, true);
                        }

                        pawnArea.height = 200;
                        pawnArea.y += w + 10;
                        if(pawnArea.width >= 50)
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

                var toClone = FactionEdit.TryGetOriginal(Current.Faction.Def.defName) ?? Current.Faction.Def;
                clonedFac = CloningUtility.Clone(toClone);
                clonedFac.humanlikeFaction = false;
                clonedFac.fixedName = $"TEMP FACTION CLONE ({clonedFac.defName})";

                Current.Apply(clonedFac);
                DestroyPawns();

                var faction = new Faction();
                faction.def = clonedFac;
                faction.loadID = -1;
                faction.colorFromSpectrum = Rand.Range(0f, 1f);
                faction.hidden = true;
                faction.ideos = Find.FactionManager?.FirstFactionOfDef(Current.Faction.Def)?.ideos;
                faction.Name = clonedFac.fixedName;

                PawnGenPatch.Active = true;
                FactionLeaderPatch.Active = true;
                ThingIDPatch.Active = true;

                foreach (var item in FactionEdit.GetAllPawnKinds(clonedFac))
                {
                    try
                    {
                        var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(item, faction)
                        {
                            ForceGenerateNewPawn = true,
                            AllowDowned = false,
                            AllowDead = false,
                            CanGeneratePawnRelations = false,
                            RelationWithExtraPawnChanceFactor = 0,
                            ColonistRelationChanceFactor = 0
                        });
                        pawns.Add(pawn);
                    }
                    catch(Exception e)
                    {
                        ModCore.Error($"Failed to generate pawn of type '{item.LabelCap}':", e);
                        pawns.Add(null);
                    }                    
                }

                ThingIDPatch.Active = false;
                FactionLeaderPatch.Active = false;
                PawnGenPatch.Active = false;
            }
            GUI.enabled = true;

            ui.End();
        }

        private void DrawMaterialFilter(Listing_Standard ui)
        {
            var matRect = ui.GetRect(28);
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
            if (Current.ApparelStuffFilter != null)
            {
                var filter = ui.GetRect(240);
                ThingFilterUI.DoThingFilterConfigWindow(filter, filterState, Current.ApparelStuffFilter, forceHideHitPointsConfig: true, forceHiddenFilters: new SpecialThingFilterDef[]
                {
                    SpecialThingFilterDefOf.AllowDeadmansApparel,
                    SpecialThingFilterDefOf.AllowNonDeadmansApparel,
                    SpecialThingFilterDefOf.AllowFresh,
                    DefDatabase<SpecialThingFilterDef>.GetNamed("AllowRotten")
                });
            }
        }
    }

    public class SpecialThingFilterWorker_PlantFood : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            return this.AlwaysMatches(t.def);
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            return def.ingestible != null && (def.ingestible.foodType & FoodTypeFlags.Plant) > FoodTypeFlags.None;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return this.AlwaysMatches(def);
        }
    }
}
