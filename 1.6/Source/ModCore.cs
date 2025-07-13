using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class ModCore : Mod
{
    public Dialog_FactionLoadout settingsDialog = null;
    public static MySettings Settings;
    public Dialog_FactionLoadout SettingsDialog => settingsDialog ??= new Dialog_FactionLoadout();

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
        Settings = GetSettings<MySettings>();
        LongEventHandler.QueueLongEvent(LoadLate, "FactionLoadout_LoadingScreenText", false, null);
    }

    public override string SettingsCategory()
    {
        return "FactionLoadout_SettingName".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        SettingsDialog.DoWindowContents(inRect);
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

            Messages.Message($"Applied faction edit '{preset.Name}': modified {changed} factions.", MessageTypeDefOf.PositiveEvent);
        }

        Harmony harmony = new Harmony("co.uk.epicguru.factionloadout");
#if DEBUG
        Harmony.DEBUG = true;
#endif
        harmony.Patch(AccessTools.Method(typeof(PawnApparelGenerator), "GenerateStartingApparelFor"), postfix: new HarmonyMethod(typeof(ApparelGenPatch), "Postfix"));
        harmony.Patch(
            AccessTools.Method(typeof(Faction), "TryGenerateNewLeader"),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(FactionLeaderPatch), "Prefix"), priority: Priority.First)
        );
        harmony.Patch(
            AccessTools.Method(typeof(FactionUtility), "HostileTo"),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(FactionUtilityPawnGenPatch), "Prefix"), priority: Priority.First)
        );
        harmony.Patch(
            AccessTools.Method(typeof(ThingIDMaker), "GiveIDTo"),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(ThingIDPatch), nameof(ThingIDPatch.Prefix)), priority: Priority.First)
        );
        harmony.Patch(
            AccessTools.Method(typeof(IdeoUtility), nameof(IdeoUtility.IdeoChangeToWeight)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(IdeoUtilityPatch), nameof(IdeoUtilityPatch.Prefix)), priority: Priority.First)
        );
        harmony.Patch(AccessTools.Method(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor"), postfix: new HarmonyMethod(AccessTools.Method(typeof(WeaponGenPatch), "Postfix")));
        harmony.Patch(
            AccessTools.Method(typeof(PawnGenerator), "GenerateNewPawnInternal"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenPatchCore), nameof(PawnGenPatchCore.Postfix)))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GenerateRandomAge)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenAgePatchCore), nameof(PawnGenAgePatchCore.Prefix)))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PawnGenerator), "GetBodyTypeFor"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenPatchBodyTypeDef), nameof(PawnGenPatchBodyTypeDef.Postfix)))
        );

        harmony.Patch(
            AccessTools.Method(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing)),
            prefix: new HarmonyMethod(typeof(OptionListingUtility_Patch), nameof(OptionListingUtility_Patch.DrawOptionListing_Patch))
        );

        harmony.Patch(
            AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetupRecruitable)),
            prefix: new HarmonyMethod(typeof(PawnGenPatchRecruitable), nameof(PawnGenPatchRecruitable.Prefix))
        );

        if (MySettings.PatchKindInRequests)
        {
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(PawnGenerationRequest), nameof(PawnGenerationRequest.KindDef)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PawnGenRequestKindPatch), nameof(PawnGenRequestKindPatch.Postfix)))
            );
        }

        Preset.AddMissingSpecialFactionsIfNeeded();
        RewarmVEFactionCache();
        Log($"Game comp finalized init, applied {count} presets that affected {edits} factions.");
    }

    /**
     * Fixes https://github.com/feldoh/TotalControl/issues/34
     * VE Assumes it has a few caches of the original faction goodwill etc.
     * As our factions are added later than the cache is warmed this fails.
     * So we re-warm the cache after we've added our factions.
     */
    public static void RewarmVEFactionCache()
    {
        AccessTools.Method(AccessTools.TypeByName("VFECore.ScenPartUtility"), "SetCache")?.Invoke(null, null);
    }
}

public class HotSwappableAttribute : Attribute;
