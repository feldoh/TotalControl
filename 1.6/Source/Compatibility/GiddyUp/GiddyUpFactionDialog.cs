using System.Collections.Generic;
using System.Linq;
using FactionLoadout;
using FactionLoadout.UISupport;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Dedicated dialog for editing GiddyUp faction-level mount restrictions.
/// Corresponds to GiddyUp's FactionRestrictions DefModExtension.
/// </summary>
public class GiddyUpFactionDialog : Window
{
    private readonly FactionEdit _edit;
    private Vector2 _scrollPos;

    // Buffer strings for weight TextFieldNumeric
    private string _wildWeightBuf = "";
    private string _nonWildWeightBuf = "";

    // Cached values read from the FactionDef's existing FactionRestrictions extension (if any)
    private bool _defaultsRead;
    private int? _defMountChance;
    private int? _defWildWeight;
    private int? _defNonWildWeight;

    private const float BtnW = 90f;
    private const float RowH = 22f + 4f; // Text.LineHeight + padding

    public GiddyUpFactionDialog(FactionEdit edit)
    {
        _edit = edit;
        doCloseX = true;
        closeOnCancel = true;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(960f, 560f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(inRect.TopPartPixels(32f), "GU_FactionDialog_Title".Translate());
        Text.Font = GameFont.Small;

        EnsureDefaultsRead();

        Rect contentRect = new(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
        GiddyUpFactionData data = GiddyUpModule.GetOrCreateFactionData(_edit);

        float contentH = CalcContentHeight(data, contentRect.width - 16f);
        Rect viewRect = new(0, 0, contentRect.width - 16f, Mathf.Max(contentH, contentRect.height));

        Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);
        Listing_Standard ui = new();
        ui.Begin(viewRect);

        DrawMountChance(ui, data);
        ui.GapLine();
        DrawWeights(ui, data);
        ui.GapLine();
        DrawAnimalList(ui, "GU_AllowedWildAnimals".Translate(), "GU_AllowedWildAnimals_Note".Translate(), data.AllowedWildAnimals ??= [], "GU_AddWildAnimal");
        ui.GapLine();
        DrawAnimalList(ui, "GU_AllowedNonWildAnimals".Translate(), "GU_AllowedNonWildAnimals_Note".Translate(), data.AllowedNonWildAnimals ??= [], "GU_AddNonWildAnimal");

        ui.End();
        Widgets.EndScrollView();
    }

    private void EnsureDefaultsRead()
    {
        if (_defaultsRead)
            return;
        _defaultsRead = true;

        if (!GiddyUpReflection.IsFactionResolved)
            return;

        FactionDef factionDef = _edit.Faction?.Def;
        if (factionDef?.modExtensions == null)
            return;

        DefModExtension ext = factionDef.modExtensions.FirstOrDefault(e => e.GetType() == GiddyUpReflection.FactionRestrictionsType);
        if (ext == null)
            return;

        int mc = (int)GiddyUpReflection.FactionMountChanceField.GetValue(ext);
        if (mc >= 0)
            _defMountChance = mc;

        if (GiddyUpReflection.WildAnimalWeightField != null)
        {
            int ww = (int)GiddyUpReflection.WildAnimalWeightField.GetValue(ext);
            if (ww >= 0)
                _defWildWeight = ww;
        }

        if (GiddyUpReflection.NonWildAnimalWeightField != null)
        {
            int nw = (int)GiddyUpReflection.NonWildAnimalWeightField.GetValue(ext);
            if (nw >= 0)
                _defNonWildWeight = nw;
        }
    }

    private void DrawMountChance(Listing_Standard ui, GiddyUpFactionData data)
    {
        Widgets.Label(ui.GetRect(RowH), "<b>" + "GU_FactionMountChance".Translate() + "</b>");
        ui.Gap(2f);

        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        if (data.MountChance != null)
        {
            Widgets.Label(labelRect, "GU_MountChanceValue".Translate(data.MountChance.Value));
            Rect sliderRect = new(fieldRect.x, fieldRect.y, fieldRect.width - BtnW - 4f, fieldRect.height);
            Rect btnRect = new(fieldRect.xMax - BtnW, fieldRect.y, BtnW, fieldRect.height);
            int val = (int)Widgets.HorizontalSlider(sliderRect, data.MountChance.Value, 0, 100, true, data.MountChance.Value + "%");
            data.MountChance = val;
            if (Widgets.ButtonText(btnRect, "FactionLoadout_Clear".Translate()))
                data.MountChance = null;
        }
        else
        {
            string defaultLabel =
                _defMountChance != null ? "GU_MountChanceDefault_Known".Translate(_defMountChance.Value).ToString() : "GU_FactionMountChanceDefault".Translate().ToString();
            Color prev = GUI.color;
            GUI.color = Color.gray;
            Widgets.Label(labelRect, defaultLabel);
            GUI.color = prev;
            if (Widgets.ButtonText(new Rect(fieldRect.x, fieldRect.y, BtnW + 20f, fieldRect.height), "FactionLoadout_Override".Translate()))
                data.MountChance = _defMountChance ?? 50;
        }
    }

    private void DrawWeights(Listing_Standard ui, GiddyUpFactionData data)
    {
        Widgets.Label(ui.GetRect(RowH), "<b>" + "GU_AnimalWeights".Translate() + "</b>");

        string noteText = "GU_AnimalWeights_Note".Translate().ToString();
        float noteH = Text.CalcHeight(noteText, ui.ColumnWidth);
        Color prev = GUI.color;
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        Widgets.Label(ui.GetRect(noteH), noteText);
        Text.Font = GameFont.Small;
        GUI.color = prev;

        ui.Gap(2f);
        DrawWeightRow(ui, "GU_WildAnimalWeight".Translate(), ref data.WildAnimalWeight, ref _wildWeightBuf, _defWildWeight);
        DrawWeightRow(ui, "GU_NonWildAnimalWeight".Translate(), ref data.NonWildAnimalWeight, ref _nonWildWeightBuf, _defNonWildWeight);
    }

    private static void DrawWeightRow(Listing_Standard ui, string label, ref int? value, ref string buffer, int? defValue)
    {
        Rect row = ui.GetRect(RowH);
        Rect labelRect = row.LeftHalf();
        Rect fieldRect = row.RightHalf();

        Widgets.Label(labelRect, label);

        if (value != null)
        {
            if (buffer == null || !int.TryParse(buffer, out int parsed) || parsed != value.Value)
                buffer = value.Value.ToString();

            int val = value.Value;
            Rect inputRect = new(fieldRect.x, fieldRect.y, fieldRect.width - BtnW - 4f, fieldRect.height);
            Rect btnRect = new(fieldRect.xMax - BtnW, fieldRect.y, BtnW, fieldRect.height);
            Widgets.TextFieldNumeric(inputRect, ref val, ref buffer, 0, 9999);
            value = val;
            if (Widgets.ButtonText(btnRect, "FactionLoadout_Clear".Translate()))
            {
                value = null;
                buffer = "";
            }
        }
        else
        {
            Rect defaultLabelRect = new(fieldRect.x, fieldRect.y, fieldRect.width - BtnW - 4f, fieldRect.height);
            Rect overrideBtnRect = new(fieldRect.xMax - BtnW, fieldRect.y, BtnW, fieldRect.height);
            string defaultLabel = defValue != null ? "GU_WeightDefault_Known".Translate(defValue.Value).ToString() : "GU_WeightDefault".Translate().ToString();
            Color prev = GUI.color;
            GUI.color = Color.gray;
            Widgets.Label(defaultLabelRect, defaultLabel);
            GUI.color = prev;
            TooltipHandler.TipRegion(defaultLabelRect, "GU_WeightDefault_Tip".Translate());
            if (Widgets.ButtonText(overrideBtnRect, "FactionLoadout_Override".Translate()))
            {
                value = defValue ?? 100;
                buffer = value.Value.ToString();
            }
        }
    }

    private static void DrawAnimalList(Listing_Standard ui, string header, string note, List<string> list, string addKey)
    {
        Widgets.Label(ui.GetRect(RowH), "<b>" + header + "</b>");

        Color prev = GUI.color;
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        float noteH = Text.CalcHeight(note, ui.ColumnWidth);
        Widgets.Label(ui.GetRect(noteH), note);
        Text.Font = GameFont.Small;
        GUI.color = prev;

        ui.Gap(2f);

        string toRemove = null;
        foreach (string defName in list)
        {
            Rect row = ui.GetRect(RowH);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            string label = kind != null ? $"{kind.LabelCap} ({defName})" : defName;
            Rect btnRect = new(row.xMax - BtnW, row.y, BtnW, row.height);
            Widgets.Label(new Rect(row.x, row.y, row.width - BtnW - 4f, row.height), label);
            if (Widgets.ButtonText(btnRect, "Remove".Translate()))
                toRemove = defName;
        }

        if (toRemove != null)
            list.Remove(toRemove);

        ui.Gap(2f);

        if (ui.ButtonText(addKey.Translate()))
        {
            List<PawnKindDef> candidateList = DefDatabase<PawnKindDef>
                .AllDefsListForReading.Where(k => k.RaceProps.Animal && !list.Contains(k.defName))
                .OrderBy(k => (string)k.LabelCap)
                .ToList();

            if (candidateList.Count > 0)
            {
                var items = CustomFloatMenu.MakeItems(
                    candidateList,
                    k => new MenuItemText(k, $"{k.LabelCap} ({k.defName})", tooltip: k.description) { Size = new Vector2(424, 28) }
                );
                CustomFloatMenu menu = CustomFloatMenu.Open(items, item => list.Add(item.GetPayload<PawnKindDef>().defName));
                menu.Columns = 1;
            }
        }
    }

    private static float CalcContentHeight(GiddyUpFactionData data, float width)
    {
        const float rowAdv = RowH + 2f; // RowH + verticalSpacing
        const float gapLine = 12f;
        const float gap2 = 2f;
        const float btnRowH = 30f + 2f;

        // Mount chance section: header + gap2 + row
        float h = rowAdv + gap2 + rowAdv;

        // GapLine
        h += gapLine;

        // Weights section: header + note + gap2 + 2 rows
        Text.Font = GameFont.Tiny;
        float weightsNoteH = Text.CalcHeight("GU_AnimalWeights_Note".Translate().ToString(), width) + 2f;
        Text.Font = GameFont.Small;
        h += rowAdv + weightsNoteH + gap2 + rowAdv + rowAdv;

        // GapLine
        h += gapLine;

        // Wild animals section: header + note + gap2 + items + gap2 + add btn
        Text.Font = GameFont.Tiny;
        float wildNoteH = Text.CalcHeight("GU_AllowedWildAnimals_Note".Translate().ToString(), width) + 2f;
        Text.Font = GameFont.Small;
        int wildCount = data.AllowedWildAnimals?.Count ?? 0;
        h += rowAdv + wildNoteH + gap2 + wildCount * rowAdv + gap2 + btnRowH;

        // GapLine
        h += gapLine;

        // Non-wild animals section: header + note + gap2 + items + gap2 + add btn
        Text.Font = GameFont.Tiny;
        float nonWildNoteH = Text.CalcHeight("GU_AllowedNonWildAnimals_Note".Translate().ToString(), width) + 2f;
        Text.Font = GameFont.Small;
        int nonWildCount = data.AllowedNonWildAnimals?.Count ?? 0;
        h += rowAdv + nonWildNoteH + gap2 + nonWildCount * rowAdv + gap2 + btnRowH;

        return h + 20f; // safety margin
    }
}
