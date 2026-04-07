using System;
using System.Collections;
using System.Collections.Generic;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Static helpers for the toggle-override pattern used throughout the editor.
/// Each overload handles a different field kind: nullable struct, nullable class, or IList.
/// </summary>
public static class OverrideDrawSupport
{
    // ==================== DrawOverride (struct) ====================

    public static void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T? field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height,
        Func<PawnKindEdit, T?> pasteGet,
        Action resetBuffers
    )
        where T : struct
    {
        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = field != null;

        const float toggleW = 120f;
        bool hasPaste = PawnKindClipboard.HasData && pasteGet != null;
        float pasteW = hasPaste ? 28f : 0f;

        string overrideLabel = "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate());
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, toggleW, 32), overrideLabel))
        {
            field = active ? null : defaultValue;
            active = !active;
        }

        if (hasPaste)
        {
            Rect pasteRect = new(rect.x + toggleW + 2, rect.y, pasteW - 2, 32);
            if (Widgets.ButtonText(pasteRect, "▼"))
            {
                field = pasteGet(PawnKindClipboard.Clipboard.Clone);
                resetBuffers();
            }
            TooltipHandler.TipRegion(pasteRect, "FactionLoadout_PasteFromClipboard".Translate(PawnKindClipboard.Clipboard?.SourceLabel));
        }

        Rect content = new(rect.x + toggleW + pasteW + 2, rect.y, ui.ColumnWidth - (toggleW + pasteW + 4), rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);
        GUI.enabled = active;
        drawContent(content, active, defaultValue);
        GUI.enabled = true;
        ui.Gap();
    }

    // ==================== DrawOverride (class) ====================

    public static void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height,
        Func<PawnKindEdit, T> pasteGet,
        Action resetBuffers
    )
        where T : class
    {
        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = field != null;

        const float toggleW = 120f;
        bool hasPaste = PawnKindClipboard.HasData && pasteGet != null;
        float pasteW = hasPaste ? 28f : 0f;

        string overrideLabel = "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate());
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, toggleW, 32), overrideLabel))
        {
            field = active ? null : defaultValue;
            active = !active;
        }

        if (hasPaste)
        {
            Rect pasteRect = new(rect.x + toggleW + 2, rect.y, pasteW - 2, 32);
            if (Widgets.ButtonText(pasteRect, "▼"))
            {
                field = pasteGet(PawnKindClipboard.Clipboard.Clone);
                resetBuffers();
            }
            TooltipHandler.TipRegion(pasteRect, "FactionLoadout_PasteFromClipboard".Translate(PawnKindClipboard.Clipboard?.SourceLabel));
        }

        Rect content = new(rect.x + toggleW + pasteW + 2, rect.y, ui.ColumnWidth - (toggleW + pasteW + 4), rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);
        GUI.enabled = active;
        drawContent(content, active, defaultValue);
        GUI.enabled = true;
        ui.Gap();
    }

    // ==================== DrawOverride (IList) ====================

    public static void DrawOverride<T>(
        Listing_Standard ui,
        T defaultValue,
        ref T field,
        string label,
        Action<Rect, bool, T> drawContent,
        float height,
        bool cloneDefault,
        Func<PawnKindEdit, T> pasteGet,
        Action resetBuffers
    )
        where T : IList
    {
        ui.Label($"<b>{label}</b>");
        Rect rect = ui.GetRect(height);
        bool active = field != null;

        const float toggleW = 120f;
        bool hasPaste = PawnKindClipboard.HasData && pasteGet != null;
        float pasteW = hasPaste ? 28f : 0f;

        string overrideLabel = "FactionLoadout_OverrideYesNo".Translate(active ? "#81f542" : "#ff4d4d", active ? "Yes".Translate() : "No".Translate());
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, toggleW, 32), overrideLabel))
        {
            if (active)
            {
                field = default;
            }
            else
            {
                field = (T)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T).GenericTypeArguments));
                if (cloneDefault && defaultValue != null)
                {
                    foreach (object value in defaultValue)
                        field.Add(value);
                }
            }

            active = !active;
        }

        if (hasPaste)
        {
            Rect pasteRect = new(rect.x + toggleW + 2, rect.y, pasteW - 2, 32);
            if (Widgets.ButtonText(pasteRect, "▼"))
            {
                field = pasteGet(PawnKindClipboard.Clipboard.Clone);
                resetBuffers();
            }
            TooltipHandler.TipRegion(pasteRect, "FactionLoadout_PasteFromClipboard".Translate(PawnKindClipboard.Clipboard?.SourceLabel));
        }

        Rect content = new(rect.x + toggleW + pasteW + 2, rect.y, ui.ColumnWidth - (toggleW + pasteW + 4), rect.height);
        Widgets.DrawBoxSolidWithOutline(content, Color.black * 0.2f, Color.white * 0.3f);
        content = content.ExpandedBy(-2);
        GUI.enabled = active;
        drawContent(content, active, defaultValue);
        GUI.enabled = true;
        ui.Gap();
    }
}
