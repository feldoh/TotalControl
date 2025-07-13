using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class Dialog_FactionLoadout : Window
{
    public override Vector2 InitialSize => new Vector2(800f, 600f);
    public Vector2 scrollPosition = Vector2.zero;

    public Dialog_FactionLoadout()
    {
        doCloseButton = true;
        closeOnAccept = true;
        closeOnCancel = true;
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        int presetHeight = (Preset.LoadedPresets.Count + 1) * 30;
        int restHeight = 300; // Adjust this value as needed

        float scrollViewHeight = presetHeight + restHeight;

        Rect viewRect = new Rect(0, 0, inRect.width - 20, scrollViewHeight);
        Rect viewPortRect = new Rect(0, 30, inRect.width, inRect.height - 70);
        scrollPosition = GUI.BeginScrollView(viewPortRect, scrollPosition, viewRect);
        Listing_Standard ui = new Listing_Standard();

        try
        {
            ui.Begin(viewRect);

            ui.Label("FactionLoadout_Settings_FactionPresetDesc".Translate());
            ui.GapLine();

            ui.CheckboxLabeled(
                "FactionLoadout_Settings_VanillaRestrictions".Translate(),
                ref MySettings.VanillaRestrictions,
                "FactionLoadout_Settings_VanillaRestrictionsDesc".Translate()
            );
            ui.GapLine();
            ui.CheckboxLabeled("FactionLoadout_Settings_Verbose".Translate(), ref MySettings.VerboseLogging, "FactionLoadout_Settings_VerboseDesc".Translate());
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_PatchKindInRequests".Translate(),
                ref MySettings.PatchKindInRequests,
                "FactionLoadout_Settings_PatchKindInRequestsDesc".Translate()
            );
            ui.GapLine();
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
                Widgets.CheckboxLabeled(area, "FactionLoadout_Active".Translate().CapitalizeFirst(), ref active, placeCheckboxNearText: true);
                if (currentActive != active)
                {
                    MySettings.ActivePreset = active ? preset.GUID : null;
                    ModCore.Settings.Write();
                }

                GUI.color = Color.white;
                area.x += 90;
                GUI.color = deleteMode ? Color.red : Color.white;
                if (Widgets.ButtonText(area, deleteMode ? "Delete".Translate().CapitalizeFirst() : "FactionLoadout_Edit".Translate().CapitalizeFirst()))
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
                ui.Label("FactionLoadout_NothingHere".Translate());

            ui.GapLine();
            if (ui.ButtonText("FactionLoadout_CreateNewPreset".Translate()))
            {
                Preset preset = new();
                Preset.AddNewPreset(preset);
                preset.Save();

                MySettings.ActivePreset = preset.GUID;

                PresetUI.OpenEditor(preset);

                Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
            }
        }
        finally
        {
            ui.End();
            GUI.EndScrollView();
        }
    }

    public override void PostClose()
    {
        base.PostClose();
        Find.WindowStack.WindowOfType<PresetUI>()?.Close();
        ModCore.Settings.Write();
    }
}
