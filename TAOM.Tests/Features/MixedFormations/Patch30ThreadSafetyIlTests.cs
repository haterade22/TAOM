using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.MixedFormations;
using TAOM.Features.MixedFormations.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MixedFormations;

/// <summary>
/// <c>Patch30_FormationGetOrderPositionOfUnit</c> runs on the engine's TWParallel workers for every unit of every
/// formation (plan 032). Its body needs a live mission and scene, so these are structural checks of the IL, not
/// behaviour tests: the prefix contains no <c>FormationAdapter</c> construction, a call to the service's
/// <c>FindLaidOutFormation</c> and a call to <c>NoteFallback</c>, and two adapter getters read the engine members
/// they are meant to. A call being present proves nothing about its order, about the control flow around it (that
/// the catch is where <c>NoteFallback</c> is called, say) or about what the engine does when the prefix runs.
/// <c>FormationLayoutServiceTests</c> covers the service's behaviour; the prefix itself is checked in game.
/// </summary>
[TestClass]
public class Patch30ThreadSafetyIlTests
{
    private static MethodBase[] CallsIn(MethodBase m) =>
        IlCallScanner.ExtractCalledMethods(m, m.GetMethodBody().GetILAsByteArray()).ToArray();

    private static readonly MethodInfo Prefix =
        typeof(Patch30_FormationGetOrderPositionOfUnit).GetMethod(nameof(Patch30_FormationGetOrderPositionOfUnit.Prefix));

    [TestMethod]
    public void Prefix_NeverConstructsAFormationAdapter()
    {
        Assert.IsFalse(CallsIn(Prefix).Any(m => m is ConstructorInfo && m.DeclaringType == typeof(FormationAdapter)),
            "the worker-thread prefix must reuse the adapter the service holds, never allocate one per call");
    }

    [TestMethod]
    public void RepresentativeIsCavalry_StillReadsTheEvaluatingQuery()
    {
        var getter = typeof(FormationAdapter).GetProperty(nameof(FormationAdapter.RepresentativeIsCavalry))!.GetMethod;

        Assert.IsTrue(CallsIn(getter).Any(m => m.DeclaringType == typeof(FormationQuerySystem) && m.Name == "get_IsCavalryFormation"),
            "RepresentativeIsCavalry (SmartCavalryAI's flag and MixedFormations' gate) must keep reading the evaluating FormationQuerySystem.IsCavalryFormation");
    }

    [TestMethod]
    public void Prefix_AsksTheServiceForTheLaidOutFormation()
    {
        Assert.IsTrue(CallsIn(Prefix).Any(m => m.DeclaringType == typeof(IFormationLayoutService)
                && m.Name == nameof(IFormationLayoutService.FindLaidOutFormation)),
            "the prefix must call the service's lock-free FindLaidOutFormation lookup");
    }

    // A throw in the prefix body falls back to vanilla; the service logs the first of each mission in full and
    // counts the rest for its mission-end summary (FormationLayoutServiceTests pins both lines).
    [TestMethod]
    public void Prefix_ReportsAThrowToTheService()
    {
        Assert.IsTrue(CallsIn(Prefix).Any(m => m.DeclaringType == typeof(IFormationLayoutService)
                && m.Name == nameof(IFormationLayoutService.NoteFallback)),
            "the prefix must call the service's NoteFallback (fallback accounting)");
    }

    // The service's layouts are keyed by IFormationAdapter.FormationKey, and the prefix looks them up by the engine
    // Formation itself; the two meet only because the adapter's key is its wrapped formation. If that ever
    // changed, every lookup would miss and Mixed Formations would stop placing units with no error.
    [TestMethod]
    public void FormationKey_IsTheWrappedFormation()
    {
        var getter = typeof(FormationAdapter).GetProperty(nameof(FormationAdapter.FormationKey))!.GetMethod;
        var il = getter.GetMethodBody().GetILAsByteArray();
        var formationField = typeof(FormationAdapter).GetField("_formation", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(formationField, "FormationAdapter._formation is gone");

        // The body ends ldfld _formation; ret (a Debug build may put nops before it), and calls nothing.
        Assert.AreEqual(0, CallsIn(getter).Length, "the FormationKey getter must be a plain field read");
        Assert.AreEqual(0x2A, il[il.Length - 1], "the getter must return straight after the field read");
        Assert.AreEqual(0x7B, il[il.Length - 6], "the getter must return a field");
        Assert.AreEqual(formationField, typeof(FormationAdapter).Module.ResolveField(System.BitConverter.ToInt32(il, il.Length - 5)),
            "FormationKey must return the wrapped Formation, the key Patch30 looks the layout up by");
    }
}
