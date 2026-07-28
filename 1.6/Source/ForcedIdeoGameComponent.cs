using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionLoadout;

/// <summary>Where a forced ideology reference resolves from. Append-only — never reorder.</summary>
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
/// Presets stay portable — they only carry the source and key. The link to a game-specific
/// <see cref="Ideo"/> lives here because it must survive save/load and must not produce duplicate
/// ideos on reload. <see cref="Ideo.fileName"/> is NOT serialized by vanilla, so it can't be the
/// key; <see cref="Ideo.id"/> is serialized, so we bind by id.
///
/// Binding granularity:
/// - <see cref="ForcedIdeoSource.Preset"/> refs bind per faction INSTANCE (loadID) — presets are
///   randomised generators, and each faction rolling its own themed ideology is the point.
/// - <see cref="ForcedIdeoSource.SavedFile"/> refs bind per faction DEF — the file is
///   deterministic, so instances of one def share a single realised ideo (caps ideo-count bloat).
/// Realised ideos are registered as minor ideoligions of every faction that uses them, so the
/// ideology listing, vanilla's 4:1 pawn-belief weighting, and GC protection all agree with what
/// raiders actually believe.
///
/// Cleanup policy (<see cref="CleanupOrphanedBindings"/>): when a binding's reference is no longer
/// produced by the active preset, the ideo is unregistered from faction minor lists and the
/// binding dropped, letting vanilla GC collect it once memberless. A formerly forced PRIMARY is
/// left in place — yanking a faction's live primary mid-save would be more disruptive than the
/// stale config.
///
/// The whole feature no-ops in classic ideology mode (single hidden ideo, ideology UI off).
/// </summary>
public class ForcedIdeoGameComponent : GameComponent
{
    /// <summary>Persistent composite reference key → the realised <see cref="Ideo.id"/> in this game.</summary>
    public Dictionary<string, int> refToIdeoId = new();

    /// <summary>Bucket used in place of a faction for faction-less pawns (wild men, creepjoiners).</summary>
    public const int NoFactionBucket = -1;

    /// <summary>
    /// Session-only set of references that resolve to something on disk / in the DefDatabase but
    /// then fail to realise, so we don't retry (and re-log) on every generated pawn.
    /// </summary>
    [Unsaved(false)]
    public HashSet<string> failedRefs = new();

    /// <summary>
    /// Session fast cache: resolved reference → live Ideo. Avoids the per-pawn composite-string
    /// build and id scan on the generation hot path. Key: (faction loadID or -1, faction def
    /// index or -1, source, key) — allocation-free to construct and hash.
    /// </summary>
    [Unsaved(false)]
    public Dictionary<(int loadId, int defIndex, ForcedIdeoSource source, string key), Ideo> resolvedCache = new();

    /// <summary>
    /// Negative cache for SavedFile refs whose .rid is absent: refKey → realtime after which to
    /// re-probe. Keeps the "file saved mid-session gets picked up" self-healing without paying a
    /// disk probe per generated pawn.
    /// </summary>
    [Unsaved(false)]
    public Dictionary<string, float> missingFileRecheckAt = new();

    public const float MissingFileRecheckSeconds = 10f;

    /// <summary>
    /// Session-wide fast exit for the pawn-generation prefix: true iff any active edit forces an
    /// ideology. Recomputed on preset apply and game init; flipped true directly by the pickers so
    /// live in-game edits take effect immediately. Never needs to flip false mid-session — a stale
    /// true only costs the normal lookup path, not correctness.
    /// </summary>
    public static bool AnyIdeologyEditsActive;

    public ForcedIdeoGameComponent(Game game) { }

    public static ForcedIdeoGameComponent Current => Verse.Current.Game?.GetComponent<ForcedIdeoGameComponent>();

    public static bool ClassicMode => Find.IdeoManager?.classicMode ?? false;

    /// <summary>Scans the active faction edits for any forced-ideology configuration.</summary>
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
    /// generation cost lands on the loading screen, not mid-raid), and emits one consolidated
    /// warning for saved-file references missing on this machine.
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

        // 1. Fast path: allocation-free tuple cache, hit on every pawn after the first.
        (int, int, ForcedIdeoSource, string) cacheKey = CacheKeyFor(faction, source, key);
        if (resolvedCache.TryGetValue(cacheKey, out Ideo cached))
        {
            if (cached != null && Find.IdeoManager.IdeosListForReading.Contains(cached))
            {
                EnsureRegisteredWith(faction, cached);
                return cached;
            }
            resolvedCache.Remove(cacheKey); // ideo was removed from the game; fall through and re-resolve
        }

        // 2. Persistent binding (string key built only on cache misses — once per ref per session).
        string refKey = RefKeyFor(faction, source, key);
        if (refToIdeoId.TryGetValue(refKey, out int existingId))
        {
            Ideo existing = FindById(existingId);
            if (existing != null)
            {
                resolvedCache[cacheKey] = existing;
                EnsureRegisteredWith(faction, existing);
                return existing;
            }
            // The ideo was garbage-collected (no holders left); drop the stale id and re-realise.
            refToIdeoId.Remove(refKey);
        }

        if (failedRefs.Contains(refKey))
            return null;

        if (source == ForcedIdeoSource.SavedFile && missingFileRecheckAt.TryGetValue(refKey, out float recheckAt) && Time.realtimeSinceStartup < recheckAt)
            return null;

        // 3. Never drive the disk-based Scribe loader while another scribe operation is active —
        //    doing so would corrupt the in-progress save/load. (Only matters for SavedFile, but
        //    generation also runs safest outside a scribe pass.)
        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            ModCore.Debug($"Deferring forced ideology realisation for '{refKey}': Scribe is {Scribe.mode}.");
            return null;
        }

        // Isolate the realisation's RNG so we don't perturb the generating pawn's own sequence.
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

    public static (int, int, ForcedIdeoSource, string) CacheKeyFor(Faction faction, ForcedIdeoSource source, string key)
    {
        // SavedFile is deterministic → share one realisation across instances of a faction def.
        if (source == ForcedIdeoSource.SavedFile)
            return (NoFactionBucket, faction?.def?.index ?? -1, source, key);
        return (faction?.loadID ?? NoFactionBucket, -1, source, key);
    }

    public static string RefKeyFor(Faction faction, ForcedIdeoSource source, string key)
    {
        if (source == ForcedIdeoSource.SavedFile)
            return "def:" + (faction?.def?.defName ?? "none") + ":" + source + ":" + key;
        return (faction?.loadID ?? NoFactionBucket) + ":" + source + ":" + key;
    }

    /// <summary>Loads a fully-specified ideology from a <c>.rid</c> file. Deterministic.</summary>
    public Ideo LoadFromFile(string fileName, string refKey)
    {
        string path = GenFilePaths.AbsPathForIdeo(fileName);
        if (!File.Exists(path))
        {
            // Stay quiet (a file created later is picked up on the next probe), but don't
            // re-probe the disk for every generated pawn in the meantime.
            missingFileRecheckAt[refKey] = Time.realtimeSinceStartup + MissingFileRecheckSeconds;
            return null;
        }

        missingFileRecheckAt.Remove(refKey);
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

        // The user opted in, so meme restrictions don't block generation — but other mods can
        // read a faction's requiredMemes/allowed memes as a contract, so leave a breadcrumb.
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

    public static Ideo FindById(int id)
    {
        List<Ideo> ideos = Find.IdeoManager.IdeosListForReading;
        for (int i = 0; i < ideos.Count; i++)
        {
            if (ideos[i].id == id)
                return ideos[i];
        }
        return null;
    }

    // ==================== Init-time passes ====================

    /// <summary>All refKeys the active preset can currently produce, across live factions.</summary>
    public HashSet<string> BuildValidRefKeys()
    {
        HashSet<string> valid = new();
        List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
        foreach (KeyValuePair<string, FactionEdit> pair in FactionEdit.ActiveFactionEdits)
        {
            List<Faction> instances = factions.Where(f => f.def.defName == pair.Key).ToList();
            bool synthetic = instances.Count == 0; // special editor-only factions (wild men etc.)

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

            AddRef(pair.Value.ForcedPrimaryIdeoSourceKind, pair.Value.ForcedPrimaryIdeoKey);
            foreach (PawnKindEdit kindEdit in pair.Value.KindEdits)
                AddRef(kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
        }
        return valid;
    }

    /// <summary>
    /// Drops bindings whose reference the active preset no longer produces, and unregisters their
    /// ideos from faction minor lists so vanilla GC can reclaim them once memberless. Primaries
    /// are left in place (see class doc).
    /// </summary>
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
        List<string> missing = null;
        foreach (FactionEdit edit in FactionEdit.ActiveFactionEdits.Values)
        {
            void Check(ForcedIdeoSource source, string key)
            {
                if (source != ForcedIdeoSource.SavedFile || string.IsNullOrEmpty(key))
                    return;
                if (File.Exists(GenFilePaths.AbsPathForIdeo(key)))
                    return;
                missing ??= new List<string>();
                if (!missing.Contains(key))
                    missing.Add(key);
            }

            Check(edit.ForcedPrimaryIdeoSourceKind, edit.ForcedPrimaryIdeoKey);
            foreach (PawnKindEdit kindEdit in edit.KindEdits)
                Check(kindEdit.ForcedIdeoSourceKind, kindEdit.ForcedIdeoKey);
        }

        if (missing != null)
            ModCore.Warn(
                $"Forced ideology saved file(s) not found on this machine: {string.Join(", ", missing)}. Affected pawns keep their faction's ideology until the file(s) exist in the Ideos folder."
            );
    }

    /// <summary>
    /// Realises every kind-level reference for existing factions up front, so ideology generation
    /// (10-100ms for presets) happens on the loading screen instead of mid-raid on the first
    /// forced pawn. Faction primaries are already realised by EnsurePrimaryIdeo.
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
