using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding-verification gate for TAOM's GameModel overrides (the ~37 <c>Taom*Model</c> classes).
///
/// Scope note — what the C# compiler already covers vs. what this adds:
/// the compiler enforces that every <c>override</c> matches a base <c>virtual</c> with the exact
/// signature (CS0115), so a green build already proves override-signature binding. That is what
/// actually caught the v1.4.5 <c>CalculateRenownGain</c> (3→5 param) and <c>GetScoreOfStartingAlliance</c>
/// drift during the migration — the build failed. A runtime "override matches base" assertion would
/// therefore be tautological. This gate instead targets the two GameModel failure modes the compiler
/// does NOT catch:
///   1. <b>Unregistered model</b> — a Taom*Model compiles but is never <c>campaignStarter.AddModel(...)</c>'d
///      in SubModule.cs, so the engine silently uses the vanilla Default. Pure silent no-op in-game.
///   2. <b>Shadow without override</b> — a public/protected method that hides a base virtual with the
///      same signature but lacks the <c>override</c> keyword. The engine calls the base; the TAOM
///      method never runs. (C# emits CS0114 as a warning, which a warning-tolerant build ignores.)
/// </summary>
[TestClass]
public class GameModelOverrideBindingTests
{
    private const string GameModelRoot = "TaleWorlds.Core.GameModel";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Assembly TaomAssembly => typeof(TAOM.IoC).Assembly;

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryTaomGameModel_IsRegistered_InSubModule()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        if (models.Count < 20)
            Assert.Inconclusive($"Only {models.Count} GameModel subclasses discovered (expected ~37) — assembly-load problem, not a genuine pass.");

        var subModule = ReadRepoFile("Main", "SubModule.cs");
        if (subModule == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root.");

        var unregistered = models
            .Where(m => !subModule.Contains($"new {m.Name}("))
            .Select(m => m.FullName)
            .ToList();

        if (unregistered.Count > 0)
            Assert.Fail(
                $"{unregistered.Count} GameModel(s) compile but are never AddModel'd in SubModule.cs. " +
                "The engine will silently use the vanilla Default instead — a no-op in-game:\n  " +
                string.Join("\n  ", unregistered));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryTaomGameModel_OverridesABaseVirtual_AndDoesNotShadowWithoutOverride()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        if (models.Count < 20)
            Assert.Inconclusive($"Only {models.Count} GameModel subclasses discovered — assembly-load problem.");

        var problems = new List<string>();
        foreach (var model in models)
        {
            var declared = model.GetMethods(DeclaredMembers).Where(m => !m.IsStatic).ToList();

            if (!declared.Any(IsOverride))
                problems.Add($"{model.Name}: declares no override — the model extends {model.BaseType?.Name} but changes nothing.");

            foreach (var method in declared.Where(m => (m.IsPublic || m.IsFamily) && !IsOverride(m) && !m.IsVirtual))
            {
                var shadowed = FindBaseVirtual(model.BaseType, method.Name,
                    method.GetParameters().Select(p => p.ParameterType).ToArray());
                if (shadowed != null)
                    problems.Add(
                        $"{model.Name}.{method.Name}(...) shadows virtual {shadowed.DeclaringType?.Name}.{method.Name} " +
                        "without 'override' — the engine calls the base method, this one never runs.");
            }
        }

        if (problems.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{problems.Count} GameModel override-integrity problem(s):");
            foreach (var p in problems) sb.AppendLine("  " + p);
            Assert.Fail(sb.ToString());
        }
    }

    // --- helpers ---

    private static List<Type> DiscoverGameModels()
    {
        Type[] all;
        try { all = TaomAssembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray(); }

        return all
            .Where(t => t != null && !t.IsAbstract && !t.Name.Contains('<'))
            .Where(DerivesFromGameModel)
            .OrderBy(t => t.Name)
            .ToList();
    }

    private static bool DerivesFromGameModel(Type t)
    {
        for (var b = t.BaseType; b != null; b = b.BaseType)
            if (b.FullName == GameModelRoot)
                return true;
        return false;
    }

    private static bool IsOverride(MethodInfo m) =>
        m.IsVirtual && !m.IsAbstract && m.GetBaseDefinition().DeclaringType != m.DeclaringType;

    private static MethodInfo FindBaseVirtual(Type start, string name, Type[] parameterTypes)
    {
        for (var b = start; b != null && b.FullName != GameModelRoot; b = b.BaseType)
        {
            var m = b.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null, types: parameterTypes, modifiers: null);
            if (m != null && m.IsVirtual)
                return m;
        }
        return null;
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        if (dir == null) return null;
        var path = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
