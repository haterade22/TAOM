using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>#1113: the one MCM switch. Its default is on, and a default that ships can never be flipped under this name.</summary>
[TestClass]
public class WarPledgeSettingsProviderTests
{
    [TestMethod]
    public void Enabled_DefaultSettings_IsOn()
    {
        Assert.IsTrue(new WarPledgeSettingsProvider(new TaomSettings()).Enabled);
    }

    [TestMethod]
    public void Enabled_SwitchedOff_IsOff()
    {
        Assert.IsFalse(new WarPledgeSettingsProvider(new TaomSettings { WarPledgeEnabled = false }).Enabled);
    }
}
