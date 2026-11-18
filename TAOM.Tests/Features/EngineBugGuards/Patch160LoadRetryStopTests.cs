using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.EngineBugGuards.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;

namespace TAOM.Tests.Features.EngineBugGuards;

/// <summary>
/// Patch160 (#1429, from Silas He's v1.5.6 fix package): a saved-game load whose step 3 throws spins on the loading
/// screen for ever. v1.5.7 <c>SandBoxGameManager.DoLoadingForGameManager</c> sets <c>_loadedGameResult</c> to null
/// before <c>Game.Current.DoLoading()</c>. No engine frame catches a later throw; TAOM's crash capture (Patch37)
/// swallows it at <c>Module.OnApplicationTick</c>, the step number does not advance, and every later frame retries
/// step 3 with no result. Step 3 of a saved-game load with no result left can
/// only be such a retry.
/// </summary>
[TestClass]
public class Patch160LoadRetryStopTests
{
    private const string ManagerTypeName = "SandBox.SandBoxGameManager";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Manager()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(ManagerTypeName);
        Assert.IsNotNull(type, ManagerTypeName + " did not resolve against the installed engine; Patch160's target type is gone.");
        return type;
    }

    [TestInitialize]
    public void Setup() => Patch160_LoadRetryStop.Initialize(Substitute.For<IModLogger>());

    [TestMethod]
    public void ShouldStopRetry_OnlyStepThreeOfASavedGameWithItsResultGone()
    {
        foreach (GameManagerLoadingSteps step in Enum.GetValues(typeof(GameManagerLoadingSteps)))
        foreach (var saved in new[] { false, true })
        foreach (var hasResult in new[] { false, true })
        {
            var expected = step == GameManagerLoadingSteps.SecondInitializeThirdState && saved && !hasResult;
            Assert.AreEqual(expected, Patch160_LoadRetryStop.ShouldStopRetry(step, saved, hasResult),
                $"step={step} saved={saved} hasResult={hasResult}");
        }
    }

    [TestMethod]
    public void Finalizer_LogsTheFirstFailureOfALoadOnly_AndNothingWithoutAnException()
    {
        var logger = Substitute.For<IModLogger>();
        Patch160_LoadRetryStop.Initialize(logger);
        var load = new object();

        Patch160_LoadRetryStop.Finalizer(null, load, GameManagerLoadingSteps.SecondInitializeThirdState);
        logger.DidNotReceive().LogError(Arg.Any<string>());

        Patch160_LoadRetryStop.Finalizer(new InvalidOperationException("first"), load, GameManagerLoadingSteps.SecondInitializeThirdState);
        Patch160_LoadRetryStop.Finalizer(new InvalidOperationException("second"), load, GameManagerLoadingSteps.SecondInitializeThirdState);
        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("first")));
    }

    // Each load builds a new SandBoxGameManager, so an earlier failure in the process must not hide a later load's stack
    // (lessons/state-lifecycle-save.md, "A static bool diagnostic latch is process-scoped").
    [TestMethod]
    public void Finalizer_ALaterLoadInTheSameProcess_LogsItsOwnFailure()
    {
        var logger = Substitute.For<IModLogger>();
        Patch160_LoadRetryStop.Initialize(logger);

        Patch160_LoadRetryStop.Finalizer(new InvalidOperationException("new campaign"), new object(), GameManagerLoadingSteps.PostInitializeFourthState);
        Patch160_LoadRetryStop.Finalizer(new InvalidOperationException("saved game"), new object(), GameManagerLoadingSteps.SecondInitializeThirdState);

        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("new campaign")));
        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("saved game")));
    }

    // The override sets nextStep to None on its first line, and GameManagerBase reads None from step 5 as "loading
    // finished". A PatchShield finalizer that swallowed a missing-API exception would open a half-loaded campaign, and its
    // unpatch would strip this prefix (#1429 review). The target must stay off PatchShield's list.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchShield_DoesNotShieldTheTarget()
    {
        var step = AccessTools.DeclaredMethod(Manager(), "DoLoadingForGameManager");
        Assert.IsNotNull(step);
        Assert.IsTrue(TAOM.Dependencies.Foundation.PatchShieldPolicy.IsExcludedTargetMethod(step.DeclaringType?.FullName, step.Name),
            "SandBoxGameManager.DoLoadingForGameManager is not on PatchShieldPolicy.ExcludedTargetMethods.");
    }

    // Harmony binds prefix and finalizer parameters by name (lessons/harmony-il.md).
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SandBoxGameManager_StepParameters_HaveTheNamesThePatchBinds()
    {
        var step = AccessTools.DeclaredMethod(Manager(), "DoLoadingForGameManager");
        Assert.IsNotNull(step);
        CollectionAssert.AreEqual(new[] { "gameManagerLoadingStep", "nextStep" },
            step.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SandBoxGameManager_StepMethodFieldAndProperty_ResolveAgainstInstalledEngine()
    {
        var step = AccessTools.DeclaredMethod(Manager(), "DoLoadingForGameManager",
            new[] { typeof(GameManagerLoadingSteps), typeof(GameManagerLoadingSteps).MakeByRefType() });
        Assert.IsNotNull(step, "SandBoxGameManager.DoLoadingForGameManager(step, out next) did not resolve; Patch160 targets it.");

        var result = AccessTools.Field(Manager(), "_loadedGameResult");
        Assert.IsNotNull(result, "SandBoxGameManager._loadedGameResult did not resolve; Patch160 injects it.");
        Assert.AreEqual(typeof(LoadResult), result.FieldType);

        Assert.IsNotNull(Manager().GetProperty("LoadingSavedGame", BindingFlags.Instance | BindingFlags.Public));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SandBoxGameManager_StepMethod_HasOneDeclaredOverload()
    {
        var overloads = Manager()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Count(m => m.Name == "DoLoadingForGameManager");
        Assert.AreEqual(1, overloads);
    }
}
