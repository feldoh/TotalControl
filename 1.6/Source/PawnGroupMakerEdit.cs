using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Defensive full-replacement for one <see cref="PawnGroupMaker"/>.
/// When <see cref="FactionEdit.PawnGroupMakerEdits"/> is non-null, its entries replace
/// the faction def's <c>pawnGroupMakers</c> list entirely on Apply().
/// All def references are stored as raw strings so preset data survives mod removal without data loss
/// </summary>
public class PawnGroupMakerEdit : IExposable
{
    /// <summary>
    /// True when this group was created by the user rather than loaded from the original
    /// faction def. Purely informational — the full-replacement apply logic does not use
    /// it for matching.
    /// </summary>
    public bool IsUserAdded;

    /// <summary>defName of the <see cref="PawnGroupKindDef"/> for this group.</summary>
    public string KindDefName = "";

    public float Commonality = 100f;
    public float MaxTotalPoints = 9999999f;

    /// <summary>defNames of disallowed <see cref="RaidStrategyDef"/>s.</summary>
    public List<string> DisallowedStrategyDefNames = null;

    public List<PawnGenOptionEdit> Options = [];
    public List<PawnGenOptionEdit> Traders = [];
    public List<PawnGenOptionEdit> Carriers = [];
    public List<PawnGenOptionEdit> Guards = [];

    public bool IsNew => IsUserAdded;

    public PawnGroupKindDef KindDef => DefDatabase<PawnGroupKindDef>.GetNamedSilentFail(KindDefName);

    public int TotalKindCount => Options.Count + Traders.Count + Carriers.Count + Guards.Count;

    /// <summary>
    /// Returns all PawnKindDefs referenced across every role list.
    /// Note that the same PawnKindDef may be returned multiple times if it appears in multiple roles.
    /// </summary>
    public IEnumerable<PawnKindDef> GetAllKinds()
    {
        foreach (PawnGenOptionEdit o in Options)
        {
            PawnKindDef d = o.KindDef;
            if (d != null)
                yield return d;
        }

        foreach (PawnGenOptionEdit o in Traders)
        {
            PawnKindDef d = o.KindDef;
            if (d != null)
                yield return d;
        }

        foreach (PawnGenOptionEdit o in Carriers)
        {
            PawnKindDef d = o.KindDef;
            if (d != null)
                yield return d;
        }

        foreach (PawnGenOptionEdit o in Guards)
        {
            PawnKindDef d = o.KindDef;
            if (d != null)
                yield return d;
        }
    }

    public static PawnGroupMakerEdit FromPawnGroupMaker(PawnGroupMaker maker)
    {
        static List<PawnGenOptionEdit> Convert(List<PawnGenOption> list) => list == null ? [] : list.Select(PawnGenOptionEdit.FromOption).ToList();

        return new PawnGroupMakerEdit
        {
            IsUserAdded = false,
            KindDefName = maker.kindDef?.defName ?? "",
            Commonality = maker.commonality,
            MaxTotalPoints = maker.maxTotalPoints,
            DisallowedStrategyDefNames = maker.disallowedStrategies?.Select(s => s.defName).ToList(),
            Options = Convert(maker.options),
            Traders = Convert(maker.traders),
            Carriers = Convert(maker.carriers),
            Guards = Convert(maker.guards),
        };
    }

    public PawnGroupMaker ToPawnGroupMaker()
    {
        static List<PawnGenOption> Convert(List<PawnGenOptionEdit> list) => list?.Select(e => e.ToPawnGenOption()).Where(opt => opt.kind != null).ToList() ?? [];

        List<RaidStrategyDef> strategies = null;
        if (DisallowedStrategyDefNames is { Count: > 0 })
            strategies = DisallowedStrategyDefNames.Select(DefDatabase<RaidStrategyDef>.GetNamedSilentFail).Where(d => d != null).ToList();

        return new PawnGroupMaker
        {
            kindDef = KindDef,
            commonality = Commonality,
            maxTotalPoints = MaxTotalPoints,
            disallowedStrategies = strategies,
            options = Convert(Options),
            traders = Convert(Traders),
            carriers = Convert(Carriers),
            guards = Convert(Guards),
        };
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref IsUserAdded, "isUserAdded", false);
        Scribe_Values.Look(ref KindDefName, "kindDef", "");
        Scribe_Values.Look(ref Commonality, "commonality", 100f);
        Scribe_Values.Look(ref MaxTotalPoints, "maxTotalPoints", 9999999f);
        Scribe_Collections.Look(ref DisallowedStrategyDefNames, "disallowedStrategies", LookMode.Value);
        Scribe_Collections.Look(ref Options, "options", LookMode.Deep);
        Scribe_Collections.Look(ref Traders, "traders", LookMode.Deep);
        Scribe_Collections.Look(ref Carriers, "carriers", LookMode.Deep);
        Scribe_Collections.Look(ref Guards, "guards", LookMode.Deep);
    }
}
