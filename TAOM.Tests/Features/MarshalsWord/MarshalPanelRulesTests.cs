using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>Where the panel set goes (build-spec 10): before the legacy layout's trailing Return, last everywhere else.</summary>
[TestClass]
public class MarshalPanelRulesTests
{
    [TestMethod]
    public void SetIndex_LegacyWithATrailingReturn_GoesBeforeIt_SoItTakesTheEighthShortcut()
    {
        Assert.AreEqual(7, MarshalPanelRules.SetIndex(MarshalPanelRules.LegacyLayout, setCount: 8, lastIsReturn: true));
    }

    [TestMethod]
    public void SetIndex_LegacyOnAGamepad_HasNoTrailingReturn_AndGoesLast()
    {
        Assert.AreEqual(3, MarshalPanelRules.SetIndex(MarshalPanelRules.LegacyLayout, setCount: 3, lastIsReturn: false));
    }

    [TestMethod]
    public void SetIndex_TheDefaultLayout_GoesLast_EvenWhenItsLastSetIsAReturn()
    {
        Assert.AreEqual(9, MarshalPanelRules.SetIndex(0, setCount: 9, lastIsReturn: false));
        Assert.AreEqual(5, MarshalPanelRules.SetIndex(0, setCount: 5, lastIsReturn: true), "only the legacy layout keeps a Return it must stay in front of");
    }

    [TestMethod]
    public void SetIndex_AnEmptyList_IsIndexZero()
    {
        Assert.AreEqual(0, MarshalPanelRules.SetIndex(MarshalPanelRules.LegacyLayout, setCount: 0, lastIsReturn: true));
        Assert.AreEqual(0, MarshalPanelRules.SetIndex(0, 0, false));
    }
}
