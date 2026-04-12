using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using FactionLoadout.Modules;
using FactionLoadout.Util;
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

    /// <summary>Raw XML preserved for faction-level module data belonging to inactive modules.</summary>
    private Dictionary<string, string> preservedFactionModuleXml;

    public DefRef<FactionDef> Faction = new();
    public List<PawnKindEdit> KindEdits = [];
    public List<PawnGroupMakerEdit> PawnGroupMakerEdits = null;
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
        Scribe_Collections.Look(ref PawnGroupMakerEdits, "groupEdits", LookMode.Deep);
        ExposeModuleFactionData();

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
            return;

        MaterializeXenotypeChances();
        if (!(xenotypeChances.NullOrEmpty() && xenotypeChancesByDef.NullOrEmpty()))
            OverrideFactionXenotypes = true;
    }

    /// <summary>
    /// Serializes/deserializes faction-level module data. Mirrors PawnKindEdit.ExposeModuleData
    /// but operates on a &lt;factionModules&gt; node and calls module.ExposeFactionData instead.
    /// Unrecognized child nodes are preserved as raw XML to prevent data loss when modules are
    /// temporarily disabled.
    /// </summary>
    private void ExposeModuleFactionData()
    {
        IReadOnlyList<ITotalControlModule> modules = ModuleRegistry.Modules;

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            XmlNode factionModulesNode = Scribe.loader.curXmlParent?["factionModules"];
            if (factionModulesNode == null)
                return;

            foreach (XmlNode child in factionModulesNode.ChildNodes)
            {
                ITotalControlModule module = ModuleRegistry.GetModule(child.Name);
                if (module is { IsActive: true })
                {
                    XmlNode previousParent = Scribe.loader.curXmlParent;
                    Scribe.loader.curXmlParent = child;
                    try
                    {
                        module.ExposeFactionData(this);
                    }
                    catch (Exception e)
                    {
                        ModCore.Error($"Error loading faction module data for '{module.ModuleName}' (key: {module.ModuleKey})", e);
                    }

                    Scribe.loader.curXmlParent = previousParent;
                }
                else
                {
                    preservedFactionModuleXml ??= new Dictionary<string, string>();
                    preservedFactionModuleXml[child.Name] = child.InnerXml;
                    ModCore.Debug($"Preserving faction module data for absent module '{child.Name}'");
                }
            }
        }
        else if (Scribe.mode == LoadSaveMode.Saving)
        {
            bool hasActiveModules = modules.Any(m => m.IsActive);
            bool hasPreserved = preservedFactionModuleXml is { Count: > 0 };

            if (!hasActiveModules && !hasPreserved)
                return;

            Scribe.saver.EnterNode("factionModules");
            try
            {
                foreach (ITotalControlModule module in modules)
                {
                    if (!module.IsActive)
                        continue;
                    Scribe.saver.EnterNode(module.ModuleKey);
                    try
                    {
                        module.ExposeFactionData(this);
                    }
                    catch (Exception e)
                    {
                        ModCore.Error($"Error saving faction module data for '{module.ModuleName}' (key: {module.ModuleKey})", e);
                    }

                    Scribe.saver.ExitNode();
                }

                if (preservedFactionModuleXml != null)
                {
                    HashSet<string> activeKeys = new(modules.Where(m => m.IsActive).Select(m => m.ModuleKey));
                    foreach (KeyValuePair<string, string> kvp in preservedFactionModuleXml)
                    {
                        if (activeKeys.Contains(kvp.Key))
                            continue;
                        Scribe.saver.writer.WriteStartElement(kvp.Key);
                        Scribe.saver.writer.WriteRaw(kvp.Value);
                        Scribe.saver.writer.WriteEndElement();
                    }
                }
            }
            finally
            {
                Scribe.saver.ExitNode();
            }
        }
        else if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            foreach (ITotalControlModule module in modules)
            {
                if (!module.IsActive)
                    continue;
                try
                {
                    module.ExposeFactionData(this);
                }
                catch (Exception e)
                {
                    ModCore.Error($"Error in post-load init for faction module '{module.ModuleName}' (key: {module.ModuleKey})", e);
                }
            }
        }
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
        if (def == null)
            return [];
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

        if (DefCache.DefaultFactionKinds != null && DefCache.DefaultFactionKinds.TryGetValue(def, out List<PawnKindDef> defaultKinds))
        {
            kinds.AddRange(defaultKinds);
        }

        return kinds.ToArray();
    }

    public static void ClearState()
    {
        originalFactionDefs.Clear();
        factionSpecificPawnKindReplacements.Clear();
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

    #region PawnGroupMakers

    /// <summary>
    /// Lazily snapshots the original faction's pawnGroupMakers into
    /// <see cref="PawnGroupMakerEdits"/> on first call. Returns the existing list if
    /// already initialised.
    /// </summary>
    public List<PawnGroupMakerEdit> GetOrInitPawnGroupMakerEdits()
    {
        if (PawnGroupMakerEdits != null)
            return PawnGroupMakerEdits;

        FactionDef def = Faction.Def;
        if (def?.pawnGroupMakers == null)
        {
            PawnGroupMakerEdits = [];
            return PawnGroupMakerEdits;
        }

        PawnGroupMakerEdits = def.pawnGroupMakers.Select(PawnGroupMakerEdit.FromPawnGroupMaker).ToList();
        return PawnGroupMakerEdits;
    }

    /// <summary>Clears <see cref="PawnGroupMakerEdits"/>, restoring live faction defaults.</summary>
    public void ResetGroupEdits()
    {
        PawnGroupMakerEdits = null;
    }

    /// <summary>
    /// Returns all pawnkind defs known to TC for the "Add new…" discovery.
    /// When <see cref="PawnGroupMakerEdits"/> is set, reads from it; otherwise reads
    /// from the live <see cref="FactionDef"/>.
    /// </summary>
    public IEnumerable<PawnKindDef> GetAllKindDefsForUI() =>
        PawnGroupMakerEdits != null ? PawnGroupMakerEdits.SelectMany(g => g.GetAllKinds()).Distinct() : GetAllPawnKinds(Faction.Def);

    /// <summary>
    /// Returns the set of pawnkinds that have a <see cref="PawnKindEdit"/> in
    /// this faction edit but are not present in any spawn group.  These are
    /// "orphaned" and are unlikely to appear in game unless they are spawned by code or other triggers.
    /// </summary>
    public HashSet<PawnKindDef> GetOrphanedKinds()
    {
        // Normalise clone defs back to originals before comparing — after Apply(),
        // faction def group makers contain cloned PawnKindDefs (e.g. Archer_TCCln_Gentle)
        // while edit.Def references the original (Archer).
        HashSet<string> inGroups = GetAllKindDefsForUI().Select(k => PawnKindEdit.NormaliseDef(k).defName).ToHashSet();
        return KindEdits.Where(e => e.Def != null && !e.IsGlobal && !inGroups.Contains(e.Def.defName)).Select(e => e.Def).ToHashSet();
    }

    #endregion

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

        // Apply group edits before discovering pawnkinds so that newly added
        // pawnkinds are visible to the rest of the Apply() pipeline.
        if (PawnGroupMakerEdits != null)
        {
            def.pawnGroupMakers = PawnGroupMakerEdits.Select(e => e.ToPawnGroupMaker()).ToList();
        }

        // Give each active module a chance to apply faction-level data to the FactionDef.
        foreach (ITotalControlModule module in ModuleRegistry.Modules)
        {
            if (!module.IsActive)
                continue;
            try
            {
                module.ApplyFaction(this, def);
            }
            catch (Exception e)
            {
                ModCore.Error($"Error applying faction module '{module.ModuleName}'", e);
            }
        }

        PawnKindEdit global = GetGlobalEditor();
        if ((global?.RaidCommonalityFromPointsCurve?.PointsCount ?? 0) > 0)
            def.raidCommonalityFromPointsCurve = global.RaidCommonalityFromPointsCurve;
        if ((global?.RaidLootValueFromPointsCurve?.PointsCount ?? 0) > 0)
            def.raidLootValueFromPointsCurve = global.RaidLootValueFromPointsCurve;
        if ((global?.MaxPawnCostPerTotalPointsCurve?.PointsCount ?? 0) > 0)
            def.maxPawnCostPerTotalPointsCurve = global.MaxPawnCostPerTotalPointsCurve;

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

    /// <summary>
    /// Copies top-level faction fields (tech level, xenotype overrides) from
    /// <paramref name="source"/> into this edit. KindEdits and identity fields
    /// (Faction, Active, DeletedOrClosed) are left untouched.
    /// </summary>
    public void CopyFrom(FactionEdit source)
    {
        TechLevel = source.TechLevel;
        OverrideFactionXenotypes = source.OverrideFactionXenotypes;
        xenotypeChances = source.xenotypeChances != null ? new Dictionary<string, float>(source.xenotypeChances) : [];
        xenotypeChancesByDef = source.xenotypeChancesByDef != null ? new Dictionary<XenotypeDef, float>(source.xenotypeChancesByDef) : [];
        // Group edits are not copied by the faction-level clipboard — they are
        // structural changes that should not be blindly overwritten.
    }

    public override string ToString()
    {
        return $"FactionEdit [{Faction}]";
    }
}
