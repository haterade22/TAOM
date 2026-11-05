using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Engine pin for MapMenuGate (Main/Adapters/MapMenuGate.cs): it calls vanilla
/// CampaignUIHelper.GetMapScreenActionIsEnabledWithReason as MapScreen.OnFrameTick does (v1.4.8
/// CampaignUIHelper.cs:2387, MapScreen.cs:1355). Pinned so a rename or a shape change after an engine update is
/// a red test rather than a MissingMethodException on the first F6 press.
/// </summary>
[TestClass]
public class MapMenuGateBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetMapScreenActionIsEnabledWithReason_IsAPublicStaticBoolWithAnOutTextObject()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var helper = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper);

        var method = helper.GetMethod("GetMapScreenActionIsEnabledWithReason", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(method, "CampaignUIHelper.GetMapScreenActionIsEnabledWithReason is gone; the map-menu gate lost vanilla's own term.");
        Assert.AreEqual(typeof(bool), method!.ReturnType);
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.IsTrue(parameters[0].IsOut);
        Assert.AreEqual(typeof(TaleWorlds.Localization.TextObject).MakeByRefType(), parameters[0].ParameterType);
    }
}
