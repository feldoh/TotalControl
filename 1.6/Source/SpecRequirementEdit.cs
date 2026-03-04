using FactionLoadout.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public class SpecRequirementEdit : IExposable, IDeepCopyable<SpecRequirementEdit>
    {
        public ThingDef Thing;
        public ThingDef Material;
        public ThingStyleDef Style;
        public QualityCategory? Quality;
        public bool Biocode;
        public Color Color;
        public ApparelSelectionMode SelectionMode = ApparelSelectionMode.AlwaysTake;
        public float SelectionChance = 1f;

        public SpecRequirementEdit DeepClone() =>
            new()
            {
                Thing = Thing,
                Material = Material,
                Style = Style,
                Quality = Quality,
                Biocode = Biocode,
                Color = Color,
                SelectionMode = SelectionMode,
                SelectionChance = SelectionChance,
            };

        public void ExposeData()
        {
            Scribe_Defs.Look(ref Thing, "thing");
            Scribe_Defs.Look(ref Material, "material");
            Scribe_Defs.Look(ref Style, "style");
            Scribe_Values.Look(ref Quality, "quality");
            Scribe_Values.Look(ref Biocode, "biocode");
            Scribe_Values.Look(ref Color, "color");
            Scribe_Values.Look(ref SelectionMode, "selectionMode", ApparelSelectionMode.AlwaysTake);
            Scribe_Values.Look(ref SelectionChance, "selectionChance");
        }
    }

    public enum ApparelSelectionMode
    {
        AlwaysTake,
        RandomChance,
        FromPool1,
        FromPool2,
        FromPool3,
        FromPool4,
    }
}
