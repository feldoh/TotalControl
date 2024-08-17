using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class Dialog_FactionLoadout : Window
{
    public override Vector2 InitialSize => new Vector2(800f, 480f);

    public Dialog_FactionLoadout()
    {
        doCloseButton = true;
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }
    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard ui = new Listing_Standard
        {
            ColumnWidth = inRect.width,
            maxOneColumn = true
        };
        ui.Begin(inRect);
        
        ui.Label("FactionLoadout_Settings_FactionPresetDesc".Translate());
        ui.GapLine();

        bool deleteMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Preset toDelete = null;

        foreach (Preset preset in Preset.LoadedPresets)
        {
            Rect area = ui.GetRect(30);
            area.width = 80;

            bool active = MySettings.ActivePreset == preset.GUID;

            GUI.color = active ? Color.green : Color.red;
            bool currentActive = active;
            Widgets.CheckboxLabeled(area, "Active".Translate().CapitalizeFirst(), ref active,
                placeCheckboxNearText: true);
            if (currentActive != active)
                MySettings.ActivePreset = active ? preset.GUID : null;

            GUI.color = Color.white;
            area.x += 90;
            GUI.color = deleteMode ? Color.red : Color.white;
            if (Widgets.ButtonText(area,
                    deleteMode ? "Delete".Translate().CapitalizeFirst() : "Edit".Translate().CapitalizeFirst()))
            {
                if (!deleteMode)
                {
                    PresetUI.OpenEditor(preset);
                    Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                    Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
                }
                else
                {
                    toDelete = preset;
                }
            }

            GUI.color = Color.white;

            area.x += 90;
            area.width = 9999;
            Widgets.Label(area, preset.Name);
        }

        if (toDelete != null)
            Preset.DeletePreset(toDelete);

        if (Preset.LoadedPresets.EnumerableNullOrEmpty())
            ui.Label(
                "Huh, there's nothing here... Why not create a new preset by clicking the button below?"
            );

        ui.GapLine();
        if (ui.ButtonText("Create new preset..."))
        {
            Preset preset = new();
            Preset.AddNewPreset(preset);
            preset.Save();

            MySettings.ActivePreset = preset.GUID;

            PresetUI.OpenEditor(preset);

            Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
            Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
        }

        ui.End();
    }
    
    public override void PostClose()
    {
        base.PostClose();
        Find.WindowStack.WindowOfType<PresetUI>()?.Close();
    }
}