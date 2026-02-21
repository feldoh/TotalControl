namespace FactionLoadout.Util;

/// <summary>
/// Implemented by mod-owned types that know how to produce a complete deep copy of themselves.
/// The covariant <c>out T</c> parameter allows <see cref="PawnKindEdit.DeepCopyValue"/> to match
/// any implementor like call-site generics in Java would.
/// </summary>
public interface IDeepCopyable<out T>
{
    T DeepClone();
}
