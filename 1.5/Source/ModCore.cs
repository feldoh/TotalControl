using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public class ModCore : Mod
    {
        public static void Debug(string msg)
        {
            if (!MySettings.VerboseLogging)
                return;
            Verse.Log.Message($"<color=#1c6beb>[FacLoadout] [DEBUG]</color> {msg ?? "<null>"}");
        }

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

        public ModCore(ModContentPack content)
            : base(content)
        {
            GetSettings<MySettings>();
            LongEventHandler.QueueLongEvent(
                LoadLate,
                "FactionLoadout_LoadingScreenText",
                false,
                null
            );
        }

        public override string SettingsCategory()
        {
            return "FactionLoadout_SettingName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard ui = new Listing_Standard
            {
                ColumnWidth = inRect.width * 0.5f,
                maxOneColumn = false
            };
            ui.Begin(inRect);
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_VanillaRestrictions".Translate(),
                ref MySettings.VanillaRestrictions,
                "FactionLoadout_Settings_VanillaRestrictionsDesc".Translate()
            );
            ui.GapLine();
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_Verbose".Translate(),
                ref MySettings.VerboseLogging,
                "FactionLoadout_Settings_VerboseDesc".Translate()
            );
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
                Widgets.CheckboxLabeled(area, "Active".Translate().CapitalizeFirst(), ref active, placeCheckboxNearText: true);
                if (currentActive != active)
                    MySettings.ActivePreset = active ? preset.GUID : null;

                GUI.color = Color.white;
                area.x += 90;
                GUI.color = deleteMode ? Color.red : Color.white;
                if (Widgets.ButtonText(area, deleteMode ? "Delete".Translate().CapitalizeFirst() : "Edit".Translate().CapitalizeFirst()))
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

        private void LoadLate()
        {
            Preset.LoadAllPresets();

            int count = 0;
            int edits = 0;
            foreach (Preset preset in Preset.LoadedPresets)
            {
                if (MySettings.ActivePreset != preset.GUID)
                    continue;
                int changed = preset.TryApplyAll();
                edits += changed;
                count++;

                Messages.Message(
                    $"Applied faction edit '{preset.Name}': modified {changed} factions.",
                    MessageTypeDefOf.PositiveEvent
                );
            }

            Harmony harmony = new Harmony("co.uk.epicguru.factionloadout");
#if DEBUG
Harmony.DEBUG = true;
#endif
            harmony.Patch(
                AccessTools.Method(typeof(PawnApparelGenerator), "GenerateStartingApparelFor"),
                postfix: new HarmonyMethod(typeof(ApparelGenPatch), "Postfix")
            );
            harmony.Patch(
                AccessTools.Method(typeof(Faction), "TryGenerateNewLeader"),
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(FactionLeaderPatch), "Prefix"),
                    priority: Priority.First
                )
            );
            harmony.Patch(
                AccessTools.Method(typeof(FactionUtility), "HostileTo"),
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(FactionUtilityPawnGenPatch), "Prefix"),
                    priority: Priority.First
                )
            );
            harmony.Patch(
                AccessTools.Method(typeof(ThingIDMaker), "GiveIDTo"),
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(ThingIDPatch), "Prefix"),
                    priority: Priority.First
                )
            );
            harmony.Patch(
                AccessTools.Method(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(WeaponGenPatch), "Postfix"))
            );
            harmony.Patch(
                AccessTools.Method(typeof(PawnGenerator), "GenerateNewPawnInternal"),
                postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(PawnGenPatchCore), nameof(PawnGenPatchCore.Postfix))
                )
            );
            harmony.Patch(
                AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GenerateRandomAge)),
                prefix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(PawnGenAgePatchCore),
                        nameof(PawnGenAgePatchCore.Prefix)
                    )
                )
            );
            harmony.Patch(
                AccessTools.Method(typeof(PawnGenerator), "GetBodyTypeFor"),
                postfix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(PawnGenPatchBodyTypeDef),
                        nameof(PawnGenPatchBodyTypeDef.Postfix)
                    )
                )
            );
            if (MySettings.PatchKindInRequests)
            {
                harmony.Patch(
                    AccessTools.PropertyGetter(typeof(PawnGenerationRequest), nameof(PawnGenerationRequest.KindDef)),
                    postfix: new HarmonyMethod(
                        AccessTools.Method(
                            typeof(PawnGenRequestKindPatch),
                            nameof(PawnGenRequestKindPatch.Postfix)
                        )
                    )
                );
            }

            Log(
                $"Game comp finalized init, applied {count} presets that affected {edits} factions."
            );
        }
    }

    public class HotSwappableAttribute : Attribute { }
}
