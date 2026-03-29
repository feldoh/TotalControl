using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Confirmation dialog shown before resetting all group edits.
/// Lists any pawnkinds that were added by the user and will become orphaned.
/// </summary>
public class Dialog_ResetGroupsConfirm : Window
{
    private readonly FactionEdit _edit;
    private List<string> _addedKindNames;

    public Dialog_ResetGroupsConfirm(FactionEdit edit)
    {
        _edit = edit;
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
        draggable = false;

        // Find pawnkinds that are in GroupEdits but NOT in the original faction def.
        _addedKindNames = [];
        if (edit.PawnGroupMakerEdits == null)
            return;

        HashSet<string> original = new(
            FactionEdit.GetAllPawnKinds(FactionEdit.TryGetOriginal(edit.Faction.DefName) ?? edit.Faction.Def ?? new FactionDef()).Select(k => k.defName)
        );

        foreach (PawnGroupMakerEdit g in edit.PawnGroupMakerEdits)
        {
            foreach (PawnKindDef k in g.GetAllKinds())
            {
                if (!original.Contains(k.defName) && !_addedKindNames.Contains(k.LabelCap.ToString()))
                    _addedKindNames.Add(k.LabelCap);
            }
        }
    }

    public override Vector2 InitialSize => new(480f, _addedKindNames.Count > 0 ? 300f : 180f);

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard ui = new();
        ui.Begin(inRect);

        Text.Font = GameFont.Medium;
        ui.Label("FactionLoadout_GroupEditor_ResetConfirmTitle".Translate());
        Text.Font = GameFont.Small;

        ui.GapLine();
        ui.Label("FactionLoadout_GroupEditor_ResetConfirmBody".Translate());

        if (_addedKindNames.Count > 0)
        {
            ui.Gap(6f);
            GUI.color = new Color(1f, 0.7f, 0.2f);
            ui.Label("FactionLoadout_GroupEditor_ResetConfirmOrphans".Translate(_addedKindNames.Count));
            GUI.color = Color.white;
            foreach (string name in _addedKindNames)
                ui.Label($"  · {name}");
            ui.Gap(4f);
            GUI.color = Color.grey;
            ui.Label("<i>" + "FactionLoadout_GroupEditor_ResetConfirmOrphanNote".Translate() + "</i>");
            GUI.color = Color.white;
        }

        ui.GapLine();

        Rect btnRow = ui.GetRect(28f);
        if (Widgets.ButtonText(new Rect(btnRow.x, btnRow.y, 100f, 24f), "Cancel".Translate()))
            Close();

        GUI.color = Color.red;
        if (Widgets.ButtonText(new Rect(btnRow.xMax - 120f, btnRow.y, 120f, 24f), "FactionLoadout_GroupEditor_ResetConfirmButton".Translate()))
        {
            _edit.ResetGroupEdits();
            Close();
        }

        GUI.color = Color.white;
        ui.End();
    }
}
