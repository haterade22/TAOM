using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// ADR-007 for both module assemblies, the intent of TAOM.Tests' ServiceSignatureGateTests: a type named
/// *Service, an I*Adapter interface or the transport seam may not name a TaleWorlds class (other than
/// TextObject) or a Coop class in a parameter, return or field of a public, internal or protected
/// member. Arrays, by-ref and generic arguments are unwrapped; value types, interfaces and delegates
/// pass. There is no baseline: these assemblies start clean and stay clean.
/// </summary>
[TestClass]
public class ServiceSignatureGateTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    {
        "TaleWorlds.", "SandBox", "StoryMode", "Common", "GameInterface", "Coop.Core", "LiteNetLib", "protobuf-net", "Autofac",
    };

    private static readonly Assembly[] ModuleAssemblies =
    {
        typeof(CoopCompatSubModule).Assembly, typeof(TAOM.CoopCompat.Transport.CoopTransport).Assembly,
    };

    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic;

    [TestMethod]
    public void ServicesAdaptersAndTheTransportSeam_NameNoTaleWorldsOrCoopClass()
    {
        var hits = Scan(ModuleAssemblies);

        Assert.AreEqual(0, hits.Count, "A service, adapter interface or the transport seam names an engine or Coop class (ADR-007):"
            + Environment.NewLine + string.Join(Environment.NewLine, hits.Select(h => h.Key + "  <- " + h.Value)));
    }

    [TestMethod]
    public void TheScan_SeesTheServicesItGuards()
    {
        var names = ModuleAssemblies.SelectMany(a => a.GetTypes()).Where(InScope).Select(t => t.Name).ToList();

        CollectionAssert.IsSubsetOf(new[] { "SettingsHandshakeService", "MomentumRelayService", "ICoopTransport" }, names);
    }

    [TestMethod]
    public void Scan_FlagsEachWayASignatureNamesAnEngineOrCoopClass()
    {
        var keys = Scan(new[] { BuildPlantedAssembly() }).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new[]
        {
            "IPlantedAdapter::TaleWorlds.CampaignSystem.Hero Fetch()",
            "PlantedFooService::LiteNetLib.NetPeer Peer",
            "PlantedFooService::TaleWorlds.CampaignSystem.Clan Returns()",
            "PlantedFooService::Void .ctor(TaleWorlds.CampaignSystem.Hero)",
            "PlantedFooService::Void List(System.Collections.Generic.List`1[TaleWorlds.CampaignSystem.Hero])",
            "PlantedFooService::Void ProtectedTakes(TaleWorlds.CampaignSystem.Hero)",
        }, keys, string.Join(", ", keys));
    }

    private static SortedDictionary<string, string> Scan(IEnumerable<Assembly> assemblies)
    {
        var hits = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in assemblies.SelectMany(LoadTypes).Where(InScope))
        {
            foreach (var member in type.GetMembers(Declared))
            {
                var bad = member switch
                {
                    MethodBase m when Visible(m) => Offending(m),
                    FieldInfo f when !f.IsPrivate => Offending(f.FieldType),
                    _ => null,
                };
                var key = type.FullName + "::" + member;
                if (bad != null && !hits.ContainsKey(key)) hits[key] = bad.FullName!;
            }
        }

        return hits;
    }

    private static bool InScope(Type type)
    {
        if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) return false;

        var name = type.Name.Split('`')[0];
        return name.EndsWith("Service", StringComparison.Ordinal)
            || (type.IsInterface && name.StartsWith("I", StringComparison.Ordinal) && name.EndsWith("Adapter", StringComparison.Ordinal))
            || name == "ICoopTransport";
    }

    private static bool Visible(MethodBase m) => !m.IsPrivate;

    private static Type? Offending(MethodBase method)
    {
        var bad = method.GetParameters().Select(p => Offending(p.ParameterType)).FirstOrDefault(t => t != null);
        return bad ?? (method is MethodInfo info ? Offending(info.ReturnType) : null);
    }

    private static Type? Offending(Type type)
    {
        if (type.HasElementType) return Offending(type.GetElementType()!);

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var inner = type.GetGenericArguments().Select(Offending).FirstOrDefault(t => t != null);
            if (inner != null) return inner;
        }

        if (!type.IsClass || type == typeof(TextObject) || typeof(Delegate).IsAssignableFrom(type)) return null;

        var assemblyName = type.Assembly.GetName().Name ?? "";
        return ForbiddenAssemblyPrefixes.Any(p => assemblyName.StartsWith(p, StringComparison.Ordinal)) ? type : null;
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Assert.Fail("Types of " + assembly.GetName().Name + " could not load: "
                + string.Join("; ", (ex.LoaderExceptions ?? new Exception[0]).Select(e => e?.Message).Distinct()));
            return Array.Empty<Type>();
        }
    }

    // In-memory types: a *Service naming an engine or Coop class in every position the gate reads, plus one adapter.
    private static Assembly BuildPlantedAssembly()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("CoopCompatServicePlanted" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("planted");

        var foo = module.DefineType("PlantedFooService", TypeAttributes.Public | TypeAttributes.Class);
        var ctor = foo.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(Hero) });
        ctor.GetILGenerator().Emit(OpCodes.Ret);
        Method(foo, "Returns", MethodAttributes.Public, typeof(Clan));
        Method(foo, "List", MethodAttributes.Public, typeof(void), typeof(List<Hero>));
        Method(foo, "ProtectedTakes", MethodAttributes.Family, typeof(void), typeof(Hero));
        Method(foo, "PrivateTakes", MethodAttributes.Private, typeof(void), typeof(Hero));
        Method(foo, "TextObjectOk", MethodAttributes.Public, typeof(TextObject), typeof(TextObject));
        Method(foo, "FuncOk", MethodAttributes.Public, typeof(void), typeof(Func<int, string>));
        foo.DefineField("Peer", typeof(LiteNetLib.NetPeer), FieldAttributes.Public);
        foo.CreateType();

        var adapter = module.DefineType("IPlantedAdapter", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        adapter.DefineMethod("Fetch", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            typeof(Hero), Type.EmptyTypes);
        adapter.CreateType();
        return assembly;
    }

    private static void Method(TypeBuilder type, string name, MethodAttributes attributes, Type returns, params Type[] parameters)
    {
        var method = type.DefineMethod(name, attributes, returns, parameters);
        var il = method.GetILGenerator();
        if (returns != typeof(void)) il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
    }
}
