using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using FactionLoadout.Util;
using Verse;

namespace FactionLoadout;

public class ForcedHediff : IExposable, IDeepCopyable<ForcedHediff>
{
    private Lazy<HediffDef> resolvedHediffDef;
    public string hediffDef;
    public List<DefRef<BodyPartDef>> parts;
    public int maxParts = 1;
    public IntRange maxPartsRange = IntRange.One;
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

    public ForcedHediff DeepClone() =>
        new ForcedHediff
        {
            hediffDef = hediffDef,
            parts = parts == null ? null : new List<DefRef<BodyPartDef>>(parts),
            maxParts = maxParts,
            maxPartsRange = maxPartsRange,
            chance = chance,
        };

    public void ExposeData()
    {
        Scribe_Values.Look(ref hediffDef, "hediffDef");
        if (
            Scribe.mode == LoadSaveMode.LoadingVars
            && Scribe.loader.curXmlParent?["parts"] is { } partsNode
            && partsNode.HasChildNodes
            && partsNode.SelectSingleNode("li/defName") == null
        )
        {
            List<BodyPartDef> old = null;
            Scribe_Collections.Look(ref old, "parts", LookMode.Def);
            parts = old?.Where(d => d != null).Select(d => new DefRef<BodyPartDef>(d)).ToList();
        }
        else
        {
            Scribe_Collections.Look(ref parts, "parts", LookMode.Deep);
        }
        Scribe_Values.Look(ref maxParts, "maxParts", 1);
        Scribe_Values.Look(ref maxPartsRange, "maxPartsRange", IntRange.One);
        Scribe_Values.Look(ref chance, "chance", 1f);
    }
}
