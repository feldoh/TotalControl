using System.Collections.Generic;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using Verse;

namespace FactionLoadout
{
    [HotSwappable]
    public class InventoryOptionEdit() : IExposable, IDeepCopyable<InventoryOptionEdit>
    {
        public static int GetSize(InventoryOptionEdit option)
        {
            return option?.GetSize() ?? 0;
        }

        public static int GetSize(PawnInventoryOption option)
        {
            if (option == null)
                return 0;

            int count = option.thingDef != null ? 1 : 0;
            if (option.subOptionsTakeAll != null)
            {
                count += option.subOptionsTakeAll.Sum(GetSize);
            }
            if (option.subOptionsChooseOne != null)
            {
                count += option.subOptionsChooseOne.Sum(GetSize);
            }
            return count;
        }

        public ThingDef Thing = ThingDefOf.WoodLog;
        public IntRange CountRange = IntRange.One;
        public float ChoiceChance = 1f;
        public float SkipChance;

        public string BufferA,
            BufferB;

        public List<InventoryOptionEdit> SubOptionsTakeAll;
        public List<InventoryOptionEdit> SubOptionsChooseOne;

        public InventoryOptionEdit DeepClone() =>
            new()
            {
                Thing = Thing,
                CountRange = CountRange,
                ChoiceChance = ChoiceChance,
                SkipChance = SkipChance,
                SubOptionsTakeAll = SubOptionsTakeAll?.Select(o => o.DeepClone()).ToList(),
                SubOptionsChooseOne = SubOptionsChooseOne?.Select(o => o.DeepClone()).ToList(),
            };

        public InventoryOptionEdit(PawnInventoryOption option)
            : this()
        {
            if (option == null)
                return;

            Thing = option.thingDef;
            CountRange = option.countRange;
            ChoiceChance = option.choiceChance;
            SkipChance = option.skipChance;

            SubOptionsTakeAll = option.subOptionsTakeAll is { Count: > 0 } optsTakeAll ? optsTakeAll.Select(x => new InventoryOptionEdit(x)).ToList() : null;
            SubOptionsChooseOne = option.subOptionsChooseOne is { Count: > 0 } optsChooseOne ? optsChooseOne.Select(x => new InventoryOptionEdit(x)).ToList() : null;
        }

        public PawnInventoryOption ConvertToVanilla() =>
            new()
            {
                thingDef = Thing,
                choiceChance = ChoiceChance,
                skipChance = SkipChance,
                countRange = CountRange,
                subOptionsTakeAll = SubOptionsTakeAll is { Count: > 0 } ? SubOptionsTakeAll.Select(o => o.ConvertToVanilla()).ToList() : null,
                subOptionsChooseOne = SubOptionsChooseOne is { Count: > 0 } ? SubOptionsChooseOne.Select(o => o.ConvertToVanilla()).ToList() : null,
            };

        public int GetSize()
        {
            int size = Thing != null ? 1 : 0;
            if (SubOptionsChooseOne != null)
            {
                size += SubOptionsChooseOne.Sum(item => item.GetSize());
            }
            if (SubOptionsTakeAll != null)
            {
                size += SubOptionsTakeAll.Sum(item => item.GetSize());
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
