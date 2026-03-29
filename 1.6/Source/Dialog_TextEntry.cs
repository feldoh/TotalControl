using System;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public class Dialog_TextEntry : Window
{
    private string message;
    private string input;
    private Action<string> onConfirm;

    public Dialog_TextEntry(string message, Action<string> onConfirm)
    {
        this.message = message;
        this.onConfirm = onConfirm;
        input = string.Empty;
        doCloseX = true;
        closeOnAccept = false;
        closeOnCancel = true;
    }

    public override Vector2 InitialSize
    {
        get
        {
            Vector2 size = Text.CalcSize(message);
            size.x += 100;
            size.y *= 7;
            return size;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
        listingStandard.Label(message);
        input = listingStandard.TextEntry(input);
        if (listingStandard.ButtonText("Accept".Translate()))
        {
            onConfirm?.Invoke(input);
            Close();
        }
        if (listingStandard.ButtonText("Cancel".Translate()))
        {
            Close();
        }
        listingStandard.End();
    }
}
