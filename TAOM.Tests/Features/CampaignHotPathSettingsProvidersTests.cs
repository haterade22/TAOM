using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.FieldCamp;
using TAOM.Features.SupplyLines;
using TAOM.Features.CaravanTrade;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.FieldCommission;
using TAOM.Features.FieldCommission.Domain;
using TAOM.Features.PartyIconScale;
using TAOM.Features.QuickActions;
using TAOM.Features.RealmBorders;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.WarChronicle;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features;

/// <summary>
/// Campaign settings providers are read per party per hour, per caravan score, per party per day and
/// every map frame. Each takes the MCM settings reference once, in a private lazy <c>Settings</c>
/// accessor, and reads through it; no other member may resolve MCM's <c>Instance</c>, which walks MCM's
/// settings containers on every call. The accessor caches only a non-null instance, so a resolve before
/// MCM is up cannot pin the defaults. Pattern: BattleBalanceSettingsProvider (02157b18).
/// </summary>
[TestClass]
public class CampaignHotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any type TaomSettings derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(FieldCommissionSettingsProvider))]
    [DataRow(typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(PartyIconScaleConfig))]
    [DataRow(typeof(TimeAccelerationSettingsProvider))]
    // #746: read every campaign frame (supply lines always, the field camp while it stands).
    [DataRow(typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(CampSettingsProvider))]
    // #765: the war-effect strength is re-read by the registry's daily re-bake.
    [DataRow(typeof(WarChronicleSettingsProvider))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // A second (internal, test-only) constructor must leave exactly one public constructor, or DryIoc
    // cannot select one at registration. FieldCommission registers through a delegate and
    // PartyIconScaleConfig is static, so neither is a row here.
    [DataTestMethod]
    [DataRow(typeof(ICaravanTradeSettingsProvider), typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(ICastleRecruitmentSettingsProvider), typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(IAlignmentDesertionSettingsProvider), typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(IRealmBordersSettings), typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(IQuickActionsSettingsProvider), typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(ITimeAccelerationSettingsProvider), typeof(TimeAccelerationSettingsProvider))]
    [DataRow(typeof(ISupplyLinesSettingsProvider), typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(ICampSettingsProvider), typeof(CampSettingsProvider))]
    [DataRow(typeof(IWarChronicleSettingsProvider), typeof(WarChronicleSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }

    // Read THROUGH the cached object, never snapshotted: MCM edits its one registered TaomSettings in
    // place, so an edit after the provider is built and read must reach the getter. Each "before" value
    // is the compiled MCM default, so a getter wired to the wrong setting fails too.
    [TestMethod]
    public void CaravanTrade_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICaravanTradeConfigProvider>();
        cfg.GetConfig().Returns(new CaravanTradeConfig());
        var sut = new CaravanTradeSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(1.6f, sut.RangeMultiplier, 0.0001f);

        mcm.EnableCaravanTrade = false;
        mcm.CaravanRangeMultiplier = 2.5f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(2.5f, sut.RangeMultiplier, 0.0001f);
    }

    [TestMethod]
    public void CastleRecruitment_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig());
        var sut = new CastleRecruitmentSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsAiEnabled);
        Assert.AreEqual(3, sut.NotablesPerCastle);

        mcm.EnableCastleRecruitmentAi = false;
        mcm.CastleNotablesPerCastle = 5;

        Assert.IsFalse(sut.IsAiEnabled);
        Assert.AreEqual(5, sut.NotablesPerCastle);
    }

    [TestMethod]
    public void AlignmentDesertion_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<IAlignmentDesertionConfigProvider>();
        cfg.GetConfig().Returns(new AlignmentDesertionConfig());
        var sut = new AlignmentDesertionSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsEnabled);
        Assert.AreEqual(0.5f, sut.Rate, 0.0001f);

        mcm.EnableAlignmentDesertion = false;
        mcm.AlignmentDesertionRate = 0.25f;

        Assert.IsFalse(sut.IsEnabled);
        Assert.AreEqual(0.25f, sut.Rate, 0.0001f);
    }

    [TestMethod]
    public void RealmBorders_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new RealmBordersSettingsProvider(Substitute.For<IModLogger>(), mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.HeraldicBands);

        mcm.EnableRealmBorders = false;
        mcm.RealmBordersHeraldicBands = true;

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.HeraldicBands);
    }

    [TestMethod]
    public void FieldCommission_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(new FieldCommissionConfig());
        var sut = new FieldCommissionSettingsProvider(inner, mcm);
        Assert.IsTrue(sut.GetConfig().Enabled);

        mcm.EnableFieldCommission = false;

        Assert.IsFalse(sut.GetConfig().Enabled);
    }

    [TestMethod]
    public void QuickActions_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new QuickActionsSettingsProvider(mcm);
        Assert.IsTrue(sut.EnableInventorySearch);

        mcm.EnableInventorySearch = false;

        Assert.IsFalse(sut.EnableInventorySearch);
    }

    [TestMethod]
    public void SupplyLines_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SupplyLinesSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.ShowRouteVisual);

        mcm.EnableSupplyLines = false;
        mcm.SupplyShowRouteVisual = false;
        mcm.SupplyGoodsMarkupFactor = 2f;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.ShowRouteVisual);
        Assert.AreEqual(2f, sut.GoodsMarkupFactor);
    }

    [TestMethod]
    public void FieldCamp_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new CampSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(4f, sut.CampSetupHours);

        mcm.EnableFieldCamps = false;
        mcm.CampSetupHours = 8f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(8f, sut.CampSetupHours);
    }

    [TestMethod]
    public void TimeAcceleration_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new TimeAccelerationSettingsProvider(mcm);
        Assert.AreEqual(4, sut.FastForwardMultiplier);
        Assert.AreEqual(8, sut.ExtraFastForwardMultiplier);
        Assert.AreEqual(16, sut.CtrlSpaceMultiplier);

        mcm.FastForwardMultiplier = 10;
        mcm.CtrlSpaceMultiplier = 32;

        Assert.AreEqual(10, sut.FastForwardMultiplier);
        Assert.AreEqual(10, sut.ExtraFastForwardMultiplier, "extra is floored at fast");
        Assert.AreEqual(32, sut.CtrlSpaceMultiplier);
    }

    // No-MCM pins: TaomSettings.Instance is null in the test host (MCM is never initialised), so these
    // hold today's fallbacks through the caching change.
    [TestMethod]
    public void QuickActions_NoMcm_InventorySearchDefaultsOn()
        => Assert.IsTrue(new QuickActionsSettingsProvider().EnableInventorySearch);

    [TestMethod]
    public void PartyIconScale_NoMcm_GetScaleIsTheDefault()
        => Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.GetScale(), 0.0001f);

    [TestMethod]
    public void CastleRecruitment_NoMcm_TogglesFallBackToJson()
    {
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig { Enabled = false, AiEnabled = false });
        var sut = new CastleRecruitmentSettingsProvider(cfg);

        Assert.IsFalse(sut.IsEnabled);
        Assert.IsFalse(sut.IsAiEnabled);
    }
}
