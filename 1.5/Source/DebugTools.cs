using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace FactionLoadout;

public class DebugTools
{
    private static void DoTableInternalWeapons(string tag)
    {
        DebugTables.MakeTablesDialog(
            DefDatabase<ThingDef>.AllDefs.Where(td => td.weaponTags?.Contains(tag) ?? false).OrderBy(d => d.modContentPack?.Name ?? "Core"),
            new TableDataGetter<ThingDef>("defName", d => d.defName),
            new TableDataGetter<ThingDef>("name", d => d.LabelCap),
            new TableDataGetter<ThingDef>("source", d => d.modContentPack?.Name ?? "Core"),
            new TableDataGetter<ThingDef>("tags", d => GenText.ToSpaceList(d.weaponTags.Select(t => t.ToString())))
        );
    }

    [DebugOutput("Weapons", name = "Weapons for tag")]
    public static void WeaponsByTag()
    {
        Find.WindowStack.Add(
            new FloatMenu(
                DefDatabase<ThingDef>
                    .AllDefs.Where(td => td.weaponTags != null)
                    .SelectMany(t => t.weaponTags)
                    .Distinct()
                    .OrderBy(tagName => tagName)
                    .Select(tag => new FloatMenuOption(tag, () => DoTableInternalWeapons(tag)))
                    .ToList()
            )
        );
    }

    [DebugAction("Spawning", "Spawn Faction Pawn", false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
    private static List<DebugActionNode> SpawnFactionPawn()
    {
        List<DebugActionNode> debugActionNodeList = [];
        // Get all factions
        foreach (Faction faction in Find.FactionManager.AllFactions)
        {
            DebugActionNode debugActionNode = new(faction.def.defName, DebugActionType.ToolMap);
            foreach (PawnKindDef pawnKindDef in faction.def.GetKindDefs().OrderBy(kd => kd.defName))
            {
                debugActionNode.AddChild(
                    new DebugActionNode(pawnKindDef.defName, DebugActionType.ToolMap)
                    {
                        category = DebugToolsSpawning.GetCategoryForPawnKind(pawnKindDef),
                        action = () =>
                        {
                            Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, faction);
                            foreach (XenotypeChance xenotypeSetXenotypeChance in faction.def.xenotypeSet.xenotypeChances)
                            {
                                ModCore.Log($"{xenotypeSetXenotypeChance.xenotype.defName} chance: {xenotypeSetXenotypeChance.chance}");
                            }

                            // gen 100 times and print % of each defname
                            Dictionary<string, int> xenotypeCount = new();
                            for (int i = 0; i < 100; i++)
                            {
                                faction.def.xenotypeSet.xenotypeChances.TryRandomElementByWeight(x => x.chance, out XenotypeChance xenotypeChance);
                                if (xenotypeCount.ContainsKey(xenotypeChance.xenotype.defName))
                                {
                                    xenotypeCount[xenotypeChance.xenotype.defName]++;
                                }
                                else
                                {
                                    xenotypeCount[xenotypeChance.xenotype.defName] = 1;
                                }
                            }

                            GenSpawn.Spawn(pawn, UI.MouseCell(), Find.CurrentMap);
                            DebugToolsSpawning.PostPawnSpawn(pawn);
                        }
                    }
                );
            }

            debugActionNodeList.Add(debugActionNode);
        }

        return debugActionNodeList;
    }
}
