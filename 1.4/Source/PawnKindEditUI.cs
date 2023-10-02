using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Debug = System.Diagnostics.Debug;

namespace FactionLoadout
{
    [HotSwappable]
    public class PawnKindEditUI : Window
    {
        private static List<string> AllTechHediffTags;
        private static List<string> AllApparelTags;
        private static List<string> AllWeaponsTags;
        private static List<ThingDef> AllApparel;
        private static List<ThingDef> AllWeapons;
        private static List<ThingDef> AllTech;
        private static List<ThingDef> AllInvItems;
        private static List<ThingDef> AllHumanlikeRaces;
        private static List<PawnKindDef> AllAnimalKindDefs;

        public static Texture2D TryGetIcon(Def def, out Color color)
        {
            color = Color.white;

            if (def == null)
                return null;

            switch (def)
            {
                case PawnKindDef kd:
                    return TryGetIcon(kd.race, out color);
                case ThingDef td:
                    if (td.defName.StartsWith("Corpse_"))
                        return TryGetIcon(DefDatabase<ThingDef>.GetNamed(td.defName.Substring(7), false), out color);
                    var stuff = GenStuff.DefaultStuffFor(td);
                    color = stuff == null ? td.uiIconColor : td.GetColorForStuff(stuff);
                    return Widgets.GetIconFor(td, stuff);
                case FactionDef fd:
                    if (!fd.colorSpectrum.NullOrEmpty())
                        color = fd.colorSpectrum.FirstOrDefault();
                    return fd.FactionIcon;
                default:
                    return null;
            };
        }

        private static void ScanDefs()
        {
            if (AllTechHediffTags != null)
                return;

            var techTags = new HashSet<string>(128);
            var apparelTags = new HashSet<string>(128);
            var weaponTags = new HashSet<string>(128);
            var apparel = new HashSet<ThingDef>(256);
            var allHumanlikeRaces = new HashSet<ThingDef>(256);
            var weapons = new HashSet<ThingDef>(256);
            var allTech = new HashSet<ThingDef>(128);
            var allInv = new HashSet<ThingDef>(1024);
            var allAnimalKindDefs = new HashSet<PawnKindDef>(1024);

            foreach (var def in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (def.RaceProps.Animal && def.RaceProps.packAnimal)
                {
                    allAnimalKindDefs.Add(def);
                }
            }

            foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if(def.race != null && !def.race.Animal)                
                    allHumanlikeRaces.Add(def);                

                if(def.isTechHediff && !def.IsNaturalOrgan)
                {
                    if(def.techHediffsTags != null)
                        foreach(var item in def.techHediffsTags)                    
                            if(item != null)
                                techTags.Add(item);                    

                    allTech.Add(def);
                }

                if(def.IsApparel)
                {
                    if(def.apparel?.tags != null)                  
                        foreach(var item in def.apparel.tags)                    
                            if (item != null)
                                apparelTags.Add(item);

                    apparel.Add(def);                    
                }

                if (def.IsWeapon)
                {
                    if (def.weaponTags != null)
                        foreach (var item in def.weaponTags)
                            if (item != null)
                                weaponTags.Add(item);

                    weapons.Add(def);
                }

                if (def.category == ThingCategory.Item)
                    allInv.Add(def);
            }

            AllTechHediffTags = new List<string>(techTags);
            AllTechHediffTags.Sort();

            AllApparelTags = new List<string>(apparelTags);
            AllApparelTags.Sort();

            AllWeaponsTags = new List<string>(weaponTags);
            AllWeaponsTags.Sort();

            AllApparel = new List<ThingDef>(apparel);
            AllApparel.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

            AllWeapons = new List<ThingDef>(weapons);
            AllWeapons.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

            AllTech = new List<ThingDef>(allTech);
            AllTech.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

            AllInvItems = new List<ThingDef>(allInv);
            AllInvItems.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

            AllHumanlikeRaces = new List<ThingDef>(allHumanlikeRaces);
            AllHumanlikeRaces.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));

            AllAnimalKindDefs = new List<PawnKindDef>(allAnimalKindDefs);
            AllAnimalKindDefs.Sort((a, b) => ((string)a.LabelCap).CompareTo(b.LabelCap));
        }

        private static void DrawRegionTitle(Listing_Standard ui, string title)
        {
            ui.GapLine(26);
            Widgets.Label(ui.GetRect(42), $"<size=26><b><color=#73fff2>{title}</color></b></size>");
        }

        public readonly PawnKindEdit Current;

        private string maxTechBuffer = null;
        private List<string> tagBin = new List<string>();
        private List<Def> thingBin = new List<Def>();
        private Vector2[] scrolls = new Vector2[64];
        private string[] buffers = new string[64];
        private int scrollIndex;
        private int bufferIndex;
        private float lastHeight;
        private Vector2 globalScroll;
        private int selectedTab;
        private List<Tab> tabs;
        private PawnKindDef DefaultKind
        {
            get
            {
                if (Current.DeletedOrClosed)
                    return Current.Def;

                var found = FactionEdit.TryGetOriginal(Current.ParentEdit.Faction.DefName);
                if (found != null)
                {
                    var found2 = found.GetKindDefs().Where(k => k.defName == Current.Def.defName).FirstOrDefault();
                    if (found2 != null)
                        return found2;
                }

                return Current.Def;
            }
        }

        public PawnKindEditUI(PawnKindEdit toEdit)
        {
            draggable = true;
            resizeable = true;            
            doCloseX = true;
            Current = toEdit;

            ScanDefs();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            windowRect = new Rect(UI.screenWidth * 0.5f, 30, UI.screenWidth * 0.5f - 20, UI.screenHeight - 50);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if(Current == null || Current.DeletedOrClosed)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Small;

            scrollIndex = 0;
            bufferIndex = 0;

            if(tabs == null)
            {
                tabs = new List<Tab>()
                {
                    new Tab("General", DrawGeneralTab),
                };

                bool isAnimal = DefaultKind.RaceProps.Animal;
                if (!isAnimal)
                {
                    tabs.AddRange(new Tab[]
                    {
                        new Tab("Appearance", DrawAppearanceTab),
                        new Tab("Apparel", DrawApparelTab),
                        new Tab("Weapon", DrawWeaponTab),
                        new Tab("Implants & Bionics", DrawImplantsAndBionicsTab),
                        new Tab("Inventory", DrawInventoryTab),
                    });
                }
            }

            Rect titleArea = inRect;
            titleArea.height = 40;
            string title = $"<size=32><b>Pawn Type: <color=#cf9af5>{(Current.IsGlobal ? "Global (affects all faction pawns)" : Current.Def.LabelCap)}</color></b></size>";
            Widgets.Label(titleArea, title);

            Rect tabRect = inRect;
            tabRect.height = 40;
            tabRect.y += 50;
            for(int i = 0; i < tabs.Count; i++)
            {
                Rect button = tabRect;
                button.width = 140;
                button.x += 150 * i;

                var tab = tabs[i];
                Color bg = selectedTab == i ? new Color32(49, 82, 133, 255) : new Color(0.2f, 0.2f, 0.2f, 1f);
                if (Widgets.CustomButtonText(ref button, $"<b>{tab.Name}</b>", bg, Color.white, Color.white))
                {
                    selectedTab = i;
                }

                if(selectedTab == i)
                {
                    Rect contentArea = inRect;
                    contentArea.yMin += 100;
                    Widgets.BeginScrollView(contentArea, ref globalScroll, new Rect(0, 0, inRect.width - 24, lastHeight));
                    lastHeight = 0f;

                    var ui = new Listing_Standard();
                    ui.ColumnWidth = inRect.width - 24;
                    ui.Begin(new Rect(0, 0, inRect.width - 24, 1000000));

                    tab.Draw(ui);

                    lastHeight = ui.CurHeight;
                    ui.End();
                    Widgets.EndScrollView();
                }
            }
        }        

        private void DrawGeneralTab(Listing_Standard ui)
        {
            bool isAnimal = DefaultKind.RaceProps.Animal;

            if (!Current.IsGlobal && isAnimal)
            {
                DrawOverride(ui, DefaultKind, ref Current.ReplaceWith, "Replace with...", DrawReplaceWith);
            }

            DrawOverride(ui, DefaultKind.label, ref Current.Label, "Custom name", DrawCustomName);
            if (!Current.IsGlobal && !isAnimal)
            {
                // Human-likes can change race.
                DrawOverride(ui, DefaultKind.race, ref Current.Race, "Species", DrawRace);
                return;
            }

            DrawOverride(ui, DefaultKind.itemQuality, ref Current.ItemQuality, "Average Gear Quality", DrawItemQuality);
        }

        private void DrawAppearanceTab(Listing_Standard ui)
        {
            DrawOverride(ui, null, ref Current.CustomHair, "Forced Hair Styles", DrawHairStyles, GetHeightFor(Current.CustomHair), false);
            DrawOverride(ui, null, ref Current.CustomHairColors, "Forced Hair Colors", DrawHairColors, GetHeightFor(Current.CustomHairColors, 36), false);
        }

        private void DrawHairStyles(Rect rect, bool active, List<HairDef> nullList)
        {
            MenuItemBase MakeItem(HairDef def)
            {
                return new MenuItemIcon(def, $"{def.LabelCap} ({def.modContentPack?.Name ?? "<no-mod>"})", def.Icon)
                {
                    Size = new Vector2(100, 100),
                    BGColor = Color.white
                };
            }

            var sel = DrawDefList(rect, active, ref scrolls[scrollIndex++], Current.CustomHair, nullList, DefDatabase<HairDef>.AllDefsListForReading, true, MakeItem);
            if (sel != null)
            {
                sel.AllowChangeTint = true;
                sel.Tint = new Color32(245, 212, 78, 255);
                sel.Columns = 4;
            }
        }

        private void DrawHairColors(Rect rect, bool active, List<Color> nullList)
        {
            DrawColorList(rect, active, ref scrolls[scrollIndex++], Current.CustomHairColors, nullList);
        }

        private void DrawCustomName(Rect rect, bool active, string defaultName)
        {
            if (active)
            {
                float w = Mathf.Max(400, rect.height * 0.5f);
                var input = rect;
                input.width = w;
                Current.Label = Widgets.TextEntryLabeled(input, "Custom name:  ", Current.Label);
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {defaultName}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }

        private void DrawRace(Rect rect, bool active, ThingDef defaultRace)
        {
            DrawDefSelector(rect, active, AllHumanlikeRaces, Current.Race, DefaultKind.race, r => { Current.Race = r; });
        }

        private void DrawReplaceWith(Rect rect, bool active, PawnKindDef defaultKind)
        {
            DrawDefSelector(rect, active, AllAnimalKindDefs, Current.ReplaceWith, DefaultKind, r => { Current.ReplaceWith = r; });
        }

        private void DrawApparelTab(Listing_Standard ui)
        {
            DrawForceNaked(ui);
            if (Current.ForceNaked)
                return;

            DrawOverride(ui, DefaultKind.apparelMoney, ref Current.ApparelMoney, "Apparel Value", DrawApparelMoney);
            DrawOverride(ui, DefaultKind.apparelTags, ref Current.ApparelTags, "Allowed Apparel Types", DrawApparelTags, GetHeightFor(Current.ApparelTags), true);
            // Disabled for now. Not very useful.
            //DrawOverride(ui, DefaultFac.apparelDisallowTags, ref Current.ApparelDisallowedTags, "Disallowed Apparel Types", DrawDisallowedApparelTags, GetHeightFor(Current.ApparelDisallowedTags), true);
            DrawOverride(ui, DefaultKind.apparelColor, ref Current.ApparelColor, "Apparel Color (where applicable)", DrawApparelColor);
            DrawOverride(ui, DefaultKind.apparelRequired, ref Current.ApparelRequired, "Required Apparel (simple)", DrawRequiredApparel, GetHeightFor(Current.ApparelRequired), true);
            DrawSpecificGear(ui, ref Current.SpecificApparel, "Required Apparel (advanced)", t => t.IsApparel, ThingDefOf.Apparel_Parka);
        }

        private void DrawForceNaked(Listing_Standard ui)
        {
            var nakedBox = ui.GetRect(32);
            Widgets.CheckboxLabeled(nakedBox, "Force naked: ", ref Current.ForceNaked, placeCheckboxNearText: true);
            ui.Gap();
        }

        private void DrawSpecificGear(Listing_Standard ui, ref List<SpecRequirementEdit> edits, string label, Func<ThingDef, bool> thingFilter, ThingDef defaultThing)
        {
            float height = edits == null ? 32 : 300;

            ui.Label($"<b>{label}</b>");
            var rect = ui.GetRect(height);
            bool active = edits != null;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
            {
                if (active)
                    edits = null;
                else
                    edits = new List<SpecRequirementEdit>();                
                active = !active;
            }
            var content = new Rect(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
            Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
            content = content.ExpandedBy(-2);

            ref Vector2 scroll = ref scrolls[scrollIndex++];
            if (active)
            {                
                Widgets.BeginScrollView(content, ref scroll, new Rect(0, 0, 100, 152 * edits.Count - 10));
                var tempUI = new Listing_Standard();
                tempUI.Begin(new Rect(0, 0, content.width - 20, 152 * edits.Count));
                DrawSpecificGearContent(tempUI, thingFilter, edits);
                tempUI.End();
                Widgets.EndScrollView();
                content.y += content.height + 5;
                content.height = 28;
                content.width = 250;
                if(Widgets.ButtonText(content, "<b>Add New</b>"))
                {
                    edits.Add(new SpecRequirementEdit() { Thing = defaultThing });
                }
            }
            else
            {
                string text = "[Default] <i>None</i>";
                GUI.enabled = false;
                Widgets.Label(content.GetCentered(text), text);
                GUI.enabled = true;
            }

            ui.Gap();
        }

        private void DrawSpecificGearContent(Listing_Standard ui, Func<ThingDef, bool> thingFilter, List<SpecRequirementEdit> edits)
        {
            bool active = edits != null;
            if (!active)
                return;            

            for(int i = 0; i < edits.Count; i++)
            {
                var item = edits[i];

                if (item?.Thing == null)
                    continue;

                Rect area = ui.GetRect(140);
                Widgets.DrawBoxSolidWithOutline(area, default, Color.white * 0.75f);

                Rect icon = area;
                icon.width = icon.height = 64;
                Widgets.DefIcon(icon, item.Thing, item.Material, thingStyleDef: item.Style, color: item.Color == default ? null : item.Color);

                Rect label = icon;
                label.x += 70;
                label.y += 14;
                label.width = 225;
                Widgets.LabelFit(label, $"<b>{item.Thing.LabelCap}</b>");

                Rect delete = new Rect(area.xMax - 105, area.y + 5, 100, 20);
                GUI.color = Color.red;
                if(Widgets.ButtonText(delete, "<b>REMOVE</b>"))
                {
                    edits.RemoveAt(i);
                    i--;
                }
                GUI.color = Color.white;

                Rect defSel = area;
                defSel.x += 8;
                defSel.y += 10;
                defSel.width = 220;
                defSel.height = 50;
                Widgets.DrawHighlightIfMouseover(defSel);
                if(Widgets.ButtonInvisible(defSel))
                {
                    var defs = DefDatabase<ThingDef>.AllDefsListForReading.Where(thingFilter);
                    var items = CustomFloatMenu.MakeItems(defs, d => new MenuItemText(d, d.LabelCap, TryGetIcon(d, out var c), c, d.description));
                    CustomFloatMenu.Open(items, raw =>
                    {
                        var a = raw.GetPayload<ThingDef>();
                        item.Thing = a;
                        item.Style = null;
                        item.Material = null;
                    });
                }

                bool canDoStuff = item.Thing?.MadeFromStuff ?? false;
                Rect material = area;
                material.width = 220;
                material.height = 24;
                material.y += 62;
                material.x += 10;

                if (canDoStuff)
                    Widgets.Label(material, "<b>Material: </b>");
                else
                    item.Material = null;

                material.x += 68;
                if (canDoStuff)
                {
                    if (item.Material != null)
                        Widgets.DefLabelWithIcon(material, item.Material, 5);
                    else
                        Widgets.Label(material, "None");
                }                

                material.x = area.x + 5;
                material.width = 220;
                if (canDoStuff)
                {
                    if (item.Material == null)
                    {
                        var faction = Find.WindowStack.WindowOfType<FactionEditUI>()?.Current?.Faction?.Def;
                        var techLevel = MySettings.VanillaRestrictions ? faction?.techLevel ?? TechLevel.Undefined : TechLevel.Undefined;
                        item.Material = GenStuff.AllowedStuffsFor(item.Thing, techLevel).FirstOrDefault();
                    }

                    Widgets.DrawHighlightIfMouseover(material);
                    if (Widgets.ButtonInvisible(material))
                    {
                        var faction = Find.WindowStack.WindowOfType<FactionEditUI>()?.Current?.Faction?.Def;
                        var techLevel = MySettings.VanillaRestrictions ? faction?.techLevel ?? TechLevel.Undefined : TechLevel.Undefined;
                        var defs = GenStuff.AllowedStuffsFor(item.Thing, techLevel);
                        var items = CustomFloatMenu.MakeItems(defs, d => new MenuItemText(d, d.LabelAsStuff.CapitalizeFirst(), TryGetIcon(d, out var c), c, d.description));
                        CustomFloatMenu.Open(items, raw =>
                        {
                            var s = raw.GetPayload<ThingDef>();
                            item.Material = s;
                        });
                    }
                }                

                Rect style = area;
                style.width = 220;
                style.height = 24;
                style.y += 62 + 24;
                style.x += 10;

                Widgets.Label(style, "<b>Style: </b>");
                style.x += 68;
                bool canHaveStyle = item.Thing != null && item.Thing.CanBeStyled();
                if (!canHaveStyle)
                    item.Style = null;
                if (item.Style != null)
                    Widgets.Label(style, item.Style.Category?.LabelCap ?? "<VALID_STYLE_BUT_MISSING_CAT>");
                else
                    Widgets.Label(style, $"None {(canHaveStyle ? "" : "(cannot be styled)")}");

                style.x = area.x + 5;
                Widgets.DrawHighlightIfMouseover(style);
                if (Widgets.ButtonInvisible(style) && canHaveStyle)
                {
                    var items = CustomFloatMenu.MakeItems(StyleHelper.GetValidStyles(item.Thing), s => new MenuItemText(s.style, s.name, s.exampleIcon));
                    items.Add(new MenuItemText(null, "_ No Style _", null, default, "This item will have no style at all."));
                    CustomFloatMenu.Open(items, raw =>
                    {
                        item.Style = raw.Payload == null ? null : raw.GetPayload<ThingStyleDef>();
                    });
                }

                Rect biocode = material;
                biocode.y = material.yMax + 26;
                biocode.x += 4;
                biocode.height -= 4;
                biocode.width = 100;
                if (item.Thing != null && item.Thing.HasAssignableCompFrom(typeof(CompBiocodable)))
                    Widgets.CheckboxLabeled(biocode, "<b>Biocode: </b>", ref item.Biocode);
                else
                    item.Biocode = false;

                bool canDoQuality = item.Thing?.CompDefForAssignableFrom<CompQuality>() != null;
                Rect qualityCheck = area;
                qualityCheck.x += 230;
                qualityCheck.y += 10;
                qualityCheck.width = 150;
                qualityCheck.height = 28;
                if(canDoQuality && Widgets.ButtonText(qualityCheck, $"<b>Specific quality: </b><color={(item.Quality != null ? "#81f542" : "#ff4d4d")}>{(item.Quality != null ? "Yes" : "No")}</color>"))
                {
                    if(item.Quality == null)
                    {
                        item.Quality = QualityCategory.Normal;
                    }
                    else
                    {
                        item.Quality = null;
                    }
                }
                else if(!canDoQuality)
                {
                    item.Quality = null;
                }

                Rect quality = qualityCheck;
                quality.y += 34;
                if (canDoQuality && item.Quality != null && Widgets.ButtonText(quality, item.Quality.ToString()))
                {
                    var enums = Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>();
                    FloatMenuUtility.MakeMenu(enums, e => e.ToString(), e => () =>
                    {
                        item.Quality = e;
                    });
                }

                bool canDoColor = item.Thing?.CompDefForAssignableFrom<CompColorable>() != null;
                Rect color = quality;
                color.x += 2;
                color.y += 34;
                if(canDoColor)
                    Widgets.Label(color, "<b>Color: </b>");
                color.x += 60;
                bool isDefault = item.Color == default;

                if (canDoColor)
                {
                    Widgets.DrawBoxSolidWithOutline(color, item.Color, Color.white);
                    Widgets.DrawHighlightIfMouseover(color);
                    if (Widgets.ButtonInvisible(color))
                    {
                        if (isDefault)
                            item.Color = Color.white;
                        Find.WindowStack.Add(new Window_ColorPicker(item.Color, c =>
                        {
                            c.a = 1f;
                            item.Color = c;
                        }));
                    }
                    if (isDefault)
                    {
                        Widgets.Label(color.GetCentered("No color"), "No color");
                    }
                    else
                    {
                        var c = item.Color;
                        c.a = 1f;
                        item.Color = c;

                        color.x += 154;
                        color.width = 48;
                        if (Widgets.ButtonText(color, "Clear"))
                        {
                            item.Color = default;
                        }
                    }
                }
                else
                {
                    item.Color = default;
                }

                Rect modeBox = area;
                modeBox.xMin += 500;
                modeBox.y += 45;
                modeBox.width = 220;

                string name = item.SelectionMode switch
                {
                    ApparelSelectionMode.AlwaysTake => "Always picked",
                    ApparelSelectionMode.RandomChance => "Random chance to be picked",
                    ApparelSelectionMode.FromPool1 => "Part of pool 1",
                    ApparelSelectionMode.FromPool2 => "Part of pool 2",
                    ApparelSelectionMode.FromPool3 => "Part of pool 3",
                    ApparelSelectionMode.FromPool4 => "Part of pool 4",
                    _ => ""
                };

                Rect modeLabel = modeBox;
                modeLabel = modeLabel.ExpandedBy(-5);
                modeLabel.height = 30;
                Widgets.Label(modeLabel, "Selection mode:");

                Rect modeButton = modeBox;
                modeButton = modeButton.ExpandedBy(-5);
                modeButton.y += 22;
                modeButton.height = 30;

                if(Widgets.ButtonText(modeButton, name))
                {
                    IEnumerable<ApparelSelectionMode> MakeEnumerable(Array normal)
                    {
                        foreach (var item in normal)
                            yield return (ApparelSelectionMode)item;
                    }

                    string Name(ApparelSelectionMode mode)
                    {
                        switch (mode)
                        {
                            case ApparelSelectionMode.AlwaysTake:
                                return "Always picked";
                            case ApparelSelectionMode.RandomChance:
                                return "Random chance to be picked";
                            case ApparelSelectionMode.FromPool1:
                                return "Part of pool 1";
                            case ApparelSelectionMode.FromPool2:
                                return "Part of pool 2";
                            case ApparelSelectionMode.FromPool3:
                                return "Part of pool 3";
                            case ApparelSelectionMode.FromPool4:
                                return "Part of pool 4";
                            default:
                                return mode.ToString();
                        }
                    }
                    
                    var values = MakeEnumerable(Enum.GetValues(typeof(ApparelSelectionMode)));
                    FloatMenuUtility.MakeMenu(values, e => Name(e), e => () =>
                    {
                        item.SelectionMode = e;
                    });                    
                }

                Rect chanceRect = modeBox;
                chanceRect = modeButton.ExpandedBy(-5);
                chanceRect.y += 34;
                chanceRect.height = 30;
                if(item.SelectionMode != ApparelSelectionMode.AlwaysTake)
                    Widgets.HorizontalSlider(chanceRect, ref item.SelectionChance, FloatRange.ZeroToOne, label: $"{(item.SelectionMode == ApparelSelectionMode.RandomChance ? "Chance" : "Weight")}: {item.SelectionChance * 100f:F0}%");

                ui.Gap();
            }
        }

        private void DrawWeaponTab(Listing_Standard ui)
        {
            DrawOverride(ui, DefaultKind.weaponMoney, ref Current.WeaponMoney, "Weapon Value", DrawWeaponMoney);
            DrawOverride(ui, QualityCategory.Normal, ref Current.ForcedWeaponQuality, "Forced Weapon Quality", DrawWeaponQuality);
            DrawOverride(ui, DefaultKind.biocodeWeaponChance, ref Current.BiocodeWeaponChance, "Biocode Chance", DrawBiocodeChance);
            DrawOverride(ui, DefaultKind.weaponTags, ref Current.WeaponTags, "Allowed Weapon Types", DrawWeaponTags, GetHeightFor(Current.WeaponTags), true);
            DrawSpecificGear(ui, ref Current.SpecificWeapons, "Required Weapons (advanced)", t => t.IsWeapon, ThingDef.Named("Gun_AssaultRifle"));
        }

        private void DrawImplantsAndBionicsTab(Listing_Standard ui)
        {
            DrawOverride(ui, DefaultKind.techHediffsMoney, ref Current.TechMoney, "Implants & Bionics Value", DrawTechMoney);
            DrawOverride(ui, DefaultKind.techHediffsTags, ref Current.TechHediffTags, "Allowed Implants & Bionics Types", DrawTechTags, GetHeightFor(Current.TechHediffTags), true);
            DrawOverride(ui, DefaultKind.techHediffsDisallowTags, ref Current.TechHediffDisallowedTags, "Disallowed Implants & Bionics Types", DrawDisallowedTechTags, GetHeightFor(Current.TechHediffDisallowedTags), true);
            DrawOverride(ui, DefaultKind.techHediffsRequired, ref Current.TechRequired, "Required Implants & Bionics", DrawRequiredTech, GetHeightFor(Current.TechRequired), true);
            DrawOverride(ui, DefaultKind.techHediffsChance, ref Current.TechHediffChance, "Implants & Bionics Chance", DrawTechChance);
            DrawOverride(ui, DefaultKind.techHediffsMaxAmount, ref Current.TechHediffsMaxAmount, "Max # of Implants & Bionics", DrawMaxTech);
        }

        private void DrawInventoryTab(Listing_Standard ui)
        {
            if (Current.IsGlobal)
            {
                var rect = ui.GetRect(30);
                Widgets.DrawHighlightIfMouseover(rect);
                TooltipHandler.TipRegion(rect, "If true, the override inventory entirely replaces the default one.\nIf false, the override inventory is added to the default inventory.");
                Widgets.CheckboxLabeled(rect, "Replace default inventory? ", ref Current.ReplaceDefaultInventory, placeCheckboxNearText: true);
            }
            DrawInventory(ui);
        }

        private float GetHeightFor(IList list, float itemHeight = 26)
        {
            if (list == null)
                return 32;

            return Math.Min(36 + itemHeight * 1 + (list.Count - 1) * itemHeight, 120);
        }

        private void DrawInventory(Listing_Standard ui)
        {
            float height = 32;
            var field = Current.Inventory;

            ui.Label($"<b>Inventory</b>");
            var rect = ui.GetRect(height);
            bool active = field != null;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
            {
                if (active)
                {
                    Current.Inventory = null;
                }
                else
                {
                    Current.Inventory = new InventoryOptionEdit(Current.Def.inventoryOptions);
                }
                active = !active;
            }
            var content = new Rect(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
            Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
            content = content.ExpandedBy(-2);
            GUI.enabled = active;

            if(Current.Inventory != null)
            {
                Current.Inventory.Thing = null;
                DrawInvPart(ui, Current.Inventory, false, false);
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] Max. {InventoryOptionEdit.GetSize(Current.Def.inventoryOptions)} items";
                Widgets.Label(content.GetCentered(txt), txt);
            }

            GUI.enabled = true;
        }

        private bool DrawInvPart(Listing_Standard ui, InventoryOptionEdit part, bool isChildOfAll, bool isChildOfOne)
        {
            var defRect = ui.GetRect(28);
            bool delete = false;

            if(part.Thing != null)
            {
                var delRect = defRect;
                delRect.width = 48;
                GUI.color = Color.red;
                if(Widgets.ButtonText(delRect, " [DEL]"))
                {
                    delete = true;
                }
                GUI.color = Color.white;
                defRect.xMin += 52;

                if(isChildOfAll || isChildOfOne)
                    defRect.xMin += 100;
                defRect.width = 240;
                Widgets.DefLabelWithIcon(defRect, part.Thing);
                if (Widgets.ButtonInvisible(defRect))
                {
                    var items = CustomFloatMenu.MakeItems(AllInvItems, d => new MenuItemText(d, d.LabelCap, TryGetIcon(d, out var c), c, d.description));
                    CustomFloatMenu.Open(items, raw =>
                    {
                        part.Thing = raw.GetPayload<ThingDef>();
                    });
                }

                var clone = defRect;
                if (isChildOfAll || isChildOfOne)
                {
                    defRect.y += 14;
                    defRect.xMin -= 100;
                    defRect.width = 100;
                    defRect.height = 20;

                    if(isChildOfAll)
                        Widgets.HorizontalSlider(defRect, ref part.SkipChance, FloatRange.ZeroToOne, label: $"Skip chance: {100f * part.SkipChance:F0}%");
                    if(isChildOfOne)
                        Widgets.HorizontalSlider(defRect, ref part.ChoiceChance, FloatRange.ZeroToOne, label: $"Weight: {100f * part.ChoiceChance:F0}%");
                }

                clone.x += 220;
                clone.width = 100;
                int min = part.CountRange.min;
                int max = part.CountRange.max;

                part.BufferA ??= min.ToString();
                part.BufferB ??= max.ToString();

                Widgets.TextFieldNumericLabeled(clone, "Min:  ", ref min, ref part.BufferA, 1);
                clone.x += 110;
                Widgets.TextFieldNumericLabeled(clone, "Max:  ", ref max, ref part.BufferB, 1);

                part.CountRange = new IntRange(min, max);
            }

            bool hasTakeAll = part.SubOptionsTakeAll?.Count > 0;
            bool hasTakeOne = part.SubOptionsChooseOne?.Count > 0;
            ui.Gap(5);
            Rect addRect = ui.GetRect(20);
            addRect.width = 80;
            if(hasTakeAll)
            {
                ui.Label("<b>TAKE ALL:</b>");
                ui.Indent(20);
                for (int i = 0; i < part.SubOptionsTakeAll.Count; i++)
                {
                    if (DrawInvPart(ui, part.SubOptionsTakeAll[i], true, false))
                    {
                        part.SubOptionsTakeAll.RemoveAt(i);
                        i--;
                    }
                }
                ui.Outdent(20);
            }

            if (hasTakeOne)
            {
                ui.Label("<b>TAKE ONE:</b>");
                ui.Indent(20);
                for (int i = 0; i < part.SubOptionsChooseOne.Count; i++)
                {
                    if(DrawInvPart(ui, part.SubOptionsChooseOne[i], false, true))
                    {
                        part.SubOptionsChooseOne.RemoveAt(i);
                        i--;
                    }
                }
                ui.Outdent(20);
            }

            if (Widgets.ButtonText(addRect, "+ Take all"))
            {
                part.SubOptionsTakeAll ??= new List<InventoryOptionEdit>();
                part.SubOptionsTakeAll.Add(new InventoryOptionEdit());
            }
            addRect.x += 90;
            if (Widgets.ButtonText(addRect, "+ Take one"))
            {
                part.SubOptionsChooseOne ??= new List<InventoryOptionEdit>();
                part.SubOptionsChooseOne.Add(new InventoryOptionEdit());
            }

            ui.GapLine();

            return delete;
        }

        private void DrawChance(ref float? field, float defaultValue, Rect rect, bool active)
        {
            if (active)
            {
                var fieldVal = field!.Value;
                Widgets.HorizontalSlider(rect, ref fieldVal, FloatRange.ZeroToOne, label: $"Chance: {(100f * field):F0}% (default: {(100f * defaultValue):F0}%)");
                field = fieldVal;
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {(100f * defaultValue):F0}%";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }

        private void DrawEnumSelector<T>(Rect rect, bool active, T? field, T defaultValue, Action<T> apply, Func<T, string> makeName = null) where T : struct
        {
            string Name(T t) => makeName == null ? t.ToString() : makeName(t);

            IEnumerable<object> MakeEnumerable(IEnumerable normal)
            {
                foreach (var item in normal)
                    yield return item;
            }

            if (Widgets.ButtonText(rect, active ? Name(field.Value) : (Current.IsGlobal ? "---" : $"[Default] {Name(defaultValue)}")))
            {
                var values = MakeEnumerable(Enum.GetValues(typeof(T)));
                FloatMenuUtility.MakeMenu(values, e => Name((T)e), e => () => apply((T)e));
            }
        }

        private void DrawDefSelector<T>(Rect rect, bool active, IEnumerable<T> defs, T field, T defaultValue, Action<T> apply, Func<T, string> makeName = null) where T : Def
        {
            string Name(T t) => makeName == null ? (string)t.LabelCap : makeName(t);

            if (Widgets.ButtonText(rect, active ? Name(field) : (Current.IsGlobal ? "---" : $"[Default] {Name(defaultValue)}")))
            {
                var items = CustomFloatMenu.MakeItems(defs, d => new MenuItemText(d, d.LabelCap, TryGetIcon(d, out var c), c, d.description));
                CustomFloatMenu.Open(items, raw =>
                {
                    var real = raw.GetPayload<T>();
                    apply(real);
                });
            }
        }

        private void DrawItemQuality(Rect rect, bool active, QualityCategory _)
        {
            DrawEnumSelector(rect, active, Current.ItemQuality, Current.Def.itemQuality, q => Current.ItemQuality = q);
        }

        private void DrawWeaponQuality(Rect rect, bool active, QualityCategory _)
        {
            DrawEnumSelector(rect, active, Current.ForcedWeaponQuality, Current.Def.forceWeaponQuality ?? QualityCategory.Normal, q => Current.ForcedWeaponQuality = q); ;
        }

        private void DrawApparelColor(Rect rect, bool active, Color def)
        {
            if (active)
            {
                var label = rect;
                label = label.ExpandedBy(-3);
                label.width = 100;

                var picker = rect;
                picker.xMin += 100;
                picker = picker.ExpandedBy(-3);

                Widgets.Label(label, "Pick color: ");
                if (Mouse.IsOver(picker))
                {
                    var border = Color.white - Current.ApparelColor.Value;
                    border.a = 1;
                    border = Color.Lerp(border, Current.ApparelColor.Value, 0.2f);
                    Widgets.DrawBoxSolidWithOutline(picker, Current.ApparelColor.Value, border, 2);
                }
                else
                {
                    Widgets.DrawBoxSolid(picker, Current.ApparelColor.Value);
                }
                if (Widgets.ButtonInvisible(picker))
                {
                    Find.WindowStack.Add(new Window_ColorPicker(Current.ApparelColor.Value, col =>
                    {
                        col.a = 1f;
                        Current.ApparelColor = col;
                    })
                    { 
                        grayOutIfOtherDialogOpen = false,
                    });
                }
            }
            else
            {
                bool forced = Current.Def.apparelColor != Color.white;
                string txt = $"Color: {(forced ? "" : "None specified")}";
                var label = rect;
                label = label.ExpandedBy(-3);
                label.width = 200;
                var preview = rect;
                preview.xMin += 100;
                preview = preview.ExpandedBy(-3);
                Widgets.Label(label, txt);
                if (forced)                
                    Widgets.DrawBoxSolidWithOutline(preview, Current.Def.apparelColor, Color.black, 2);                
            }
        }

        private void DrawBiocodeChance(Rect rect, bool active, float def)
        {
            DrawChance(ref Current.BiocodeWeaponChance, def, rect, active);
        }        

        private void DrawTechChance(Rect rect, bool active, float def)
        {
            DrawChance(ref Current.TechHediffChance, def, rect, active);
        }

        private void DrawMaxTech(Rect rect, bool active, int _)
        {
            if (maxTechBuffer == null && active)
                maxTechBuffer = Current.TechHediffsMaxAmount.Value.ToString();

            if (active)
            {
                int value = Current.TechHediffsMaxAmount.Value;
                Widgets.IntEntry(rect, ref value, ref maxTechBuffer);
                Current.TechHediffsMaxAmount = value;
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {Current.Def.techHediffsMaxAmount}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }
        
        private void DrawTechTags(Rect rect, bool active, List<string> defaultTags)
        {
            DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.TechHediffTags, Current.Def.techHediffsTags, AllTechHediffTags);
        }

        private void DrawDisallowedTechTags(Rect rect, bool active, List<string> defaultTags)
        {
            DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.TechHediffDisallowedTags, Current.Def.techHediffsDisallowTags, AllTechHediffTags);
        }

        private void DrawApparelTags(Rect rect, bool active, List<string> defaultTags)
        {
            if (!AllApparelTags.Contains("UNUSED"))
                AllApparelTags.Add("UNUSED");
            DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ApparelTags, Current.Def.apparelTags, AllApparelTags);
        }

        private void DrawDisallowedApparelTags(Rect rect, bool active, List<string> defaultTags)
        {
            DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ApparelDisallowedTags, Current.Def.apparelDisallowTags, AllApparelTags);
        }

        private void DrawRequiredApparel(Rect rect, bool active, List<ThingDef> defaultReq)
        {
            DrawDefList(rect, active, ref scrolls[scrollIndex++], Current.ApparelRequired, Current.Def.apparelRequired, AllApparel, false);
        }

        private void DrawWeaponTags(Rect rect, bool active, List<string> defaultTags)
        {
            DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.WeaponTags, Current.Def.weaponTags, AllWeaponsTags);
        }

        private void DrawRequiredTech(Rect rect, bool active, List<ThingDef> defaultReq)
        {
            DrawDefList(rect, active, ref scrolls[scrollIndex++], Current.TechRequired, Current.Def.techHediffsRequired, AllTech, true);
        }

        private CustomFloatMenu DrawDefList<T>(Rect rect, bool active, ref Vector2 scroll, IList<T> current, IList<T> defaultThings, IEnumerable<T> allThings, bool allowDupes, Func<T, MenuItemBase> makeItems = null) where T : Def
        {
            string MakeString(IList<T> list)
            {
                if (list == null || list.Count == 0)
                    return "<i>None</i>";

                var raw = string.Join(", ", list);
                if (raw.Length > 43)
                    raw = raw.Substring(0, 40) + "...";
                return raw;
            }

            if (active)
            {
                CustomFloatMenu toReturn = null;
                if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
                {
                    var items = CustomFloatMenu.MakeItems(allThings, makeItems ?? (d => new MenuItemText(d, d.LabelCap, TryGetIcon(d, out var c), c, d.description)));
                    toReturn = CustomFloatMenu.Open(items, raw =>
                    {
                        var t = raw.GetPayload<T>();
                        if (allowDupes || !current.Contains(t))
                                current.Add(t);
                    });
                }
                rect.yMin += 30;

                Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 26 * current.Count));
                Rect curr = new Rect(26, 3, 1000, 30);
                Rect currButton = new Rect(3, 3, 20, 20);

                foreach (var thing in current)
                {
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(currButton, " X"))
                        thingBin.Add(thing);
                    GUI.color = Color.white;
                    if (thing != null && thing is not StyleItemDef)
                        Widgets.DefLabelWithIcon(curr, thing);
                    else if (thing is StyleItemDef si)
                    {
                        Rect label = curr;
                        label.xMin += 34;
                        Rect icon = curr;
                        icon.width = icon.height;
                        Widgets.DrawTextureFitted(icon, si.Icon, 1f);
                        Widgets.Label(label, si.LabelCap);
                    }

                    curr.y += 26;
                    currButton.y += 26;
                }
                Widgets.EndScrollView();

                foreach (var item in thingBin)
                {
                    current.Remove((T)item);
                }
                thingBin.Clear();

                return toReturn;
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {MakeString(defaultThings)}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
            return null;
        }

        private void DrawApparelMoney(Rect rect, bool active, FloatRange defaultRange)
        {
            DrawFloatRange(rect, active, ref Current.ApparelMoney, Current.Def.apparelMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
        }

        private void DrawTechMoney(Rect rect, bool active, FloatRange defaultRange)
        {
            DrawFloatRange(rect, active, ref Current.TechMoney, Current.Def.techHediffsMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
        }

        private void DrawWeaponMoney(Rect rect, bool active, FloatRange defaultRange)
        {
            DrawFloatRange(rect, active, ref Current.WeaponMoney, Current.Def.weaponMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
        }

        private void DrawFloatRange(Rect rect, bool active, ref FloatRange? current, FloatRange defaultRange, ref string buffer, ref string buffer2)
        {
            if (active)
            {
                int value = (int)current.Value.min;
                Rect left = rect;
                left.width = 220;
                Widgets.IntEntry(left, ref value, ref buffer);
                current = new FloatRange(value, current.Value.max);

                value = (int)current.Value.max;
                Rect right = new Rect(rect.xMax - 220, rect.y, 220, rect.height);
                Widgets.IntEntry(right, ref value, ref buffer2);
                current = new FloatRange(current.Value.min, value);

                Rect mid = new Rect(rect);
                mid.xMin += 220;
                mid.xMax -= 220;
                string txt = $"{current.Value.TrueMin:F0} to {current.Value.TrueMax:F0}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {defaultRange}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }

        private void DrawColorList(Rect rect, bool active, ref Vector2 scroll, IList<Color> current, IList<Color> defaultColors)
        {
            string MakeString(IList<Color> list)
            {
                if (list == null || list.Count == 0)
                    return "<i>None</i>";

                var raw = string.Join(", ", list);
                if (raw.Length > 73)
                    raw = raw.Substring(0, 70) + "...";
                return raw;
            }

            if (active)
            {
                if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
                {
                    Find.WindowStack.Add(new Window_ColorPicker(new Color32(240, 216, 122, 255), selected =>
                    {
                        selected.a = 1f;
                        current.Add(selected);
                    }));
                }
                rect.yMin += 30;

                Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 38 * current.Count));
                Rect curr = new Rect(26, 3, rect.width, 36);
                Rect currButton = new Rect(3, 3, 20, 20);

                for(int i = 0; i < current.Count; i++)
                {
                    var color = current[i];

                    GUI.color = Color.red;
                    if (Widgets.ButtonText(currButton, " X"))
                    {
                        current.RemoveAt(i);
                        i--;
                        continue;
                    }
                    GUI.color = Color.white;

                    Rect real = curr.ExpandedBy(-4, -2);

                    Widgets.DrawBoxSolid(real, color);
                    Widgets.DrawHighlightIfMouseover(real);
                    if (Widgets.ButtonInvisible(real))
                    {
                        Find.WindowStack.Add(new Window_ColorPicker(color, selected =>
                        {
                            selected.a = 1f;
                            current[i] = selected;
                        }));
                    }

                    curr.y += 38;
                    currButton.y += 38;
                }
                Widgets.EndScrollView();
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {MakeString(defaultColors)}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }

        private void DrawStringList(Rect rect, bool active, ref Vector2 scroll, IList<string> current, IList<string> defaultTags, IEnumerable<string> allTags)
        {
            string MakeString(IList<string> list)
            {
                if (list == null || list.Count == 0)
                    return "<i>None</i>";

                var raw = string.Join(", ", list);
                if (raw.Length > 73)
                    raw = raw.Substring(0, 70) + "...";
                return raw;
            }

            if (active)
            {
                if (Widgets.ButtonText(new Rect(rect.x + 3, rect.y + 3, 130, 26), "Add new..."))
                {
                    var items = CustomFloatMenu.MakeItems(allTags, t => new MenuItemText(t, t));
                    CustomFloatMenu.Open(items, raw =>
                    {
                        string t = raw.GetPayload<string>();
                        if (!current.Contains(t))
                            current.Add(t);
                    });
                }
                rect.yMin += 30;

                Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, 100, 26 * current.Count));
                Rect curr = new Rect(26, 3, 1000, 30);
                Rect currButton = new Rect(3, 3, 20, 20);

                foreach (var tag in current)
                {
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(currButton, " X"))
                        tagBin.Add(tag);
                    GUI.color = Color.white;
                    Widgets.Label(curr, tag);

                    curr.y += 26;
                    currButton.y += 26;
                }
                Widgets.EndScrollView();

                foreach (var item in tagBin)
                {
                    current.Remove(item);
                }
                tagBin.Clear();
            }
            else
            {
                string txt = Current.IsGlobal ? "---" : $"[Default] {MakeString(default)}";
                Widgets.Label(rect.GetCentered(txt), txt);
            }
        }

        // Generic, takes T and T?
        private void DrawOverride<T>(Listing_Standard ui, T defaultValue, ref T? field, string label, Action<Rect, bool, T> drawContent, float height = 32) where T : struct
        {
            ui.Label($"<b>{label}</b>");
            var rect = ui.GetRect(height);
            bool active = field != null;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
            {
                if (active)
                    field = null;
                else
                {
                    field = defaultValue;                    
                }
                active = !active;
            }
            var content = new Rect(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
            Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
            content = content.ExpandedBy(-2);
            GUI.enabled = active;
            drawContent(content, active, defaultValue);
            GUI.enabled = true;
            ui.Gap();
        }

        // Generic, takes T and T
        private void DrawOverride<T>(Listing_Standard ui, T defaultValue, ref T field, string label, Action<Rect, bool, T> drawContent, float height = 32) where T : class
        {
            ui.Label($"<b>{label}</b>");
            var rect = ui.GetRect(height);
            bool active = field != null;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
            {
                if (active)
                    field = null;
                else
                {
                    field = defaultValue;
                }
                active = !active;
            }
            var content = new Rect(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
            Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
            content = content.ExpandedBy(-2);
            GUI.enabled = active;
            drawContent(content, active, defaultValue);
            GUI.enabled = true;
            ui.Gap();
        }

        // For lists, takes T and T
        private void DrawOverride<T>(Listing_Standard ui, T defaultValue, ref T field, string label, Action<Rect, bool, T> drawContent, float height = 32, bool cloneDefault = true) where T : IList
        {
            ui.Label($"<b>{label}</b>");
            var rect = ui.GetRect(height);
            bool active = field != null;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 120, 32), $"Override: <color={(active ? "#81f542" : "#ff4d4d")}>{(active ? "Yes" : "No")}</color>"))
            {
                if (active)
                    field = default;
                else
                {  
                    field = (T)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T).GenericTypeArguments));
                    if(cloneDefault && defaultValue != null)
                        foreach (var value in defaultValue)
                            field.Add(value);                       
                }
                active = !active;
            }
            var content = new Rect(rect.x + 122, rect.y, ui.ColumnWidth - 124, rect.height);
            Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
            content = content.ExpandedBy(-2);
            GUI.enabled = active;
            drawContent(content, active, defaultValue);
            GUI.enabled = true;
            ui.Gap();
        }

        private class Tab
        {
            public readonly string Name;

            private readonly Action<Listing_Standard> draw;

            public Tab(string name, Action<Listing_Standard> draw)
            {
                this.Name = name;
                this.draw = draw;
            }

            public void Draw(Listing_Standard ui)
            {
                DrawRegionTitle(ui, Name);
                draw?.Invoke(ui);
            }
        }
    }
}
