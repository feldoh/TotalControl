using System;

namespace FactionLoadout.Util;

/// <summary>
/// Applied to fields on <see cref="PawnKindEdit"/> that must NOT be copied during
/// clipboard copy operations. This covers identity fields, runtime state, and
/// fields with special handling elsewhere.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NoCopyAttribute : Attribute { }
