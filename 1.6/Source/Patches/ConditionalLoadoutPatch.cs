using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionLoadout.Patches;

/// <summary>
/// Pre-split consequence arrays built at Apply() time.
/// Splitting by SelectionMode at cache-build time means zero categorisation work per pawn.
/// </summary>
public struct ResolvedConditionalRule
{
    public SpecRequirementEdit[] WeaponsAlways;
    public SpecRequirementEdit[] WeaponsChance;
    public SpecRequirementEdit[] WeaponsPool1;
    public SpecRequirementEdit[] WeaponsPool2;
    public SpecRequirementEdit[] WeaponsPool3;
    public SpecRequirementEdit[] WeaponsPool4;

    public SpecRequirementEdit[] ApparelAlways;
    public SpecRequirementEdit[] ApparelChance;
    public SpecRequirementEdit[] ApparelPool1;
    public SpecRequirementEdit[] ApparelPool2;
    public SpecRequirementEdit[] ApparelPool3;
    public SpecRequirementEdit[] ApparelPool4;

    public ConditionalInventoryItem[] Inventory;
}

/// <summary>
/// Harmony Postfix on PawnGenerator.GenerateNewPawnInternal.
///
/// Fires after all other TC patches (WeaponGenPatch, ApparelGenPatch, PawnGenPatchCore),
/// so pawn.equipment, pawn.apparel, and pawn.inventory.innerContainer are all fully
/// populated. Implements per-pawnkind "IF $trigger THEN $consequences" rules.
///
/// Performance: O(1) early exit via ConditionalIndex.TryGetValue for the vast majority
/// of pawns whose kindDef has no configured conditional rules.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
public static class ConditionalLoadoutPatch
{
    /// <summary>
    /// Outer key: cloned PawnKindDef (already faction-aware — TC clones per faction, so
    /// each key is a distinct (faction × kindDef) pair). Inner key: trigger ThingDef.
    /// Built at Apply() time; cleared in RemoveActiveEdits().
    /// </summary>
    public static Dictionary<PawnKindDef, Dictionary<ThingDef, ResolvedConditionalRule>> ConditionalIndex = new();

    // Static reuse lists — same pattern as WeaponGenPatch / ApparelGenPatch.
    // Cleared and re-populated per pawn that passes the early-exit check.
    private static readonly List<SpecRequirementEdit> wAlways = [];
    private static readonly List<SpecRequirementEdit> wChance = [];
    private static readonly List<SpecRequirementEdit> wPool1 = [];
    private static readonly List<SpecRequirementEdit> wPool2 = [];
    private static readonly List<SpecRequirementEdit> wPool3 = [];
    private static readonly List<SpecRequirementEdit> wPool4 = [];

    private static readonly List<SpecRequirementEdit> aAlways = [];
    private static readonly List<SpecRequirementEdit> aChance = [];
    private static readonly List<SpecRequirementEdit> aPool1 = [];
    private static readonly List<SpecRequirementEdit> aPool2 = [];
    private static readonly List<SpecRequirementEdit> aPool3 = [];
    private static readonly List<SpecRequirementEdit> aPool4 = [];

    // Static set reused per pawn — avoids heap pressure on large raids.
    private static readonly HashSet<ThingDef> triggeredDefs = new();

    [HarmonyPostfix]
    public static void Postfix(Pawn __result)
    {
        if (__result == null)
            return;

        // O(1) early exit — the vast majority of pawns stop here.
        if (!ConditionalIndex.TryGetValue(__result.kindDef, out Dictionary<ThingDef, ResolvedConditionalRule> rulesByTrigger))
            return;

        triggeredDefs.Clear();

        // Collect every trigger def that is present on this pawn.
        if (__result.equipment != null)
        {
            foreach (ThingWithComps eq in __result.equipment.AllEquipmentListForReading)
            {
                if (eq?.def != null && rulesByTrigger.ContainsKey(eq.def))
                    triggeredDefs.Add(eq.def);
            }
        }

        if (__result.apparel != null)
        {
            foreach (Apparel ap in __result.apparel.WornApparel)
            {
                if (ap?.def != null && rulesByTrigger.ContainsKey(ap.def))
                    triggeredDefs.Add(ap.def);
            }
        }

        foreach (Thing t in __result.inventory.innerContainer)
        {
            if (t?.def != null && rulesByTrigger.ContainsKey(t.def))
                triggeredDefs.Add(t.def);
        }

        if (triggeredDefs.Count == 0)
            return;

        // Accumulate consequences from all triggered rules into the static lists.
        wAlways.Clear();
        wChance.Clear();
        wPool1.Clear();
        wPool2.Clear();
        wPool3.Clear();
        wPool4.Clear();
        aAlways.Clear();
        aChance.Clear();
        aPool1.Clear();
        aPool2.Clear();
        aPool3.Clear();
        aPool4.Clear();

        List<ConditionalInventoryItem> inventoryItems = null;

        foreach (ThingDef trigger in triggeredDefs)
        {
            ResolvedConditionalRule rule = rulesByTrigger[trigger];

            AddRange(wAlways, rule.WeaponsAlways);
            AddRange(wChance, rule.WeaponsChance);
            AddRange(wPool1, rule.WeaponsPool1);
            AddRange(wPool2, rule.WeaponsPool2);
            AddRange(wPool3, rule.WeaponsPool3);
            AddRange(wPool4, rule.WeaponsPool4);

            AddRange(aAlways, rule.ApparelAlways);
            AddRange(aChance, rule.ApparelChance);
            AddRange(aPool1, rule.ApparelPool1);
            AddRange(aPool2, rule.ApparelPool2);
            AddRange(aPool3, rule.ApparelPool3);
            AddRange(aPool4, rule.ApparelPool4);

            if (rule.Inventory is { Length: > 0 })
            {
                inventoryItems ??= new List<ConditionalInventoryItem>();
                inventoryItems.AddRange(rule.Inventory);
            }
        }

        // Apply weapon/equipment consequences.
        bool primarySet = __result.equipment?.Primary != null;
        foreach (SpecRequirementEdit spec in GetWhatToGiveWeapons())
        {
            if (spec.Thing == null)
                continue;

            ThingWithComps weapon;
            try
            {
                weapon = WeaponGenPatch.GenerateNewWeapon(__result, spec);
                if (weapon == null)
                    continue;
            }
            catch (Exception e)
            {
                ModCore.Error($"[Conditional] Exception generating weapon '{spec.Thing.LabelCap}'", e);
                continue;
            }

            if (weapon.def.equipmentType == EquipmentType.Primary && !primarySet)
            {
                __result.equipment?.AddEquipment(weapon);
                primarySet = true;
            }
            else
            {
                // Route additional primaries and all sidearms to inventory so sidearm mods pick them up.
                __result.inventory.innerContainer.TryAdd(weapon);
            }
        }

        // Apply apparel consequences.
        if (__result.apparel != null)
        {
            foreach (SpecRequirementEdit spec in GetWhatToGiveApparel(__result))
            {
                if (spec.Thing == null)
                    continue;

                Apparel app;
                try
                {
                    app = ApparelGenPatch.GenerateNewApparel(__result, spec);
                    if (app == null)
                        continue;
                }
                catch (Exception e)
                {
                    ModCore.Error($"[Conditional] Exception generating apparel '{spec.Thing.LabelCap}'", e);
                    continue;
                }

                __result.apparel.Wear(app, false);
            }
        }

        // Apply inventory consequences.
        if (inventoryItems != null)
        {
            foreach (ConditionalInventoryItem item in inventoryItems)
            {
                if (item.ResolvedThing == null)
                    continue;

                int count = item.CountRange.RandomInRange;
                if (count <= 0)
                    continue;

                Thing thing = ThingMaker.MakeThing(item.ResolvedThing);
                thing.stackCount = count;

                if (item.Quality != null)
                    thing.TryGetComp<CompQuality>()?.SetQuality(item.Quality.Value, ArtGenerationContext.Outsider);

                __result.inventory.innerContainer.TryAdd(thing);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Pool-selection helpers — replicate WeaponGenPatch / ApparelGenPatch logic
    // -------------------------------------------------------------------------

    private static IEnumerable<SpecRequirementEdit> GetWhatToGiveWeapons()
    {
        foreach (SpecRequirementEdit item in wAlways)
            yield return item;

        foreach (SpecRequirementEdit item in wChance)
        {
            if (Rand.Chance(item.SelectionChance))
                yield return item;
        }

        SpecRequirementEdit sel = wPool1.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (sel != null)
            yield return sel;

        sel = wPool2.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (sel != null)
            yield return sel;

        sel = wPool3.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (sel != null)
            yield return sel;

        sel = wPool4.RandomElementByWeightWithFallback(i => i.SelectionChance, null);
        if (sel != null)
            yield return sel;
    }

    private static IEnumerable<SpecRequirementEdit> GetWhatToGiveApparel(Pawn pawn)
    {
        foreach (SpecRequirementEdit item in aAlways)
            yield return item;

        foreach (SpecRequirementEdit item in aChance)
        {
            if (Rand.Chance(item.SelectionChance))
                yield return item;
        }

        // PawnCanWear filter on pool items — mirrors ApparelGenPatch.GetWhatToGive(pawn)
        SpecRequirementEdit sel = aPool1.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (sel != null)
            yield return sel;

        sel = aPool2.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (sel != null)
            yield return sel;

        sel = aPool3.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (sel != null)
            yield return sel;

        sel = aPool4.Where(a => a.Thing?.apparel?.PawnCanWear(pawn) ?? true).RandomElementByWeightWithFallback(i => i.SelectionChance);
        if (sel != null)
            yield return sel;
    }

    // -------------------------------------------------------------------------
    // Cache helpers called from PawnKindEdit.Apply() / RemoveActiveEdits()
    // -------------------------------------------------------------------------

    public static void BuildIndex(PawnKindDef def, List<ConditionalLoadoutRule> rules)
    {
        // Always clear stale entry first (handles re-apply scenarios).
        ConditionalIndex.Remove(def);

        if (rules == null || rules.Count == 0)
            return;

        Dictionary<ThingDef, ResolvedConditionalRule> rulesByTrigger = new();

        foreach (ConditionalLoadoutRule rule in rules)
        {
            if (rule.ResolvedTrigger == null)
                continue;

            ResolvedConditionalRule resolved = BuildResolved(rule);

            if (rulesByTrigger.TryGetValue(rule.ResolvedTrigger, out ResolvedConditionalRule existing))
                rulesByTrigger[rule.ResolvedTrigger] = MergeResolved(existing, resolved);
            else
                rulesByTrigger[rule.ResolvedTrigger] = resolved;
        }

        if (rulesByTrigger.Count > 0)
            ConditionalIndex[def] = rulesByTrigger;
    }

    public static void RemoveIndex(PawnKindDef def)
    {
        ConditionalIndex.Remove(def);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static ResolvedConditionalRule BuildResolved(ConditionalLoadoutRule rule)
    {
        return new ResolvedConditionalRule
        {
            WeaponsAlways = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.AlwaysTake),
            WeaponsChance = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.RandomChance),
            WeaponsPool1 = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.FromPool1),
            WeaponsPool2 = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.FromPool2),
            WeaponsPool3 = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.FromPool3),
            WeaponsPool4 = FilterMode(rule.ConsequenceWeapons, ApparelSelectionMode.FromPool4),
            ApparelAlways = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.AlwaysTake),
            ApparelChance = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.RandomChance),
            ApparelPool1 = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.FromPool1),
            ApparelPool2 = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.FromPool2),
            ApparelPool3 = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.FromPool3),
            ApparelPool4 = FilterMode(rule.ConsequenceApparel, ApparelSelectionMode.FromPool4),
            Inventory = rule.ConsequenceInventory?.ToArray() ?? [],
        };
    }

    private static SpecRequirementEdit[] FilterMode(List<SpecRequirementEdit> src, ApparelSelectionMode mode)
    {
        if (src == null || src.Count == 0)
            return [];
        return src.Where(s => s.SelectionMode == mode).ToArray();
    }

    private static ResolvedConditionalRule MergeResolved(ResolvedConditionalRule a, ResolvedConditionalRule b)
    {
        return new ResolvedConditionalRule
        {
            WeaponsAlways = Concat(a.WeaponsAlways, b.WeaponsAlways),
            WeaponsChance = Concat(a.WeaponsChance, b.WeaponsChance),
            WeaponsPool1 = Concat(a.WeaponsPool1, b.WeaponsPool1),
            WeaponsPool2 = Concat(a.WeaponsPool2, b.WeaponsPool2),
            WeaponsPool3 = Concat(a.WeaponsPool3, b.WeaponsPool3),
            WeaponsPool4 = Concat(a.WeaponsPool4, b.WeaponsPool4),
            ApparelAlways = Concat(a.ApparelAlways, b.ApparelAlways),
            ApparelChance = Concat(a.ApparelChance, b.ApparelChance),
            ApparelPool1 = Concat(a.ApparelPool1, b.ApparelPool1),
            ApparelPool2 = Concat(a.ApparelPool2, b.ApparelPool2),
            ApparelPool3 = Concat(a.ApparelPool3, b.ApparelPool3),
            ApparelPool4 = Concat(a.ApparelPool4, b.ApparelPool4),
            Inventory = Concat(a.Inventory, b.Inventory),
        };
    }

    private static T[] Concat<T>(T[] x, T[] y)
    {
        if (x == null || x.Length == 0)
            return y ?? [];
        if (y == null || y.Length == 0)
            return x;
        return x.Concat(y).ToArray();
    }

    private static void AddRange<T>(List<T> list, T[] arr)
    {
        if (arr != null && arr.Length > 0)
            list.AddRange(arr);
    }
}
