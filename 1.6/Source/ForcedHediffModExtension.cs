using System.Collections.Generic;
using Verse;

namespace FactionLoadout;

public class ForcedHediffModExtension : ForcedExtrasModExtension;

public class ForcedExtrasModExtension : DefModExtension
{
    public List<ForcedHediff> forcedHediffs = [];
    public List<ForcedGene> forcedGenes = [];
}
