using System.Collections.Generic;
using FactionLoadout.UISupport;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class WeaponTab : EditTab
{
    public WeaponTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_Weapon".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        DrawOverride(ui, DefaultKind.weaponMoney, ref Current.WeaponMoney, "Weapon Value", DrawWeaponMoney, pasteGet: e => e.WeaponMoney);
        DrawOverride(ui, QualityCategory.Normal, ref Current.ForcedWeaponQuality, "Forced Weapon Quality", DrawWeaponQuality, pasteGet: e => e.ForcedWeaponQuality);
        DrawOverride(ui, DefaultKind.biocodeWeaponChance, ref Current.BiocodeWeaponChance, "Biocode Chance", DrawBiocodeChance, pasteGet: e => e.BiocodeWeaponChance);
        DrawOverride(
            ui,
            DefaultKind.weaponTags,
            ref Current.WeaponTags,
            "Allowed Weapon Types",
            DrawWeaponTags,
            GetHeightFor(Current.WeaponTags),
            true,
            pasteGet: e => e.WeaponTags
        );
        DrawSpecificGear(ui, ref Current.SpecificWeapons, "Required Weapons (advanced)", t => t.IsWeapon, ThingDef.Named("Gun_AssaultRifle"));
        DrawOverride(
            ui,
            null,
            ref Current.WeaponBlacklist,
            "FactionLoadout_WeaponBlacklist".Translate(),
            DrawWeaponBlacklist,
            GetHeightFor(Current.WeaponBlacklist),
            false,
            pasteGet: e => e.WeaponBlacklist
        );
    }

    private void DrawWeaponQuality(Rect rect, bool active, QualityCategory _)
    {
        DrawEnumSelector(rect, active, Current.ForcedWeaponQuality, Current.Def.forceWeaponQuality ?? QualityCategory.Normal, q => Current.ForcedWeaponQuality = q);
    }

    private void DrawBiocodeChance(Rect rect, bool active, float def)
    {
        DrawChance(ref Current.BiocodeWeaponChance, def, rect, active);
    }

    private void DrawWeaponMoney(Rect rect, bool active, FloatRange defaultRange)
    {
        DrawFloatRange(rect, active, ref Current.WeaponMoney, Current.Def.weaponMoney, ref buffers[bufferIndex++], ref buffers[bufferIndex++]);
    }

    private void DrawWeaponTags(Rect rect, bool active, System.Collections.Generic.List<string> defaultTags)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.WeaponTags, Current.Def.weaponTags, DefCache.AllWeaponsTags);
    }

    private void DrawWeaponBlacklist(Rect rect, bool active, System.Collections.Generic.List<DefRef<ThingDef>> defaultList)
    {
        DrawDefRefList(rect, active, ref scrolls[scrollIndex++], Current.WeaponBlacklist, null, DefCache.AllWeapons);
    }
}
