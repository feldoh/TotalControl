using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout;

[HotSwappable]
public class FactionEdit : IExposable
{
    private static readonly Dictionary<string, FactionDef> originalFactionDefs = new();
    private static Dictionary<(FactionDef, PawnKindDef), PawnKindDef> factionSpecificPawnKindReplacements = new();
    public bool Active = true;
    public ThingFilter ApparelStuffFilter;
    public TechLevel? TechLevel = null;
    public bool DeletedOrClosed;

    public DefRef<FactionDef> Faction = new();
    public List<PawnKindEdit> KindEdits = [];
    public Dictionary<string, float> xenotypeChances = [];
    public Dictionary<XenotypeDef, float> xenotypeChancesByDef = [];
    public bool OverrideFactionXenotypes = false;

    public static PawnKindDef GetReplacementForPawnKind(FactionDef faction, PawnKindDef original)
    {
        if (original == PawnKindDefOf.WildMan)
            faction = Preset.SpecialWildManFaction;
        factionSpecificPawnKindReplacements.TryGetValue((faction, original), out PawnKindDef replacement);
        ModCore.Debug($"Found replacement for {original.defName} in {faction.defName}: {replacement?.defName ?? "<null>"}");
        return replacement ?? original;
    }

    public IEnumerable<PawnGroupMaker> GroupMakers
    {
        get
        {
            if (!Faction.HasValue || Faction.Def.pawnGroupMakers == null)
                yield break;

            foreach (PawnGroupMaker maker in Faction.Def.pawnGroupMakers)
                yield return maker;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Active, "active", true);
        Scribe_Deep.Look(ref ApparelStuffFilter, "apparelStuffFilter");
        Scribe_Deep.Look(ref Faction, "faction");
        Scribe_Values.Look(ref TechLevel, "techLevel");
        Scribe_Collections.Look(ref KindEdits, "kindEdits", LookMode.Deep);
        Scribe_Collections.Look(ref xenotypeChances, "xenotypeChances", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.Saving)
            MaterializeXenotypeChances();
        Scribe_Values.Look(ref OverrideFactionXenotypes, "overrideFactionXenotypes", false);
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
            return;

        MaterializeXenotypeChances();
        if (!(xenotypeChances.NullOrEmpty() && xenotypeChancesByDef.NullOrEmpty()))
            OverrideFactionXenotypes = true;
    }

    /**
     * Can't trust XenotypeDefs to actually exist, so we would like to stick to names.
     * So we need to update the actual defs from the names.
     */
    public void MaterializeXenotypeChances(bool replace = false)
    {
        if (replace)
            xenotypeChancesByDef.Clear();
        if (ModLister.BiotechInstalled && !xenotypeChances.NullOrEmpty())
        {
            xenotypeChances.Do(pair =>
            {
                if (DefDatabase<XenotypeDef>.GetNamedSilentFail(pair.Key) is { } def)
                {
                    xenotypeChancesByDef[def] = pair.Value;
                }
                else
                {
                    ModCore.Log($"XenotypeDef '{pair.Key}' not found while processing edit for '{Faction.DefName}', skipping.");
                }
            });
        }
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

        if (def.basicMemberKind != null)
            def.basicMemberKind = func(def.basicMemberKind);

        if (def.fixedLeaderKinds == null)
            return;
        {
            for (var i = 0; i < def.fixedLeaderKinds.Count; i++)
            {
                PawnKindDef replacement = func(def.fixedLeaderKinds[i]);
                def.fixedLeaderKinds[i] = replacement;
                if (replacement != null)
                    continue;
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
            .SelectMany(group =>
                Enumerable.Empty<PawnGenOption>().ConcatIfNotNull(group.options).ConcatIfNotNull(group.traders).ConcatIfNotNull(group.carriers).ConcatIfNotNull(group.guards)
            )
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
        return def == null ? null : Enumerable.FirstOrDefault(KindEdits, edit => edit.AppliesTo(def));
    }

    public bool HasGlobalEditor()
    {
        return GetGlobalEditor() != null;
    }

    public PawnKindEdit GetGlobalEditor()
    {
        return Enumerable.FirstOrDefault(KindEdits, edit => edit.IsGlobal);
    }

    public void Apply(FactionDef def, bool updateDefDatabase = true)
    {
        if (!Active)
            ModCore.Warn($"Applying faction edit to {def.label}, but this edit is not active!");

        // DISABLED FOR NOW.
        //if (ApparelStuffFilter != null)
        //    def.apparelStuffFilter = ApparelStuffFilter;

        def = EnsureOriginal(def);
        PawnKindEdit global = GetGlobalEditor();
        if ((global?.RaidCommonalityFromPointsCurve?.PointsCount ?? 0) > 0)
            def.raidCommonalityFromPointsCurve = global.RaidCommonalityFromPointsCurve;
        if ((global?.RaidLootValueFromPointsCurve?.PointsCount ?? 0) > 0)
            def.raidLootValueFromPointsCurve = global.RaidLootValueFromPointsCurve;

        if (TechLevel != null)
            def.techLevel = TechLevel.Value;

        IReadOnlyList<PawnKindDef> kinds = GetAllPawnKinds(def);

        foreach (PawnKindDef fkind in kinds)
        {
            PawnKindDef kind = PawnKindEdit.NormaliseDef(fkind);
            PawnKindEdit editor = GetEditFor(kind);
            PawnKindDef safeKind = global != null || editor != null ? CloningUtility.Clone(kind) : kind;
            global?.Apply(safeKind, null);
            if (editor?.Apply(safeKind, global) is { } newKind && newKind != safeKind)
                safeKind = newKind;

            if (ModsConfig.BiotechActive && (xenotypeChancesByDef?.Count ?? 0) >= 1 && (!editor?.ForceSpecificXenos ?? false) && safeKind.RaceProps.Humanlike)
            {
                safeKind.xenotypeSet ??= new XenotypeSet();
                safeKind.xenotypeSet.xenotypeChances ??= [];
                safeKind.xenotypeSet.xenotypeChances.Clear();
                foreach (KeyValuePair<XenotypeDef, float> rate in xenotypeChancesByDef ?? [])
                    safeKind.xenotypeSet.xenotypeChances.Add(new XenotypeChance(rate.Key, rate.Value));
            }

            if ((global?.RenameDef ?? false) || (editor?.RenameDef ?? false))
            {
                List<PawnKindEdit> stashedEdits = PawnKindEdit.RemoveActiveEdits(safeKind);
                safeKind.defName = GetNewNameForPawnKind(kind, def);
                if (stashedEdits != null)
                    PawnKindEdit.SetActiveEdits(safeKind, stashedEdits);
                PawnKindEdit.RecordReplacement(kind, safeKind);
                if (updateDefDatabase)
                    DefDatabase<PawnKindDef>.Add(safeKind);
            }
            if (kind != safeKind)
                ReplaceKind(def, kind, safeKind);
        }

        if (!ModsConfig.BiotechActive || xenotypeChancesByDef.NullOrEmpty())
            return;
        def.xenotypeSet ??= new XenotypeSet();
        def.xenotypeSet?.xenotypeChances?.Clear();
        foreach (KeyValuePair<XenotypeDef, float> rate in xenotypeChancesByDef)
            def.xenotypeSet?.xenotypeChances?.Add(new XenotypeChance(rate.Key, rate.Value));
    }

    public static string GetNewNameForPawnKind(PawnKindDef pawnKindDef, FactionDef factionDef) => $"{pawnKindDef.defName}_TCCln_{factionDef.defName}";

    private void ReplaceKind(FactionDef faction, PawnKindDef original, PawnKindDef replacement)
    {
        ModCore.Debug($"Replacing PawnKind '{original?.defName ?? "<null>"}' with '{replacement?.defName ?? "<null>"}' in faction {faction.defName}");
        TweakAllPawnKinds(faction, current => current == original ? replacement : current);
        factionSpecificPawnKindReplacements.SetOrAdd((faction, original), replacement);
    }

    public override string ToString()
    {
        return $"FactionEdit [{Faction}]";
    }
}
