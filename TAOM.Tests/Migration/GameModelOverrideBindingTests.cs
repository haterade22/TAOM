using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopProgression.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding-verification gate for TAOM's GameModel overrides (the <c>Taom*Model</c> classes;
/// taleworlds-api-snapshot/gamemodel-bases.md has the current count).
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

    /// <summary>
    /// GameModels that compile but are deliberately NOT registered, by class name, with the reason.
    /// Checked against the comment-stripped SubModule, so a commented-out AddModel line no longer
    /// counts as a registration; a parked model is reported as parked, never as registered.
    /// </summary>
    private static readonly Dictionary<string, string> ParkedModels = new(StringComparer.Ordinal)
    {
        ["TaomPartyNavigationModel"] =
            "NavalTravel parked 2026-06-26 at the SubModule wiring (#296/#120): TAOM_Map has no naval navmesh",
    };

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
        // The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.
        if (models.Count < 20)
            Assert.Fail($"Only {models.Count} GameModel subclasses discovered (taleworlds-api-snapshot/gamemodel-bases.md has the current count) — assembly-load problem, not a genuine pass.");

        // Comment-stripped: a commented-out AddModel line is not a registration.
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        foreach (var parked in models.Where(m => ParkedModels.ContainsKey(m.Name)))
            Console.WriteLine($"Parked, not registered by design: {parked.FullName} ({ParkedModels[parked.Name]})");

        var unregistered = models
            .Where(m => !ParkedModels.ContainsKey(m.Name))
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
    public void ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var names = DiscoverGameModels().Select(m => m.Name).ToList();
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        foreach (var parked in ParkedModels)
        {
            Assert.IsTrue(names.Contains(parked.Key),
                $"{parked.Key} is listed as parked but no longer exists as a GameModel: remove it from ParkedModels.");
            Assert.IsFalse(subModule.Contains($"new {parked.Key}("),
                $"{parked.Key} is registered again: remove it from ParkedModels ({parked.Value}).");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryTaomGameModel_OverridesABaseVirtual_AndDoesNotShadowWithoutOverride()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        // The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.
        if (models.Count < 20)
            Assert.Fail($"Only {models.Count} GameModel subclasses discovered — assembly-load problem.");

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

    /// <summary>
    /// #394 regression pin. The career <c>Health</c> passive is applied campaign-side by
    /// <c>TaomCharacterStatsModel.MaxHitpoints</c> — that override is the ONLY thing that puts a
    /// "max health" pip into the character-screen tooltip, <c>Hero.MaxHitPoints</c>, and the
    /// daily heal cap. It shipped missing, so the pip was invisible and inert outside a mission.
    ///
    /// The two generic gates above cannot catch its removal: the model still declares
    /// <c>MaxCharacterTier</c>, so "declares no override" stays green while the health limb is gone.
    /// This test also pins the exact v1.4.7 signature — the second parameter is
    /// <c>bool includeDescriptions</c>, NOT a StatExplainer — so an engine-bump rewrite that guesses
    /// wrong fails here instead of silently binding to nothing.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TaomCharacterStatsModel_DeclaresMaxHitpointsOverride_ForCareerHealthPassive()
    {
        var declared = typeof(TaomCharacterStatsModel)
            .GetMethods(DeclaredMembers)
            .Where(m => m.Name == "MaxHitpoints")
            .ToList();

        if (declared.Count == 0)
            Assert.Fail(
                "TaomCharacterStatsModel declares no MaxHitpoints override, so nothing applies the " +
                "career Health passive on the campaign layer: the character screen, Hero.MaxHitPoints " +
                "and the daily heal cap all fall through to DefaultCharacterStatsModel's flat 100. " +
                "A 'max health' career pip is then invisible and inert outside a mission (#394).");

        var method = declared[0];
        Assert.IsTrue(IsOverride(method),
            "TaomCharacterStatsModel.MaxHitpoints does not carry 'override' — it shadows the base " +
            "virtual, so the engine calls DefaultCharacterStatsModel and the career Health passive never runs.");

        var parameters = method.GetParameters();
        CollectionAssert.AreEqual(
            new[] { typeof(CharacterObject), typeof(bool) },
            parameters.Select(p => p.ParameterType).ToArray(),
            "MaxHitpoints signature drifted from the v1.4.7 engine's " +
            "(CharacterObject character, bool includeDescriptions = false).");
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
}
