using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// #993: <c>OrderOfBattleVM</c> builds every hero with <c>new OrderOfBattleHeroItemVM(Agent)</c>, whose
/// constructor sets <c>ImageIdentifier</c> itself and never calls <c>RefreshInformation</c>, so a
/// patch on <c>RefreshInformation</c> alone left the initial portraits uncoloured. The patch must
/// target both.
/// </summary>
[TestClass]
public class OrderOfBattleHeroItemVMPatchTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TargetMethods_ResolveRefreshInformationAndTheAgentConstructor()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var provider = AccessTools.Method(typeof(OrderOfBattleHeroItemVM_RefreshInformation_Patch), "TargetMethods");
        Assert.IsNotNull(provider, "The patch has no TargetMethods(); it would only hook RefreshInformation.");

        var targets = ((IEnumerable<MethodBase>)provider!.Invoke(null, null)!).ToList();

        Assert.AreEqual(2, targets.Count);
        Assert.IsTrue(targets.All(t => t != null), "A target did not resolve against the installed engine.");
        Assert.IsTrue(targets.Any(t => t.Name == nameof(OrderOfBattleHeroItemVM.RefreshInformation)));
        var ctor = targets.OfType<ConstructorInfo>().SingleOrDefault();
        Assert.IsNotNull(ctor, "The Agent constructor is not a target.");
        CollectionAssert.AreEqual(new[] { typeof(Agent) }, ctor!.GetParameters().Select(p => p.ParameterType).ToArray());
    }
}
