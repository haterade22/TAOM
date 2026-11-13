using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// The engine calls GetTypes() on the submodule DLL before any OnSubModuleLoad, when no Coop
/// assembly is loaded yet. Loading a type loads its base type, its interfaces, their generic
/// arguments and the types of its by-value fields, so none of those may lead to a Coop assembly,
/// directly or through TAOM.CoopCompat.Coop.dll. Method signatures and bodies resolve later, and
/// reference-typed fields do not load their type.
/// </summary>
[TestClass]
public class TypeScanGateTests
{
    private static readonly string[] CoopAssemblies =
    {
        "Common", "GameInterface", "Coop.Core", "LiteNetLib", "Autofac", "protobuf-net", "protobuf-net.Core",
    };

    private static readonly Dictionary<Type, bool> Memo = new();

    // True when loading this type needs a Coop assembly.
    private static bool NeedsCoop(Type type)
    {
        if (type.IsGenericParameter || type.IsByRef || type.IsPointer || type.IsArray)
            return type.HasElementType && NeedsCoop(type.GetElementType()!);
        if (Memo.TryGetValue(type, out var known)) return known;
        Memo[type] = false;
        var needs = CoopAssemblies.Contains(type.Assembly.GetName().Name)
            || (type.IsGenericType && type.GetGenericArguments().Any(NeedsCoop))
            || (type.BaseType != null && NeedsCoop(type.BaseType))
            || type.GetInterfaces().Any(NeedsCoop)
            || ByValueFields(type).Any(NeedsCoop);
        Memo[type] = needs;
        return needs;
    }

    private static IEnumerable<Type> ByValueFields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(f => f.FieldType)
            .Where(t => t.IsValueType && t != type);

    [TestMethod]
    public void NoSubmoduleType_NeedsACoopAssemblyToLoad()
    {
        var types = typeof(CoopCompatSubModule).Assembly.GetTypes();

        var offenders = types.Where(NeedsCoop).Select(t => t.FullName).OrderBy(n => n).ToList();

        Assert.IsTrue(types.Length > 5, "The scan found too few types to prove anything.");
        CollectionAssert.AreEqual(new string[0], offenders, "Types the engine's scan cannot load without Coop: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void TheCoopFacingTypes_LiveInTheSecondAssembly()
    {
        var coopFacing = typeof(TAOM.CoopCompat.Transport.CoopTransport).Assembly;

        Assert.AreEqual("TAOM.CoopCompat.Coop", coopFacing.GetName().Name);
        Assert.AreSame(coopFacing, typeof(TAOM.CoopCompat.Messages.CoopCompatHello).Assembly);
        Assert.AreNotSame(coopFacing, typeof(CoopCompatSubModule).Assembly);
    }

    [TestMethod]
    public void TheGate_CatchesAMessageType()
    {
        Assert.IsTrue(NeedsCoop(typeof(TAOM.CoopCompat.Messages.CoopCompatHello)), "The gate must flag a type that implements a Coop interface.");
    }
}
