using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport;
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

        windowRect = new Rect(20, 110, Mathf.Max(UI.screenWidth * 0.5f - 550, 450), 1000);
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
        string saveLabel = Current.IsPackaged ? "FactionLoadout_SaveToSourceFile".Translate().ToString() : "Save".Translate().ToString().ToUpper();
        if (Widgets.ButtonText(button, $"<color=white>{saveLabel}</color>"))
            Current.Save();

        // Save & exit
        button = buttonsRect;
        button.x = Mathf.Lerp(button.x, button.xMax, 1f / 3f);
        button.width *= 0.3f;
        button = button.ExpandedBy(-2f, -5f);
        if (Widgets.ButtonText(button, $"<color=white>{"FactionLoadout_Preset_SaveAndExit".Translate()}</color>"))
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
        if (Widgets.ButtonText(button, $"<color=yellow>{"Close".Translate().ToString().ToUpper()}</color>"))
            Close();

        GUI.color = Color.white;
        ui.GapLine();

        if (Current.IsPackaged)
        {
            Rect warningRect = ui.GetRect(44);
            Widgets.DrawBoxSolid(warningRect, new Color(0.45f, 0.35f, 0.05f, 0.85f));
            warningRect = warningRect.ContractedBy(6f);
            Widgets.Label(warningRect, "FactionLoadout_PackagedPresetWarning".Translate(Current.PackagedModName).ToString());
        }

        // Missing faction handling.
        if (Current.HasMissingFactions())
        {
            ui.Label($"<color=red>{"FactionLoadout_Preset_MissingWarning".Translate()}</color>");
            ui.Label($"<b>{"FactionLoadout_Preset_MissingHeader".Translate()}</b>");
            ui.GapLine();
            foreach (string str in Current.GetMissingFactionAndModNames())
                ui.Label($" - {str}");
        }

        Rect nameArea = ui.GetRect(28);
        nameArea.width = 200;
        Widgets.Label(nameArea, "FactionLoadout_Preset_EditName".Translate());
        nameArea.x += 80;
        nameArea.height -= 5;
        Current.Name = Widgets.TextField(nameArea, Current.Name);

        ui.Label($"<b>{"FactionLoadout_Preset_EditCount".Translate(Current.factionChanges.Count)}</b>");
        ui.Gap();

        float factionListHeight = Mathf.Max(100f, inRect.height - ui.CurHeight - 60f);
        Widgets.BeginScrollView(ui.GetRect(factionListHeight), ref scroll, new Rect(0, 0, inRect.width - 20, Current.factionChanges.Count * (28 * 2 + 10)));

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
            string deleteLabel = $"[{"Delete".Translate()}]";
            area.width = Mathf.Max(80, Text.CalcSize(deleteLabel).x + 10);
            if (Widgets.ButtonText(area, deleteLabel))
            {
                item.DeletedOrClosed = true;
                Current.factionChanges.RemoveAt(i);
                i--;
                continue;
            }

            GUI.color = Color.white;

            area.x += area.width + 10;
            if (item.Faction.IsMissing)
            {
                area.width = 120;
                GUI.color = new Color(1f, 0.75f, 0.2f);
                if (Widgets.ButtonText(area, "FactionLoadout_EditAnyway".Translate()))
                    FactionEditUI.OpenEditor(item);
                GUI.color = Color.white;
                area.x += 130;
                area.width = inRect.width - 20 - area.x;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                Widgets.Label(area, "FactionLoadout_FactionMissing".Translate());
                GUI.color = Color.white;
            }
            else
            {
                string editLabel = "FactionLoadout_Edit".Translate().CapitalizeFirst();
                area.width = Mathf.Max(80, Text.CalcSize(editLabel).x + 10);
                if (Widgets.ButtonText(area, editLabel))
                    FactionEditUI.OpenEditor(item);
                area.x += area.width + 10;
                Widgets.CheckboxLabeled(area, "Enabled".Translate(), ref item.Active, placeCheckboxNearText: true);
            }

            ui.GapLine(10);
        }

        ui.End();
        Widgets.EndScrollView();
        ui = oldUI;

        ui.Gap();
        if (ui.ButtonText("FactionLoadout_Preset_AddFactionEdit".Translate()))
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
            if (
                Preset.FactionlessPawnKindsSet.Count > 0
                && !Current.HasEditFor(Preset.SpecialFactionlessPawnsFaction)
                && !raw.Any(f => f.defName == Preset.SpecialFactionlessPawnsFaction.defName)
            )
            {
                raw.Add(Preset.SpecialFactionlessPawnsFaction);
            }
            List<MenuItemBase> items = CustomFloatMenu.MakeItems(
                raw,
                f => new MenuItemText(f, $"{f.LabelCap} ({f.defName})", DefUtils.TryGetIcon(f, out Color c), c, f.description)
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
