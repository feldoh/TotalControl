using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>Shared float-menu builder and display helper for forced-ideology references.</summary>
public static class ForcedIdeoRefUI
{
    /// <summary>Sentinel key stored when the source is <see cref="ForcedIdeoSource.FactionPrimary"/> (the key itself is unused).</summary>
    public const string FactionPrimaryKey = "primary";

    /// <summary>
    /// Builds the picker options: optionally the faction-primary option, then ideology presets
    /// (base game + mods, with description tooltips), then user-saved .rid files.
    /// </summary>
    public static List<FloatMenuOption> BuildOptions(bool includeFactionPrimary, Action<ForcedIdeoSource, string> onPick)
    {
        List<FloatMenuOption> options = [];

        if (includeFactionPrimary)
        {
            options.Add(
                new FloatMenuOption(
                    "FactionLoadout_General_IdeoFactionPrimary".Translate(),
                    () => onPick(ForcedIdeoSource.FactionPrimary, FactionPrimaryKey)
                )
            );
        }

        foreach (IdeoPresetDef preset in DefDatabase<IdeoPresetDef>.AllDefsListForReading)
        {
            IdeoPresetDef localPreset = preset;
            string label = localPreset.LabelCap + " " + "FactionLoadout_General_IdeoPresetSuffix".Translate();
            FloatMenuOption option = new(label, () => onPick(ForcedIdeoSource.Preset, localPreset.defName));
            if (!localPreset.description.NullOrEmpty())
                option.tooltip = localPreset.description;
            options.Add(option);
        }

        foreach (FileInfo file in GenFilePaths.AllCustomIdeoFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            options.Add(new FloatMenuOption(fileName, () => onPick(ForcedIdeoSource.SavedFile, fileName)));
        }

        return options;
    }

    /// <summary>Human-readable label for a stored reference.</summary>
    public static string DisplayName(ForcedIdeoSource source, string key)
    {
        if (string.IsNullOrEmpty(key))
            return "FactionLoadout_General_IdeoNoneSelected".Translate().ToString();

        switch (source)
        {
            case ForcedIdeoSource.FactionPrimary:
                return "FactionLoadout_General_IdeoFactionPrimary".Translate().ToString();
            case ForcedIdeoSource.Preset:
                IdeoPresetDef preset = DefDatabase<IdeoPresetDef>.GetNamedSilentFail(key);
                string label = preset != null ? preset.LabelCap.ToString() : key;
                return label + " " + "FactionLoadout_General_IdeoPresetSuffix".Translate();
            default:
                return key;
        }
    }
}
