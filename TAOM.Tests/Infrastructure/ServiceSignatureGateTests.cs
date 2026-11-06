using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-007: services take adapters, never sealed TaleWorlds types. This gate reads the compiled TAOM assembly
/// and fails a public, internal or protected method, constructor or field of a type named *Service, or of an
/// adapter interface (I*Adapter), whose parameter, return or field type is a TaleWorlds class other than
/// <see cref="TextObject"/>. Arrays, by-ref and generic arguments are unwrapped, so <c>List&lt;Hero&gt;</c> fails like
/// <c>Hero</c>. Value types (structs and enums), interfaces and delegates pass. Private members are not read. Protected
/// members are read because an ADR-007 "Exceptions" boundary seam must use ids, value types and TAOM types, "never a sealed
/// TaleWorlds class": a protected seam that names one is a breach, not an exception.
///
/// <c>ServiceSignatureBaseline.txt</c> lists the members that broke the rule when this gate landed, one
/// <c>&lt;type FullName&gt;::&lt;member signature&gt;</c> per line, so each overload has its own line. It only shrinks: a member not in it fails, and a
/// listed member that no longer breaks the rule fails until its line is removed. To prune, set
/// <c>TAOM_WRITE_SERVICE_SIGNATURE_BASELINE=1</c> and run <see cref="Baseline_Regenerate_WhenAsked"/>, then lower
/// <see cref="MaxBaselineEntries"/>. Reading member signatures resolves the TaleWorlds assemblies, so the gate needs the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServiceSignatureGateTests
{
    private const string WriteEnvVar = "TAOM_WRITE_SERVICE_SIGNATURE_BASELINE";

    /// <summary>The baseline's line count. Lower it when you prune; a larger file means a line was added by hand.</summary>
    private const int MaxBaselineEntries = 75;
    private static readonly Assembly[] TaomAssembly = { typeof(TAOM.Adapters.IAgentVisualsAdapter).Assembly };

    private static readonly string[] EngineAssemblyPrefixes = { "TaleWorlds.", "SandBox", "StoryMode" };

    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic;

    private static string BaselinePath => RepoPaths.RepoPath("TAOM.Tests", "Infrastructure", "ServiceSignatureBaseline.txt");

    [TestMethod]
    public void ServiceAndAdapterSignatures_NameNoTaleWorldsClass_ExceptTheShrinkOnlyBaseline()
    {
        RequireGame();
        var live = Scan(TaomAssembly);
        var baseline = LoadBaseline();
        var added = live.Keys.Where(k => !baseline.Contains(k)).ToList();

        Assert.AreEqual(0, added.Count, "A service or adapter-interface signature names a TaleWorlds class (ADR-007). Take an adapter "
            + "interface instead, and convert at the boundary:" + Environment.NewLine
            + string.Join(Environment.NewLine, added.Select(k => k + "  <- " + live[k])));
    }

    [TestMethod]
    public void Baseline_HasNoStaleEntriesAndIsSortedUnique()
    {
        RequireGame();
        var live = Scan(TaomAssembly);
        var lines = ReadBaselineLines();

        var stale = lines.Where(k => !live.ContainsKey(k)).ToList();
        Assert.AreEqual(0, stale.Count, "These members no longer break the rule; remove them (set " + WriteEnvVar
            + "=1 and run Baseline_Regenerate_WhenAsked):" + Environment.NewLine + string.Join(Environment.NewLine, stale));
        CollectionAssert.AreEqual(lines.OrderBy(k => k, StringComparer.Ordinal).Distinct().ToList(), lines,
            "ServiceSignatureBaseline.txt must be sorted (ordinal) with no duplicates.");
        Assert.AreEqual(MaxBaselineEntries, lines.Count, "The baseline has " + lines.Count + " lines and MaxBaselineEntries is "
            + MaxBaselineEntries + ". It only shrinks: never add a line. When you prune, lower MaxBaselineEntries.");
    }

    [TestMethod]
    public void Baseline_Regenerate_WhenAsked()
    {
        if (Environment.GetEnvironmentVariable(WriteEnvVar) != "1")
            Assert.Inconclusive("Set " + WriteEnvVar + "=1 to write the baseline: a missing file is created from the live scan, an "
                + "existing one is pruned (it never gains a line). Then set MaxBaselineEntries to the count in the header.");

        RequireGame();
        var live = Scan(TaomAssembly).Keys;
        var keys = File.Exists(BaselinePath) ? live.Intersect(ReadBaselineLines(), StringComparer.Ordinal) : live;
        var rows = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var header = new[]
        {
            "# Service and adapter-interface members (public, internal or protected) whose parameter, return or field type is a TaleWorlds class",
            "# other than TextObject (ADR-007). One line per member: <type FullName>::<member signature>, so each overload has its own line; constructors are .ctor.",
            "# The list may only shrink. Never add a line: take an adapter interface and convert at the boundary. To prune: set",
            "# " + WriteEnvVar + "=1, run ServiceSignatureGateTests.Baseline_Regenerate_WhenAsked, then lower MaxBaselineEntries.",
            "# Entries: " + rows.Count + ".",
        };
        File.WriteAllLines(BaselinePath, header.Concat(rows));
    }

    [TestMethod]
    public void Scan_FlagsEachWayASignatureNamesATaleWorldsClass()
    {
        RequireGame();
        var assembly = BuildPlantedAssembly();

        var hits = Scan(new[] { assembly });

        var keys = hits.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        CollectionAssert.AreEqual(new[]
        {
            "IPlantedAdapter::TaleWorlds.CampaignSystem.Hero Fetch()",
            "PlantedFooService::TaleWorlds.CampaignSystem.Clan Returns()",
            "PlantedFooService::TaleWorlds.CampaignSystem.Settlements.Settlement Field",
            "PlantedFooService::Void .ctor(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void InternalTakes(TaleWorlds.CampaignSystem.Hero[])",
            "PlantedFooService::Void List(System.Collections.Generic.List`1[TaleWorlds.CampaignSystem.Hero])",
            "PlantedFooService::Void PrivateProtectedTakes(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void ProtectedInternalTakes(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void ProtectedTakes(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void Takes(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void Takes(TaleWorlds.CampaignSystem.Hero, Int32)",
        }, keys, string.Join(", ", keys));
    }

    private static void RequireGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Bannerlord install not found: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    /// <summary>Offending member key to the first offending type, across the given assemblies.</summary>
    internal static SortedDictionary<string, string> Scan(IEnumerable<Assembly> assemblies)
    {
        var hits = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            foreach (var type in LoadTypes(assembly))
            {
                if (!InScope(type))
                    continue;

                foreach (var member in type.GetMembers(Declared))
                {
                    var bad = member switch
                    {
                        MethodBase m when Visible(m) => Offending(m),
                        FieldInfo f when f.IsPublic || f.IsAssembly || f.IsFamily || f.IsFamilyOrAssembly || f.IsFamilyAndAssembly => Offending(f.FieldType),
                        _ => null,
                    };
                    var key = type.FullName + "::" + member;
                    if (bad != null && !hits.ContainsKey(key))
                        hits[key] = bad.FullName!;
                }
            }
        }

        return hits;
    }

    private static bool InScope(Type type) =>
        !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)
        && (ShortName(type).EndsWith("Service", StringComparison.Ordinal)
            || (type.IsInterface && type.Name.StartsWith("I", StringComparison.Ordinal) && ShortName(type).EndsWith("Adapter", StringComparison.Ordinal)));

    // Reflection names a generic type Name`1.
    private static string ShortName(Type type) => type.Name.Split('`')[0];

    private static bool Visible(MethodBase m) => m.IsPublic || m.IsAssembly || m.IsFamily || m.IsFamilyOrAssembly || m.IsFamilyAndAssembly;

    private static Type? Offending(MethodBase method)
    {
        var bad = method.GetParameters().Select(p => Offending(p.ParameterType)).FirstOrDefault(t => t != null);
        return bad ?? (method is MethodInfo info ? Offending(info.ReturnType) : null);
    }

    private static Type? Offending(Type type)
    {
        if (type.HasElementType)
            return Offending(type.GetElementType()!);

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var inner = type.GetGenericArguments().Select(Offending).FirstOrDefault(t => t != null);
            if (inner != null)
                return inner;
        }

        if (!type.IsClass || type == typeof(TextObject) || typeof(Delegate).IsAssignableFrom(type))
            return null;

        var assemblyName = type.Assembly.GetName().Name ?? "";
        return EngineAssemblyPrefixes.Any(p => assemblyName.StartsWith(p, StringComparison.Ordinal)) ? type : null;
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Assert.Fail("Types of " + assembly.GetName().Name + " could not load, so they cannot be scanned: "
                + string.Join("; ", (ex.LoaderExceptions ?? new Exception[0]).Select(e => e.Message).Distinct()));
            return Array.Empty<Type>();
        }
    }

    private static List<string> ReadBaselineLines()
    {
        Assert.IsTrue(File.Exists(BaselinePath), "Baseline not found at " + BaselinePath + ". Set " + WriteEnvVar
            + "=1 and run Baseline_Regenerate_WhenAsked to create it.");
        return File.ReadAllLines(BaselinePath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
    }

    private static HashSet<string> LoadBaseline() => new(ReadBaselineLines(), StringComparer.Ordinal);

    private sealed class PlantedGenericService<T> { }

    private interface IPlantedGenericAdapter<T> { }

    [TestMethod]
    public void AGenericService_OrAdapter_IsInScope()
    {
        Assert.IsTrue(InScope(typeof(PlantedGenericService<>)), "reflection names it PlantedGenericService`1");
        Assert.IsTrue(InScope(typeof(IPlantedGenericAdapter<>)), "reflection names it IPlantedGenericAdapter`1");
    }

    // In-memory types: one *Service naming a TaleWorlds class in every position the gate reads, one clean *Service, one adapter.
    private static Assembly BuildPlantedAssembly()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ServiceSignaturePlanted" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("planted");

        var foo = module.DefineType("PlantedFooService", TypeAttributes.Public | TypeAttributes.Class);
        var ctor = foo.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(Hero) });
        ctor.GetILGenerator().Emit(OpCodes.Ret);
        Method(foo, "Takes", MethodAttributes.Public, typeof(void), typeof(Hero));
        Method(foo, "Takes", MethodAttributes.Public, typeof(void), typeof(Hero), typeof(int));
        Method(foo, "InternalTakes", MethodAttributes.Assembly, typeof(void), typeof(Hero[]));
        Method(foo, "Returns", MethodAttributes.Public, typeof(Clan));
        Method(foo, "List", MethodAttributes.Public, typeof(void), typeof(List<Hero>));
        foo.DefineField("Field", typeof(Settlement), FieldAttributes.Public);
        Method(foo, "ProtectedTakes", MethodAttributes.Family, typeof(void), typeof(Hero));
        Method(foo, "ProtectedInternalTakes", MethodAttributes.FamORAssem, typeof(void), typeof(Hero));
        Method(foo, "PrivateProtectedTakes", MethodAttributes.FamANDAssem, typeof(void), typeof(Hero));
        Method(foo, "ProtectedValueOk", MethodAttributes.Family, typeof(TaleWorlds.Library.Vec2), typeof(string));
        Method(foo, "PrivateTakes", MethodAttributes.Private, typeof(void), typeof(Hero));
        Method(foo, "TextObjectOk", MethodAttributes.Public, typeof(TextObject), typeof(TextObject));
        Method(foo, "ValueOk", MethodAttributes.Public, typeof(TaleWorlds.Library.Vec2), typeof(int));
        Method(foo, "FuncOk", MethodAttributes.Public, typeof(void), typeof(Func<int, string>));
        foo.CreateType();

        var clean = module.DefineType("PlantedCleanService", TypeAttributes.Public | TypeAttributes.Class);
        Method(clean, "Fine", MethodAttributes.Public, typeof(string), typeof(int));
        clean.CreateType();

        var adapter = module.DefineType("IPlantedAdapter", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        adapter.DefineMethod("Fetch", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            typeof(Hero), Type.EmptyTypes);
        adapter.CreateType();
        return assembly;
    }

    private static void Method(TypeBuilder type, string name, MethodAttributes attributes, Type returns, params Type[] parameters)
    {
        type.DefineMethod(name, attributes, returns, parameters).GetILGenerator().Emit(OpCodes.Ret);
    }
}
