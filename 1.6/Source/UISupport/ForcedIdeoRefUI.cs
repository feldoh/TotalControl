using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>Shared float-menu builder and display helper for forced-ideology references.</summary>
public static class ForcedIdeoRefUI
{
    /// <summary>Sentinel key stored when the source is <see cref="ForcedIdeoSource.FactionPrimary"/> (the key itself is unused).</summary>
    public const string FactionPrimaryKey = "primary";

    /// <summary>Row width for picker entries — preset labels plus suffixes run long.</summary>
    public const float PickerItemWidth = 270f;

    /// <summary>
    /// Opens ideology picker with def-based ones, with their icons, then user-saved .rid files.
    /// </summary>
    public static void OpenPicker(bool includeFactionPrimary, Action<ForcedIdeoSource, string> onPick, Action onClear = null, string clearLabel = null)
    {
        List<MenuItemBase> items = [];

        if (onClear != null)
        {
            items.Add(MakeItem(null, clearLabel ?? "FactionLoadout_None".Translate().ToString(), null, null));
        }

        if (includeFactionPrimary)
        {
            items.Add(
                MakeItem(
                    (ForcedIdeoSource.FactionPrimary, FactionPrimaryKey),
                    "FactionLoadout_General_IdeoFactionPrimary".Translate().ToString(),
                    "FactionLoadout_General_IdeoFactionPrimaryPickTooltip".Translate().ToString(),
                    null
                )
            );
        }

        List<MenuItemBase> presets = [];
        foreach (IdeoPresetDef preset in DefDatabase<IdeoPresetDef>.AllDefsListForReading)
        {
            string label = BuildPresetLabel(preset.LabelCap.ToString());
            string tip = "FactionLoadout_General_IdeoPresetPickTooltip".Translate().ToString();
            if (!preset.description.NullOrEmpty())
                tip += "\n\n" + preset.description;
            presets.Add(MakeItem((ForcedIdeoSource.Preset, preset.defName), label, tip, preset.Icon));
        }
        presets.Sort();
        items.AddRange(presets);

        List<MenuItemBase> files = [];
        foreach (FileInfo file in GenFilePaths.AllCustomIdeoFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            string label = BuildSavedFileLabel(fileName, isMissing: false);
            files.Add(MakeItem((ForcedIdeoSource.SavedFile, fileName), label, "FactionLoadout_General_IdeoSavedPickTooltip".Translate().ToString(), null));
        }
        files.Sort();
        items.AddRange(files);

        if (items.Count == 0)
        {
            Messages.Message("FactionLoadout_General_IdeoNoTemplates".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        CustomFloatMenu.Open(
            items,
            item =>
            {
                if (item.Payload == null)
                {
                    onClear?.Invoke();
                    return;
                }
                (ForcedIdeoSource source, string key) = ((ForcedIdeoSource, string))item.Payload;
                onPick(source, key);
                ForcedIdeoGameComponent.AnyIdeologyEditsActive = true;
            },
            columns: 1,
            stretchItems: true
        );
    }

    public static MenuItemText MakeItem(object payload, string label, string tooltip, Texture2D icon) =>
        new(payload, label, icon, tooltip: tooltip) { Size = new Vector2(PickerItemWidth, 28f) };

    /// <summary>
    /// Label for a stored reference. SavedFile references whose .rid is absent get a red warning.
    /// </summary>
    public static string DisplayName(ForcedIdeoSource source, string key)
    {
        if (string.IsNullOrEmpty(key))
            return "FactionLoadout_None".Translate().ToString();

        switch (source)
        {
            case ForcedIdeoSource.FactionPrimary:
                return "FactionLoadout_General_IdeoFactionPrimary".Translate().ToString();
            case ForcedIdeoSource.Preset:
                IdeoPresetDef preset = DefDatabase<IdeoPresetDef>.GetNamedSilentFail(key);
                string label = preset != null ? preset.LabelCap.ToString() : key;
                return BuildPresetLabel(label);
            case ForcedIdeoSource.SavedFile:
            default:
                return BuildSavedFileLabel(key, isMissing: !File.Exists(GenFilePaths.AbsPathForIdeo(key)));
        }
    }

    private static string BuildPresetLabel(string presetName) =>
        "FactionLoadout_General_IdeoPresetLabel".Translate(presetName).ToString();

    private static string BuildSavedFileLabel(string fileName, bool isMissing)
    {
        if (!isMissing)
            return "FactionLoadout_General_IdeoSavedLabel".Translate(fileName).ToString();

        string missingMarker = "FactionLoadout_General_IdeoFileMissing".Translate().ToString();
        return "FactionLoadout_General_IdeoSavedLabelMissing".Translate(fileName, missingMarker).ToString();
    }

    /// <summary>Whether ideology overrides are unavailable in the current game (classic mode).</summary>
    public static bool DisabledByClassicMode => Current.Game != null && ForcedIdeoGameComponent.ClassicMode;
}
