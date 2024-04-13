using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionLoadout
{
    [HotSwappable]
    public class InventoryOptionEdit : IExposable
    {
        public static int GetSize(InventoryOptionEdit option)
        {
            if (option == null)
                return 0;

            return option.GetSize();
        }

        public static int GetSize(PawnInventoryOption option)
        {
            if (option == null)
                return 0;

            int count = option.thingDef != null ? 1 : 0;
            if(option.subOptionsTakeAll != null)
            {
                foreach(var item in option.subOptionsTakeAll)
                {
                    count += GetSize(item);
                }
            }
            if (option.subOptionsChooseOne != null)
            {
                foreach (var item in option.subOptionsChooseOne)
                {
                    count += GetSize(item);
                }
            }
            return count;
        }

        public ThingDef Thing;
        public IntRange CountRange = IntRange.one;
        public float ChoiceChance = 1f;
        public float SkipChance;

        public string BufferA, BufferB;

        public List<InventoryOptionEdit> SubOptionsTakeAll;
        public List<InventoryOptionEdit> SubOptionsChooseOne;

        public InventoryOptionEdit()
        {
            Thing = ThingDefOf.WoodLog;
        }

        public InventoryOptionEdit(PawnInventoryOption option) : this()
        {
            if (option == null)
                return;

            this.Thing = option.thingDef;
            this.CountRange = option.countRange;
            this.ChoiceChance = option.choiceChance;
            this.SkipChance = option.skipChance;

            if(option.subOptionsTakeAll?.Count > 0)
            {
                SubOptionsTakeAll = new List<InventoryOptionEdit>();
                foreach(var thing in option.subOptionsTakeAll)
                {
                    SubOptionsTakeAll.Add(new InventoryOptionEdit(thing));
                }
            }
            if (option.subOptionsChooseOne?.Count > 0)
            {
                SubOptionsChooseOne = new List<InventoryOptionEdit>();
                foreach (var thing in option.subOptionsChooseOne)
                {
                    SubOptionsChooseOne.Add(new InventoryOptionEdit(thing));
                }
            }
        }

        public PawnInventoryOption ConvertToVanilla()
        {
            var obj = new PawnInventoryOption();
            obj.thingDef = Thing;
            obj.choiceChance = ChoiceChance;
            obj.skipChance = SkipChance;
            obj.countRange = CountRange;

            if (SubOptionsTakeAll?.Count > 0)
            {
                obj.subOptionsTakeAll = new List<PawnInventoryOption>();
                foreach (var item in SubOptionsTakeAll)
                    obj.subOptionsTakeAll.Add(item.ConvertToVanilla());
            }

            if (SubOptionsChooseOne?.Count > 0)
            {
                obj.subOptionsChooseOne = new List<PawnInventoryOption>();
                foreach (var item in SubOptionsChooseOne)
                    obj.subOptionsChooseOne.Add(item.ConvertToVanilla());
            }

            return obj;
        }

        public int GetSize()
        {
            int size = Thing != null ? 1 : 0;
            if(SubOptionsChooseOne != null)
            {
                foreach(var item in SubOptionsChooseOne)
                {
                    size += item.GetSize();
                }
            }
            if (SubOptionsTakeAll != null)
            {
                foreach (var item in SubOptionsTakeAll)
                {
                    size += item.GetSize();
                }
            }
            return size;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref Thing, "thing");
            Scribe_Values.Look(ref CountRange, "count");
            Scribe_Values.Look(ref ChoiceChance, "choiceChance");
            Scribe_Values.Look(ref SkipChance, "skipChance");

            Scribe_Collections.Look(ref SubOptionsTakeAll, "takeAll", LookMode.Deep);
            Scribe_Collections.Look(ref SubOptionsChooseOne, "takeOne", LookMode.Deep);
        }
    }
}
