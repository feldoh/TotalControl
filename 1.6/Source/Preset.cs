using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FactionLoadout.Util;
using RimWorld;
using Verse;

namespace FactionLoadout
{
    public class Preset : IExposable
    {
        public static IReadOnlyList<Preset> LoadedPresets => loadedPresets;

        private static List<Preset> loadedPresets = new List<Preset>();

        public static void LoadAllPresets()
        {
            loadedPresets.Clear();
            var files = IO.ListXmlFiles(IO.SaveDataPath);
            foreach (var file in files)
            {
                try
                {
                    var preset = new Preset();
                    IO.LoadFromFile(preset, file.FullName);
                    loadedPresets.Add(preset);
                }
                catch (Exception e)
                {
                    ModCore.Error($"Failed to load preset from '{file.FullName}'", e);
                }
            }
        }

        public static void AddNewPreset(Preset preset)
        {
            if (preset == null || loadedPresets.Contains(preset))
                return;

            loadedPresets.Add(preset);
        }

        public static void DeletePreset(Preset preset)
        {
            if (preset == null || !loadedPresets.Contains(preset))
                return;

            loadedPresets.Remove(preset);
            try
            {
                preset.DeleteFile();
            }
            catch (Exception e)
            {
                ModCore.Error($"Failed to delete preset file for {preset.Name} ({preset.GUID})", e);
            }
        }

        public static string SpecialCreepjoinerFactionDefName = "FactionLoadout_Special_CreepJoiner";
        public static string SpecialWildManFactionDefName = "FactionLoadout_Special_WildMan";
        public static string SpecialFactionlessPawnsFactionDefName = "FactionLoadout_Special_Factionless";

        public static FactionDef SpecialCreepjoinerFaction = new()
        {
            hidden = true,
            defName = SpecialCreepjoinerFactionDefName,
            label = "Special CreepJoiner",
            description = "This is a special faction that is used to edit a faux CreepJoiner faction.",
            humanlikeFaction = true,
            raidsForbidden = true,
            requiredCountAtGameStart = 0,
            pawnGroupMakers =
            [
                new PawnGroupMaker
                {
                    kindDef = PawnGroupKindDefOf.Combat,
                    options = DefDatabase<CreepJoinerFormKindDef>.AllDefsListForReading.Select(creepKind => new PawnGenOption { kind = creepKind }).ToList(),
                },
            ],
        };

        public static FactionDef SpecialWildManFaction = new()
        {
            hidden = true,
            defName = SpecialWildManFactionDefName,
            label = "Special WildMan",
            description = "This is a special faction that is used to edit a faux WildMan faction.",
            humanlikeFaction = true,
            raidsForbidden = true,
            requiredCountAtGameStart = 0,
            basicMemberKind = PawnKindDefOf.WildMan,
        };

        public static FactionDef SpecialFactionlessPawnsFaction = new()
        {
            hidden = true,
            defName = SpecialFactionlessPawnsFactionDefName,
            label = "Factionless Pawns",
            description = "A special group for editing humanlike pawnkinds that don't belong to any faction. Populated automatically at startup.",
            humanlikeFaction = true,
            raidsForbidden = true,
            requiredCountAtGameStart = 0,
        };

        public static HashSet<PawnKindDef> FactionlessPawnKindsSet = new();

        public string Name = "My preset";
        public List<FactionEdit> factionChanges = new List<FactionEdit>();

        public string GUID
        {
            get
            {
                if (guid == null)
                    EnsureGUID();
                return guid;
            }
        }

        private string guid;

        public void ExposeData()
        {
            EnsureGUID();
            Scribe_Values.Look(ref Name, "name", "My preset");
            Scribe_Values.Look(ref guid, "guid");
            AddMissingSpecialFactionsIfNeeded();
            Scribe_Collections.Look(ref factionChanges, "factionChanges", LookMode.Deep);
        }

        public static void AddMissingSpecialFactionsIfNeeded()
        {
            if (DefDatabase<FactionDef>.GetNamed(SpecialCreepjoinerFactionDefName, false) == null)
                DefDatabase<FactionDef>.Add(SpecialCreepjoinerFaction);
            if (DefDatabase<FactionDef>.GetNamed(SpecialWildManFactionDefName, false) == null)
                DefDatabase<FactionDef>.Add(SpecialWildManFaction);
            if (DefDatabase<FactionDef>.GetNamed(SpecialFactionlessPawnsFactionDefName, false) == null)
                DefDatabase<FactionDef>.Add(SpecialFactionlessPawnsFaction);
            PopulateFactionlessPawnKinds();
        }

        public static void PopulateFactionlessPawnKinds()
        {
            // Build the set of all pawnkinds that belong to at least one real faction.
            var inAnyFaction = new HashSet<PawnKindDef>();
            foreach (FactionDef f in DefDatabase<FactionDef>.AllDefsListForReading)
            {
                // Skip our own synthetic special factions.
                if (
                    f.defName == SpecialCreepjoinerFactionDefName
                    || f.defName == SpecialWildManFactionDefName
                    || f.defName == SpecialFactionlessPawnsFactionDefName
                )
                {
                    continue;
                }

                void AddOptions(List<PawnGenOption> list)
                {
                    if (list == null)
                        return;
                    foreach (PawnGenOption opt in list)
                    {
                        if (opt.kind != null)
                            inAnyFaction.Add(opt.kind);
                    }
                }

                if (f.pawnGroupMakers != null)
                {
                    foreach (PawnGroupMaker maker in f.pawnGroupMakers)
                    {
                        AddOptions(maker.options);
                        AddOptions(maker.guards);
                        AddOptions(maker.traders);
                        AddOptions(maker.carriers);
                    }
                }

                if (f.basicMemberKind != null)
                    inAnyFaction.Add(f.basicMemberKind);

                if (f.fixedLeaderKinds != null)
                {
                    foreach (PawnKindDef k in f.fixedLeaderKinds)
                        inAnyFaction.Add(k);
                }
            }

            // Collect humanlike pawnkinds not claimed by any real faction or named special faction.
            FactionlessPawnKindsSet.Clear();
            var options = new List<PawnGenOption>();
            foreach (PawnKindDef k in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (k.race?.race?.Humanlike != true)
                    continue;
                if (k == PawnKindDefOf.WildMan)
                    continue;
                if (k is CreepJoinerFormKindDef)
                    continue;
                if (inAnyFaction.Contains(k))
                    continue;

                FactionlessPawnKindsSet.Add(k);
                options.Add(new PawnGenOption { kind = k });
            }

            SpecialFactionlessPawnsFaction.pawnGroupMakers = options.Count > 0
                ?
                [
                    new PawnGroupMaker
                    {
                        kindDef = PawnGroupKindDefOf.Combat,
                        options = options,
                    },
                ]
                : null;
        }

        public static void SetupRelationsForFaction(Faction faction)
        {
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction != other)
                    faction.TryMakeInitialRelationsWith(other);
            }
        }

        public bool HasMissingFactions()
        {
            foreach (var item in factionChanges)
            {
                if (item.Faction.IsMissing)
                    return true;
            }

            return false;
        }

        public bool HasEditFor(FactionDef def)
        {
            if (def == null)
                return false;

            foreach (var item in factionChanges)
            {
                if (item.Faction.HasValue && item.Faction.Def == def)
                    return true;
            }

            return false;
        }

        public IEnumerable<string> GetMissingFactionAndModNames()
        {
            foreach (var edit in factionChanges)
            {
                if (edit.Faction.IsMissing)
                {
                    yield return $"'{edit.Faction.DefName}' from mod: <b>{edit.Faction.ModName}</b>";
                }
            }
        }

        public int TryApplyAll()
        {
            int worked = 0;
            foreach (var change in factionChanges)
            {
                if (!change.Active)
                    continue;

                if (change.Faction.IsMissing)
                {
                    ModCore.Warn($"Faction '{change.Faction.DefName}' is not loaded, so changes will not be applied.");
                    continue;
                }

                if (change.Faction.HasValue)
                {
                    change.Apply(change.Faction.Def);
                    worked++;
                    ModCore.Log($"  - Applied changes to {change.Faction.LabelCap}");
                }
            }

            ModCore.Log($"Applied preset '{Name}': {worked} factions were edited.");
            return worked;
        }

        private void EnsureGUID()
        {
            if (guid != null)
                return;

            guid = "";

            var rand = new Random();
            char[] digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            for (int i = 0; i < 16; i++)
            {
                guid += digits[rand.Next(digits.Length)];
            }
        }

        public void Save()
        {
            EnsureGUID();

            string fileName = $"{guid}.xml";
            string path = Path.Combine(IO.SaveDataPath, fileName);

            IO.SaveToFile(this, path);
        }

        public bool DeleteFile()
        {
            string fileName = $"{guid}.xml";
            string path = Path.Combine(IO.SaveDataPath, fileName);

            return IO.DeleteFile(path);
        }
    }
}
