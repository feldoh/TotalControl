using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
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
            foreach(var file in files)
            {
                try
                {
                    var preset = new Preset();
                    IO.LoadFromFile(preset, file.FullName);
                    loadedPresets.Add(preset);
                }
                catch(Exception e)
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
            catch(Exception e)
            {
                ModCore.Error($"Failed to delete preset file for {preset.Name} ({preset.GUID})", e);
            }
        }

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
            Scribe_Collections.Look(ref factionChanges, "factionChanges", LookMode.Deep);
        }

        public bool HasMissingFactions()
        {
            foreach(var item in factionChanges)
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

            foreach(var item in factionChanges)
            {
                if (item.Faction.HasValue && item.Faction.Def == def)
                    return true;
            }

            return false;
        }

        public IEnumerable<string> GetMissingFactionAndModNames()
        {
            foreach(var edit in factionChanges)
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
            foreach(var change in factionChanges)
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
            char[] digits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
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
