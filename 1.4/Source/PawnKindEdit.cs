using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public class PawnKindEdit : IExposable
    {
        private static Dictionary<PawnKindDef, List<PawnKindEdit>> activeEdits = new Dictionary<PawnKindDef, List<PawnKindEdit>>();

        public static IEnumerable<PawnKindEdit> GetEditsFor(PawnKindDef def)
        {
            if (def == null)
                yield break;

            if(activeEdits.TryGetValue(def, out var list))            
                foreach (var item in list)
                    yield return item;            
        }

        private static void AddActiveEdit(PawnKindDef def, PawnKindEdit edit)
        {
            if (def == null || edit == null)
                return;

            if(!activeEdits.TryGetValue(def, out var list))
            {
                list = new List<PawnKindEdit>();
                activeEdits.Add(def, list);
            }

            if(!list.Contains(edit))
                list.Add(edit);
        }

        public PawnKindDef Def;
        public bool IsGlobal = false;

        public FactionEdit ParentEdit
        {
            get
            {
                foreach(var preset in Preset.LoadedPresets)
                {
                    foreach(var change in preset.factionChanges)
                    {
                        if (change.KindEdits.Contains(this))
                            return change;
                    }
                }
                return null;
            }
        }

        public PawnKindDef ReplaceWith = null;
        public bool ForceNaked = false;
        public QualityCategory? ItemQuality = null;
        public float? BiocodeWeaponChance = null;
        public float? TechHediffChance = null;
        public int? TechHediffsMaxAmount = null;
        public List<string> TechHediffTags = null;
        public List<string> TechHediffDisallowedTags = null;
        public List<string> WeaponTags = null;
        public List<string> ApparelTags = null;
        public List<string> ApparelDisallowedTags = null;
        public List<ThingDef> ApparelRequired = null;
        public List<ThingDef> TechRequired = null;
        public List<SpecRequirementEdit> SpecificApparel = null;
        public List<SpecRequirementEdit> SpecificWeapons = null;
        public FloatRange? ApparelMoney = null;
        public FloatRange? TechMoney = null;
        public FloatRange? WeaponMoney = null;
        public InventoryOptionEdit Inventory = null;
        public bool ReplaceDefaultInventory = true;
        public QualityCategory? ForcedWeaponQuality = null;
        public Color? ApparelColor = null;
        public string Label = null;
        public ThingDef Race = null;
        public List<HairDef> CustomHair = null;
        public List<Color> CustomHairColors = null;
        public bool DeletedOrClosed;

        private PawnKindEdit globalEdit = null;

        public PawnKindEdit() { }

        public PawnKindEdit(PawnKindDef def)
        {
            Def = def;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref Def, "def");
            Scribe_Defs.Look(ref ReplaceWith, "replaceWith");
            Scribe_Values.Look(ref ForceNaked, "forceNaked");
            Scribe_Values.Look(ref ItemQuality, "itemQuality");
            Scribe_Values.Look(ref TechHediffChance, "techHediffChance");
            Scribe_Values.Look(ref TechHediffsMaxAmount, "techHediffsMaxAmount");
            Scribe_Collections.Look(ref TechHediffDisallowedTags, "techHediffDisallowedTags");
            Scribe_Collections.Look(ref TechHediffTags, "techHediffTags");
            Scribe_Values.Look(ref BiocodeWeaponChance, "biocodeWeaponChance");
            Scribe_Values.Look(ref ApparelMoney, "apparelMoney");
            Scribe_Values.Look(ref TechMoney, "techMoney");
            Scribe_Values.Look(ref WeaponMoney, "weaponMoney");
            Scribe_Collections.Look(ref WeaponTags, "weaponTags");
            Scribe_Collections.Look(ref ApparelTags, "apparelTags");
            Scribe_Collections.Look(ref ApparelDisallowedTags, "apparelDisallowedTags");
            Scribe_Collections.Look(ref ApparelRequired, "apparelRequired", LookMode.Def);
            Scribe_Collections.Look(ref TechRequired, "techRequired", LookMode.Def);
            Scribe_Collections.Look(ref SpecificApparel, "specificApparel", LookMode.Deep);
            Scribe_Collections.Look(ref SpecificWeapons, "specificWeapons", LookMode.Deep);
            Scribe_Deep.Look(ref Inventory, "inventory");
            Scribe_Values.Look(ref IsGlobal, "isGlobal");
            Scribe_Values.Look(ref ReplaceDefaultInventory, "replaceDefaultInventory");
            Scribe_Values.Look(ref ForcedWeaponQuality, "forcedWeaponQuality");
            Scribe_Values.Look(ref ApparelColor, "apparelColor");
            Scribe_Values.Look(ref Label, "label");
            Scribe_Defs.Look(ref Race, "race");
            Scribe_Collections.Look(ref CustomHair, "customHair", LookMode.Def);
            Scribe_Collections.Look(ref CustomHairColors, "customHairColors");
        }

        private void ReplaceMaybe<T>(ref T field, T maybe) where T : class
        {
            if (maybe == null)
                return;

            field = maybe;
        }

        private void ReplaceMaybe<T>(ref T field, T? maybe) where T : struct
        {
            if (maybe == null)
                return;

            field = maybe.Value;
        }

        private void ReplaceMaybe<T>(ref T? field, T? maybe) where T : struct
        {
            if (maybe == null)
                return;

            field = maybe.Value;
        }

        private void ReplaceMaybeList<T>(ref T field, T maybe, bool tryAdd) where T : IList, new()
        {
            if (maybe == null)
                return;

            if(tryAdd && field != null)
            {                
                foreach (var value in maybe)
                {
                    if (!field.Contains(value))
                        field.Add(value);
                }
            }
            else
            {
                field = new T();
                foreach (var value in maybe)
                    field.Add(value);
            }
        }

        private void ReplaceMaybe(ref PawnInventoryOption inv, InventoryOptionEdit maybe)
        {
            if (maybe == null)
                return;

            if (globalEdit?.Inventory != null || (IsGlobal && !ReplaceDefaultInventory))
            {
                if(inv == null)
                {
                    inv = maybe.ConvertToVanilla();
                }
                else
                {
                    var vanilla = maybe.ConvertToVanilla();
                    if(vanilla.subOptionsTakeAll != null)
                        inv.subOptionsTakeAll.AddRange(vanilla.subOptionsTakeAll);
                    if (vanilla.subOptionsChooseOne != null)
                        inv.subOptionsChooseOne.AddRange(vanilla.subOptionsChooseOne);
                }
            }
            else
            {
                inv = maybe.ConvertToVanilla();
            }
        }

        public PawnKindDef Apply(PawnKindDef def, PawnKindEdit global, bool addToEdits = true)
        {
            if (def == null)
                return null;

            if (addToEdits)
                AddActiveEdit(def, this);

            if (ReplaceWith != null)
                return ReplaceWith;

            // Only human-likes can have race replaced.
            if (def.RaceProps.Animal)
                Race = null;

            globalEdit = global;

            ReplaceMaybe(ref def.itemQuality, ItemQuality);
            ReplaceMaybe(ref def.biocodeWeaponChance, BiocodeWeaponChance);
            ReplaceMaybe(ref def.techHediffsChance, TechHediffChance);
            ReplaceMaybe(ref def.techHediffsMaxAmount, TechHediffsMaxAmount);           
            ReplaceMaybe(ref def.apparelMoney, ApparelMoney);
            ReplaceMaybe(ref def.techHediffsMoney, TechMoney);
            ReplaceMaybe(ref def.weaponMoney, WeaponMoney);
            ReplaceMaybe(ref def.inventoryOptions, Inventory);
            ReplaceMaybe(ref def.forceWeaponQuality, ForcedWeaponQuality);
            ReplaceMaybe(ref def.label, Label);
            ReplaceMaybe(ref def.race, Race);

            ReplaceMaybeList(ref def.techHediffsTags, TechHediffTags, global?.TechHediffTags != null);
            ReplaceMaybeList(ref def.techHediffsDisallowTags, TechHediffDisallowedTags, global?.TechHediffDisallowedTags != null);
            ReplaceMaybeList(ref def.weaponTags, WeaponTags, global?.WeaponTags != null);
            ReplaceMaybeList(ref def.apparelTags, ApparelTags, global?.ApparelTags != null);
            ReplaceMaybeList(ref def.apparelDisallowTags, ApparelDisallowedTags, global?.ApparelDisallowedTags != null);
            ReplaceMaybeList(ref def.apparelRequired, ApparelRequired, global?.ApparelRequired != null);
            ReplaceMaybeList(ref def.techHediffsRequired, TechRequired, global?.TechRequired != null);

            bool removeSpecific = ApparelRequired != null || SpecificApparel != null;
            if (removeSpecific)
                def.specificApparelRequirements = null;

            // Can't be done like this. Disabled for now.
            if (Race != null)
            {
                // Try find life stages of new race.
                var realKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k != def && k.defName != def.defName && k.race == Race);
                if (realKind != null)
                    def.lifeStages = realKind.lifeStages;
            }

            // Special case: color cannot be pure white, because Rimworld will then ignore it.
            // If color is not null and is pure white, change it to a slightly off-white.
            Color? color = ApparelColor;
            if (color != null && color == Color.white)
                color = new Color(0.995f, 0.995f, 0.995f, 1f);
            ReplaceMaybe(ref def.apparelColor, color);

            globalEdit = null;
            return def;
        }

        public bool AppliesTo(PawnKindDef def)
        {
            return def != null && Def.defName == def.defName;
        }
    }
}
