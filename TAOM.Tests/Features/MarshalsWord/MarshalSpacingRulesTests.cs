using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The spacing repair (build-spec 9.3): when it may apply at all, and how a company's four limits read as infantry or as cavalry.</summary>
[TestClass]
public class MarshalSpacingRulesTests
{
    // Formation.InfantryInterval/Distance and CavalryInterval/Distance (Formation.cs:3074-3092) at spacing 1.
    private static readonly SpacingLimits Infantry = new(MinDistance: 0f, MaxDistance: 0.4f, MinInterval: 0f, MaxInterval: 0.38f);
    private static readonly SpacingLimits Cavalry = new(MinDistance: 1.7f, MaxDistance: 2.0f, MinInterval: 0.18f, MaxInterval: 0.5f);

    [TestMethod]
    public void WhyNot_EverythingOn_IsEmpty()
    {
        Assert.AreEqual("", MarshalSpacingRules.WhyNot(enabled: true, repair: true, coopActive: false, dedicatedServer: false));
    }

    [TestMethod]
    [DataRow(false, true, false, false, "setting-off")]
    [DataRow(true, false, false, false, "repair-off")]
    [DataRow(true, true, true, false, "coop")]
    [DataRow(true, true, false, true, "dedicated-server")]
    public void WhyNot_EachReason_IsNamedOnItsOwn(bool enabled, bool repair, bool coop, bool dedicated, string reason)
    {
        Assert.AreEqual(reason, MarshalSpacingRules.WhyNot(enabled, repair, coop, dedicated));
    }

    [TestMethod]
    public void WhyNot_SeveralReasons_NamesTheFirstOnly()
    {
        Assert.AreEqual("setting-off", MarshalSpacingRules.WhyNot(false, true, true, true));
    }

    [TestMethod]
    public void Read_AllFourLimitsAtTheInfantryValues_ReadsAsInfantry()
    {
        Assert.AreEqual(SpacingReading.Infantry, MarshalSpacingRules.Read(Infantry, Infantry, Cavalry));
    }

    [TestMethod]
    public void Read_AllFourLimitsAtTheCavalryValues_ReadsAsCavalry()
    {
        Assert.AreEqual(SpacingReading.Cavalry, MarshalSpacingRules.Read(Cavalry, Infantry, Cavalry));
    }

    [TestMethod]
    public void Read_OneLimitStillAtTheCavalryValue_ReadsMixed_SoAHalfRepairedCompanyFails()
    {
        var oneWrong = Infantry with { MinDistance = Cavalry.MinDistance };

        Assert.AreEqual(SpacingReading.Mixed, MarshalSpacingRules.Read(oneWrong, Infantry, Cavalry));
    }

    [TestMethod]
    public void Read_ALimitThatIsNotANumber_ReadsMixed()
    {
        Assert.AreEqual(SpacingReading.Mixed, MarshalSpacingRules.Read(Infantry with { MaxInterval = float.NaN }, Infantry, Cavalry));
    }

    [TestMethod]
    public void Read_InfantryAndCavalryValuesThatCoincide_ProveNothing()
    {
        Assert.AreEqual(SpacingReading.NoTest, MarshalSpacingRules.Read(Infantry, Infantry, Infantry));
    }

    [TestMethod]
    public void Read_OnlyTheLimitsThatDifferAreTested()
    {
        var infantry = new SpacingLimits(0f, 0.9f, 0f, 0.9f);
        var cavalry = new SpacingLimits(1.7f, 0.9f, 0.18f, 0.9f);

        Assert.AreEqual(SpacingReading.Infantry, MarshalSpacingRules.Read(infantry, infantry, cavalry), "the two maxima coincide and cannot tell");
        Assert.AreEqual(SpacingReading.Cavalry, MarshalSpacingRules.Read(cavalry, infantry, cavalry));
    }
}
