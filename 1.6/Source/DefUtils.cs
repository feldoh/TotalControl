using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

public static class DefUtils
{
    public static Texture2D TryGetIcon(Def def, out Color color)
    {
        while (true)
        {
            color = Color.white;

            if (def == null) return null;

            switch (def)
            {
                case PawnKindDef kd:
                    def = kd.race;
                    continue;
                case ThingDef td:
                    if (td.defName.StartsWith("Corpse_"))
                    {
                        def = DefDatabase<ThingDef>.GetNamed(td.defName.Substring(7), false);
                        continue;
                    }
                    ThingDef stuff = GenStuff.DefaultStuffFor(td);
                    color = stuff == null ? td.uiIconColor : td.GetColorForStuff(stuff);
                    return Widgets.GetIconFor(td, stuff);
                case FactionDef fd:
                    if (!fd.colorSpectrum.NullOrEmpty()) color = fd.colorSpectrum.FirstOrDefault();
                    return fd.FactionIcon;
                default:
                    return null;
            }
        }
    }

    public static string BuildApparelTooltip(ThingDef def)
    {
        if (def?.apparel == null)
            return def?.description;

        StringBuilder parts = new();

        if (def.apparel.layers?.Count > 0)
        {
            string layers = string.Join(", ", def.apparel.layers.Select(l => !string.IsNullOrEmpty(l.LabelCap) ? l.LabelCap.ToString() : l.defName));
            parts.AppendLine("FactionLoadout_Apparel_Layers".Translate(layers).ToString());
        }

        if (def.apparel.bodyPartGroups?.Count > 0)
        {
            string coverage = string.Join(", ", def.apparel.bodyPartGroups.Select(b => !string.IsNullOrEmpty(b.LabelCap) ? b.LabelCap.ToString() : b.defName));
            parts.AppendLine("FactionLoadout_Apparel_Coverage".Translate(coverage).ToString());
        }

        if (!string.IsNullOrEmpty(def.description))
        {
            if (parts.Length > 0)
                parts.AppendLine();
            parts.Append(def.description);
        }

        return parts.Length > 0 ? parts.ToString().TrimEnd() : null;
    }
}
