using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionLoadout
{
    [HotSwappable]
    public class FactionEdit : IExposable
    {
        private static Dictionary<string, FactionDef> originalFactionDefs = new Dictionary<string, FactionDef>();

        public static void TweakAllPawnKinds(FactionDef def, Func<PawnKindDef, PawnKindDef> func)
        {
            if (def == null || func == null)
                return;

            void WorkOn(List<PawnGenOption> group)
            {
                if (group == null)
                    return;

                for (int i = 0; i < group.Count; i++)
                {
                    var replace = func(group[i].kind);
                    group[i].kind = replace;
                    if (replace == null)
                    {
                        group.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (def.pawnGroupMakers != null)
            {
                foreach (var group in def.pawnGroupMakers)
                {
                    WorkOn(group.options);   
                    WorkOn(group.traders);
                    WorkOn(group.carriers);
                    WorkOn(group.guards);
                }
            }

            if (def.basicMemberKind != null)
            {
                def.basicMemberKind = func(def.basicMemberKind);
            }

            if (def.fixedLeaderKinds != null)
            {
                for (int i = 0; i < def.fixedLeaderKinds.Count; i++)
                {
                    var replacement = func(def.fixedLeaderKinds[i]);
                    def.fixedLeaderKinds[i] = replacement;
                    if (replacement == null)
                    {
                        def.fixedLeaderKinds.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public static IReadOnlyList<PawnKindDef> GetAllPawnKinds(FactionDef def)
        {
            HashSet<PawnKindDef> kinds = new HashSet<PawnKindDef>();
            if(def.pawnGroupMakers != null)
            {
                foreach (var group in def.pawnGroupMakers)
                {
                    if (group.options != null)
                        foreach (var thing in group.options)
                            kinds.Add(thing.kind);
                    if (group.traders != null)
                        foreach (var thing in group.traders)
                            kinds.Add(thing.kind);
                    if (group.carriers != null)
                        foreach (var thing in group.carriers)
                            kinds.Add(thing.kind);
                    if (group.guards != null)
                        foreach (var thing in group.guards)
                            kinds.Add(thing.kind);
                }
            }
            
            if (def.basicMemberKind != null)
                kinds.Add(def.basicMemberKind);
            if (def.fixedLeaderKinds != null)
                foreach (var item in def.fixedLeaderKinds)
                    kinds.Add(item);

            return kinds.ToArray();
        }

        public static FactionDef TryGetOriginal(string factionDefName)
        {
            if (factionDefName == null)
                return null;

            if (originalFactionDefs.TryGetValue(factionDefName, out var found))
                return found;

            return null;
        }

        private static void EnsureOriginal(FactionDef def)
        {
            if (def == null)
                return;

            if (originalFactionDefs.ContainsKey(def.defName))            
                return;

            var copy = CloningUtility.Clone(def);
            originalFactionDefs.Add(def.defName, copy);
        }
        
        public IEnumerable<PawnGroupMaker> GroupMakers
        {
            get
            {
                if(!Faction.HasValue || Faction.Def.pawnGroupMakers == null)
                {
                    yield break;
                }
                foreach(var maker in Faction.Def.pawnGroupMakers)
                {
                    yield return maker;
                }
            }
        }

        public DefRef<FactionDef> Faction = new DefRef<FactionDef>();
        public List<PawnKindEdit> KindEdits = new List<PawnKindEdit>();
        public ThingFilter ApparelStuffFilter;
        public Dictionary<XenotypeDef, float> xenotypeChances = new Dictionary<XenotypeDef, float>();
        public bool Active = true;
        public bool DeletedOrClosed;

        public bool HasEditFor(PawnKindDef def)
        {
            return GetEditFor(def) != null;
        }

        public PawnKindEdit GetEditFor(PawnKindDef def)
        {
            if (def == null)
                return null;

            foreach (var edit in KindEdits)
                if (edit.AppliesTo(def))
                    return edit;

            return null;
        }

        public bool HasGlobalEditor()
        {
            return GetGlobalEditor() != null;
        }

        public PawnKindEdit GetGlobalEditor()
        {
            foreach(var edit in KindEdits)
            {
                if (edit.IsGlobal)
                    return edit;
            }
            return null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Active, "active", true);
            Scribe_Deep.Look(ref ApparelStuffFilter, "apparelStuffFilter");
            Scribe_Deep.Look(ref Faction, "faction");
            Scribe_Collections.Look(ref KindEdits, "kindEdits", LookMode.Deep);
            Scribe_Collections.Look(ref xenotypeChances, "xenotypeChances", LookMode.Def, LookMode.Value);
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
                        
            var global = GetGlobalEditor();
            if(global != null)
            {
                foreach (var kind in kinds)
                {
                    global.Apply(kind, null);
                }
            }

            foreach (var kind in kinds)
            {
                var editor = GetEditFor(kind);
                if (editor == null)
                    continue;

                var replaceWith = editor.Apply(kind, global);
                if(replaceWith != kind)
                    ReplaceKind(def, kind, replaceWith);
            }

            if (ModsConfig.BiotechActive && xenotypeChances != null)
            {
                def.xenotypeSet.xenotypeChances.Clear();
                foreach (var rate in xenotypeChances)
                {
                    def.xenotypeSet.xenotypeChances.Add(new XenotypeChance(rate.Key, rate.Value));
                }
            }
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
}
