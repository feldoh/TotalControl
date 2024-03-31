using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionLoadout;

[HotSwappable]
public class FactionEdit : IExposable
{
    private static readonly Dictionary<string, FactionDef> originalFactionDefs = new();
    public bool Active = true;
    public ThingFilter ApparelStuffFilter;
    public bool DeletedOrClosed;

    public DefRef<FactionDef> Faction = new();
    public List<PawnKindEdit> KindEdits = new();
    public Dictionary<XenotypeDef, float> xenotypeChances = new();

    public IEnumerable<PawnGroupMaker> GroupMakers
    {
        get
        {
            if (!Faction.HasValue || Faction.Def.pawnGroupMakers == null) yield break;

            foreach (PawnGroupMaker maker in Faction.Def.pawnGroupMakers) yield return maker;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Active, "active", true);
        Scribe_Deep.Look(ref ApparelStuffFilter, "apparelStuffFilter");
        Scribe_Deep.Look(ref Faction, "faction");
        Scribe_Collections.Look(ref KindEdits, "kindEdits", LookMode.Deep);
        Scribe_Collections.Look(ref xenotypeChances, "xenotypeChances", LookMode.Def, LookMode.Value);
    }

    public static void TweakAllPawnKinds(FactionDef def, Func<PawnKindDef, PawnKindDef> func)
    {
        if (def == null || func == null)
            return;

        if (def.pawnGroupMakers != null)
            foreach (PawnGroupMaker group in def.pawnGroupMakers)
            {
                WorkOn(group.options);
                WorkOn(group.traders);
                WorkOn(group.carriers);
                WorkOn(group.guards);
            }

        if (def.basicMemberKind != null) def.basicMemberKind = func(def.basicMemberKind);

        if (def.fixedLeaderKinds == null) return;
        {
            for (var i = 0; i < def.fixedLeaderKinds.Count; i++)
            {
                PawnKindDef replacement = func(def.fixedLeaderKinds[i]);
                def.fixedLeaderKinds[i] = replacement;
                if (replacement != null) continue;
                def.fixedLeaderKinds.RemoveAt(i);
                i--;
            }
        }

        return;

        void WorkOn(IList<PawnGenOption> group)
        {
            if (group == null)
                return;

            foreach (PawnGenOption t in group)
            {
                PawnKindDef replace = func(t.kind);
                if (replace != null)
                {
                    t.kind = replace;
                }
            }
        }
    }

    public static IReadOnlyList<PawnKindDef> GetAllPawnKinds(FactionDef def)
    {
        HashSet<PawnKindDef> kinds = (def.pawnGroupMakers ?? Enumerable.Empty<PawnGroupMaker>())
            .SelectMany(group => Enumerable.Empty<PawnGenOption>()
                .ConcatIfNotNull(group.options)
                .ConcatIfNotNull(group.traders)
                .ConcatIfNotNull(group.carriers)
                .ConcatIfNotNull(group.guards))
            .Select(pgo => pgo.kind)
            .ToHashSet();

        if (def.basicMemberKind != null)
            kinds.Add(def.basicMemberKind);
        if (def.fixedLeaderKinds != null)
            kinds.AddRange(def.fixedLeaderKinds);

        return kinds.ToArray();
    }

    public static FactionDef TryGetOriginal(string factionDefName)
    {
        if (factionDefName == null)
            return null;

        return originalFactionDefs.TryGetValue(factionDefName, out FactionDef found) ? found : null;
    }

    private static FactionDef EnsureOriginal(FactionDef def)
    {
        if (def == null || originalFactionDefs.ContainsKey(def.defName))
            return def;

        FactionDef copy = CloningUtility.Clone(def);
        originalFactionDefs.Add(def.defName, copy);
        return def;
    }

    public bool HasEditFor(PawnKindDef def)
    {
        return GetEditFor(def) != null;
    }

    public PawnKindEdit GetEditFor(PawnKindDef def)
    {
        return def == null
            ? null
            : Enumerable.FirstOrDefault(KindEdits, edit => edit.AppliesTo(def));
    }

    public bool HasGlobalEditor()
    {
        return GetGlobalEditor() != null;
    }

    public PawnKindEdit GetGlobalEditor()
    {
        return Enumerable.FirstOrDefault(KindEdits, edit => edit.IsGlobal);
    }

    public void Apply(FactionDef def)
    {
        if (!Active)
            ModCore.Warn($"Applying faction edit to {def.label}, but this edit is not active!");

        // DISABLED FOR NOW.
        //if (ApparelStuffFilter != null)
        //    def.apparelStuffFilter = ApparelStuffFilter;

        def = EnsureOriginal(def);
        PawnKindEdit global = GetGlobalEditor();
        IReadOnlyList<PawnKindDef> kinds = GetAllPawnKinds(def);

        foreach (PawnKindDef kind in kinds)
        {
            PawnKindEdit editor = GetEditFor(kind);
            PawnKindDef safeKind = global != null || editor != null ? CloningUtility.Clone(kind) : kind;
            global?.Apply(safeKind, null);
            if (editor?.Apply(safeKind, global) is { } newKind && newKind != safeKind) safeKind = newKind;

            if (ModsConfig.BiotechActive && (xenotypeChances?.Count ?? 0) >= 1 && (!editor?.ForceSpecificXenos ?? false) && safeKind.RaceProps.Humanlike)
            {
                safeKind.xenotypeSet ??= new XenotypeSet();
                safeKind.xenotypeSet.xenotypeChances ??= [];
                safeKind.xenotypeSet.xenotypeChances.Clear();
                foreach (KeyValuePair<XenotypeDef, float> rate in xenotypeChances ?? []) safeKind.xenotypeSet.xenotypeChances.Add(new XenotypeChance(rate.Key, rate.Value));
            }

            if (kind != safeKind) ReplaceKind(def, kind, safeKind);
        }

        if (!ModsConfig.BiotechActive || xenotypeChances == null || xenotypeChances.Count < 1) return;
        def.xenotypeSet ??= new XenotypeSet();
        def.xenotypeSet?.xenotypeChances?.Clear();
        foreach (KeyValuePair<XenotypeDef, float> rate in xenotypeChances) def.xenotypeSet?.xenotypeChances?.Add(new XenotypeChance(rate.Key, rate.Value));
    }

    private void ReplaceKind(FactionDef faction, PawnKindDef original, PawnKindDef replacement)
    {
        if (MySettings.VerboseLogging)
            ModCore.Log($"Replacing PawnKind '{original?.defName ?? "<null>"}' with '{replacement?.defName ?? "<null>"}' in faction {faction.defName}");
        TweakAllPawnKinds(faction, current => current == original ? replacement : current);
    }

    public override string ToString()
    {
        return $"FactionEdit [{Faction}]";
    }
}
