using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.NameplateCull;
using TAOM.Features.NameplateCull.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// The thin prefix: true runs vanilla, false skips it, and every doubt is true. The signature mentions a SandBox type, so
/// the class needs the game assemblies; the prefix is called with a null instance, which it only passes through.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NameplateCullHookTests
{
    private static bool _gameLoaded;
    private INameplateCullService _service = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _service = Substitute.For<INameplateCullService>();
        NameplateCullCalls.Initialize(_service);
    }

    [TestCleanup]
    public void Cleanup() => NameplateCullCalls.Initialize(null);

    // By reflection: the prefix's parameter type lives in SandBox.ViewModelCollection, which the test project does not reference.
    private static bool RunPrefix() =>
        (bool)typeof(SettlementNameplatesVM_Update_NameplateCull_Patch).GetMethod("Prefix")!.Invoke(null, new object?[] { null })!;

    [TestMethod]
    public void Prefix_NoService_RunsVanilla()
    {
        NameplateCullCalls.Initialize(null);

        Assert.IsTrue(RunPrefix());
    }

    [TestMethod]
    public void Prefix_TheCullRanTheUpdate_SkipsVanilla()
    {
        _service.TryUpdate(Arg.Any<object>()).Returns(true);

        Assert.IsFalse(RunPrefix(), "false skips the original");
    }

    [TestMethod]
    public void Prefix_TheCullDidNotRun_RunsVanilla()
    {
        _service.TryUpdate(Arg.Any<object>()).Returns(false);

        Assert.IsTrue(RunPrefix());
    }

    [TestMethod]
    public void Prefix_TheServiceThrows_RunsVanilla()
    {
        _service.TryUpdate(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));

        Assert.IsTrue(RunPrefix());
    }

    // The real patch class applied to the real method, so Harmony builds the replacement and the JIT compiles it (a member
    // the replacement cannot resolve, or an initializer that cannot run, would surface here), then takes it off again. The
    // method is never run: a nameplate view model cannot be built in a test host. Compiling the patched Update at this point
    // is also the check that installing at game init needs no campaign state.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        const string id = "taom.tests.nameplatecull.jit";
        var harmony = new Harmony(id);
        var target = AccessTools.Method(AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM"), "Update", Type.EmptyTypes);
        try
        {
            harmony.CreateClassProcessor(typeof(SettlementNameplatesVM_Update_NameplateCull_Patch)).Patch();

            var info = Harmony.GetPatchInfo(target);
            Assert.IsTrue(info.Prefixes.Any(p => p.owner == id), "the prefix is attached");
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
