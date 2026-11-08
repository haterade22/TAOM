using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;

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

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BuildOption_EmptyBonusNames_ReturnsOptionWithoutWarnings()
    {
        var sut = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);

        var option = sut.BuildOption(Definition("taom_empty"));

        Assert.AreEqual("taom_empty", option.StringId);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BuildOption_UnknownBonusNames_LogsExactWarningsAndReturnsOption()
    {
        var sut = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);
        var definition = Definition("taom_unknown");
        definition.Skills = new[] { "NotASkill" };
        definition.Attribute = "NotAnAttribute";

        var option = sut.BuildOption(definition);

        Assert.AreEqual("taom_unknown", option.StringId);
        _logger.Received(1).LogWarning("Unknown skill name: 'NotASkill'");
        _logger.Received(1).LogWarning("Unknown attribute name: 'NotAnAttribute'");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BuildOption_NullSkillName_ThrowsArgumentNullException()
    {
        var sut = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);
        var definition = Definition("taom_invalid");
        definition.Skills = new string[] { null! };

        Assert.ThrowsException<ArgumentNullException>(() => sut.BuildOption(definition));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AddOptionsToMenu_InvalidThenValid_LogsSkipsAndContinues()
    {
        var sut = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);
        var invalid = Definition("taom_invalid");
        invalid.Skills = new string[] { null! };
        var valid = Definition("taom_valid");
        var menu = new NarrativeMenu(
            "test_menu", "", "", new TextObject("Title"), new TextObject("Description"),
            new List<NarrativeMenuCharacter>(), null);

        var added = sut.AddOptionsToMenu(menu, new[] { invalid, valid });

        Assert.AreEqual(1, added);
        Assert.AreEqual(1, menu.CharacterCreationMenuOptions.Count);
        Assert.AreEqual("taom_valid", menu.CharacterCreationMenuOptions[0].StringId);
        _logger.Received(1).LogError(Arg.Is<string>(message => message.Contains("taom_invalid")));
    }

    private static NarrativeOptionDefinition Definition(string stringId) => new()
    {
        StringId = stringId,
        Text = "Text",
        Description = "Description",
        Skills = Array.Empty<string>(),
        Attribute = "",
        OccupationType = "",
        TitleType = "",
        CultureId = "",
    };
}
