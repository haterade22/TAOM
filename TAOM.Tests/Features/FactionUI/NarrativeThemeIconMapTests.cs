using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;
using TAOM.Features.FactionUI.CharacterCreation;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Each backstory option shows its first skill's icon, matched on the text the player
/// actually sees, so the icons survive a language other than English and a change of language. The
/// options come from TAOM's own backstory data provider, the same one the backstory menus are built from.
/// </summary>
[TestClass]
public class NarrativeThemeIconMapTests
{
    private INarrativeDataProvider _menus = null!;
    private ITextLocalizerAdapter _localizer = null!;
    private NarrativeThemeIconMap _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _menus = Substitute.For<INarrativeDataProvider>();
        _menus.LoadMenuOptions(Arg.Any<string>()).Returns(Array.Empty<NarrativeOptionDefinition>());
        _menus.LoadMenuOptions("childhood").Returns(new[]
        {
            new NarrativeOptionDefinition { StringId = "taom_childhood_leadership", Text = "your leadership skills.", Skills = new[] { "Leadership", "Steward" } },
            new NarrativeOptionDefinition { StringId = "taom_childhood_nothing", Text = "nothing in particular.", Skills = Array.Empty<string>() },
        });
        _localizer = Substitute.For<ITextLocalizerAdapter>();
        _localizer.ActiveLanguage.Returns("English");
        _localizer.Localize(Arg.Any<string>()).Returns(c => ((string)c[0]).Substring(((string)c[0]).IndexOf('}') + 1));
        _sut = new NarrativeThemeIconMap(_menus, _localizer);
    }

    [TestMethod]
    public void SkillIconFor_AnOptionsShownText_ReturnsItsFirstSkillsVanillaSprite()
    {
        Assert.AreEqual(@"SPGeneral\Skills\gui_skills_icon_leadership_small", _sut.SkillIconFor("your leadership skills."));
    }

    [TestMethod]
    public void SkillIconFor_LooksUpTheTranslatedText_ThroughTheOptionsOwnKey()
    {
        _localizer.Localize("{=taom_cc_taom_childhood_leadership_text}your leadership skills.").Returns("tes talents de chef.");

        Assert.IsNotNull(_sut.SkillIconFor("tes talents de chef."));
    }

    [TestMethod]
    public void SkillIconFor_AfterTheLanguageChanges_MatchesTheNewLanguagesText()
    {
        Assert.IsNotNull(_sut.SkillIconFor("your leadership skills."));
        _localizer.ActiveLanguage.Returns("Français");
        _localizer.Localize("{=taom_cc_taom_childhood_leadership_text}your leadership skills.").Returns("tes talents de chef.");

        Assert.IsNotNull(_sut.SkillIconFor("tes talents de chef."));
        Assert.IsNull(_sut.SkillIconFor("your leadership skills."), "the English text is no longer what the player sees");
    }

    [TestMethod]
    public void SkillIconFor_ReadsTheMenusOncePerLanguage()
    {
        _sut.SkillIconFor("your leadership skills.");
        _sut.SkillIconFor("nothing in particular.");

        _menus.Received(1).LoadMenuOptions("childhood");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("nothing in particular.")]
    [DataRow("an option no menu has")]
    public void SkillIconFor_NoSkillToShow_ReturnsNull(string? text)
    {
        Assert.IsNull(_sut.SkillIconFor(text));
    }

    [TestMethod]
    public void SkillIconFor_AnOptionWithoutAnIdOrText_IsSkipped()
    {
        _menus.LoadMenuOptions("youth").Returns(new[]
        {
            new NarrativeOptionDefinition { StringId = "", Text = "no id.", Skills = new[] { "Bow" } },
            new NarrativeOptionDefinition { StringId = "taom_youth_blank", Text = "", Skills = new[] { "Bow" } },
        });

        Assert.IsNull(_sut.SkillIconFor("no id."));
    }
}
