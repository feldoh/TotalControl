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
/// Organised into four sections: Ammo, Shield, Sidearms, and Attachments.
/// Generic helpers (DrawFloatRangeRow, DrawFloatSliderRow, DrawStringListSection,
/// Window_ThingFilterEditor) live in FactionLoadout.UISupport so other modules can reuse them.
/// </summary>
public static class CEUI
{
    private const float RowH = UIHelpers.OverrideRowH;

    public static void DrawTab(Listing_Standard ui, PawnKindEdit edit, PawnKindDef defaultKind)
    {
        CEData data = CEModule.GetOrCreateData(edit);
        LoadoutPropertiesExtension defExt = defaultKind?.GetModExtension<LoadoutPropertiesExtension>();

        DrawSectionHeader(ui, "FactionLoadout_CE_Section_Ammo".Translate());
        DrawAmmoCategoryRow(ui, data, defExt);
        ui.GapLine();
        DrawMagazineCountRow(ui, data, defExt);
        ui.GapLine();
        DrawMinAmmoRow(ui, data, defExt);
        ui.GapLine();
        DrawWeightedAmmoCategoriesSection(ui, data);

        DrawSectionHeader(ui, "FactionLoadout_CE_Section_WeaponAmmoMap".Translate());
        DrawWeaponAmmoMappingsSection(ui, data, edit);

        DrawSectionHeader(ui, "FactionLoadout_CE_Section_Shield".Translate());
        DrawShieldMoneyRow(ui, data, defExt);
        ui.GapLine();
        DrawShieldChanceRow(ui, data, defExt);
        ui.GapLine();
        DrawForceShieldMaterialRow(ui, data, defExt);
        ui.GapLine();
        DrawShieldTagsSection(ui, data);
        ui.GapLine();
        DrawShieldMaterialFilterRow(ui, data);

        DrawSectionHeader(ui, "FactionLoadout_CE_Section_Sidearms".Translate());
        DrawForcedSidearmSection(ui, data);
        ui.GapLine();
        if (data.ForcedSidearm != null)
        {
            GUI.color = Color.grey;
            ui.Label("FactionLoadout_CE_SidearmListSuppressed".Translate());
            GUI.color = Color.white;
        }
        else
        {
            DrawSidearmsListSection(ui, data);
        }

        DrawSectionHeader(ui, "FactionLoadout_CE_Section_Attachments".Translate());
        DrawPrimaryAttachmentsSection(ui, data);
    }

    private static void DrawSectionHeader(Listing_Standard ui, string label)
    {
        ui.Gap(8f);
        ui.Label($"<b>{label}</b>");
        ui.Gap(2f);
    }

    #region Ammo

    private static void DrawAmmoCategoryRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        bool hasOverride = data.ForcedAmmoCategoryDefName != null;
        string defLabel = defExt?.forcedAmmoCategory?.LabelCap.ToString();

        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_ForcedAmmoCategory".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            AmmoCategoryDef resolved = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(data.ForcedAmmoCategoryDefName);
            string currentLabel = resolved != null ? resolved.LabelCap.ToString() : data.ForcedAmmoCategoryDefName + " (?)";
            if (Widgets.ButtonText(fieldRect.LeftPart(0.55f), currentLabel))
            {
                OpenAmmoCategoryMenu(data);
            }

            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                data.ForcedAmmoCategoryDefName = null;
            }
        }
        else
        {
            string hint = defLabel != null ? $"({defLabel})" : "(–)";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                OpenAmmoCategoryMenu(data);
            }
        }
    }

    private static void OpenAmmoCategoryMenu(CEData data)
    {
        List<AmmoCategoryDef> allCategories = DefDatabase<AmmoCategoryDef>.AllDefsListForReading.OrderBy(d => d.LabelCap.ToString()).ToList();

        var items = CustomFloatMenu.MakeItems(allCategories, d => new MenuItemText(d, $"{d.LabelCap} ({d.defName})", tooltip: d.description));
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
        FloatRange defVal = defExt?.primaryMagazineCount ?? FloatRange.Zero;
        UIHelpers.DrawFloatRangeRow(
            ui,
            "FactionLoadout_CE_PrimaryMagazineCount".Translate(),
            ref data.PrimaryMagazineCount,
            0f,
            99f,
            defVal != FloatRange.Zero ? defVal : new FloatRange(1f, 3f)
        );
    }

    private static void DrawMinAmmoRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        bool hasOverride = data.MinAmmoCount.HasValue;
        int defVal = defExt?.minAmmoCount ?? 0;

        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_MinAmmoCount".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            int val = data.MinAmmoCount.Value;
            string buf = val.ToString();
            Widgets.TextFieldNumeric(fieldRect.LeftPart(0.45f), ref val, ref buf, 0, 9999);
            data.MinAmmoCount = val;

            if (Widgets.ButtonText(fieldRect.RightPart(0.38f), "FactionLoadout_Clear".Translate()))
            {
                data.MinAmmoCount = null;
            }
        }
        else
        {
            string hint = defVal > 0 ? $"({defVal})" : "(–)";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                data.MinAmmoCount = defVal > 0 ? defVal : 1;
            }
        }
    }

    private static void DrawWeightedAmmoCategoriesSection(Listing_Standard ui, CEData data)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(ui.GetRect(Text.LineHeight), "FactionLoadout_CE_WeightedAmmoCategories".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        ui.Gap(2f);

        data.WeightedAmmoCategories ??= [];

        int toRemove = -1;
        for (int i = 0; i < data.WeightedAmmoCategories.Count; i++)
        {
            WeightedAmmoCategoryData entry = data.WeightedAmmoCategories[i];
            Rect row = ui.GetRect(RowH);

            AmmoCategoryDef def = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(entry.AmmoCategoryDefName);
            string catLabel = def != null ? def.LabelCap.ToString() : entry.AmmoCategoryDefName + " (?)";

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.LeftPart(0.38f), catLabel);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(row.LeftPart(0.62f).RightPart(0.42f), "FactionLoadout_CE_Chance".Translate() + ":");
            Text.Anchor = TextAnchor.UpperLeft;

            Rect chanceField = row.LeftPart(0.76f).RightPart(0.15f);
            Rect removeRect = row.RightPart(0.22f);

            float chance = entry.Chance;
            string chanceBuf = chance.ToString("F2");
            Widgets.TextFieldNumeric(chanceField, ref chance, ref chanceBuf, 0f, 999f);
            entry.Chance = chance;

            if (Widgets.ButtonText(removeRect, "FactionLoadout_Clear".Translate()))
            {
                toRemove = i;
            }
        }

        if (toRemove >= 0)
        {
            data.WeightedAmmoCategories.RemoveAt(toRemove);
        }

        ui.Gap(2f);
        Rect addRow = ui.GetRect(RowH);
        if (Widgets.ButtonText(addRow.LeftPart(0.5f), "FactionLoadout_CE_AddAmmoCategory".Translate()))
        {
            List<AmmoCategoryDef> allCats = DefDatabase<AmmoCategoryDef>.AllDefsListForReading.OrderBy(d => d.LabelCap.ToString()).ToList();
            var items = CustomFloatMenu.MakeItems(allCats, d => new MenuItemText(d, $"{d.LabelCap} ({d.defName})", tooltip: d.description));
            CustomFloatMenu.Open(
                items,
                item =>
                {
                    AmmoCategoryDef selected = item.GetPayload<AmmoCategoryDef>();
                    data.WeightedAmmoCategories ??= [];
                    data.WeightedAmmoCategories.Add(new WeightedAmmoCategoryData { AmmoCategoryDefName = selected.defName, Chance = 1f });
                }
            );
        }
    }

    private static void DrawWeaponAmmoMappingsSection(Listing_Standard ui, CEData data, PawnKindEdit edit)
    {
        // Collect weapons/tags configured in the main Weapon tab, deduplicating by key
        List<string> shownKeys = [];

        if (edit.SpecificWeapons != null)
        {
            foreach (SpecRequirementEdit spec in edit.SpecificWeapons)
            {
                if (spec.Thing == null)
                    continue;
                string key = spec.Thing.defName;
                if (shownKeys.Contains(key))
                    continue;
                shownKeys.Add(key);
                DrawWeaponAmmoMappingRow(ui, data, key, false, spec.Thing.LabelCap.ToString());
            }
        }

        if (edit.WeaponTags != null)
        {
            foreach (string tag in edit.WeaponTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                string key = tag;
                if (shownKeys.Contains("tag:" + key))
                    continue;
                shownKeys.Add("tag:" + key);
                DrawWeaponAmmoMappingRow(ui, data, key, true, "tag: " + tag);
            }
        }

        if (shownKeys.Count == 0)
        {
            Rect hint = ui.GetRect(RowH);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(hint, "FactionLoadout_CE_WeaponAmmoMap_Hint".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        // Prune orphaned mapping entries whose source weapon/tag was removed from the main tab
        if (data.WeaponAmmoMappings != null)
        {
            data.WeaponAmmoMappings.RemoveAll(m =>
            {
                string k = m.IsTag ? "tag:" + m.WeaponKey : m.WeaponKey;
                return !shownKeys.Contains(k);
            });
            if (data.WeaponAmmoMappings.Count == 0)
            {
                data.WeaponAmmoMappings = null;
            }
        }
    }

    private static void DrawWeaponAmmoMappingRow(Listing_Standard ui, CEData data, string weaponKey, bool isTag, string weaponLabel)
    {
        // Find existing mapping entry
        WeaponAmmoMapEntry entry = null;
        int entryIdx = -1;
        if (data.WeaponAmmoMappings != null)
        {
            for (int i = 0; i < data.WeaponAmmoMappings.Count; i++)
            {
                if (data.WeaponAmmoMappings[i].WeaponKey == weaponKey && data.WeaponAmmoMappings[i].IsTag == isTag)
                {
                    entry = data.WeaponAmmoMappings[i];
                    entryIdx = i;
                    break;
                }
            }
        }

        // Header row: weapon label + Clear or Override button
        Rect headerRow = ui.GetRect(RowH);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(headerRow.LeftHalf(), weaponLabel);
        Text.Anchor = TextAnchor.UpperLeft;

        if (entry != null)
        {
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                data.WeaponAmmoMappings.RemoveAt(entryIdx);
                if (data.WeaponAmmoMappings.Count == 0)
                {
                    data.WeaponAmmoMappings = null;
                }
                return;
            }

            // Per-choice rows: ammo category button + chance field + remove button
            entry.Choices ??= [];
            int toRemove = -1;
            for (int i = 0; i < entry.Choices.Count; i++)
            {
                WeightedAmmoCategoryData choice = entry.Choices[i];
                Rect row = ui.GetRect(RowH);

                AmmoCategoryDef resolved = DefDatabase<AmmoCategoryDef>.GetNamedSilentFail(choice.AmmoCategoryDefName);
                string catLabel = resolved != null ? resolved.LabelCap.ToString() : (choice.AmmoCategoryDefName != null ? choice.AmmoCategoryDefName + " (?)" : "(select…)");

                if (Widgets.ButtonText(row.LeftPart(0.4f), catLabel))
                {
                    int capturedI = i;
                    List<WeightedAmmoCategoryData> capturedChoices = entry.Choices;
                    OpenAmmoCategoryMenuForChoices(catDef => capturedChoices[capturedI].AmmoCategoryDefName = catDef.defName);
                }

                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(row.LeftPart(0.63f).RightPart(0.42f), "FactionLoadout_CE_Chance".Translate() + ":");
                Text.Anchor = TextAnchor.UpperLeft;

                float chance = choice.Chance;
                string chanceBuf = chance.ToString("F2");
                Widgets.TextFieldNumeric(row.LeftPart(0.77f).RightPart(0.15f), ref chance, ref chanceBuf, 0f, 999f);
                choice.Chance = chance;

                if (Widgets.ButtonText(row.RightPart(0.21f), "FactionLoadout_Clear".Translate()))
                {
                    toRemove = i;
                }
            }

            if (toRemove >= 0)
            {
                entry.Choices.RemoveAt(toRemove);
            }

            Rect addRow = ui.GetRect(RowH);
            if (Widgets.ButtonText(addRow.LeftPart(0.5f), "FactionLoadout_CE_AddAmmoCategory".Translate()))
            {
                List<WeightedAmmoCategoryData> capturedChoices = entry.Choices;
                OpenAmmoCategoryMenuForChoices(catDef => capturedChoices.Add(new WeightedAmmoCategoryData { AmmoCategoryDefName = catDef.defName, Chance = 1f }));
            }
        }
        else
        {
            // Override: open picker immediately; only create entry on selection
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                CEData capturedData = data;
                string capturedKey = weaponKey;
                bool capturedIsTag = isTag;
                OpenAmmoCategoryMenuForChoices(catDef =>
                {
                    capturedData.WeaponAmmoMappings ??= [];
                    capturedData.WeaponAmmoMappings.Add(
                        new WeaponAmmoMapEntry
                        {
                            WeaponKey = capturedKey,
                            IsTag = capturedIsTag,
                            Choices = [new WeightedAmmoCategoryData { AmmoCategoryDefName = catDef.defName, Chance = 1f }],
                        }
                    );
                });
            }
        }

        ui.GapLine();
    }

    /// <summary>Opens the ammo category float-menu and calls <paramref name="onSelect"/> with the chosen def.</summary>
    private static void OpenAmmoCategoryMenuForChoices(System.Action<AmmoCategoryDef> onSelect)
    {
        List<AmmoCategoryDef> allCats = DefDatabase<AmmoCategoryDef>.AllDefsListForReading.OrderBy(d => d.LabelCap.ToString()).ToList();
        var items = CustomFloatMenu.MakeItems(allCats, d => new MenuItemText(d, $"{d.LabelCap} ({d.defName})", tooltip: d.description));
        CustomFloatMenu.Open(items, item => onSelect(item.GetPayload<AmmoCategoryDef>()));
    }

    #endregion

    #region Shields

    private static void DrawShieldMoneyRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        FloatRange defVal = defExt?.shieldMoney ?? FloatRange.Zero;
        UIHelpers.DrawFloatRangeRow(
            ui,
            "FactionLoadout_CE_ShieldMoney".Translate(),
            ref data.ShieldMoney,
            0f,
            99999f,
            defVal != FloatRange.Zero ? defVal : new FloatRange(100f, 500f)
        );
    }

    private static void DrawShieldChanceRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        float defVal = defExt?.shieldChance ?? 0f;
        UIHelpers.DrawFloatSliderRow(ui, "FactionLoadout_CE_ShieldChance".Translate(), ref data.ShieldChance, 0f, 1f, defVal > 0f ? defVal : 0.5f, asPercent: true);
    }

    private static void DrawForceShieldMaterialRow(Listing_Standard ui, CEData data, LoadoutPropertiesExtension defExt)
    {
        bool hasOverride = data.ForceShieldMaterial.HasValue;
        bool defVal = defExt?.forceShieldMaterial ?? false;

        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_ForceShieldMaterial".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            bool current = data.ForceShieldMaterial.Value;
            string toggleLabel = current ? "Yes".Translate().ToString() : "No".Translate().ToString();
            if (Widgets.ButtonText(fieldRect.LeftPart(0.45f), toggleLabel))
            {
                data.ForceShieldMaterial = !current;
            }

            if (Widgets.ButtonText(fieldRect.RightPart(0.38f), "FactionLoadout_Clear".Translate()))
            {
                data.ForceShieldMaterial = null;
            }
        }
        else
        {
            string hint = $"({(defVal ? "Yes".Translate() : "No".Translate())})";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), hint);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                data.ForceShieldMaterial = defVal;
            }
        }
    }

    private static void DrawShieldTagsSection(Listing_Standard ui, CEData data)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(ui.GetRect(Text.LineHeight), "FactionLoadout_CE_ShieldTags".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        ui.Gap(2f);

        data.ShieldTags ??= [];
        UIHelpers.DrawStringListSection(ui, data.ShieldTags, indent: false);
    }

    private static void DrawShieldMaterialFilterRow(Listing_Standard ui, CEData data)
    {
        bool hasOverride = data.ShieldMaterialFilter != null;

        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "FactionLoadout_CE_ShieldMaterialFilter".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        if (hasOverride)
        {
            if (Widgets.ButtonText(fieldRect.LeftPart(0.55f), "FactionLoadout_CE_EditFilter".Translate()))
            {
                Find.WindowStack.Add(new Window_ThingFilterEditor(data.ShieldMaterialFilter));
            }

            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                data.ShieldMaterialFilter = null;
            }
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(fieldRect.LeftPart(0.55f), "(–)");
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(fieldRect.RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                data.ShieldMaterialFilter = new ThingFilter();
                data.ShieldMaterialFilter.SetAllowAll(null);
            }
        }
    }

    #endregion

    #region Sidearms

    /// <summary>
    /// Returns configuration errors that will prevent this sidearm from generating a weapon,
    /// mirroring CE's TryGenerateWeaponWithAmmoFor guard conditions.
    /// Returns an empty list when the entry is valid (or intentionally disabled via 0% chance).
    /// </summary>
    private static List<string> GetSidearmIssues(SidearmData s)
    {
        List<string> issues = [];

        // CE guard: weaponTags.NullOrEmpty() → immediate return, nothing generated
        if (s.WeaponTags == null || s.WeaponTags.Count == 0)
            issues.Add("FactionLoadout_CE_Warn_NoTags".Translate());

        // CE guard: sidearmMoney.RandomInRange == 0 → w.Price <= 0 never true → nothing qualifies
        if (s.SidearmMoney == null || (s.SidearmMoney.Value.min <= 0f && s.SidearmMoney.Value.max <= 0f))
            issues.Add("FactionLoadout_CE_Warn_NoMoney".Translate());

        // Note: generate chance 0 is intentional (temporary disable) — not included here.
        // It is shown separately as a grey note in DrawSidearmDataFields.

        return issues;
    }

    /// <summary>
    /// Returns true when the entry is intentionally disabled via a 0% generate chance.
    /// This is not an error — the user may be keeping the config for later use.
    /// </summary>
    private static bool IsSidearmDisabled(SidearmData s) => s.GenerateChance.HasValue && s.GenerateChance.Value <= 0f;

    private static void DrawForcedSidearmSection(Listing_Standard ui, CEData data)
    {
        Rect headerRow = ui.GetRect(RowH);
        Text.Anchor = TextAnchor.MiddleLeft;

        if (data.ForcedSidearm != null && GetSidearmIssues(data.ForcedSidearm).Count > 0)
        {
            GUI.color = Color.yellow;
            Widgets.Label(headerRow.LeftHalf(), "⚠ " + "FactionLoadout_CE_ForcedSidearm".Translate());
            GUI.color = Color.white;
        }
        else
        {
            Widgets.Label(headerRow.LeftHalf(), "FactionLoadout_CE_ForcedSidearm".Translate());
        }

        Text.Anchor = TextAnchor.UpperLeft;

        if (data.ForcedSidearm != null)
        {
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                data.ForcedSidearm = null;
                return;
            }

            ui.Gap(2f);
            DrawSidearmDataFields(ui, data.ForcedSidearm, indent: false);
        }
        else
        {
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                data.ForcedSidearm = new SidearmData();
            }
        }
    }

    private static void DrawSidearmsListSection(Listing_Standard ui, CEData data)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(ui.GetRect(Text.LineHeight), "FactionLoadout_CE_Sidearms".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        ui.Gap(2f);

        data.Sidearms ??= [];

        int toRemove = -1;
        for (int i = 0; i < data.Sidearms.Count; i++)
        {
            Rect itemHeader = ui.GetRect(RowH);
            Text.Anchor = TextAnchor.MiddleLeft;

            List<string> issues = GetSidearmIssues(data.Sidearms[i]);
            if (issues.Count > 0)
            {
                GUI.color = Color.yellow;
                Widgets.Label(itemHeader.LeftHalf(), $"  ⚠ [{i + 1}]");
                GUI.color = Color.white;
            }
            else
            {
                Widgets.Label(itemHeader.LeftHalf(), $"  [{i + 1}]");
            }

            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(itemHeader.RightHalf().RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                toRemove = i;
            }
            else
            {
                ui.Gap(2f);
                DrawSidearmDataFields(ui, data.Sidearms[i], indent: true);
                ui.GapLine();
            }
        }

        if (toRemove >= 0)
        {
            data.Sidearms.RemoveAt(toRemove);
        }

        ui.Gap(2f);
        Rect addRow = ui.GetRect(RowH);
        if (Widgets.ButtonText(addRow.LeftPart(0.4f), "FactionLoadout_CE_AddSidearm".Translate()))
        {
            data.Sidearms ??= [];
            data.Sidearms.Add(new SidearmData());
        }
    }

    private static void DrawSidearmDataFields(Listing_Standard ui, SidearmData s, bool indent)
    {
        string prefix = indent ? "  " : "";

        // Show issues that will prevent this sidearm from generating
        List<string> issues = GetSidearmIssues(s);
        if (issues.Count > 0)
        {
            GUI.color = Color.yellow;
            foreach (string issue in issues)
            {
                Widgets.Label(ui.GetRect(Text.LineHeight), prefix + issue);
            }
            GUI.color = Color.white;
            ui.Gap(4f);
        }

        // Show grey note when intentionally disabled via 0% generate chance
        if (IsSidearmDisabled(s))
        {
            GUI.color = Color.grey;
            Widgets.Label(ui.GetRect(Text.LineHeight), prefix + "FactionLoadout_CE_Note_Disabled".Translate());
            GUI.color = Color.white;
            ui.Gap(4f);
        }

        UIHelpers.DrawFloatRangeRow(ui, prefix + "FactionLoadout_CE_SidearmMoney".Translate(), ref s.SidearmMoney, 0f, 99999f, new FloatRange(100f, 500f));
        ui.Gap(2f);

        UIHelpers.DrawFloatRangeRow(ui, prefix + "FactionLoadout_CE_MagazineCount".Translate(), ref s.MagazineCount, 0f, 99f, new FloatRange(1f, 3f));
        ui.Gap(2f);

        UIHelpers.DrawFloatSliderRow(ui, prefix + "FactionLoadout_CE_GenerateChance".Translate(), ref s.GenerateChance, 0f, 1f, 1f, asPercent: true);
        ui.Gap(2f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(ui.GetRect(Text.LineHeight), prefix + "FactionLoadout_CE_WeaponTags".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        s.WeaponTags ??= [];
        UIHelpers.DrawStringListSection(ui, s.WeaponTags, PawnKindEditUI.AllWeaponsTags ?? [], indent: true);
        ui.Gap(2f);

        DrawAttachmentDataInline(ui, ref s.Attachments, prefix + "FactionLoadout_CE_Attachments".Translate());
    }

    #endregion

    #region Attachments

    private static void DrawPrimaryAttachmentsSection(Listing_Standard ui, CEData data)
    {
        DrawAttachmentDataInline(ui, ref data.PrimaryAttachments, "FactionLoadout_CE_PrimaryAttachments".Translate());
    }

    private static void DrawAttachmentDataInline(Listing_Standard ui, ref AttachmentData att, string label)
    {
        Rect headerRow = ui.GetRect(RowH);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(headerRow.LeftHalf(), label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (att != null)
        {
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Clear".Translate()))
            {
                att = null;
                return;
            }

            ui.Gap(2f);

            UIHelpers.DrawFloatRangeRow(ui, "  " + "FactionLoadout_CE_AttachmentCount".Translate(), ref att.AttachmentCount, 0f, 99f, new FloatRange(1f, 2f));
            ui.Gap(2f);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(ui.GetRect(Text.LineHeight), "  " + "FactionLoadout_CE_AttachmentTags".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            att.AttachmentTags ??= [];
            UIHelpers.DrawStringListSection(ui, att.AttachmentTags, indent: true);
        }
        else
        {
            if (Widgets.ButtonText(headerRow.RightHalf().RightPart(0.4f), "FactionLoadout_Override".Translate()))
            {
                att = new AttachmentData();
            }
        }
    }

    #endregion
}
