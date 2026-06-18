using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Draws the big live pawn portrait plus its control strip (rotate, regenerate, and
/// headgear/clothes toggles). Pure drawing + view state; the expensive pawn lifecycle
/// lives in <see cref="PreviewPawnController"/>.
/// </summary>
public class PawnPreviewWidget
{
    private Rot4 rotation = Rot4.South;
    private bool renderHeadgear = true;
    private bool renderClothes = true;

    public void Draw(Rect inRect, PreviewPawnController controller)
    {
        Widgets.DrawMenuSection(inRect);
        Rect content = inRect.ContractedBy(8f);

        const float stripH = 92f;
        Rect portraitArea = new(content.x, content.y, content.width, content.height - stripH - 4f);
        Rect strip = new(content.x, portraitArea.yMax + 4f, content.width, stripH);

        DrawPortrait(portraitArea, controller);
        DrawControls(strip, controller);
    }

    private void DrawPortrait(Rect area, PreviewPawnController controller)
    {
        if (!controller.InGame)
        {
            DrawCentered(area, $"<color=yellow>{"FactionLoadout_FactionEdit_PreviewError".Translate()}</color>");
            return;
        }
        if (controller.PreviewFailed)
        {
            DrawCentered(area, $"<color=orange>{"FactionLoadout_Preview_Failed".Translate()}</color>");
            return;
        }
        if (controller.PreviewPawn == null)
        {
            DrawCentered(area, "FactionLoadout_Preview_Placeholder".Translate());
            return;
        }

        // Centered portrait-aspect sub-rect so the pawn fills nicely without distortion.
        const float aspect = 0.7f; // width / height
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
            ModCore.Error("Failed to render preview portrait.", e);
            controller.MarkFailed();
        }
    }

    private void DrawControls(Rect strip, PreviewPawnController controller)
    {
        // Row 1: rotate ◀ / regenerate / rotate ▶
        Rect row1 = new(strip.x, strip.y, strip.width, 30f);
        float rotW = 40f;
        if (Widgets.ButtonText(new Rect(row1.x, row1.y, rotW, row1.height), "◀"))
            rotation = rotation.Rotated(RotationDirection.Counterclockwise);
        if (Widgets.ButtonText(new Rect(row1.xMax - rotW, row1.y, rotW, row1.height), "▶"))
            rotation = rotation.Rotated(RotationDirection.Clockwise);
        Rect regenRect = new(row1.x + rotW + 4f, row1.y, row1.width - 2 * (rotW + 4f), row1.height);
        if (Widgets.ButtonText(regenRect, "FactionLoadout_Preview_Regenerate".Translate()))
            controller.RequestRegenerate();

        // Row 2: headgear / clothes toggles
        Rect row2 = new(strip.x, row1.yMax + 6f, strip.width, 24f);
        Rect half1 = new(row2.x, row2.y, row2.width / 2f - 4f, row2.height);
        Rect half2 = new(row2.x + row2.width / 2f + 4f, row2.y, row2.width / 2f - 4f, row2.height);
        Widgets.CheckboxLabeled(half1, "FactionLoadout_Preview_ShowHeadgear".Translate(), ref renderHeadgear);
        Widgets.CheckboxLabeled(half2, "FactionLoadout_Preview_ShowClothes".Translate(), ref renderClothes);
    }

    private static void DrawCentered(Rect area, string text)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        Widgets.Label(area, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
