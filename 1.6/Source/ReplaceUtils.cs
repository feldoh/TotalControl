using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionLoadout;

public static class ReplaceUtils
{
    public static void ReplaceMaybe<T>(ref T field, T maybe)
        where T : class
    {
        if (maybe == null)
            return;
        field = maybe;
    }

    public static void ReplaceMaybe<T>(ref T field, T? maybe)
        where T : struct
    {
        if (maybe == null)
            return;
        field = maybe.Value;
    }

    public static void ReplaceMaybe<T>(ref T? field, T? maybe)
        where T : struct
    {
        if (maybe == null)
            return;
        field = maybe.Value;
    }

    public static void ReplaceMaybe(ref PawnInventoryOption inv, InventoryOptionEdit maybe, PawnKindEdit edit, PawnKindEdit global)
    {
        if (maybe == null)
            return;

        if (global?.Inventory != null || (edit.IsGlobal && !edit.ReplaceDefaultInventory))
        {
            if (inv == null)
            {
                inv = maybe.ConvertToVanilla();
            }
            else
            {
                PawnInventoryOption vanilla = maybe.ConvertToVanilla();
                if (vanilla.subOptionsTakeAll != null)
                    inv.subOptionsTakeAll.AddRange(vanilla.subOptionsTakeAll);
                if (vanilla.subOptionsChooseOne != null)
                    inv.subOptionsChooseOne.AddRange(vanilla.subOptionsChooseOne);
            }
        }
        else
        {
            inv = maybe.ConvertToVanilla();
        }
    }

    public static void ReplaceMaybeList<T>(ref T field, T maybe, bool tryAdd)
        where T : IList, new()
    {
        if (maybe == null)
            return;

        if (tryAdd && field != null)
        {
            foreach (object value in maybe)
                if (!field.Contains(value))
                    field.Add(value);
        }
        else
        {
            field = [];
            foreach (object value in maybe)
                field.Add(value);
        }
    }

    public static void ReplaceMaybeDefRefList<T>(ref List<T> field, List<DefRef<T>> maybe, bool tryAdd)
        where T : Def, new()
    {
        if (maybe == null)
            return;

        List<T> resolved = maybe.Where(r => r.HasValue).Select(r => r.Def).ToList();

        if (tryAdd && field != null)
        {
            foreach (T value in resolved)
                if (!field.Contains(value))
                    field.Add(value);
        }
        else
        {
            field = resolved;
        }
    }
}
