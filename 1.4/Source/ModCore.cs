using System;
using HarmonyLib;
using RimWorld;
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
            if (e != null)
                Verse.Log.Error(e.ToString());
        }

        public ModCore(ModContentPack content) : base(content)
        {
            GetSettings<MySettings>();
            LongEventHandler.QueueLongEvent(LoadLate, "FactionLoadout_LoadingScreenText", false, null);
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
            ui.CheckboxLabeled("Enable vanilla restrictions:  ", ref MySettings.VanillaRestrictions,
                "If true, some vanilla restrictions are applied, such as only allowing materials that a faction has a high enough tech level for, or not giving forced weapons to non-violent pawns.");
            ui.GapLine();
            ui.CheckboxLabeled("Verbose Logging:  ", ref MySettings.VerboseLogging,
                "Adds more logs to track down what's being replaced where.");
            ui.GapLine();

            ui.Label(
                "Here you can manage faction edit <b>presets</b>.\nEach preset contains a collection of faction edits. Only one preset can be active at a time.\n<i>Hold the SHIFT key to delete presets.</i>");
            ui.GapLine();

            bool deleteMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Preset toDelete = null;

            foreach (var preset in Preset.LoadedPresets)
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

            if (toDelete != null)
                Preset.DeletePreset(toDelete);

            if (Preset.LoadedPresets.EnumerableNullOrEmpty())
                ui.Label("Huh, there's nothing here... Why not create a new preset by clicking the button below?");

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

        private void LoadLate()
        {
            Preset.LoadAllPresets();

            int count = 0;
            int edits = 0;
            foreach (Preset preset in Preset.LoadedPresets)
            {
                if (MySettings.ActivePreset != preset.GUID) continue;
                int changed = preset.TryApplyAll();
                edits += changed;
                count++;

                Messages.Message($"Applied faction edit '{preset.Name}': modified {changed} factions.", MessageTypeDefOf.PositiveEvent);
            }

            Harmony harmony = new Harmony("co.uk.epicguru.factionloadout");
            harmony.Patch(AccessTools.Method(typeof(PawnApparelGenerator), "GenerateStartingApparelFor"),
                postfix: new HarmonyMethod(typeof(ApparelGenPatch), "Postfix"));
            harmony.Patch(AccessTools.Method(typeof(Faction), "TryGenerateNewLeader"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FactionLeaderPatch), "Prefix"), priority: Priority.First));
            harmony.Patch(AccessTools.Method(typeof(FactionUtility), "HostileTo"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FactionUtilityPawnGenPatch), "Prefix"), priority: Priority.First));
            harmony.Patch(AccessTools.Method(typeof(ThingIDMaker), "GiveIDTo"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ThingIDPatch), "Prefix"), priority: Priority.First));
            harmony.Patch(AccessTools.Method(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(WeaponGenPatch), "Postfix")));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), "GenerateNewPawnInternal"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenPatchCore), nameof(PawnGenPatchCore.Postfix))));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GenerateRandomAge)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenAgePatchCore), nameof(PawnGenAgePatchCore.Prefix))));

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
        public static bool VerboseLogging = false;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref ActivePreset, "activePreset", null);
            Scribe_Values.Look(ref VanillaRestrictions, "vanillaRestrictions", true);
            Scribe_Values.Look(ref VerboseLogging, "verboseLogging", false);
        }
    }
}
