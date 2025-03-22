using System;
using System.Collections.Generic;
using Verse;

namespace FactionLoadout;

public class ForcedHediff : IExposable
{
    private Lazy<HediffDef> resolvedHediffDef;
    public string hediffDef;
    public List<BodyPartDef> parts;
    public int maxParts = 1;
    public IntRange maxPartsRange = IntRange.one;
    public float chance = 1f;

    public int PartsToHit()
    {
        return maxPartsRange.max > 1 ? maxPartsRange.RandomInRange : maxParts;
    }

    public HediffDef HediffDef
    {
        get
        {
            resolvedHediffDef ??= new Lazy<HediffDef>(() => DefDatabase<HediffDef>.GetNamedSilentFail(hediffDef));
            return resolvedHediffDef.Value;
        }
        set
        {
            hediffDef = value.defName;
            resolvedHediffDef = new Lazy<HediffDef>(() => value);
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref hediffDef, "hediffDef");
        Scribe_Collections.Look(ref parts, "parts", LookMode.Def);
        Scribe_Values.Look(ref maxParts, "maxParts", 1);
        Scribe_Values.Look(ref maxPartsRange, "maxPartsRange", IntRange.one);
        Scribe_Values.Look(ref chance, "chance", 1f);
    }
}
