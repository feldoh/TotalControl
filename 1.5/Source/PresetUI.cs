using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class PresetUI : Window
{
    public static void OpenEditor(Preset pre)
    {
        if (pre == null)
            return;

        Find.WindowStack.Add(new PresetUI(pre));
    }

    public readonly Preset Current;

    private Vector2 scroll;

    public PresetUI(Preset pre)
    {
        Current = pre;
        draggable = true;
        resizeable = true;
        doCloseX = false;
        closeOnCancel = false;
        closeOnCancel = false;
        closeOnClickedOutside = false;
    }

    public override void PostOpen()
    {
        base.PostOpen();

        windowRect = new Rect(20, 110, Mathf.Max(UI.screenWidth * 0.5f - 550, 450), 500);
    }

    public override void PostClose()
    {
        base.PostClose();
        Find.WindowStack.WindowOfType<FactionEditUI>()?.Close();
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (Current == null)
        {
            Close();
            return;
        }

        Listing_Standard ui = new();
        ui.Begin(inRect);

        Rect rect = ui.GetRect(50);
        Widgets.Label(rect, $"<size=34><b>Preset: <color=#cf9af5>{Current.Name}</color></b></size>");

        Rect buttonsRect = ui.GetRect(32);

        // Save button
        Rect button = buttonsRect;
        button.x = Mathf.Lerp(button.x, button.xMax, 0f);
        button.width *= 0.3f;
        button = button.ExpandedBy(-2f, -5f);
        GUI.color = Color.green;
        if (Widgets.ButtonText(button, "<color=white>SAVE</color>"))
            Current.Save();

        // Save & exit
        button = buttonsRect;
        button.x = Mathf.Lerp(button.x, button.xMax, 1f / 3f);
        button.width *= 0.3f;
        button = button.ExpandedBy(-2f, -5f);
        if (Widgets.ButtonText(button, "<color=white>SAVE & EXIT</color>"))
        {
            Current.Save();
            Close();
        }

        // Exit button.
        GUI.color = Color.Lerp(Color.white, Color.red, 0.65f);
        button = buttonsRect;
        button.x = Mathf.Lerp(button.x, button.xMax, 2f / 3f);
        button.width *= 0.3f;
        button = button.ExpandedBy(-2f, -5f);
        if (Widgets.ButtonText(button, "<color=yellow>EXIT</color>"))
            Close();

        GUI.color = Color.white;
        ui.GapLine();

        // Missing faction handling.
        if (Current.HasMissingFactions())
        {
            ui.Label("<color=red><b>WARNING:</b> This preset has missing factions, probably because they are added by a mod that is not loaded:</color>");
            ui.Label("<b>Missing factions</b>");
            ui.GapLine();
            foreach (string str in Current.GetMissingFactionAndModNames())
                ui.Label($" - {str}");
        }

        Rect nameArea = ui.GetRect(28);
        nameArea.width = 200;
        Widgets.Label(nameArea, "Edit name: ");
        nameArea.x += 80;
        nameArea.height -= 5;
        Current.Name = Widgets.TextField(nameArea, Current.Name);

        ui.Label($"<b>This preset edits {Current.factionChanges.Count} factions:</b>");
        ui.Gap();

        Widgets.BeginScrollView(ui.GetRect(200), ref scroll, new Rect(0, 0, inRect.width - 20, Current.factionChanges.Count * (28 * 2 + 10)));

        Listing_Standard oldUI = ui;
        ui = new Listing_Standard();
        ui.Begin(new Rect(0, 0, inRect.width - 20, 99999));

        for (int i = 0; i < Current.factionChanges.Count; i++)
        {
            FactionEdit item = Current.factionChanges[i];
            Rect area = ui.GetRect(28);
            Widgets.Label(area, $"<b>{item.Faction.LabelCap}</b> <i>({item.Faction.DefName})</i>");

            area = ui.GetRect(28);
            area.width = 80;
            area.y -= 5;
            GUI.color = Color.red;
            if (Widgets.ButtonText(area, "[DELETE]"))
            {
                item.DeletedOrClosed = true;
                Current.factionChanges.RemoveAt(i);
                i--;
                continue;
            }

            GUI.color = Color.white;

            area.x += 90;
            if (item.Faction.IsMissing)
            {
                item.Active = false;
            }
            else
            {
                if (Widgets.ButtonText(area, "EDIT"))
                    FactionEditUI.OpenEditor(item);
                area.x += 90;
                Widgets.CheckboxLabeled(area, "Enabled", ref item.Active, placeCheckboxNearText: true);
            }

            ui.GapLine(10);
        }

        ui.End();
        Widgets.EndScrollView();
        ui = oldUI;

        ui.Gap();
        if (ui.ButtonText("Add new faction edit..."))
        {
            List<FactionDef> raw = DefDatabase<FactionDef>.AllDefsListForReading.Where(f => !Current.HasEditFor(f)).ToList();
            if (!Current.HasEditFor(Preset.SpecialCreepjoinerFaction) && !raw.Any(f => f.defName == Preset.SpecialCreepjoinerFaction.defName))
            {
                raw.Add(Preset.SpecialCreepjoinerFaction);
            }
            if (!Current.HasEditFor(Preset.SpecialWildManFaction) && !raw.Any(f => f.defName == Preset.SpecialWildManFaction.defName))
            {
                raw.Add(Preset.SpecialWildManFaction);
            }
            List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                raw,
                f => new MenuItemText(f, $"{f.LabelCap} ({f.defName})", PawnKindEditUI.TryGetIcon(f, out Color c), c, f.description)
            );

            CustomFloatMenu.Open(
                items,
                menuItemBase =>
                {
                    FactionDef e = menuItemBase.GetPayload<FactionDef>();

                    FactionEdit edit = new() { Faction = e };
                    Current.factionChanges.Add(edit);
                }
            );
        }

        ui.End();
    }
}
