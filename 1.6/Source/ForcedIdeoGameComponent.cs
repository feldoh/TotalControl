using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>Where a forced ideology reference resolves from.</summary>
public enum ForcedIdeoSource
{
    /// <summary>A user-saved ideology file (<c>.rid</c>) in RimWorld's Ideos folder; key = filename (no extension).</summary>
    SavedFile,

    /// <summary>An <see cref="IdeoPresetDef"/> shipped by the base game or a mod; key = defName.</summary>
    Preset,

    /// <summary>The pawn's faction's actual primary ideology in the current game; key is unused.</summary>
    FactionPrimary,
}

/// <summary>
/// Per-save binding between a portable forced-ideology reference (source + key, stored in a
/// <see cref="Preset"/>) and the concrete <see cref="Ideo"/> instance realised into THIS game.
///
/// Presets stay portable, they carry the source and key. The link to a game-specific
/// <see cref="Ideo"/> lives here because it must survive save/load and must not produce duplicate
/// ideos on reload. <see cref="Ideo.fileName"/> is NOT serialised by vanilla, so it can't be the
/// key; <see cref="Ideo.id"/> is serialised, so we bind by id.
///
/// Realised ideos are registered as minor ideoligions of every faction that uses them, so the
/// ideology listing, vanilla's 4:1 pawn-belief weighting, and GC protection all agree with what
/// raiders actually believe.
///
/// </summary>
public class ForcedIdeoGameComponent : GameComponent
{
    /// <summary>Persistent composite reference key -> the realised <see cref="Ideo.id"/> in this game.</summary>
    public Dictionary<string, int> refToIdeoId = new();

    /// <summary>Bucket used in place of a faction for faction-less pawns (wild men, creepjoiners).</summary>
    public const int NoFactionBucket = -1;

    /// <summary>
    /// Session-only set of references that fail to realise — a missing .rid file, or a def/file
    /// present but unloadable — so we don't retry (and re-log) on every generated pawn.
    /// </summary>
    [Unsaved(false)]
    public HashSet<string> failedRefs = [];

    [Unsaved(false)]
    public Dictionary<(int loadId, int defIndex, ForcedIdeoSource source, string key), Ideo> resolvedCache = new();

    public static bool AnyIdeologyEditsActive;

    public ForcedIdeoGameComponent(Game game) { }

    public static ForcedIdeoGameComponent Current => Verse.Current.Game?.GetComponent<ForcedIdeoGameComponent>();

    public static bool ClassicMode => Find.IdeoManager?.classicMode ?? false;

    public static void RecomputeAnyEditsActive()
    {
        foreach (FactionEdit edit in FactionEdit.ActiveFactionEdits.Values)
        {
            if (!string.IsNullOrEmpty(edit.ForcedPrimaryIdeoKey))
            {
                AnyIdeologyEditsActive = true;
                return;
            }
            foreach (PawnKindEdit kindEdit in edit.KindEdits)
            {
                if (!string.IsNullOrEmpty(kindEdit.ForcedIdeoKey))
                {
                    AnyIdeologyEditsActive = true;
                    return;
                }
            }
        }
        AnyIdeologyEditsActive = false;
    }

    /// <summary>
    /// Runs on new game and on load: refreshes the fast-exit flag, prunes orphaned bindings,
    /// applies faction-level forced primaries, pre-realises kind-level references (so the
    /// generation cost lands on the loading screen, not mid-raid)
    /// </summary>
    public override void FinalizeInit()
    {
        if (!ModsConfig.IdeologyActive || ClassicMode)
            return;

        RecomputeAnyEditsActive();
        CleanupOrphanedBindings();
        if (!AnyIdeologyEditsActive)
            return;

        WarnMissingSavedFiles();

        foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            EnsurePrimaryIdeo(faction);

        PreRealizeKindRefs();
    }

    /// <summary>
    /// If the active preset forces a primary ideology for <paramref name="faction"/>'s def,
    /// realise it and make it the faction's primary. Idempotent. Called from FinalizeInit and
    /// from the FactionManager.Add hook (mid-game factions) — NOT from the pawn-generation path.
    /// The player's faction is never touched.
    /// </summary>
    public void EnsurePrimaryIdeo(Faction faction)
    {
        if (faction?.ideos == null || faction.IsPlayer || ClassicMode)
            return;

        FactionEdit edit = FactionEdit.GetActiveEditFor(faction.def);
        if (edit == null || string.IsNullOrEmpty(edit.ForcedPrimaryIdeoKey) || edit.ForcedPrimaryIdeoSourceKind == ForcedIdeoSource.FactionPrimary)
            return;

        Ideo ideo = GetOrInjectIdeo(faction, edit.ForcedPrimaryIdeoSourceKind, edit.ForcedPrimaryIdeoKey);
        if (ideo == null)
        {
            ModCore.Debug($"Forced primary ideology for faction '{faction.Name}' did not resolve ({edit.ForcedPrimaryIdeoSourceKind} '{edit.ForcedPrimaryIdeoKey}').");
            return;
        }
        if (faction.ideos.IsPrimary(ideo))
            return;

        Ideo oldPrimary = faction.ideos.PrimaryIdeo;
        faction.ideos.IdeosMinorListForReading.Remove(ideo);
        faction.ideos.SetPrimary(ideo);

        // Keep the faction's face coherent: if the leader was generated on the old primary
        // (leaders are often created before the forced primary lands), migrate them.
        if (faction.leader?.ideo != null && oldPrimary != null && faction.leader.Ideo == oldPrimary)
            faction.leader.ideo.SetIdeo(ideo);

        ModCore.Log($"Set forced primary ideology '{ideo.name}' on faction '{faction.Name}'.");
    }

    /// <summary>
    /// Returns the ideo for the given reference, realising it into the game (and registering it
    /// with <paramref name="faction"/>) if it isn't already present. Returns null if Ideology is
    /// inactive, classic mode is on, the reference can't be resolved, or it's unsafe to touch the
    /// loader right now.
    /// </summary>
    public Ideo GetOrInjectIdeo(Faction faction, ForcedIdeoSource source, string key)
    {
        if (!ModsConfig.IdeologyActive || ClassicMode)
            return null;
        if (source == ForcedIdeoSource.FactionPrimary)
            return faction?.ideos?.PrimaryIdeo;
        if (string.IsNullOrEmpty(key))
            return null;

        // Three stages, cheapest first: tuple cache -> persistent id binding -> realise from disk/def.
        (int, int, ForcedIdeoSource, string) cacheKey = CacheKeyFor(faction, source, key);
        Ideo cached = ResolveFromCache(cacheKey, faction);
        if (cached != null)
            return cached;

        string refKey = RefKeyFor(faction, source, key);
        Ideo bound = ResolveFromBinding(refKey, cacheKey, faction);
        if (bound != null)
            return bound;

        return failedRefs.Contains(refKey) ? null : RealiseNewIdeo(faction, source, key, refKey, cacheKey);
    }

    /// <summary>
    /// Returns the cached ideo, or null if there's no usable entry; a stale entry whose ideo has left the game is evicted so the caller re-resolves.
    /// </summary>
    public Ideo ResolveFromCache((int, int, ForcedIdeoSource, string) cacheKey, Faction faction)
    {
        if (!resolvedCache.TryGetValue(cacheKey, out Ideo cached))
            return null;
        if (cached != null && Find.IdeoManager.IdeosListForReading.Contains(cached))
        {
            EnsureRegisteredWith(faction, cached);
            return cached;
        }
        resolvedCache.Remove(cacheKey); // ideo was removed from the game; re-resolve
        return null;
    }

    /// <summary>
    /// Returns the bound ideo and warms the cache, or null if unbound using the id binding that survives save/load.
    /// </summary>
    public Ideo ResolveFromBinding(string refKey, (int, int, ForcedIdeoSource, string) cacheKey, Faction faction)
    {
        if (!refToIdeoId.TryGetValue(refKey, out int existingId))
            return null;
        Ideo existing = FindById(existingId);
        if (existing != null)
        {
            resolvedCache[cacheKey] = existing;
            EnsureRegisteredWith(faction, existing);
            return existing;
        }
        refToIdeoId.Remove(refKey); // garbage-collected (no holders left); re-realise
        return null;
    }

    /// <summary>
    /// Realise a brand-new ideo from disk/def, register it, and bind it for this save.
    /// Deferred while a Scribe pass is active because using the disk-based loader then
    /// would corrupt the in-progress save/load.
    /// </summary>
    public Ideo RealiseNewIdeo(Faction faction, ForcedIdeoSource source, string key, string refKey, (int, int, ForcedIdeoSource, string) cacheKey)
    {
        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            ModCore.Debug($"Deferring forced ideology realisation for '{refKey}': Scribe is {Scribe.mode}.");
            return null;
        }

        // Isolate the realisation's RNG so we don't mess with the generating pawn's own sequence.
        Rand.PushState();
        try
        {
            Ideo ideo = source switch
            {
                ForcedIdeoSource.SavedFile => LoadFromFile(key, refKey),
                ForcedIdeoSource.Preset => GenerateFromPreset(key, refKey, faction?.def),
                _ => null,
            };
            if (ideo == null)
                return null;

            Find.IdeoManager.Add(ideo);
            refToIdeoId[refKey] = ideo.id;
            resolvedCache[cacheKey] = ideo;
            EnsureRegisteredWith(faction, ideo);

            ModCore.Log($"Realised forced ideology '{ideo.name}' from {source} '{key}' for faction '{faction?.Name ?? "<none>"}' (id {ideo.id}).");
            return ideo;
        }
        finally
        {
            Rand.PopState();
        }
    }

    /// <summary>
    /// Registers <paramref name="ideo"/> as a minor ideoligion of <paramref name="faction"/> so
    /// the ideology listing shows it under the faction, vanilla's own pawn-gen weighting can use
    /// it, and faction membership blocks GC. Needed on cache hits too: SavedFile refs are shared
    /// across instances of one faction def, and each instance registers on first use.
    /// </summary>
    public static void EnsureRegisteredWith(Faction faction, Ideo ideo)
    {
        if (faction?.ideos == null || faction.ideos.Has(ideo))
            return;
        faction.ideos.IdeosMinorListForReading.Add(ideo);
    }

    public static (int, int, ForcedIdeoSource, string) CacheKeyFor(Faction faction, ForcedIdeoSource source, string key) =>
        source == ForcedIdeoSource.SavedFile ? (NoFactionBucket, faction?.def?.index ?? -1, source, key) : (faction?.loadID ?? NoFactionBucket, -1, source, key);

    public static string RefKeyFor(Faction faction, ForcedIdeoSource source, string key)
    {
        if (source == ForcedIdeoSource.SavedFile)
            return "def:" + (faction?.def?.defName ?? "none") + ":" + source + ":" + key;
        return (faction?.loadID ?? NoFactionBucket) + ":" + source + ":" + key;
    }

    /// <summary>Loads an ideology from a <c>.rid</c> file.</summary>
    public Ideo LoadFromFile(string fileName, string refKey)
    {
        string path = GenFilePaths.AbsPathForIdeo(fileName);
        if (!File.Exists(path))
        {
            // Missing on this machine, mark the ref failed so we don't retry for every generated pawn.
            failedRefs.Add(refKey);
            return null;
        }

        if (!GameDataSaveLoader.TryLoadIdeo(path, out Ideo ideo) || ideo == null)
        {
            failedRefs.Add(refKey);
            ModCore.Warn($"Forced ideology file '{fileName}' exists but could not be loaded (invalid .rid file).");
            return null;
        }

        IdeoGenerator.InitLoadedIdeo(ideo);
        return ideo;
    }

    /// <summary>
    /// Generates an ideology from an <see cref="IdeoPresetDef"/> (base game / mod). The preset is a
    /// generator, not a fixed ideology, so the result is randomised — but we realise it once per
    /// faction instance per save and reuse it, so it stays stable within a save.
    /// </summary>
    public Ideo GenerateFromPreset(string defName, string refKey, FactionDef forFaction)
    {
        IdeoPresetDef preset = DefDatabase<IdeoPresetDef>.GetNamedSilentFail(defName);
        if (preset == null)
        {
            failedRefs.Add(refKey);
            ModCore.Warn($"Forced ideology preset def '{defName}' not found (mod removed?).");
            return null;
        }

        FactionDef fac = forFaction ?? Faction.OfPlayerSilentFail?.def ?? DefDatabase<FactionDef>.AllDefsListForReading.FirstOrDefault();

        // We don't let faction meme restrictions block generation, log any conflicts and move on
        foreach (MemeDef meme in preset.memes)
        {
            if (!IdeoUtility.IsMemeAllowedFor(meme, fac))
                ModCore.Warn($"Forced ideology preset '{defName}' contains meme '{meme.defName}' which faction def '{fac?.defName}' does not normally allow.");
        }

        // Mirror Page_ChooseIdeoPreset.DoPreset: ensure a structure meme is present before generating.
        List<MemeDef> memes = preset.memes.ToList();
        if (
            !memes.Any(m => m.category == MemeCategory.Structure)
            && DefDatabase<MemeDef>
                .AllDefsListForReading.Where(m => m.category == MemeCategory.Structure && IdeoUtility.IsMemeAllowedFor(m, fac))
                .TryRandomElement(out MemeDef structure)
        )
        {
            memes.Add(structure);
        }

        return IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(fac, forcedMemes: memes, classicExtra: preset.classicPlus, forceNoWeaponPreference: true));
    }

    public static Ideo FindById(int id) => Enumerable.FirstOrDefault(Find.IdeoManager.IdeosListForReading, t => t.id == id);

    // ==================== Init-time passes ====================

    /// <summary>All refKeys the active preset can currently produce, across live factions.</summary>
    public HashSet<string> BuildValidRefKeys()
    {
        HashSet<string> valid = [];
        List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
        foreach (KeyValuePair<string, FactionEdit> pair in FactionEdit.ActiveFactionEdits)
        {
            List<Faction> instances = factions.Where(f => f.def.defName == pair.Key).ToList();
            bool synthetic = instances.Count == 0; // special editor-only factions (wild men etc.)

            AddRef(pair.Value.ForcedPrimaryIdeoSourceKind, pair.Value.ForcedPrimaryIdeoKey);
            foreach (PawnKindEdit kindEdit in pair.Value.KindEdits)
                AddRef(kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
            continue;

            void AddRef(ForcedIdeoSource source, string key)
            {
                if (string.IsNullOrEmpty(key) || source == ForcedIdeoSource.FactionPrimary)
                    return;
                if (synthetic)
                {
                    valid.Add(RefKeyFor(null, source, key));
                    return;
                }
                foreach (Faction f in instances)
                    valid.Add(RefKeyFor(f, source, key));
            }
        }
        return valid;
    }

    public void CleanupOrphanedBindings()
    {
        if (refToIdeoId.Count == 0)
            return;

        HashSet<string> valid = BuildValidRefKeys();
        List<string> orphaned = refToIdeoId.Keys.Where(k => !valid.Contains(k)).ToList();
        foreach (string refKey in orphaned)
        {
            Ideo ideo = FindById(refToIdeoId[refKey]);
            refToIdeoId.Remove(refKey);
            if (ideo == null)
                continue;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.ideos != null && !faction.ideos.IsPrimary(ideo))
                    faction.ideos.IdeosMinorListForReading.Remove(ideo);
            }
            ModCore.Log($"Unbound orphaned forced ideology '{ideo.name}' (ref '{refKey}' no longer in active preset).");
        }
        if (orphaned.Count > 0)
            resolvedCache.Clear();
    }

    /// <summary>One consolidated warning for SavedFile references whose .rid is absent here.</summary>
    public void WarnMissingSavedFiles()
    {
        List<string> missing = [];
        foreach (FactionEdit edit in FactionEdit.ActiveFactionEdits.Values)
        {
            Check(edit.ForcedPrimaryIdeoSourceKind, edit.ForcedPrimaryIdeoKey);
            foreach (PawnKindEdit kindEdit in edit.KindEdits)
                Check(kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
            continue;

            void Check(ForcedIdeoSource source, string key)
            {
                if (source != ForcedIdeoSource.SavedFile || string.IsNullOrEmpty(key))
                    return;
                if (File.Exists(GenFilePaths.AbsPathForIdeo(key)))
                    return;
                if (!missing.Contains(key))
                    missing.Add(key);
            }
        }

        if (!missing.NullOrEmpty())
            ModCore.Warn(
                $"Forced ideology saved file(s) not found on this machine: {string.Join(", ", missing)}. Affected pawns keep their faction's ideology until the file(s) exist in the Ideos folder."
            );
    }

    /// <summary>
    /// Realises every kind-level reference for existing factions up front, so ideology generation happens on the loading screen instead of mid-raid.
    /// Faction primaries are already realised by EnsurePrimaryIdeo.
    /// </summary>
    public void PreRealizeKindRefs()
    {
        List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
        foreach (KeyValuePair<string, FactionEdit> pair in FactionEdit.ActiveFactionEdits)
        {
            List<Faction> instances = factions.Where(f => f.def.defName == pair.Key).ToList();
            foreach (PawnKindEdit kindEdit in pair.Value.KindEdits)
            {
                if (string.IsNullOrEmpty(kindEdit.ForcedIdeoKey) || kindEdit.ForcedIdeoSourceKind == ForcedIdeoSource.FactionPrimary)
                    continue;
                if (instances.Count == 0)
                {
                    GetOrInjectIdeo(null, kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
                    continue;
                }
                foreach (Faction faction in instances)
                    GetOrInjectIdeo(faction, kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
            }
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref refToIdeoId, "refToIdeoId", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            refToIdeoId ??= new Dictionary<string, int>();
    }
}
