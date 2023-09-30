using System;
using Verse;

namespace FactionLoadout
{
    public class DefRef<T> : IExposable where T : Def, new()
    {
        public bool HasValue => def != null;
        public bool IsMissing => def == null && defName != null;
        public string DefName => defName;
        public string ModName => modName;
        public string LabelCap => Def?.LabelCap;

        public T Def
        {
            get
            {
                return def;
            }
            set
            {
                def = value;
                defName = value?.defName;
                modName = value?.modContentPack?.Name;
            }
        }

        public Type GenericType => typeof(T);

        private string defName = null;
        private string modName = null;
        private T def = null;  
        
        public DefRef() { }

        public DefRef(T def)
        {
            Def = def;
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName", null, true);
            Scribe_Values.Look(ref modName, "modName", null, true);
            Scribe_Defs.Look(ref def, "def");

            if(def == null && defName != null)
            {
                def = DefDatabase<T>.GetNamed(defName, false);
                ModCore.Log($"Trying to restore missing def: {defName}... {(def == null ? "Failed!" : "Success!")}");
            }
        }

        public override string ToString()
        {
            string label = "<null>";
            if (IsMissing)
                label = $"<missing:{defName}>";
            if (HasValue)
                label = $"({def.defName})";
            return $"DefRef<{GenericType.Name}> {label}";
        }

        public static implicit operator T(DefRef<T> r) => r?.Def;

        public static implicit operator DefRef<T>(T def) => new DefRef<T>(def);
    }
}
