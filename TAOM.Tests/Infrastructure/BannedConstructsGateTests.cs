using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// AGENTS.md "Banned constructs", enforced repo-wide instead of one feature at a time.
///
/// ADR-003 (no region directives) and ADR-005 (no conditional-compilation directive outside
/// Main/IoC.cs) are read from source text. C# requires a preprocessor directive to be the first
/// non-whitespace on its line, so each line is matched on its own and no comment or string is
/// blanked: a "// #region" comment or a directive inside a one-line string does not start the line and
/// passes. The one false positive left is a directive-shaped line inside a multi-line verbatim or raw
/// string, or inside a block comment. That fails loudly, which is acceptable. TAOM.Tests is scanned
/// too, because ADR-003 says test files are not exempt.
///
/// ADR-004 (no Obsolete attribute, no exception anywhere) is read from the compiled assemblies, not the
/// source: reflection sees the attribute however it is spelled or split across lines. Every type and
/// every member declared on it (methods, constructors, properties, fields including enum members,
/// events) is checked in TAOM, TAOM.Client, TAOM.Dependencies and TAOM.Tests.
/// </summary>
[TestClass]
public class BannedConstructsGateTests
{
    private static readonly string[] ScannedRoots = { "Main", "Client", "CoopCompat", "Dependencies", "TAOM.Tests", "TAOM.CoopCompat.Tests" };

    // ADR-005: "ONLY allowed in IoC.cs for build-specific service registration".
    private const string IoCRegistrationFile = "Main/IoC.cs";

    // ADR-003 names region and endregion. ADR-005 lists #if, #ifdef, #ifndef, #else and #endif; C# has
    // no #ifdef or #ifndef (the compiler rejects them), so the live conditional set is if, elif, else,
    // endif. #pragma, #nullable and #line are not conditional compilation and are not matched.
    private static readonly Regex RegionDirective =
        new(@"^[ \t]*#[ \t]*(?:end)?region\b", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ConditionalDirective =
        new(@"^[ \t]*#[ \t]*(?:if|elif|else|endif)\b", RegexOptions.Compiled | RegexOptions.Multiline);

    private const BindingFlags AllDeclared = BindingFlags.DeclaredOnly | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [TestMethod]
    public void Scan_CatchesEachBannedDirective()
    {
        var planted = new Dictionary<string, string>
        {
            ["region"] = "class A\n{\n#region Fields\n    int x;\n#endregion\n}\n",
            ["indented region"] = "class A\n{\n    #region Fields\n}\n",
            ["endregion with a space"] = "class A\n{\n  # endregion\n}\n",
            ["if DEBUG"] = "class A\n{\n#if DEBUG\n    int x;\n#endif\n}\n",
            ["if not DEBUG"] = "#if !DEBUG\nclass A { }\n#endif\n",
            ["if RELEASE"] = "#if RELEASE\nclass A { }\n#endif\n",
            ["elif"] = "#if A\n#elif NET472\n#endif\n",
            ["else alone"] = "class A\n{\n#else\n}\n",
            ["endif alone"] = "class A\n{\n#endif\n}\n",
        };

        foreach (var entry in planted)
            Assert.AreNotEqual(0, ScanDirectives("Main/Planted.cs", entry.Value).Count,
                $"missed a planted violation: {entry.Key}");
    }

    [TestMethod]
    public void Scan_ReportsFileLineAndAdr()
    {
        var region = ScanDirectives("Main/Planted.cs", "class A\n{\n    #region R\n}\n");
        var conditional = ScanDirectives("Main/Planted.cs", "class A\n{\n}\n#if X\n");

        Assert.AreEqual(1, region.Count);
        StringAssert.Contains(region[0], "Main/Planted.cs:3");
        StringAssert.Contains(region[0], "ADR-003");
        Assert.AreEqual(1, conditional.Count);
        StringAssert.Contains(conditional[0], "Main/Planted.cs:4");
        StringAssert.Contains(conditional[0], "ADR-005");
    }

    [TestMethod]
    public void Scan_IgnoresLinesThatDoNotStartWithTheDirective()
    {
        var source = string.Join("\n",
            "// #region is banned",
            "class A",
            "{",
            "    string a = \"#region\";",
            "    int b = 1; // #if DEBUG",
            "    bool IsObsolete { get; }",
            "#pragma warning disable CS0618",
            "#nullable enable",
            "#line hidden",
            "}",
            "");

        Assert.AreEqual(0, ScanDirectives("Main/Clean.cs", source).Count,
            string.Join("; ", ScanDirectives("Main/Clean.cs", source)));
    }

    [TestMethod]
    public void Scan_ExemptsIoCRegistrationFromConditionalsOnly()
    {
        Assert.AreEqual(0, ScanDirectives(IoCRegistrationFile, "#if DEBUG\nint x;\n#else\n#endif\n").Count);
        Assert.AreNotEqual(0, ScanDirectives(IoCRegistrationFile, "#region R\n").Count);
        Assert.AreNotEqual(0, ScanDirectives("Client/IoC.cs", "#if DEBUG\n#endif\n").Count);
    }

    [TestMethod]
    public void ScanObsolete_CatchesTypeAndMemberAttributes()
    {
        var assembly = BuildDynamicAssembly();

        var hits = FindObsolete(assembly);

        Assert.AreEqual(5, hits.Count, string.Join("\n", hits));
        StringAssert.Contains(string.Join("\n", hits), "PlantedObsoleteType (ADR-004)");
        StringAssert.Contains(string.Join("\n", hits), "PlantedMembers.ObsoleteMethod (ADR-004)");
        StringAssert.Contains(string.Join("\n", hits), "PlantedMembers.ObsoleteField (ADR-004)");
        StringAssert.Contains(string.Join("\n", hits), "PlantedMembers.ObsoleteProperty (ADR-004)");
        StringAssert.Contains(string.Join("\n", hits), "PlantedMembers..ctor (ADR-004)");
        Assert.IsFalse(string.Join("\n", hits).Contains("CleanMethod"), string.Join("\n", hits));
        Assert.IsFalse(string.Join("\n", hits).Contains("CleanType"), string.Join("\n", hits));
    }

    [TestMethod]
    public void ScanObsolete_PassesACleanDynamicAssembly()
    {
        var assembly = BuildDynamicAssembly(onlyClean: true);

        Assert.AreEqual(0, FindObsolete(assembly).Count, string.Join("\n", FindObsolete(assembly)));
    }

    [TestMethod]
    public void Scan_ReachesAllFourAssemblies()
    {
        var names = ScannedAssemblies().Select(a => a.GetName().Name).ToList();

        CollectionAssert.AreEquivalent(
            new[] { "TAOM", "TAOM.Client", "TAOM.Dependencies", "TAOM.Tests" }, names);
    }

    [TestMethod]
    public void NoCompiledCodeUsesObsolete()
    {
        // Reading attributes resolves their assemblies (the Bannerlord view layer is not in the test bin).
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Bannerlord install not found, so TAOM.Client's attributes cannot resolve: "
                + string.Join("; ", GameAssemblies.Diagnostics));

        var violations = ScannedAssemblies().SelectMany(FindObsolete).ToList();

        Assert.AreEqual(0, violations.Count,
            "Obsolete attributes (ADR-004, migrate every use instead):\n" + string.Join("\n", violations));
    }

    [TestMethod]
    public void NoCompiledCodeUsesRegionsOrConditionalDirectives()
    {
        var repoDir = RepoPath();
        var violations = CSharpFiles(ScannedRoots)
            .SelectMany(path => ScanDirectives(
                path.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/'),
                RepoPaths.Text(path).Replace("\r\n", "\n")))
            .ToList();

        Assert.AreEqual(0, violations.Count,
            "Banned directives (AGENTS.md \"Banned constructs\"):\n" + string.Join("\n", violations));
    }

    // Core, client, Dependencies (reached through Main's reference) and this test assembly.
    private static Assembly[] ScannedAssemblies() => new[]
    {
        typeof(IoC).Assembly,
        typeof(ClientSubModule).Assembly,
        typeof(global::TAOM.Dependencies.EarlyLog).Assembly,
        typeof(BannedConstructsGateTests).Assembly,
    };

    // One "path:line: message (ADR-nnn)" per offence. relativePath uses forward slashes.
    private static List<string> ScanDirectives(string relativePath, string source)
    {
        var hits = new List<string>();
        Collect(hits, relativePath, source, RegionDirective, "#region or #endregion is banned (ADR-003)");
        if (relativePath != IoCRegistrationFile)
            Collect(hits, relativePath, source, ConditionalDirective,
                "conditional-compilation directive is allowed only in Main/IoC.cs (ADR-005)");

        return hits;
    }

    private static void Collect(List<string> hits, string path, string source, Regex pattern, string message)
    {
        foreach (Match m in pattern.Matches(source))
        {
            var line = source.Take(m.Index).Count(c => c == '\n') + 1;
            hits.Add($"{path}:{line}: {message}");
        }
    }

    // One "Assembly: Type.Member (ADR-004)" per type or member carrying ObsoleteAttribute.
    private static List<string> FindObsolete(Assembly assembly)
    {
        var hits = new List<string>();
        foreach (var type in LoadTypes(assembly, hits))
        {
            if (HasObsolete(type))
                hits.Add($"{assembly.GetName().Name}: {type.FullName} (ADR-004)");

            foreach (var member in type.GetMembers(AllDeclared).Where(m => m.MemberType != MemberTypes.NestedType))
                if (HasObsolete(member))
                    hits.Add($"{assembly.GetName().Name}: {type.FullName}.{member.Name} (ADR-004)");
        }

        return hits;
    }

    // GetTypes, falling back to the types that did load. A type that could not load is reported, never
    // skipped silently, so the gate cannot pass over code it failed to read.
    private static IEnumerable<Type> LoadTypes(Assembly assembly, List<string> problems)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            problems.AddRange((ex.LoaderExceptions ?? new Exception[0]).Select(e =>
                $"{assembly.GetName().Name}: type could not load, so it was not scanned for ADR-004: {e.Message}"));
            return ex.Types.Where(t => t != null)!;
        }
    }

    // IsDefined, not GetCustomAttributes(Data): it matches the one attribute type by name and never builds
    // the member's other attributes, whose assemblies (the Bannerlord view layer) may not resolve here.
    private static bool HasObsolete(MemberInfo member) => member.IsDefined(typeof(ObsoleteAttribute), false);

    // Planted types built in memory. With onlyClean, only the attribute-free type is emitted.
    private static Assembly BuildDynamicAssembly(bool onlyClean = false)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("BannedConstructsPlanted" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("planted");
        var marker = new CustomAttributeBuilder(typeof(ObsoleteAttribute).GetConstructor(Type.EmptyTypes)!, new object[0]);

        var clean = module.DefineType("CleanType", TypeAttributes.Public | TypeAttributes.Class);
        EmitEmptyMethod(clean, "CleanMethod", marked: null);
        clean.CreateType();
        if (onlyClean)
            return assembly;

        var obsoleteType = module.DefineType("PlantedObsoleteType", TypeAttributes.Public | TypeAttributes.Class);
        obsoleteType.SetCustomAttribute(marker);
        obsoleteType.CreateType();

        var members = module.DefineType("PlantedMembers", TypeAttributes.Public | TypeAttributes.Class);
        EmitEmptyMethod(members, "ObsoleteMethod", marker);
        members.DefineField("ObsoleteField", typeof(int), FieldAttributes.Public).SetCustomAttribute(marker);
        var property = members.DefineProperty("ObsoleteProperty", PropertyAttributes.None, typeof(int), null);
        property.SetCustomAttribute(marker);
        var ctor = members.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        ctor.SetCustomAttribute(marker);
        ctor.GetILGenerator().Emit(OpCodes.Ret);
        members.CreateType();

        return assembly;
    }

    private static void EmitEmptyMethod(TypeBuilder type, string name, CustomAttributeBuilder? marked)
    {
        var method = type.DefineMethod(name, MethodAttributes.Public, typeof(void), Type.EmptyTypes);
        if (marked != null)
            method.SetCustomAttribute(marked);
        method.GetILGenerator().Emit(OpCodes.Ret);
    }
}
