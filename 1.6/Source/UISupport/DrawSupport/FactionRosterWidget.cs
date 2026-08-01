using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// The "Faction Render" gallery for the Faction Edit screen: a scrollable grid of live
/// portraits, one per pawnkind in the faction, each generated through the real pipeline
/// (via <see cref="PreviewPawnController"/>) so the roster shows exactly what would spawn
/// with the current edits applied.
///
/// Generation is staggered (a small budget of new pawns per frame) so opening the gallery
/// doesn't spike a frame, and every throwaway pawn is disposed on <see cref="Dispose"/> so
/// nothing leaks into the save. Clicking a cell opens that pawnkind for editing.
/// </summary>
[HotSwappable]
public class FactionRosterWidget
{
    /// <summary>Hard cap so a pathological modded faction can't try to render hundreds of pawns.</summary>
    public const int MaxRendered = 48;

    /// <summary>How many new preview pawns may be generated per frame (staggers the initial fill).</summary>
    public const int GenerationBudgetPerFrame = 2;

    public readonly FactionEdit Faction;

    private readonly Dictionary<PawnKindDef, PreviewPawnController> controllers = new();
    private List<PawnKindDef> kinds;
    private int totalKindCount;

    private Vector2 scroll;
    private Rot4 rotation = Rot4.South;
    private bool renderHeadgear = true;
    private bool renderClothes = true;

    public FactionRosterWidget(FactionEdit faction)
    {
        Faction = faction;
    }

    private void EnsureKinds()
    {
        if (kinds != null)
            return;

        List<PawnKindDef> all = Faction.GetAllKindDefsForUI().Select(PawnKindEdit.NormaliseDef).Where(k => k != null).Distinct().ToList();
        totalKindCount = all.Count;
        kinds = all.Take(MaxRendered).ToList();
    }

    public void Draw(Rect rect, TotalControlController controller)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(6f);

        if (Verse.Current.Game == null)
        {
            DrawCentered(rect, $"<color=yellow>{"FactionLoadout_Roster_NotInGame".Translate()}</color>");
            return;
        }

        EnsureKinds();

        // Tick every live controller so debounced (re)generation makes progress.
        foreach (PreviewPawnController c in controllers.Values)
            c.Tick();

        // --- Toolbar: regenerate-all + headgear/clothes toggles ---
        Rect toolbar = new(rect.x, rect.y, rect.width, 28f);
        float toggleW = 130f;
        Rect regenRect = new(toolbar.x, toolbar.y, toolbar.width - 2f * (toggleW + 6f), toolbar.height);
        if (Widgets.ButtonText(regenRect, "FactionLoadout_Roster_RegenerateAll".Translate()))
        {
            foreach (PreviewPawnController c in controllers.Values)
                c.RequestRegenerate();
        }
        Rect hgRect = new(regenRect.xMax + 6f, toolbar.y + 2f, toggleW, 24f);
        Rect clRect = new(hgRect.xMax + 6f, toolbar.y + 2f, toggleW, 24f);
        Widgets.CheckboxLabeled(hgRect, "FactionLoadout_Preview_ShowHeadgear".Translate(), ref renderHeadgear);
        Widgets.CheckboxLabeled(clRect, "FactionLoadout_Preview_ShowClothes".Translate(), ref renderClothes);

        float bodyY = toolbar.yMax + 6f;

        // Truncation note (when the faction has more kinds than we render).
        if (totalKindCount > kinds.Count)
        {
            Rect note = new(rect.x, bodyY, rect.width, 20f);
            GUI.color = new Color(1f, 0.75f, 0.2f);
            Widgets.Label(note, "FactionLoadout_Roster_Truncated".Translate(kinds.Count, totalKindCount));
            GUI.color = Color.white;
            bodyY = note.yMax + 4f;
        }

        Rect gridOut = new(rect.x, bodyY, rect.width, rect.yMax - bodyY);

        if (kinds.Count == 0)
        {
            DrawCentered(gridOut, "FactionLoadout_Roster_Empty".Translate());
            return;
        }

        // --- Grid geometry ---
        const float cellW = 150f;
        const float cellH = 190f;
        const float pad = 8f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((gridOut.width - 16f + pad) / (cellW + pad)));
        int rows = Mathf.CeilToInt(kinds.Count / (float)cols);
        float contentH = Mathf.Max(rows * (cellH + pad), gridOut.height);
        Rect view = new(0, 0, gridOut.width - 16f, contentH);

        int generatedThisFrame = 0;

        Widgets.BeginScrollView(gridOut, ref scroll, view);
        for (int i = 0; i < kinds.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            Rect cell = new(col * (cellW + pad), row * (cellH + pad), cellW, cellH);

            // Skip drawing cells scrolled fully out of view (still generate lazily below the fold
            // once scrolled to, but don't pay portrait cost for off-screen rows).
            bool visible = cell.yMax >= scroll.y && cell.y <= scroll.y + gridOut.height;
            DrawCell(cell, kinds[i], controller, visible, ref generatedThisFrame);
        }
        Widgets.EndScrollView();
    }

    private void DrawCell(Rect cell, PawnKindDef kind, TotalControlController controller, bool visible, ref int generatedThisFrame)
    {
        Widgets.DrawBoxSolid(cell, new Color(0.16f, 0.16f, 0.16f, 1f));
        Widgets.DrawHighlightIfMouseover(cell);

        Rect portrait = new(cell.x + 6f, cell.y + 6f, cell.width - 12f, cell.height - 34f);
        Rect labelRect = new(cell.x + 4f, portrait.yMax + 2f, cell.width - 8f, 24f);

        // Lazily create a controller for this kind, within the per-frame budget.
        if (!controllers.TryGetValue(kind, out PreviewPawnController c))
        {
            if (generatedThisFrame < GenerationBudgetPerFrame)
            {
                c = new PreviewPawnController(Faction, kind);
                controllers[kind] = c;
                generatedThisFrame++;
            }
        }

        if (visible)
            DrawPortrait(portrait, c);

        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        Widgets.Label(labelRect, kind.LabelCap);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(cell, $"{kind.LabelCap}\n\n{"FactionLoadout_Roster_CellTooltip".Translate()}");
        if (Widgets.ButtonInvisible(cell))
            OpenKindForEdit(kind, controller);
    }

    private void DrawPortrait(Rect area, PreviewPawnController controller)
    {
        if (controller == null || controller.PreviewPawn == null)
        {
            DrawCentered(
                area,
                controller is { PreviewFailed: true } ? $"<color=orange>{"FactionLoadout_Preview_Failed".Translate()}</color>" : "FactionLoadout_Roster_Loading".Translate()
            );
            return;
        }

        const float aspect = 0.7f;
        float w = Mathf.Min(area.width, area.height * aspect);
        float h = w / aspect;
        if (h > area.height)
        {
            h = area.height;
            w = h * aspect;
        }
        Rect portrait = new(area.x + (area.width - w) / 2f, area.y + (area.height - h) / 2f, w, h);

        try
        {
            RenderTexture tex = PortraitsCache.Get(
                controller.PreviewPawn,
                new Vector2(portrait.width, portrait.height),
                rotation,
                default,
                1f,
                supersample: true,
                compensateForUIScale: true,
                renderHeadgear,
                renderClothes
            );
            GUI.DrawTexture(portrait, tex);
        }
        catch (Exception e)
        {
            ModCore.Error("Failed to render roster portrait.", e);
            controller.MarkFailed();
        }
    }

    private void OpenKindForEdit(PawnKindDef kind, TotalControlController controller)
    {
        PawnKindEdit existing = Faction.GetEditFor(kind);
        if (existing == null)
        {
            existing = new PawnKindEdit(kind);
            Faction.KindEdits.Add(existing);
        }
        controller.OpenKind(existing);
    }

    private static void DrawCentered(Rect area, string text)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        Widgets.Label(area, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    public void Dispose()
    {
        foreach (PreviewPawnController c in controllers.Values)
            c.Dispose();
        controllers.Clear();
    }
}
