using System;
using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Draws the clipboard Copy / Paste-All toolbar strip shown above the active tab.
/// </summary>
public static class ClipboardToolbar
{
    public static void Draw(Rect toolbar, PawnKindEdit current, Action resetActiveTabBuffers)
    {
        float x = toolbar.x;
        float y = toolbar.y;
        const float btnW = 80f;
        const float btnH = 26f;
        const float gap = 4f;

        // Copy
        Rect copyBtn = new(x, y, btnW, btnH);
        if (Widgets.ButtonText(copyBtn, "FactionLoadout_Clipboard_Copy".Translate()))
            PawnKindClipboard.Copy(current);
        TooltipHandler.TipRegion(copyBtn, "FactionLoadout_Clipboard_CopyTooltip".Translate());
        x += btnW + gap;

        // Paste All
        bool hasData = PawnKindClipboard.HasData;
        GUI.enabled = hasData;
        Rect pasteBtn = new(x, y, btnW, btnH);
        if (Widgets.ButtonText(pasteBtn, "FactionLoadout_Clipboard_PasteAll".Translate()) && hasData)
        {
            PawnKindClipboard.PasteAll(current);
            resetActiveTabBuffers();
        }
        if (hasData)
            TooltipHandler.TipRegion(pasteBtn, "FactionLoadout_Clipboard_PasteAllTooltip".Translate(PawnKindClipboard.GetDescription()));
        GUI.enabled = true;
    }
}
