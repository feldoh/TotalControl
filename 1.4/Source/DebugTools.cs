using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionLoadout;

public class DebugTools
{
    [DebugAction("Spawning", "Spawn Faction Pawn", false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
    private static List<DebugActionNode> SpawnFactionPawn()
    {
        List<DebugActionNode> debugActionNodeList = [];
        // Get all factions
        foreach (Faction faction in Find.FactionManager.AllFactions)
        {
            DebugActionNode debugActionNode = new(faction.def.defName, DebugActionType.ToolMap);
            foreach (PawnKindDef pawnKindDef in faction.def.GetKindDefs().OrderBy(kd => kd.defName))
            {
                debugActionNode.AddChild(new DebugActionNode(pawnKindDef.defName, DebugActionType.ToolMap)
                {
                    category = DebugToolsSpawning.GetCategoryForPawnKind(pawnKindDef),
                    action = () =>
                    {
                        Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, faction);
                        GenSpawn.Spawn(pawn, UI.MouseCell(), Find.CurrentMap);
                        DebugToolsSpawning.PostPawnSpawn(pawn);
                    }
                });
            }

            debugActionNodeList.Add(debugActionNode);
        }

        return debugActionNodeList;
    }
}
