using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
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
/// <see cref="Preset"/>) and the concrete <see cref="Ideo"/> instance realised into THIS game
/// for a specific <see cref="Verse.Faction"/> instance.
///
/// Presets stay portable — they only carry the source and key. The link to a game-specific
/// <see cref="Ideo"/> lives here because it must survive save/load and must not produce duplicate
/// ideos on reload. <see cref="Ideo.fileName"/> is NOT serialized by vanilla, so it can't be the
/// key; <see cref="Ideo.id"/> is serialized, so we bind by id.
///
/// Binding is per faction INSTANCE (loadID), not global: two factions realising the same template
/// each get their own consistent ideo, and every realised ideo is registered as a minor ideo of
/// its faction so the ideology listing, vanilla pawn-gen weighting, and GC protection all agree
/// with what raiders actually show up believing.
/// </summary>
public class ForcedIdeoGameComponent : GameComponent
{
    /// <summary>Composite reference key (<c>"{factionLoadId}:{source}:{key}"</c>) → the realised <see cref="Ideo.id"/> in this game.</summary>
    public Dictionary<string, int> refToIdeoId = new();

    /// <summary>Bucket used in place of a faction loadID for faction-less pawns (wild men, creepjoiners).</summary>
    public const int NoFactionBucket = -1;

    /// <summary>
    /// Session-only set of references that resolve to something on disk / in the DefDatabase but
    /// then fail to realise, so we don't retry (and re-log) on every generated pawn. Missing saved
    /// files are handled separately via File.Exists so a file saved mid-session is still picked up.
    /// </summary>
    [Unsaved(false)]
    public HashSet<string> failedRefs = new();

    public ForcedIdeoGameComponent(Game game) { }

    public static ForcedIdeoGameComponent Current => Verse.Current.Game?.GetComponent<ForcedIdeoGameComponent>();

    /// <summary>Apply faction-level forced primary ideologies on new game and on load.</summary>
    public override void FinalizeInit()
    {
        if (!ModsConfig.IdeologyActive)
            return;
        ModCore.Debug($"ForcedIdeoGameComponent.FinalizeInit: checking {Find.FactionManager.AllFactionsListForReading.Count} factions ({FactionEdit.ActiveFactionEdits.Count} active faction edits).");
        foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            EnsurePrimaryIdeo(faction);
    }

    /// <summary>
    /// If the active preset forces a primary ideology for <paramref name="faction"/>'s def,
    /// realise it (per faction instance) and make it the faction's primary. Idempotent and cheap
    /// once realised, so it is safe to call from the pawn-generation hot path — this also covers
    /// factions that appear mid-game. The player's faction is never touched.
    /// </summary>
    public void EnsurePrimaryIdeo(Faction faction)
    {
        if (faction?.ideos == null || faction.IsPlayer)
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
    /// inactive, the reference can't be resolved, or it's unsafe to touch the loader right now.
    /// </summary>
    public Ideo GetOrInjectIdeo(Faction faction, ForcedIdeoSource source, string key)
    {
        if (!ModsConfig.IdeologyActive)
            return null;
        if (source == ForcedIdeoSource.FactionPrimary)
            return faction?.ideos?.PrimaryIdeo;
        if (string.IsNullOrEmpty(key))
            return null;

        string refKey = (faction?.loadID ?? NoFactionBucket) + ":" + source + ":" + key;

        // 1. Reuse the ideo we realised earlier if its id still resolves.
        if (refToIdeoId.TryGetValue(refKey, out int existingId))
        {
            Ideo existing = FindById(existingId);
            if (existing != null)
                return existing;
            // The ideo was garbage-collected (no holders left); drop the stale id and re-realise.
            refToIdeoId.Remove(refKey);
        }

        if (failedRefs.Contains(refKey))
            return null;

        // 2. Never drive the disk-based Scribe loader while another scribe operation is active —
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

            // Register with the faction so the ideology listing shows it under the faction,
            // vanilla's own pawn-gen weighting can use it, and faction membership blocks GC.
            if (faction?.ideos != null && !faction.ideos.Has(ideo))
                faction.ideos.IdeosMinorListForReading.Add(ideo);

            ModCore.Log($"Realised forced ideology '{ideo.name}' from {source} '{key}' for faction '{faction?.Name ?? "<none>"}' (id {ideo.id}).");
            return ideo;
        }
        finally
        {
            Rand.PopState();
        }
    }

    /// <summary>Loads a fully-specified ideology from a <c>.rid</c> file. Deterministic.</summary>
    public Ideo LoadFromFile(string fileName, string refKey)
    {
        string path = GenFilePaths.AbsPathForIdeo(fileName);
        if (!File.Exists(path))
            return null; // Not saved (yet). Stay silent so a file created later is picked up.

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

        // Mirror Page_ChooseIdeoPreset.DoPreset: ensure a structure meme is present before generating.
        List<MemeDef> memes = preset.memes.ToList();
        if (!memes.Any(m => m.category == MemeCategory.Structure)
            && DefDatabase<MemeDef>
                .AllDefsListForReading.Where(m => m.category == MemeCategory.Structure && IdeoUtility.IsMemeAllowedFor(m, fac))
                .TryRandomElement(out MemeDef structure))
        {
            memes.Add(structure);
        }

        return IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(fac, forcedMemes: memes, classicExtra: preset.classicPlus, forceNoWeaponPreference: true));
    }

    public static Ideo FindById(int id)
    {
        List<Ideo> ideos = Find.IdeoManager.IdeosListForReading;
        for (int i = 0; i < ideos.Count; i++)
            if (ideos[i].id == id)
                return ideos[i];
        return null;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref refToIdeoId, "refToIdeoId", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            refToIdeoId ??= new Dictionary<string, int>();
    }
}
