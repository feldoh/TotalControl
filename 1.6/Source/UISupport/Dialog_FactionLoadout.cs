using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

public class Dialog_FactionLoadout : Window
{
    public override Vector2 InitialSize => new Vector2(800f, 600f);
    public Vector2 scrollPosition = Vector2.zero;

    // Height the scroll content used on the previous frame. Seeded large so the very first
    // frame's inner rect is never shorter than the real content: a short inner rect makes
    // Listing_Standard wrap into a second column (resetting curY), which corrupts the
    // measurement. After one frame this settles to the exact measured height.
    public float lastContentHeight = 100000f;

    public Dialog_FactionLoadout()
    {
        doCloseButton = true;
        closeOnAccept = true;
        closeOnCancel = true;
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Everything except the create button lives in one scroll view; the button is pinned
        // just below it so it can never be scrolled out of reach or clipped by content growth
        // above it (the bug this replaced). The scroll view's inner height is the height the
        // content measured to last frame, so it is always correct without hardcoding row math.
        //
        // DoWindowContents is called two ways with different rect contracts:
        //   * standalone window (main-menu link) — we own the frame, so the base Window draws
        //     its Close button overlapping the bottom of inRect; reserve space for it.
        //   * inline from Dialog_ModSettings — that dialog already carved out its own Close
        //     button before handing us inRect, so no reservation is needed.
        const float buttonHeight = 35f;
        const float gap = 8f;
        bool standalone = Find.WindowStack.currentlyDrawnWindow == this;
        float bottomReserve = standalone ? Window.CloseButSize.y + 10f : 0f;

        float contentBottom = inRect.yMax - bottomReserve;
        Rect createRect = new Rect(inRect.x, contentBottom - buttonHeight, inRect.width, buttonHeight);
        Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, createRect.y - gap - inRect.y);

        Rect scrollInner = new Rect(0, 0, inRect.width - 20f, Mathf.Max(lastContentHeight, scrollOuter.height));
        scrollPosition = GUI.BeginScrollView(scrollOuter, scrollPosition, scrollInner);
        Listing_Standard ui = new Listing_Standard();
        try
        {
            ui.Begin(scrollInner);

            ui.Label("FactionLoadout_Settings_FactionPresetDesc".Translate());
            ui.GapLine();
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_VanillaRestrictions".Translate(),
                ref MySettings.VanillaRestrictions,
                "FactionLoadout_Settings_VanillaRestrictionsDesc".Translate()
            );
            ui.GapLine();
            ui.CheckboxLabeled("FactionLoadout_Settings_Verbose".Translate(), ref MySettings.VerboseLogging, "FactionLoadout_Settings_VerboseDesc".Translate());
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_PatchKindInRequests".Translate(),
                ref MySettings.PatchKindInRequests,
                "FactionLoadout_Settings_PatchKindInRequestsDesc".Translate()
            );
            ui.CheckboxLabeled("FactionLoadout_Settings_IgnorePrice".Translate(), ref MySettings.IgnorePriceLimits, "FactionLoadout_Settings_IgnorePriceDesc".Translate());
            ui.CheckboxLabeled(
                "FactionLoadout_Settings_OverrideForcedIdeos".Translate(),
                ref MySettings.OverrideForcedIdeos,
                "FactionLoadout_Settings_OverrideForcedIdeosDesc".Translate()
            );
            ui.GapLine();
            ui.Label("FactionLoadout_Settings_FactionPresetDesc".Translate());
            ui.GapLine();

            bool deleteMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Preset toDelete = null;

            foreach (Preset preset in Preset.LoadedPresets)
            {
                Rect area = ui.GetRect(30);
                area.width = 80;

                bool active = MySettings.ActivePreset == preset.GUID;

                GUI.color = active ? Color.green : Color.red;
                bool currentActive = active;
                Widgets.CheckboxLabeled(area, "FactionLoadout_Active".Translate().CapitalizeFirst(), ref active, placeCheckboxNearText: true);
                if (currentActive != active)
                {
                    MySettings.ActivePreset = active ? preset.GUID : null;
                    ModCore.Settings.Write();
                }

                GUI.color = Color.white;
                area.x += 90;

                if (preset.IsPackaged)
                {
                    GUI.color = new Color(1f, 0.75f, 0.2f);
                    if (Widgets.ButtonText(area, "FactionLoadout_PackagedLabel".Translate().CapitalizeFirst()))
                    {
                        Preset capturedPreset = preset;
                        List<FloatMenuOption> options =
                        [
                            new FloatMenuOption(
                                "FactionLoadout_CopyToMyPresets".Translate(),
                                () =>
                                {
                                    try
                                    {
                                        Preset copy = Preset.CreateCopy(capturedPreset);
                                        Preset.AddNewPreset(copy);
                                        copy.Save();
                                        PresetUI.OpenEditor(copy);
                                        Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                                        Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
                                    }
                                    catch (Exception ex)
                                    {
                                        ModCore.Error("Failed to copy packaged preset.", ex);
                                    }
                                }
                            ),
                            new FloatMenuOption(
                                "FactionLoadout_EditSourceFile".Translate(),
                                () =>
                                {
                                    PresetUI.OpenEditor(capturedPreset);
                                    Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                                    Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
                                }
                            ),
                        ];
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    GUI.color = Color.white;

                    area.x += 90;
                    area.width = 9999;
                    Widgets.Label(area, $"{preset.Name} <color=#888888><i>({preset.PackagedModName})</i></color>");
                }
                else
                {
                    GUI.color = deleteMode ? Color.red : Color.white;
                    if (Widgets.ButtonText(area, deleteMode ? "Delete".Translate().CapitalizeFirst() : "FactionLoadout_Edit".Translate().CapitalizeFirst()))
                    {
                        if (!deleteMode)
                        {
                            PresetUI.OpenEditor(preset);
                            Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
                            Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
                        }
                        else
                        {
                            toDelete = preset;
                        }
                    }

                    GUI.color = Color.white;

                    area.x += 90;
                    area.width = 9999;
                    Widgets.Label(area, preset.Name);
                }
            }

            if (toDelete != null)
                Preset.DeletePreset(toDelete);

            if (Preset.LoadedPresets.EnumerableNullOrEmpty())
                ui.Label("FactionLoadout_NothingHere".Translate());

            // Record how tall the content actually was so next frame's inner rect matches it.
            lastContentHeight = ui.CurHeight;
        }
        finally
        {
            ui.End();
            GUI.EndScrollView();
        }

        // Pinned create button, always visible below the scroll view.
        if (Widgets.ButtonText(createRect, "FactionLoadout_CreateNewPreset".Translate()))
        {
            Preset preset = new();
            Preset.AddNewPreset(preset);
            preset.Save();

            MySettings.ActivePreset = preset.GUID;

            PresetUI.OpenEditor(preset);

            Find.WindowStack.WindowOfType<Dialog_ModSettings>()?.Close();
            Find.WindowStack.WindowOfType<Dialog_Options>()?.Close();
        }
    }

    public override void PostClose()
    {
        base.PostClose();
        Find.WindowStack.WindowOfType<PresetUI>()?.Close();
        ModCore.Settings.Write();
    }
}
