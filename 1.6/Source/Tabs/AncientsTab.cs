using System.Collections.Generic;
using FactionLoadout.Modules;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class AncientsTab : EditTab
{
    private string numVFEAncientsPowersBuffer = null;
    private string numVFEAncientsWeaknessesBuffer = null;

    public AncientsTab(PawnKindEdit current, PawnKindDef defaultKind)
        : base("FactionLoadout_Tab_VFEAncients".Translate(), current, defaultKind) { }

    protected override void DrawContents(Listing_Standard ui)
    {
        if (!VFEAncientsReflectionModule.ModLoaded.Value)
            return;
        DrawOverride(
            ui,
            0,
            ref Current.NumVFEAncientsSuperPowers,
            "FactionLoadout_Ancients_SuperPowers".Translate().ToString(),
            DrawNumVFEAncientsSuperPowers,
            pasteGet: e => e.NumVFEAncientsSuperPowers
        );
        DrawOverride(
            ui,
            0,
            ref Current.NumVFEAncientsSuperWeaknesses,
            "FactionLoadout_Ancients_SuperWeaknesses".Translate().ToString(),
            DrawNumVFEAncientsSuperWeaknesses,
            pasteGet: e => e.NumVFEAncientsSuperWeaknesses
        );
        DrawOverride(
            ui,
            new List<string>(),
            ref Current.ForcedVFEAncientsItems,
            "FactionLoadout_Ancients_ForcedPowers".Translate().ToString(),
            DrawVFEAncientsPowers,
            GetHeightFor(Current.ForcedVFEAncientsItems),
            true,
            pasteGet: e => e.ForcedVFEAncientsItems
        );
    }

    // --- Private draw methods ---

    private void DrawNumVFEAncientsSuperPowers(Rect rect, bool active, int _)
    {
        if (numVFEAncientsPowersBuffer == null && active)
            numVFEAncientsPowersBuffer = Current.NumVFEAncientsSuperPowers?.ToString() ?? "";
        if (active)
        {
            int value = Current.NumVFEAncientsSuperPowers.GetValueOrDefault(0);
            Widgets.IntEntry(rect, ref value, ref numVFEAncientsPowersBuffer);
            Current.NumVFEAncientsSuperPowers = value;
        }
        else
        {
            DefModExtension ancientsExtension = VFEAncientsReflectionModule.FindVEAncientsExtension(Current.Def);
            string defaultValue = "NA";
            if (ancientsExtension != null)
                defaultValue = VFEAncientsReflectionModule.NumRandomSuperpowersField.Value?.GetValue(ancientsExtension)?.ToString();

            string txt = Current.IsGlobal ? "---" : $"[Default] {defaultValue}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawNumVFEAncientsSuperWeaknesses(Rect rect, bool active, int _)
    {
        if (numVFEAncientsWeaknessesBuffer == null && active)
            numVFEAncientsWeaknessesBuffer = Current.NumVFEAncientsSuperWeaknesses?.ToString() ?? "";

        if (active)
        {
            int value = Current.NumVFEAncientsSuperWeaknesses.GetValueOrDefault(0);
            Widgets.IntEntry(rect, ref value, ref numVFEAncientsWeaknessesBuffer);
            Current.NumVFEAncientsSuperWeaknesses = value;
        }
        else
        {
            DefModExtension ancientsExtension = VFEAncientsReflectionModule.FindVEAncientsExtension(Current.Def);
            string defaultValue = "NA";
            if (ancientsExtension != null)
                defaultValue = VFEAncientsReflectionModule.VfeAncientsExtensionType.Value?.GetField("numRandomWeaknesses")?.GetValue(ancientsExtension)?.ToString();

            string txt = Current.IsGlobal ? "---" : $"[Default] {defaultValue}";
            Widgets.Label(rect.GetCentered(txt), txt);
        }
    }

    private void DrawVFEAncientsPowers(Rect rect, bool active, List<string> defaultPowers)
    {
        DrawStringList(rect, active, ref scrolls[scrollIndex++], Current.ForcedVFEAncientsItems, new List<string>(), DefCache.AllPowerDefs);
    }
}
