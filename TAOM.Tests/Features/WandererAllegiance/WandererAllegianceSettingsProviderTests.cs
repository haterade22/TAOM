using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// Pins the MCM-over-JSON merge. <c>TaomSettings.Instance</c> is null in the MSTest host, which is
/// what makes the fail direction (MCM absent, JSON wins) testable at all.
/// </summary>
[TestClass]
public class WandererAllegianceSettingsProviderTests
{
    private static WandererAllegianceSettingsProvider Build(bool enabled, string scope)
    {
        var configProvider = Substitute.For<IWandererAllegianceConfigProvider>();
        configProvider.GetConfig().Returns(new WandererAllegianceConfig { Enabled = enabled, Scope = scope });
        return new WandererAllegianceSettingsProvider(configProvider);
    }

    [TestMethod]
    public void IsEnabled_McmAbsent_FallsBackToJson()
    {
        Assert.IsNull(TaomSettings.Instance, "this test assumes MCM is not loaded in the test host");

        Assert.IsFalse(Build(enabled: false, scope: WandererAllegianceConfig.ScopeAllWanderers).IsEnabled);
        Assert.IsTrue(Build(enabled: true, scope: WandererAllegianceConfig.ScopeAllWanderers).IsEnabled);
    }

    [TestMethod]
    public void Scope_McmAbsent_FallsBackToJson()
    {
        Assert.IsNull(TaomSettings.Instance, "this test assumes MCM is not loaded in the test host");

        Assert.AreEqual(WandererAllegianceScope.NamedCompanionsOnly,
            Build(enabled: true, scope: WandererAllegianceConfig.ScopeNamedCompanionsOnly).Scope);
        Assert.AreEqual(WandererAllegianceScope.AllWanderers,
            Build(enabled: true, scope: WandererAllegianceConfig.ScopeAllWanderers).Scope);
    }

    [TestMethod]
    public void ResolveScope_Index0_AllWanderers()
        => Assert.AreEqual(WandererAllegianceScope.AllWanderers, WandererAllegianceSettingsProvider.ResolveScope(0));

    [TestMethod]
    public void ResolveScope_Index1_NamedCompanionsOnly()
        => Assert.AreEqual(WandererAllegianceScope.NamedCompanionsOnly, WandererAllegianceSettingsProvider.ResolveScope(1));

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    [DataRow(99)]
    public void ResolveScope_OutOfRange_ReturnsNull(int index)
        => Assert.IsNull(WandererAllegianceSettingsProvider.ResolveScope(index));

    [TestMethod]
    public void ResolveScope_NoDropdown_ReturnsNull()
        => Assert.IsNull(WandererAllegianceSettingsProvider.ResolveScope(null));

    [TestMethod]
    public void ParseScope_UnknownString_IsAllWanderers()
    {
        // The config provider already normalises the string; this is the last line of defence
        // for a value that reached the provider some other way.
        Assert.AreEqual(WandererAllegianceScope.AllWanderers, WandererAllegianceSettingsProvider.ParseScope("garbage"));
        Assert.AreEqual(WandererAllegianceScope.AllWanderers, WandererAllegianceSettingsProvider.ParseScope(null));
        Assert.AreEqual(WandererAllegianceScope.NamedCompanionsOnly, WandererAllegianceSettingsProvider.ParseScope("namedcompanionsonly"));
    }

    // The MCM dropdown and the compiled default are two statements of the same choice; the same
    // drift guard AiPartySizeServiceTests keeps on its dropdown.
    [TestMethod]
    public void McmDropdownDefault_ResolvesToTheCompiledDefault()
    {
        var settings = new TaomSettings();
        var dropdown = settings.WandererAllegianceScope;

        Assert.AreEqual(2, dropdown.Count, "two scopes: all wanderers, named companions only");
        Assert.AreEqual(WandererAllegianceScope.AllWanderers,
            WandererAllegianceSettingsProvider.ResolveScope(dropdown.SelectedIndex));
        Assert.IsTrue(settings.EnableWandererAllegiance, "the shipped MCM default must match the shipped JSON (enabled)");
    }
}
