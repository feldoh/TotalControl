using RimWorld;
using Verse;

namespace FactionLoadout;

public class SpecialThingFilterWorker_PlantFood : SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return AlwaysMatches(t.def);
    }

    public override bool AlwaysMatches(ThingDef def)
    {
        return def.ingestible != null && (def.ingestible.foodType & FoodTypeFlags.Plant) > FoodTypeFlags.None;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return AlwaysMatches(def);
    }
}
