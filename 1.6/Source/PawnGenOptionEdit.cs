using RimWorld;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Defensive replacement for <see cref="PawnGenOption"/>. Stores the kind
/// defName as a raw string so that preset data survives mod removal without
/// data loss (the DefRef pattern).
/// </summary>
public class PawnGenOptionEdit : IExposable
{
    public string KindDefName = "";
    public float SelectionWeight = 1f;

    public PawnKindDef KindDef => DefDatabase<PawnKindDef>.GetNamedSilentFail(KindDefName);

    public static PawnGenOptionEdit FromOption(PawnGenOption opt)
    {
        return new PawnGenOptionEdit { KindDefName = opt.kind?.defName ?? "", SelectionWeight = opt.selectionWeight };
    }

    public PawnGenOption ToPawnGenOption()
    {
        return new PawnGenOption { kind = KindDef, selectionWeight = SelectionWeight };
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref KindDefName, "kind", "");
        Scribe_Values.Look(ref SelectionWeight, "weight", 1f);
    }
}
