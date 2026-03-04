using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Extends RimWorld's <see cref="BackstoryCategoryFilter"/> with <see cref="IExposable"/> support
/// so it can be serialized in presets. Since it inherits directly, instances can be used anywhere
/// a <see cref="BackstoryCategoryFilter"/> is expected — no conversion needed.
/// </summary>
public class BackstoryFilter : BackstoryCategoryFilter, IExposable
{
    public BackstoryFilter() { }

    public BackstoryFilter(BackstoryCategoryFilter source)
    {
        if (source == null)
            return;
        categories = source.categories?.ListFullCopy();
        exclude = source.exclude?.ListFullCopy();
        categoriesChildhood = source.categoriesChildhood?.ListFullCopy();
        excludeChildhood = source.excludeChildhood?.ListFullCopy();
        categoriesAdulthood = source.categoriesAdulthood?.ListFullCopy();
        excludeAdulthood = source.excludeAdulthood?.ListFullCopy();
        commonality = source.commonality;
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref categories, "categories");
        Scribe_Collections.Look(ref exclude, "exclude");
        Scribe_Collections.Look(ref categoriesChildhood, "categoriesChildhood");
        Scribe_Collections.Look(ref excludeChildhood, "excludeChildhood");
        Scribe_Collections.Look(ref categoriesAdulthood, "categoriesAdulthood");
        Scribe_Collections.Look(ref excludeAdulthood, "excludeAdulthood");
        Scribe_Values.Look(ref commonality, "commonality", 1f);
    }

    /// <summary>
    /// Human-readable summary for the UI, showing the primary categories.
    /// </summary>
    public string Summary
    {
        get
        {
            List<string> parts = [];
            if (!categories.NullOrEmpty())
            {
                parts.Add(string.Join(", ", categories));
            }
            if (!categoriesChildhood.NullOrEmpty())
            {
                parts.Add("FactionLoadout_Backstory_ChildPrefix".Translate() + string.Join(", ", categoriesChildhood));
            }
            if (!categoriesAdulthood.NullOrEmpty())
            {
                parts.Add("FactionLoadout_Backstory_AdultPrefix".Translate() + string.Join(", ", categoriesAdulthood));
            }
            if (parts.Count == 0)
            {
                return $"<i>{"FactionLoadout_Backstory_EmptyFilter".Translate()}</i>";
            }
            string result = string.Join(" | ", parts);
            if (!Mathf.Approximately(commonality, 1f))
            {
                result += $" (x{commonality:F1})";
            }
            return result;
        }
    }
}
