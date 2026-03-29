using System;
using FactionLoadout.Util;
using RimWorld;
using Verse;

namespace FactionLoadout;

public class ForcedTrait : IExposable, IDeepCopyable<ForcedTrait>
{
    private Lazy<TraitDef> resolvedTraitDef;

    public string traitDef;
    public int degree = 0;
    public float chance = 1f;

    public TraitDef TraitDef
    {
        get
        {
            resolvedTraitDef ??= new Lazy<TraitDef>(() => DefDatabase<TraitDef>.GetNamedSilentFail(traitDef));
            return resolvedTraitDef.Value;
        }
        set
        {
            traitDef = value.defName;
            resolvedTraitDef = new Lazy<TraitDef>(() => value);
        }
    }

    public ForcedTrait DeepClone() =>
        new()
        {
            traitDef = traitDef,
            degree = degree,
            chance = chance,
        };

    public void ExposeData()
    {
        Scribe_Values.Look(ref traitDef, "traitDef");
        Scribe_Values.Look(ref degree, "degree", 0);
        Scribe_Values.Look(ref chance, "chance", 1f);
    }
}
