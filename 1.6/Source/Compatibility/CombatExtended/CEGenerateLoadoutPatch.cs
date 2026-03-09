using System;
using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace TotalControlCECompat;

/// <summary>
/// Harmony Prefix+Finalizer on <see cref="LoadoutPropertiesExtension.GenerateLoadoutFor"/>.
///
/// Purpose: implement per-weapon ammo mapping. By the time CE's postfix calls
/// GenerateLoadoutFor, TC's WeaponGenPatch has already placed the pawn's weapon
/// (it runs first via HarmonyMethod.before). We inspect pawn.equipment.Primary,
/// look up any configured per-weapon ammo choices, and temporarily override
/// forcedAmmoCategory / weightedAmmoCategories so CE's own weighted-selection
/// logic picks the right ammo. The Finalizer restores the originals
/// so the PawnKindDef extension is not permanently mutated even if the original throws.
///
/// All AmmoCategoryDef resolution happens at Apply() time in <see cref="CEModule"/>
/// </summary>
[HarmonyPatch(typeof(LoadoutPropertiesExtension), nameof(LoadoutPropertiesExtension.GenerateLoadoutFor))]
public static class CEGenerateLoadoutPatch
{
    /// <summary>Saved originals so Finalizer can restore them.</summary>
    public struct PatchState
    {
        public AmmoCategoryDef SavedForcedCategory;
        public List<WeightedAmmoCategory> SavedWeightedCategories;
        public bool Modified;
    }

    public static void Prefix(LoadoutPropertiesExtension __instance, Pawn pawn, ref PatchState __state)
    {
        __state = new PatchState
        {
            SavedForcedCategory = __instance.forcedAmmoCategory,
            SavedWeightedCategories = __instance.weightedAmmoCategories,
            Modified = false,
        };

        if (!CEModule.KindDefMappings.TryGetValue(pawn.kindDef, out CEModule.ResolvedWeaponAmmoEntry[] entries))
        {
            return;
        }

        ThingWithComps weapon = pawn.equipment?.Primary;
        if (weapon == null)
        {
            return;
        }

        string weaponDefName = weapon.def.defName;
        List<string> weaponTags = weapon.def.weaponTags;

        // Specific def match first (more precise), then weapon tag match
        CEModule.ResolvedWeaponAmmoEntry? match = null;
        foreach (CEModule.ResolvedWeaponAmmoEntry e in entries)
        {
            if (!e.IsTag && e.WeaponKey == weaponDefName)
            {
                match = e;
                break;
            }
        }

        if (match == null && weaponTags != null)
        {
            foreach (CEModule.ResolvedWeaponAmmoEntry e in entries)
            {
                if (e.IsTag && weaponTags.Contains(e.WeaponKey))
                {
                    match = e;
                    break;
                }
            }
        }

        if (match == null)
        {
            return;
        }

        // Delegate weighted selection to CE's own logic via weightedAmmoCategories
        __instance.forcedAmmoCategory = null;
        __instance.weightedAmmoCategories = match.Value.Choices;
        __state.Modified = true;
    }

    public static Exception Finalizer(LoadoutPropertiesExtension __instance, PatchState __state, Exception __exception)
    {
        if (__state.Modified)
        {
            __instance.forcedAmmoCategory = __state.SavedForcedCategory;
            __instance.weightedAmmoCategories = __state.SavedWeightedCategories;
        }

        return __exception;
    }
}
