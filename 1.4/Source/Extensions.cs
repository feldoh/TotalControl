using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionLoadout
{
    public static class Extensions
    {
        private static HashSet<PawnKindDef> tempKinds = new HashSet<PawnKindDef>();

        public static Rect GetCentered(this Rect area, float width, float height) => new Rect(area.center.x - width * 0.5f, area.center.y - height * 0.5f, width, height);  
        
        public static Rect GetCentered(this Rect area, string text)
        {
            var size = Text.CalcSize(text);
            return area.GetCentered(size.x, size.y);
        }

        public static IEnumerable<PawnKindDef> GetKindDefs(this FactionDef def)
        {
            if (def == null)
                return null;

            tempKinds.Clear();

            void Register(List<PawnGenOption> list)
            {
                if (list == null)
                    return;
                foreach (var item in list)
                    tempKinds.Add(item.kind);
            }

            void RegisterSimple(List<PawnKindDef> list)
            {
                if (list == null)
                    return;

                foreach (var item in list)
                    tempKinds.Add(item);
            }

            if (def.pawnGroupMakers != null)
            {
                foreach (var item in def.pawnGroupMakers)
                {
                    Register(item.options);
                    Register(item.traders);
                    Register(item.guards);
                    Register(item.carriers);
                }
            }

            RegisterSimple(def.fixedLeaderKinds);
            if (def.basicMemberKind != null)
                tempKinds.Add(def.basicMemberKind);

            return tempKinds;
        }

        public static PawnKindDef RandomKindDef(this FactionDef def)
        {
            return def.GetKindDefs().RandomElement();
        }
    }
}
