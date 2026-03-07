using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using Verse;

namespace FactionLoadout;

/// <summary>One item in a conditional inventory consequence.</summary>
public class ConditionalInventoryItem : IExposable, IDeepCopyable<ConditionalInventoryItem>
{
    /// <summary>DefName of the ThingDef to give. Stored as string so save files survive mod removal.</summary>
    public string ThingDefName;

    public IntRange CountRange = IntRange.One;
    public QualityCategory? Quality;

    /// <summary>Resolved at PostLoadInit. Null if the mod providing this def is not loaded.</summary>
    [Unsaved]
    public ThingDef ResolvedThing;

    /// <summary>UI text buffers for count range fields (min/max). Not serialized.</summary>
    [Unsaved]
    public string BufferA;

    [Unsaved]
    public string BufferB;

    public void ExposeData()
    {
        Scribe_Values.Look(ref ThingDefName, "thing", null);
        Scribe_Values.Look(ref CountRange, "count", IntRange.One);
        Scribe_Values.Look(ref Quality, "quality");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            ResolvedThing = DefDatabase<ThingDef>.GetNamedSilentFail(ThingDefName);
    }

    public ConditionalInventoryItem DeepClone() =>
        new()
        {
            ThingDefName = ThingDefName,
            CountRange = CountRange,
            Quality = Quality,
            ResolvedThing = ResolvedThing,
        };
}

/// <summary>
/// A single "IF $trigger THEN $consequences" rule attached to a PawnKindEdit.
/// The trigger is a ThingDef present on the pawn at generation time (equipment, apparel,
/// or inventory). Consequences use the same selection model as the rest of TC
/// (AlwaysTake, RandomChance, FromPool1–4 via SpecRequirementEdit).
/// </summary>
public class ConditionalLoadoutRule : IExposable, IDeepCopyable<ConditionalLoadoutRule>
{
    /// <summary>DefName of the trigger ThingDef. Stored as string so save files survive mod removal.</summary>
    public string TriggerDefName;

    /// <summary>Resolved at PostLoadInit. Null if the triggering def's mod is not loaded.</summary>
    [Unsaved]
    public ThingDef ResolvedTrigger;

    /// <summary>Weapons/equipment to give when the trigger is present. Full selection model (pools etc.).</summary>
    public List<SpecRequirementEdit> ConsequenceWeapons;

    /// <summary>Apparel to give when the trigger is present. Full selection model.</summary>
    public List<SpecRequirementEdit> ConsequenceApparel;

    /// <summary>Inventory items to give when the trigger is present.</summary>
    public List<ConditionalInventoryItem> ConsequenceInventory;

    public void ExposeData()
    {
        Scribe_Values.Look(ref TriggerDefName, "trigger", null);
        Scribe_Collections.Look(ref ConsequenceWeapons, "weapons", LookMode.Deep);
        Scribe_Collections.Look(ref ConsequenceApparel, "apparel", LookMode.Deep);
        Scribe_Collections.Look(ref ConsequenceInventory, "inventory", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            ResolvedTrigger = DefDatabase<ThingDef>.GetNamedSilentFail(TriggerDefName);
    }

    public ConditionalLoadoutRule DeepClone() =>
        new()
        {
            TriggerDefName = TriggerDefName,
            ResolvedTrigger = ResolvedTrigger,
            ConsequenceWeapons = ConsequenceWeapons?.Select(s => s.DeepClone()).ToList(),
            ConsequenceApparel = ConsequenceApparel?.Select(s => s.DeepClone()).ToList(),
            ConsequenceInventory = ConsequenceInventory?.Select(i => i.DeepClone()).ToList(),
        };
}
