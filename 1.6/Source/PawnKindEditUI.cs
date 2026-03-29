using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using FactionLoadout.UISupport.DrawSupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

[HotSwappable]
public class PawnKindEditUI : Window
{
    public readonly PawnKindEdit Current;

    private readonly Dictionary<Tab, float> tabHeights = new();
    private Vector2 globalScroll;
    private int selectedTab;
    private List<Tab> tabs;

    public PawnKindDef DefaultKind
    {
        get
        {
            if (Current.DeletedOrClosed)
                return Current.Def;

            FactionDef found = FactionEdit.TryGetOriginal(Current.ParentEdit.Faction.DefName);
            if (found == null)
                return Current.Def;
            PawnKindDef found2 = found.GetKindDefs().FirstOrDefault(k => k.defName == Current.Def.defName);
            return found2 ?? Current.Def;
        }
    }

    public PawnKindEditUI(PawnKindEdit toEdit)
    {
        draggable = true;
        resizeable = true;
        doCloseX = true;
        Current = toEdit;

        DefCache.ScanDefs();
    }

    public override void PostOpen()
    {
        base.PostOpen();
        windowRect = new Rect(UI.screenWidth * 0.5f, 30, UI.screenWidth * 0.5f - 20, UI.screenHeight - 50);
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (Current == null || Current.DeletedOrClosed)
        {
            Close();
            return;
        }

        Text.Font = GameFont.Small;

        if (tabs == null)
            BuildTabs();

        if ((tabs?.Count ?? 0) == 0)
        {
            Widgets.Label(inRect, "No editable properties for this pawn kind.");
            return;
        }

        Rect titleArea = inRect;
        titleArea.height = 40;
        string title = $"<size=32><b>Pawn Type: <color=#cf9af5>{(Current.IsGlobal ? "Global (affects all faction pawns)" : Current.Def.LabelCap)}</color></b></size>";
        Widgets.Label(titleArea, title);

        Rect tabRect = inRect;
        float tabRows = (float)Math.Ceiling(tabs.Count / 5f);
        tabRect.height = tabRows * 50 + 50;
        tabRect.y += 50;

        for (int i = 0; i < tabs.Count; i++)
        {
            float row = (float)Math.Floor(i / 5f);
            if (row > 0 && i % 5 == 0)
            {
                tabRect.ExpandedBy(0, 50f);
                tabRect.yMin += 50;
            }

            Rect button = tabRect;
            button.height = 40;
            button.width = 140;
            button.x += 150 * (i - 5 * row);

            Tab tab = tabs[i];
            Color bg = selectedTab == i ? new Color32(49, 82, 133, 255) : new Color(0.2f, 0.2f, 0.2f, 1f);
            if (Widgets.CustomButtonText(ref button, $"<b>{tab.Name}</b>", bg, Color.white, Color.white))
                selectedTab = i;

            if (selectedTab != i)
                continue;

            float toolbarY = inRect.y + 100 + 50 * (tabRows - 1);
            ClipboardToolbar.Draw(
                new Rect(inRect.x, toolbarY, inRect.width, 28),
                Current,
                () =>
                {
                    if (selectedTab >= 0 && selectedTab < tabs.Count && tabs[selectedTab] is EditTab et)
                        et.ResetBuffers();
                }
            );

            Rect contentArea = inRect;
            contentArea.yMin += 100 + 50 * (tabRows - 1) + 32;
            float tabContentH = tabHeights.TryGetValue(tab, out float storedH)
                ? Mathf.Max(storedH, contentArea.height)
                : contentArea.height;
            Widgets.BeginScrollView(contentArea, ref globalScroll, new Rect(0, 0, inRect.width - 24, tabContentH));

            Listing_Standard ui = new() { ColumnWidth = inRect.width - 24 };
            ui.Begin(new Rect(0, 0, inRect.width - 24, 1000000));

            tab.Draw(ui);

            tabHeights[tab] = ui.CurHeight;
            ui.End();
            Widgets.EndScrollView();
        }
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
