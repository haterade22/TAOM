using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

[TestClass]
[TestCategory("RequiresGame")]
public class MapProfilerTargetsTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void MenuTargets_ResolveToTheThreeMenuProbeMethods_InOrder()
    {
        var targets = MapProfilerTargets.MenuTargets();

        Assert.AreEqual(3, targets.Count);
        Assert.IsTrue(targets.All(t => t != null), "A menu probe target did not resolve.");
        Assert.AreEqual("GameMenuVM.OnFrameTick", targets[0]!.DeclaringType!.Name + "." + targets[0]!.Name);
        Assert.AreEqual(0, targets[0]!.GetParameters().Length);
        Assert.AreEqual("GameMenuVM.set_ContextText", targets[1]!.DeclaringType!.Name + "." + targets[1]!.Name);
        CollectionAssert.AreEqual(new[] { typeof(string) }, targets[1]!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(ServiceStatusService).GetMethod(nameof(ServiceStatusService.RefreshIfChanged)), targets[2]);
        Assert.AreEqual(0, targets[2]!.GetParameters().Length);
    }
}
