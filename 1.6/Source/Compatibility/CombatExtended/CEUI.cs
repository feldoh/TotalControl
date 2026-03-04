using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using FactionLoadout;
using FactionLoadout.UISupport;
using UnityEngine;
using Verse;

namespace TotalControlCECompat;

/// <summary>
/// UI drawing for the Combat Extended loadout tab.
/// </summary>
public static class CEUI
{
    public static void DrawTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        CEData data = CEModule.GetOrCreateData(edit);

        // Read defaults from the def's existing LoadoutPropertiesExtension (if any)
        LoadoutPropertiesExtension defExt = defaultKind.GetModExtension<LoadoutPropertiesExtension>();

        DrawAmmoCategoryRow(ui, data, defExt);
        ui.GapLine();
        DrawMagazineCountRow(ui, data, defExt);
        ui.GapLine();
        DrawMinAmmoRow(ui, data, defExt);
    }

    private static void DrawAmmoCategoryRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        string defDefault = defExt?.forcedAmmoCategory?.defName;
        bool hasOverride = data.ForcedAmmoCategoryDefName != null;

        Rect row = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        string currentLabel = hasOverride ? data.ForcedAmmoCategoryDefName : (defDefault != null ? $"(default: {defDefault})" : "(default: random)");

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_ForcedAmmoCategory".Translate() + ": " + currentLabel);
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            if (Widgets.ButtonText(fieldRect.LeftPart(0.55f), "FactionLoadout_CE_ChangeAmmo".Translate()))
                OpenAmmoCategoryMenu(data);
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Clear".Translate()))
                data.ForcedAmmoCategoryDefName = null;
        }
        else
        {
            if (Widgets.ButtonText(fieldRect.LeftPart(0.5f), "FactionLoadout_CE_Override".Translate()))
                OpenAmmoCategoryMenu(data);
        }
    }

    private static void OpenAmmoCategoryMenu(CEData data)
    {
        List<AmmoCategoryDef> allCategories = DefDatabase<AmmoCategoryDef>.AllDefsListForReading.OrderBy(d => d.LabelCap.ToString()).ToList();

        var items = CustomFloatMenu.MakeItems(allCategories, d => new MenuItemText(d, d.LabelCap, tooltip: d.description));

        CustomFloatMenu.Open(
            items,
            item =>
            {
                AmmoCategoryDef selected = item.GetPayload<AmmoCategoryDef>();
                data.ForcedAmmoCategoryDefName = selected.defName;
            }
        );
    }

    private static void DrawMagazineCountRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        bool hasOverride = data.PrimaryMagazineCount.HasValue;
        FloatRange defVal = defExt?.primaryMagazineCount ?? FloatRange.Zero;

        Rect row = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_PrimaryMagazineCount".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            FloatRange current = data.PrimaryMagazineCount.Value;
            float min = current.min;
            float max = current.max;
            string minBuf = min.ToString("F0");
            string maxBuf = max.ToString("F0");

            Rect minRect = fieldRect.LeftPart(0.3f);
            Rect dashRect = fieldRect.LeftPart(0.55f).RightPart(0.18f);
            Rect maxRect = fieldRect.LeftPart(0.7f).RightPart(0.3f);
            Rect clearRect = fieldRect.RightPart(0.27f);

            Widgets.TextFieldNumeric(minRect, ref min, ref minBuf, 0f, 99f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(dashRect, "-");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(maxRect, ref max, ref maxBuf, 0f, 99f);

            data.PrimaryMagazineCount = new FloatRange(min, Mathf.Max(min, max));

            if (Widgets.ButtonText(clearRect, "FactionLoadout_Clear".Translate()))
                data.PrimaryMagazineCount = null;
        }
        else
        {
            string hint = defVal != FloatRange.Zero ? $"(default: {defVal.min:F0}-{defVal.max:F0})" : "(default: none)";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.6f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.37f), "FactionLoadout_CE_Override".Translate()))
                data.PrimaryMagazineCount = defVal != FloatRange.Zero ? defVal : new FloatRange(1f, 3f);
        }
    }

    private static void DrawMinAmmoRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        bool hasOverride = data.MinAmmoCount.HasValue;
        int defVal = defExt?.minAmmoCount ?? 0;

        Rect row = ui.GetRect(Text.LineHeight + 4);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_MinAmmoCount".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            int val = data.MinAmmoCount.Value;
            string buf = val.ToString();
            Widgets.TextFieldNumeric(fieldRect.LeftPart(0.4f), ref val, ref buf, 0, 9999);
            data.MinAmmoCount = val;

            if (Widgets.ButtonText(fieldRect.RightPart(0.37f), "FactionLoadout_Clear".Translate()))
                data.MinAmmoCount = null;
        }
        else
        {
            string hint = defVal > 0 ? $"(default: {defVal})" : "(default: none)";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.6f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.37f), "FactionLoadout_CE_Override".Translate()))
                data.MinAmmoCount = defVal > 0 ? defVal : 1;
        }
    }
}
