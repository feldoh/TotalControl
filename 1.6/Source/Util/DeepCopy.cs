using System;
using System.Collections;
using System.Collections.Generic;
using Verse;

namespace FactionLoadout.Util;

/// <summary>
/// Generic deep-copy dispatcher used by <see cref="PawnKindEdit.CopyFrom"/>.
/// Handles <see cref="IDeepCopyable{T}"/> types, <see cref="SimpleCurve"/>,
/// primitives, value types, Defs, and generic collections.
/// </summary>
public static class DeepCopy
{
    public static object Value(object value, Type type)
    {
        if (value == null)
            return null;

        // Types that know how to clone themselves.
        // IDeepCopyable<out T> is covariant, so any IDeepCopyable<T> matches IDeepCopyable<object>.
        if (value is IDeepCopyable<object> cloneable)
            return cloneable.DeepClone();

        // SimpleCurve has a copy constructor that takes IEnumerable<CurvePoint>.
        if (value is SimpleCurve curve)
            return new SimpleCurve(curve);

        // Primitives, enums, strings, Def references — safe to assign directly.
        if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            return value;
        if (typeof(Def).IsAssignableFrom(type))
            return value;

        // Nullable<T> — the boxed struct is safe for simple value types.
        if (Nullable.GetUnderlyingType(type) != null)
            return value;

        // Other value types (FloatRange, IntRange, Color, Vector2, …).
        if (type.IsValueType)
            return value;

        // List<T>: deep-clone elements if they implement IDeepCopyable, otherwise shallow-copy.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            IList src = (IList)value;
            IList dest = (IList)Activator.CreateInstance(type);
            foreach (object item in src)
                dest.Add(item is IDeepCopyable<object> c ? c.DeepClone() : item);
            return dest;
        }

        // Dictionary<K,V> — shallow copy (keys/values are strings, Defs, or primitives).
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            IDictionary src = (IDictionary)value;
            IDictionary dest = (IDictionary)Activator.CreateInstance(type);
            foreach (DictionaryEntry kvp in src)
                dest.Add(kvp.Key, kvp.Value);
            return dest;
        }

        // Fallback: shared reference. Log so we can catch missed types during dev.
        ModCore.Warn($"[DeepCopy] Unhandled field type {type.FullName} — using shared reference. Implement IDeepCopyable<T> if deep copy is needed.");
        return value;
    }
}
