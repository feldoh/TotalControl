using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace FactionLoadout.Util;

public static class ReflectionHelper
{
    public static Lazy<Type> DefDatabaseGenericType = new(() => typeof(DefDatabase<>));
    public static Lazy<Type> ListGenericType = new(() => typeof(List<>));

    public static Lazy<MethodInfo> GetCompGenericMethod = new(() => AccessTools.Method(typeof(Pawn), "GetComp"));
}
