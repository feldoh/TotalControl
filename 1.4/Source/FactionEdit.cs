﻿using System;
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

            for (var i = 0; i < group.Count; i++)
            {
                PawnKindDef replace = func(group[i].kind);
                group[i].kind = replace;
                if (replace != null)
                {
                    group.RemoveAt(i);
                    // We've removed the item at the current index, so we need to rewind to process the new item at this index
                    i--;
                }
            }
        }
    }

    public static IReadOnlyList<PawnKindDef> GetAllPawnKinds(FactionDef def)
    {
        var kinds = (def.pawnGroupMakers ?? Enumerable.Empty<PawnGroupMaker>())
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

    private static void EnsureOriginal(FactionDef def)
    {
        if (def == null)
            return;

        if (originalFactionDefs.ContainsKey(def.defName))
            return;

        FactionDef copy = CloningUtility.Clone(def);
        originalFactionDefs.Add(def.defName, copy);
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

        EnsureOriginal(def);

        var kinds = GetAllPawnKinds(def);

        PawnKindEdit global = GetGlobalEditor();
        if (global != null)
            foreach (PawnKindDef kind in kinds)
                global.Apply(kind, null);

        foreach (PawnKindDef kind in kinds)
        {
            PawnKindEdit editor = GetEditFor(kind);
            if (editor == null)
                continue;

            PawnKindDef replaceWith = editor.Apply(kind, global);
            if (replaceWith != kind)
                ReplaceKind(def, kind, replaceWith);
        }

        if (!ModsConfig.BiotechActive || xenotypeChances == null) return;
        def.xenotypeSet?.xenotypeChances?.Clear();
        foreach (var rate in xenotypeChances)
            def.xenotypeSet?.xenotypeChances?.Add(new XenotypeChance(rate.Key, rate.Value));
    }

    private void ReplaceKind(FactionDef faction, PawnKindDef original, PawnKindDef replacement)
    {
        ModCore.Log($"Replacing PawnKind '{original?.defName ?? "<null>"}' with '{replacement?.defName ?? "<null>"}' in faction {faction.defName}");
        TweakAllPawnKinds(faction, current => current == original ? replacement : current);
    }

    public override string ToString()
    {
        return $"FactionEdit [{Faction}]";
    }
}