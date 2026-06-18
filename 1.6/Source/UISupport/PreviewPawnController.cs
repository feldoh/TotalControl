using System;
using System.Linq;
using FactionLoadout.Patches;
using FactionLoadout.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>
/// Owns the live preview pawn for the Pawn Edit screen. Generates the pawn through the
/// REAL generation pipeline (so the always-active apparel/weapon/hediff Harmony patches
/// apply the edits exactly as they would for a raid — "match real raids"), debounces
/// regeneration so dragging a slider doesn't regenerate every frame, and disposes the
/// throwaway pawn safely so it can never leak into the save.
/// </summary>
[HotSwappable]
public class PreviewPawnController
{
    public readonly FactionEdit ParentFaction;
    public PawnKindDef TargetKind;

    public Pawn PreviewPawn { get; private set; }
    public bool PreviewFailed { get; private set; }

    /// <summary>Mirrors the old FactionEditUI "Thing ID Patch" toggle (off by default).</summary>
    public bool UseThingIDPatch;

    private bool dirty;
    private int frameCounter;
    private int dirtyAtFrame;
    private const int DebounceFrames = 20;

    public bool InGame => Verse.Current.Game != null;

    public PreviewPawnController(FactionEdit parentFaction, PawnKindDef targetKind)
    {
        ParentFaction = parentFaction;
        TargetKind = targetKind;
        // Generate immediately on the first Tick.
        dirty = true;
        dirtyAtFrame = -DebounceFrames;
    }

    /// <summary>An option changed; regenerate after a short idle (debounced).</summary>
    public void NotifyEditChanged()
    {
        dirty = true;
        dirtyAtFrame = frameCounter;
        PreviewFailed = false;
    }

    /// <summary>Force an immediate fresh roll (also re-rolls randomized pools/chances).</summary>
    public void RequestRegenerate()
    {
        PreviewFailed = false;
        Regenerate();
    }

    public void MarkFailed() => PreviewFailed = true;

    /// <summary>Call once per frame from the screen.</summary>
    public void Tick()
    {
        frameCounter++;
        if (!InGame)
            return;
        if (dirty && !PreviewFailed && frameCounter - dirtyAtFrame >= DebounceFrames)
            Regenerate();
    }

    private void Regenerate()
    {
        dirty = false;
        if (!InGame || TargetKind == null)
        {
            PreviewFailed = TargetKind == null;
            return;
        }

        DiscardPawn(PreviewPawn);
        PreviewPawn = null;

        try
        {
            PreviewPawn = GeneratePreviewPawn();
            PreviewFailed = PreviewPawn == null;
            if (PreviewPawn != null)
                PortraitsCache.SetDirty(PreviewPawn);
        }
        catch (Exception e)
        {
            ModCore.Error($"Failed to generate preview pawn for '{TargetKind?.LabelCap}'.", e);
            PreviewPawn = null;
            PreviewFailed = true;
        }
    }

    private Pawn GeneratePreviewPawn()
    {
        FactionDef toClone = FactionEdit.TryGetOriginal(ParentFaction.Faction.Def.defName) ?? ParentFaction.Faction.Def;
        FactionDef clonedFac = CloningUtility.Clone(toClone);
        clonedFac.defName = ParentFaction.Faction.Def.defName;
        clonedFac.humanlikeFaction = ParentFaction.Faction.Def.humanlikeFaction;
        // The "TEMP FACTION CLONE" prefix is load-bearing — PawnKindEdit.GetEditsFor
        // special-cases it so the edits resolve for this throwaway faction.
        clonedFac.fixedName = $"TEMP FACTION CLONE ({clonedFac.defName})";

        ParentFaction.Apply(clonedFac, false);

        Faction faction = new()
        {
            def = clonedFac,
            loadID = -1,
            colorFromSpectrum = Rand.Range(0f, 1f),
            hidden = true,
            ideos = Find.FactionManager?.FirstFactionOfDef(ParentFaction.Faction.Def)?.ideos,
            Name = clonedFac.fixedName,
            relations = Find
                .FactionManager.AllFactionsVisible.Select(otherFaction => new FactionRelation
                {
                    other = otherFaction,
                    baseGoodwill = 0,
                    kind = FactionRelationKind.Neutral,
                })
                .ToList(),
            temporary = true,
            deactivated = true,
        };

        ThingIDPatch.Active = UseThingIDPatch;
        IdeoUtilityPatch.Active = true;
        FactionUtilityPawnGenPatch.Active = true;

        try
        {
            return PawnGenerator.GeneratePawn(
                new PawnGenerationRequest(TargetKind, faction)
                {
                    ForceGenerateNewPawn = true,
                    AllowDowned = false,
                    AllowDead = false,
                    CanGeneratePawnRelations = false,
                    RelationWithExtraPawnChanceFactor = 0,
                    ColonistRelationChanceFactor = 0,
                    ForceNoIdeo = true,
                    ForbidAnyTitle = true,
                }
            );
        }
        finally
        {
            Find.FactionManager.Remove(faction);
            ThingIDPatch.Active = false;
            FactionLeaderPatch.Active = false;
            FactionUtilityPawnGenPatch.Active = false;
            IdeoUtilityPatch.Active = false;
        }
    }

    public void Dispose()
    {
        DiscardPawn(PreviewPawn);
        PreviewPawn = null;
    }

    /// <summary>
    /// Save-contamination guard (same pattern as the old FactionEditUI.DestroyPawns):
    /// never keep the throwaway pawn — discard it, stripping dangling references.
    /// </summary>
    private static void DiscardPawn(Pawn pawn)
    {
        if (pawn == null)
            return;

        if (Find.WorldPawns?.Contains(pawn) == true)
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
        else if (!pawn.Discarded)
            Find.WorldPawns?.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
    }
}
