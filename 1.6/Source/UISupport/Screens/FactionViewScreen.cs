using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.Screens;

/// <summary>
/// Home screen: global settings + active-preset management + the selected preset's
/// faction-edit list. Merges the old <c>Dialog_FactionLoadout</c> (settings + preset
/// list) and <c>PresetUI</c> (per-preset faction list) into one in-shell screen.
/// </summary>
[HotSwappable]
public class FactionViewScreen
{
    public readonly TotalControlController Controller;

    private Vector2 factionScroll;

    public FactionViewScreen(TotalControlController controller)
    {
        Controller = controller;
    }

    public void Draw(Rect inRect)
    {
        Listing_Standard ui = new();
        ui.Begin(inRect);

        // --- Global settings ---
        ui.CheckboxLabeled(
            "FactionLoadout_Settings_VanillaRestrictions".Translate(),
            ref MySettings.VanillaRestrictions,
            "FactionLoadout_Settings_VanillaRestrictionsDesc".Translate()
        );
        ui.CheckboxLabeled("FactionLoadout_Settings_Verbose".Translate(), ref MySettings.VerboseLogging, "FactionLoadout_Settings_VerboseDesc".Translate());
        ui.CheckboxLabeled(
            "FactionLoadout_Settings_PatchKindInRequests".Translate(),
            ref MySettings.PatchKindInRequests,
            "FactionLoadout_Settings_PatchKindInRequestsDesc".Translate()
        );
        ui.CheckboxLabeled("FactionLoadout_Settings_IgnorePrice".Translate(), ref MySettings.IgnorePriceLimits, "FactionLoadout_Settings_IgnorePriceDesc".Translate());
        ui.CheckboxLabeled(
            "FactionLoadout_Settings_OverrideForcedIdeos".Translate(),
            ref MySettings.OverrideForcedIdeos,
            "FactionLoadout_Settings_OverrideForcedIdeosDesc".Translate()
        );
        ui.GapLine();

        Preset preset = Controller.SelectedPreset;
        if (preset == null)
        {
            ui.Label("FactionLoadout_NothingHere".Translate());
            ui.Gap();
            if (ui.ButtonText("FactionLoadout_CreateNewPreset".Translate()))
                CreateNewPreset();
            ui.End();
            return;
        }

        DrawPresetHeader(ui, preset);
        ui.GapLine();

        ui.Label($"<b>{"FactionLoadout_Preset_EditCount".Translate(preset.factionChanges.Count)}</b>");
        ui.Gap(4);

        // --- Faction list (scrollable). Fixed row height -> compute content directly. ---
        const float rowH = 34f;
        float listH = Mathf.Max(120f, inRect.height - ui.CurHeight - 44f);
        Rect scrollOut = ui.GetRect(listH);
        float contentH = Mathf.Max(preset.factionChanges.Count * rowH + 8f, listH);
        Rect view = new(0, 0, scrollOut.width - 16f, contentH);

        Widgets.BeginScrollView(scrollOut, ref factionScroll, view);
        FactionEdit toDelete = null;
        for (int i = 0; i < preset.factionChanges.Count; i++)
        {
            FactionEdit item = preset.factionChanges[i];
            Rect row = new(0, i * rowH, view.width, rowH - 2f);
            if (i % 2 == 1)
                Widgets.DrawHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);
            if (DrawFactionRow(row, item))
                toDelete = item;
        }

        Widgets.EndScrollView();

        if (toDelete != null)
        {
            toDelete.DeletedOrClosed = true;
            preset.factionChanges.Remove(toDelete);
        }

        // --- Add faction edit ---
        if (ui.ButtonText("FactionLoadout_Preset_AddFactionEdit".Translate()))
            OpenAddFactionMenu(preset);

        ui.End();
    }

    /// <summary>Returns true if this faction was marked for deletion.</summary>
    private bool DrawFactionRow(Rect row, FactionEdit item)
    {
        bool delete = false;
        float rightX = row.xMax;

        // Delete (far right, red).
        string delText = "Delete".Translate();
        float delW = Mathf.Max(80f, Text.CalcSize(delText).x + 16f);
        Rect delBtn = new(rightX - delW, row.y + 3f, delW, row.height - 6f);
        GUI.color = Color.red;
        if (Widgets.ButtonText(delBtn, delText))
            delete = true;
        GUI.color = Color.white;
        rightX -= delW + 6f;

        if (item.Faction.IsMissing)
        {
            string editAnyway = "FactionLoadout_EditAnyway".Translate();
            float ew = Mathf.Max(120f, Text.CalcSize(editAnyway).x + 16f);
            Rect editBtn = new(rightX - ew, row.y + 3f, ew, row.height - 6f);
            GUI.color = new Color(1f, 0.75f, 0.2f);
            if (Widgets.ButtonText(editBtn, editAnyway))
                Controller.OpenFaction(item);
            GUI.color = new Color(1f, 0.4f, 0.4f);
            Rect lbl = new(row.x, row.y, editBtn.x - row.x - 6f, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(lbl, $"<b>{item.Faction.LabelCap}</b> <i>({item.Faction.DefName})</i> — {"FactionLoadout_FactionMissing".Translate()}");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return delete;
        }

        // Edit.
        string editText = "FactionLoadout_Edit".Translate().CapitalizeFirst();
        float editW = Mathf.Max(80f, Text.CalcSize(editText).x + 16f);
        Rect edit = new(rightX - editW, row.y + 3f, editW, row.height - 6f);
        if (Widgets.ButtonText(edit, editText))
            Controller.OpenFaction(item);
        rightX -= editW + 6f;

        // Enabled checkbox.
        Rect enabledRect = new(rightX - 110f, row.y, 110f, row.height);
        Widgets.CheckboxLabeled(enabledRect, "Enabled".Translate(), ref item.Active, placeCheckboxNearText: true);
        rightX -= 116f;

        // Label (fills remaining space on the left).
        Rect nameRect = new(row.x + 4f, row.y, rightX - row.x - 4f, row.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(nameRect, $"<b>{item.Faction.LabelCap}</b> <color=#888888><i>({item.Faction.DefName})</i></color>");
        Text.Anchor = TextAnchor.UpperLeft;

        return delete;
    }

    private void DrawPresetHeader(Listing_Standard ui, Preset preset)
    {
        // Name row.
        Rect nameRow = ui.GetRect(30);
        Rect nameLbl = new(nameRow.x, nameRow.y, 80f, nameRow.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(nameLbl, "FactionLoadout_Preset_EditName".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Rect nameField = new(nameRow.x + 84f, nameRow.y + 2f, 280f, 26f);
        preset.Name = Widgets.TextField(nameField, preset.Name);

        // Active toggle (only one preset may be active).
        Rect activeRect = new(nameRow.x + 380f, nameRow.y, 140f, nameRow.height);
        bool active = MySettings.ActivePreset == preset.GUID;
        bool was = active;
        GUI.color = active ? Color.green : Color.white;
        Widgets.CheckboxLabeled(activeRect, "FactionLoadout_Active".Translate().CapitalizeFirst(), ref active, placeCheckboxNearText: true);
        GUI.color = Color.white;
        if (active != was)
        {
            MySettings.ActivePreset = active ? preset.GUID : null;
            ModCore.Settings.Write();
        }

        // Action buttons.
        Rect btnRow = ui.GetRect(30);
        float bx = btnRow.x;
        const float bw = 150f;

        string saveLabel = preset.IsPackaged ? "FactionLoadout_SaveToSourceFile".Translate() : "Save".Translate().ToString().ToUpper();
        GUI.color = Color.green;
        if (Widgets.ButtonText(new Rect(bx, btnRow.y, bw, 28f), $"<color=white>{saveLabel}</color>"))
            preset.Save();
        GUI.color = Color.white;
        bx += bw + 6f;

        if (Widgets.ButtonText(new Rect(bx, btnRow.y, bw, 28f), "FactionLoadout_CopyToMyPresets".Translate()))
            CopyPreset(preset);
        bx += bw + 6f;

        if (!preset.IsPackaged)
        {
            GUI.color = Color.Lerp(Color.white, Color.red, 0.65f);
            if (Widgets.ButtonText(new Rect(bx, btnRow.y, bw, 28f), $"<color=yellow>{"Delete".Translate().ToString().ToUpper()}</color>"))
            {
                Preset.DeletePreset(preset);
                Controller.SelectPreset(Preset.LoadedPresets.FirstOrDefault());
            }
            GUI.color = Color.white;
            bx += bw + 6f;
        }

        if (Widgets.ButtonText(new Rect(bx, btnRow.y, bw, 28f), "FactionLoadout_CreateNewPreset".Translate()))
            CreateNewPreset();

        // Packaged + missing-faction warnings.
        if (preset.IsPackaged)
        {
            Rect warningRect = ui.GetRect(44);
            Widgets.DrawBoxSolid(warningRect, new Color(0.45f, 0.35f, 0.05f, 0.85f));
            Widgets.Label(warningRect.ContractedBy(6f), "FactionLoadout_PackagedPresetWarning".Translate(preset.PackagedModName).ToString());
        }

        if (preset.HasMissingFactions())
        {
            ui.Label($"<color=red>{"FactionLoadout_Preset_MissingWarning".Translate()}</color>");
            foreach (string str in preset.GetMissingFactionAndModNames())
                ui.Label($" - {str}");
        }
    }

    private void CreateNewPreset()
    {
        Preset preset = new();
        Preset.AddNewPreset(preset);
        preset.Save();
        MySettings.ActivePreset = preset.GUID;
        ModCore.Settings.Write();
        Controller.SelectPreset(preset);
    }

    private void CopyPreset(Preset src)
    {
        try
        {
            Preset copy = Preset.CreateCopy(src);
            Preset.AddNewPreset(copy);
            copy.Save();
            Controller.SelectPreset(copy);
        }
        catch (Exception ex)
        {
            ModCore.Error("Failed to copy preset.", ex);
        }
    }

    private void OpenAddFactionMenu(Preset preset)
    {
        List<FactionDef> raw = DefDatabase<FactionDef>.AllDefsListForReading.Where(f => !preset.HasEditFor(f)).ToList();
        if (!preset.HasEditFor(Preset.SpecialCreepjoinerFaction) && !raw.Any(f => f.defName == Preset.SpecialCreepjoinerFaction.defName))
            raw.Add(Preset.SpecialCreepjoinerFaction);
        if (!preset.HasEditFor(Preset.SpecialWildManFaction) && !raw.Any(f => f.defName == Preset.SpecialWildManFaction.defName))
            raw.Add(Preset.SpecialWildManFaction);
        if (
            Preset.FactionlessPawnKindsSet.Count > 0
            && !preset.HasEditFor(Preset.SpecialFactionlessPawnsFaction)
            && !raw.Any(f => f.defName == Preset.SpecialFactionlessPawnsFaction.defName)
        )
            raw.Add(Preset.SpecialFactionlessPawnsFaction);

        List<MenuItemBase> items = CustomFloatMenu.MakeItems(raw, f => new MenuItemText(f, $"{f.LabelCap} ({f.defName})", DefUtils.TryGetIcon(f, out Color c), c, f.description));

        CustomFloatMenu.Open(
            items,
            menuItemBase =>
            {
                FactionDef e = menuItemBase.GetPayload<FactionDef>();
                preset.factionChanges.Add(new FactionEdit { Faction = e });
            }
        );
    }
}
