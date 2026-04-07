using System;
using System.Collections.Generic;
using System.Linq;
using FactionLoadout.UISupport;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class Dialog_XenotypeEdit : Window
{
    private readonly FactionEdit _edit;
    private Vector2 _scrollPos;

    public Dialog_XenotypeEdit(FactionEdit edit)
    {
        _edit = edit;
        doCloseX = true;
        closeOnCancel = true;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(450f, 400f);

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard ui = new();
        ui.Begin(inRect);

        ui.CheckboxLabeled($"<b>{"FactionLoadout_EditXenoSpawnRates".Translate()}:</b>", ref _edit.OverrideFactionXenotypes);

        if (_edit.OverrideFactionXenotypes)
        {
            if (_edit.xenotypeChances.NullOrEmpty())
            {
                _edit.xenotypeChances = _edit.Faction?.Def?.xenotypeSet?.xenotypeChances?.ToDictionary(x => x.xenotype.defName, x => x.chance) ?? new Dictionary<string, float>();
                if (!_edit.xenotypeChances.ContainsKey(FactionEditUI.BaselinerDefName))
                    _edit.xenotypeChances.Add(FactionEditUI.BaselinerDefName, _edit.Faction?.Def?.xenotypeSet?.BaselinerChance ?? 1f);
            }

            _edit.xenotypeChances[FactionEditUI.BaselinerDefName] = Math.Max(0f, 1f - _edit.xenotypeChances.Sum(x => x.Key == FactionEditUI.BaselinerDefName ? 0 : x.Value));

            // Reserve space for add buttons at bottom.
            const float addButtonsHeight = 70f;
            float scrollH = Mathf.Max(30f, inRect.height - ui.CurHeight - addButtonsHeight);
            Rect scrollOutRect = ui.GetRect(scrollH);
            // Compute inner height directly from item count — avoids feedback loops where
            // Listing_Standard.CurHeight is clamped to the inner rect, preventing growth.
            const float ItemRowH = 32f; // SliderLabeledWithDelete: GetRect(30) + Gap(2)
            float contentH = _edit.xenotypeChances.Count * ItemRowH;
            Rect innerRect = new(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(contentH, scrollH));

            Widgets.BeginScrollView(scrollOutRect, ref _scrollPos, innerRect);
            Listing_Standard inner = new();
            inner.Begin(innerRect);

            List<string> toDelete = [];
            foreach (string key in _edit.xenotypeChances.Keys.OrderBy(k => DefDatabase<XenotypeDef>.GetNamedSilentFail(k)?.LabelCap.ToString() ?? k).ToList())
                _edit.xenotypeChances[key] = UIHelpers.SliderLabeledWithDelete(
                    inner,
                    $"{DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap ?? key}: {_edit.xenotypeChances[key].ToStringPercent()}",
                    _edit.xenotypeChances[key],
                    0f,
                    1f,
                    deleteAction: delegate
                    {
                        toDelete.Add(key);
                    }
                );

            foreach (string delete in toDelete)
                _edit.xenotypeChances.Remove(delete);

            inner.End();
            Widgets.EndScrollView();

            if (ui.ButtonText("FactionLoadout_AddNewByDefName".Translate()))
            {
                Find.WindowStack.Add(
                    new Dialog_TextEntry(
                        "FactionLoadout_AddNewByDefNameDesc".Translate(),
                        defName =>
                        {
                            if (_edit.xenotypeChances.ContainsKey(defName))
                            {
                                Messages.Message("FactionLoadout_DuplicateListItem".Translate(defName), MessageTypeDefOf.RejectInput);
                                return;
                            }
                            _edit.xenotypeChances[defName] = 0.1f;
                        }
                    )
                );
            }

            if (ModLister.BiotechInstalled && ui.ButtonText("Add".Translate().CapitalizeFirst() + "..."))
            {
                List<MenuItemBase> xenoItems = CustomFloatMenu.MakeItems(
                    DefDatabase<XenotypeDef>.AllDefs.Where(def => !_edit.xenotypeChances.ContainsKey(def.defName)),
                    def => new MenuItemText(def, def.LabelCap, def.Icon)
                );
                CustomFloatMenu.Open(
                    xenoItems,
                    item =>
                    {
                        XenotypeDef def = item.GetPayload<XenotypeDef>();
                        _edit.xenotypeChances[def.defName] = 0.1f;
                    }
                );
            }
        }
        else
        {
            _edit.xenotypeChances.Clear();
            _edit.xenotypeChancesByDef.Clear();
        }

        ui.End();
    }
}
