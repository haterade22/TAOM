using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class NarrativeMenuBuilderTests
{
    private IModLogger _logger;
    private IEquipmentRosterProvider _equipmentRosterProvider;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _equipmentRosterProvider = Substitute.For<IEquipmentRosterProvider>();
    }

    [TestMethod]
    public void BuildEquipmentRosterId_Male_ReturnsCorrectFormat()
    {
        var result = NarrativeMenuBuilder.BuildEquipmentRosterId("gondor", "retainer", isFemale: false);

        Assert.AreEqual("player_char_creation_gondor_retainer_m", result);
    }

    [TestMethod]
    public void BuildEquipmentRosterId_Female_ReturnsCorrectFormat()
    {
        var result = NarrativeMenuBuilder.BuildEquipmentRosterId("mordor", "hunter", isFemale: true);

        Assert.AreEqual("player_char_creation_mordor_hunter_f", result);
    }

    [TestMethod]
    public void BuildEquipmentRosterId_PreservesCultureCase()
    {
        var result = NarrativeMenuBuilder.BuildEquipmentRosterId("dolguldur", "infantry", isFemale: false);

        Assert.AreEqual("player_char_creation_dolguldur_infantry_m", result);
    }
}
