using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>#846: the one MCM toggle. The hot-path read-through rule is pinned in CampaignHotPathSettingsProvidersTests.</summary>
[TestClass]
public class HiddenHostsSettingsProviderTests
{
    [TestMethod]
    public void Enabled_CompiledDefault_IsOn() =>
        Assert.IsTrue(new HiddenHostsSettingsProvider(new TaomSettings()).Enabled);

    [TestMethod]
    public void Enabled_FollowsTheMcmSetting()
    {
        var mcm = new TaomSettings { HiddenHostsEnabled = false };

        Assert.IsFalse(new HiddenHostsSettingsProvider(mcm).Enabled);
    }

    [TestMethod]
    public void Enabled_NoMcm_FallsBackToOn() => Assert.IsTrue(new HiddenHostsSettingsProvider().Enabled);

    [TestMethod]
    public void Setting_NeedsNoRestart_AndLivesUnderTheMapUiPage()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.HiddenHostsEnabled))!;
        var attributes = property.GetCustomAttributesData();

        var text = string.Join("|", attributes.Select(a => a.ToString()));
        StringAssert.Contains(text, "Map UI/{=taom_mcm_group_hidden_hosts}Hidden Hosts");
        StringAssert.Contains(text, "RequireRestart = False");
    }
}
