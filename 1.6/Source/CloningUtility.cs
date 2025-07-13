using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace FactionLoadout
{
    public static class CloningUtility
    {
        private static Dictionary<string, Func<FactionDef, FieldInfo, object>> customFac;
        private static Dictionary<string, Func<PawnKindDef, FieldInfo, object>> customKind;
        private static List<FieldInfo> toCloneFac,
            toCloneKind;
        private static Dictionary<string, PawnKindDef> replacements;
        private static int cloneID;

        static CloningUtility()
        {
            customFac = new Dictionary<string, Func<FactionDef, FieldInfo, object>>()
            {
                { "pawnGroupMakers", CloneGroupMakers },
                { "basicMemberKind", CloneBasicMemberType },
                { "fixedLeaderKinds", CloneLeaderKinds },
                { "apparelStuffFilter", CloneThingFilter }
            };
            customKind = new Dictionary<string, Func<PawnKindDef, FieldInfo, object>>() { { "inventoryOptions", CloneInventory } };

            replacements = new Dictionary<string, PawnKindDef>();

            toCloneFac = new List<FieldInfo>();
            toCloneFac.AddRange(typeof(FactionDef).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            toCloneKind = new List<FieldInfo>();
            toCloneKind.AddRange(typeof(PawnKindDef).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        private static object CloneThingFilter(FactionDef def, FieldInfo info)
        {
            var filter = info.GetValue(def) as ThingFilter;
            if (filter == null)
                return null;

            var f2 = new ThingFilter();
            f2.CopyAllowancesFrom(filter);
            return f2;
        }

        private static object CloneList(PawnKindDef def, FieldInfo info)
        {
            var current = info.GetValue(def);
            if (current == null)
                return null;

            var created = Activator.CreateInstance(typeof(List<>).MakeGenericType(info.FieldType.GetGenericArguments()[0]), current);
            return created;
        }

        private static object CloneInventory(PawnKindDef def, FieldInfo info)
        {
            var op = info.GetValue(def) as PawnInventoryOption;
            if (op == null)
                return op;

            // Just gonna do this because I can't be bothered to write another recursive inventory clone.
            return new InventoryOptionEdit(op).ConvertToVanilla();
        }

        private static object CloneLeaderKinds(FactionDef def, FieldInfo info)
        {
            var list = info.GetValue(def) as List<PawnKindDef>;
            if (list == null)
                return null;

            var created = new List<PawnKindDef>();
            foreach (var item in list)
            {
                created.Add(MakeReplacement(item));
            }
            return created;
        }

        private static object CloneBasicMemberType(FactionDef def, FieldInfo info)
        {
            var type = info.GetValue(def) as PawnKindDef;
            if (type == null)
                return null;

            return MakeReplacement(type);
        }

        private static PawnKindDef MakeReplacement(PawnKindDef def)
        {
            if (def == null)
                return null;

            if (replacements.TryGetValue(def.defName, out var found))
                return found;

            var created = Clone(def);
            replacements.Add(def.defName, created);
            return created;
        }

        private static object CloneGroupMakers(FactionDef def, FieldInfo info)
        {
            var list = info.GetValue(def) as List<PawnGroupMaker>;
            if (list == null)
                return null;

            List<PawnGenOption> CloneOptions(List<PawnGenOption> value)
            {
                if (value == null)
                    return null;
                var list = new List<PawnGenOption>(value.Count);
                foreach (var item in value)
                {
                    list.Add(CloneOption(item));
                }

                return list;
            }

            var newList = new List<PawnGroupMaker>();
            foreach (var value in list)
            {
                var created = new PawnGroupMaker();
                created.options = CloneOptions(value.options);
                created.carriers = CloneOptions(value.carriers);
                created.guards = CloneOptions(value.guards);
                created.traders = CloneOptions(value.traders);
                newList.Add(created);
            }

            return newList;
        }

        private static PawnGenOption CloneOption(PawnGenOption op)
        {
            if (op == null)
                return op;

            var created = new PawnGenOption();
            created.kind = MakeReplacement(op.kind);
            created.selectionWeight = op.selectionWeight;

            return created;
        }

        public static FactionDef Clone(FactionDef def)
        {
            if (def == null)
                return null;

            replacements.Clear();

            FactionDef created = new FactionDef();
            foreach (FieldInfo field in toCloneFac)
                field.SetValue(created, CloneField(def, field));

            created.defName += $"_CLONED_{cloneID++}";

            ModCore.Log($"Cloned {created.LabelCap}, creating {replacements.Count} kindDefs: {string.Join(", ", replacements.Keys)}");
            replacements.Clear();

            return created;
        }

        private static object CloneField(FactionDef def, FieldInfo info)
        {
            if (info == null)
                return null;

            string name = info.Name;
            return customFac.TryGetValue(name, out Func<FactionDef, FieldInfo, object> found) ? found?.Invoke(def, info) : info.GetValue(def);
        }

        public static PawnKindDef Clone(PawnKindDef def)
        {
            if (def == null)
                return null;

            if (def is CreepJoinerFormKindDef || def == PawnKindDefOf.WildMan)
                return def;

            PawnKindDef created = new PawnKindDef();
            foreach (FieldInfo field in toCloneKind)
                field.SetValue(created, CloneField(def, field));

            return created;
        }

        private static object CloneField(PawnKindDef def, FieldInfo info)
        {
            if (info == null)
                return null;

            string name = info.Name;
            if (customKind.TryGetValue(name, out var found))
                return found?.Invoke(def, info);

            if (info.FieldType.IsGenericType && info.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return CloneList(def, info);
            }

            return info.GetValue(def);
        }
    }
}
