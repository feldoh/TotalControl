using System;
using Verse;

namespace FactionLoadout;

public class ForcedGene : IExposable
{
    private Lazy<GeneDef> resolvedGeneDef;
    public string geneDef;
    public float chance = 1f;
    public bool xenogene = false;
    public bool forceActive = false;

    public GeneDef GeneDef
    {
        get
        {
            resolvedGeneDef ??= new Lazy<GeneDef>(() => DefDatabase<GeneDef>.GetNamedSilentFail(geneDef));
            return resolvedGeneDef.Value;
        }
        set
        {
            geneDef = value.defName;
            resolvedGeneDef = new Lazy<GeneDef>(() => value);
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref geneDef, "geneDef");
        Scribe_Values.Look(ref chance, "chance", 1f);
        Scribe_Values.Look(ref xenogene, "xenogene", false);
        Scribe_Values.Look(ref forceActive, "forceActive", false);
    }
}
