using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The themed clock's time buttons. Under co-op the themed map bar shows too, and a peer that defers to the co-op
/// server cannot run Extra Fast Forward, so that button binds its visibility to the time mixin's
/// IsExtraFastForwardAvailable (TimeAccelerationMixinTests covers the value).
/// </summary>
[TestClass]
public class CampaignTimePrefabTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMCampaignTime.xml";

    private static XElement Button(string id) =>
        XDocument.Parse(ReadSource(PrefabPath)).Root!.Descendants("ButtonWidget").Single(b => (string?)b.Attribute("Id") == id);

    [TestMethod]
    public void ExtraFastForwardButton_BindsItsVisibilityToTheMixin()
    {
        Assert.AreEqual("@IsExtraFastForwardAvailable", (string?)Button("FastFastForwardButton").Attribute("IsVisible"));
    }

    [TestMethod]
    public void FastForwardButton_StaysVisible()
    {
        Assert.IsNull(Button("FastForwardButton").Attribute("IsVisible"), "plain fast forward is a request the co-op server decides");
    }
}
