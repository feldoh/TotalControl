using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public class ModCore : Mod
    {
        public static void Log(string msg)
        {
            Verse.Log.Message($"<color=#1c6beb>[FacLoadout]</color> {msg ?? "<null>"}");
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"<color=#1c6beb>[FacLoadout]</color> {msg ?? "<null>"}");
        }

        public static void Error(string msg, Exception e = null)
        {
            Verse.Log.Error($"<color=#1c6beb>[FacLoadout]</color> {msg ?? "<null>"}");
            if(e != null)
                Verse.Log.Error(e.ToString());
        }

        public ModCore(ModContentPack content) : base(content)
        {
            GetSettings<MySettings>();
            LongEventHandler.QueueLongEvent(LoadLate, "Apply Faction Overrides", false, null);
        }

        public override string SettingsCategory()
        {
            return "Faction Editor";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var ui = new Listing_Standard();
            ui.ColumnWidth = inRect.width * 0.5f;
            ui.maxOneColumn = false;
            ui.Begin(inRect);
            ui.CheckboxLabeled("Enable vanilla restrictions:  ", ref MySettings.VanillaRestrictions, "If true, some vanilla restrictions are applied, such as only allowing materials that a faction has a high enough tech level for, or not giving forced weapons to non-violent pawns.");
            ui.GapLine();

            ui.Label("Here you can manage faction edit <b>presets</b>.\nEach preset contains a collection of faction edits. Only one preset can be active at a time.\n<i>Hold the SHIFT key to delete presets.</i>");
            ui.GapLine();

            bool deleteMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Preset toDelete = null;

            foreach(var preset in Preset.LoadedPresets)
            {
                var area = ui.GetRect(30);
                area.width = 80;

                bool active = MySettings.ActivePreset == preset.GUID;

                GUI.color = active ? Color.green : Color.red;
                if (Widgets.ButtonText(area, active ? "ACTIVE" : "DISABLED"))
                {
                    MySettings.ActivePreset = null;
                    if (!active)
                        MySettings.ActivePreset = preset.GUID;
                }

                GUI.color = Color.white;
                area.x += 90;
                GUI.color = deleteMode ? Color.red : Color.white;
                if (Widgets.ButtonText(area, deleteMode ? "DELETE" : "EDIT"))
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

            if(toDelete != null)            
                Preset.DeletePreset(toDelete);            

            if (Preset.LoadedPresets.EnumerableNullOrEmpty())            
                ui.Label("Huh, there's nothing here... Why not create a new preset by clicking the button below?");            

            ui.GapLine();
            if (ui.ButtonText("Create new preset..."))
            {
                var preset = new Preset();
                Preset.AddNewPreset(preset);
                preset.Save();

                MySettings.ActivePreset = preset.GUID;

                PresetUI.OpenEditor(preset);

                Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
            }

            ui.End();
        }

        private void LoadLate()
        {
            Preset.LoadAllPresets();            

            int count = 0;
            int edits = 0;
            foreach (var preset in Preset.LoadedPresets)
            {
                if (MySettings.ActivePreset == preset.GUID)
                {
                    int changed = preset.TryApplyAll();
                    edits += changed;
                    count++;

                    Messages.Message($"Applied faction edit '{preset.Name}': modified {changed} factions.", MessageTypeDefOf.PositiveEvent);
                }
            }

            var harmony = new Harmony("co.uk.epicguru.factionloadout");
            harmony.PatchAll();

            Log($"Game comp finalized init, applied {count} presets that affected {edits} factions.");
        }
    }

    public class HotSwappableAttribute : Attribute
    {

    }

    public class MySettings : ModSettings
    {
        public static string ActivePreset = null;
        public static bool VanillaRestrictions = true;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref ActivePreset, "activePreset", null);
            Scribe_Values.Look(ref VanillaRestrictions, "vanillaRestrictions", true);
        }
    }
}
