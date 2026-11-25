using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.TrainingGrounds.Teams;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

[TestClass]
public class TeamPracticeSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsOn()
    {
        Assert.IsTrue(TeamPracticeSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefault_MatchesTheNoSettingsFallback()
    {
        Assert.AreEqual(TeamPracticeSettingsProvider.From(null), TeamPracticeSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_SwitchedOff_IsOff()
    {
        Assert.IsFalse(TeamPracticeSettingsProvider.From(new TaomSettings { EnableTeamPracticeFights = false }));
    }

    [TestMethod]
    public void IsEnabled_NoMcmInstance_IsOn()
    {
        Assert.IsTrue(new TeamPracticeSettingsProvider().IsEnabled);
    }
}
