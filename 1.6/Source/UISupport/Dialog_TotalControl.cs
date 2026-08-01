using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>
/// The single fullscreen, paused workspace that replaces the old window cascade
/// (Dialog_FactionLoadout -> PresetUI -> FactionEditUI -> PawnKindEditUI). All
/// navigation and screen drawing is delegated to <see cref="TotalControlController"/>.
/// </summary>
[HotSwappable]
public class Dialog_TotalControl : Window
{
    public readonly TotalControlController Controller;

    public Dialog_TotalControl(Preset preset = null)
    {
        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnAccept = false; // Enter is used by text fields; must not close the window.
        closeOnCancel = true; // Esc: handled by OnCancelKeyPressed (step back), closes from Home.
        closeOnClickedOutside = false;
        draggable = false;
        resizeable = false;
        preventCameraMotion = true;

        Controller = new TotalControlController(this, preset);
    }

    public override Vector2 InitialSize => new(UI.screenWidth * 0.96f, UI.screenHeight * 0.96f);

    public override float Margin => 12f;

    public override void PostOpen()
    {
        base.PostOpen();
        float w = UI.screenWidth * 0.96f;
        float h = UI.screenHeight * 0.96f;
        windowRect = new Rect((UI.screenWidth - w) / 2f, (UI.screenHeight - h) / 2f, w, h);
    }

    public override void DoWindowContents(Rect inRect)
    {
        Controller.Draw(inRect);
    }

    public override void OnCancelKeyPressed()
    {
        // Esc steps back one screen; only closes the window once at Home.
        if (Controller.HandleEscape())
        {
            Event.current.Use();
            return;
        }

        base.OnCancelKeyPressed();
    }

    public override void PostClose()
    {
        base.PostClose();
        Controller.Dispose();
        ModCore.Settings.Write();
    }
}
