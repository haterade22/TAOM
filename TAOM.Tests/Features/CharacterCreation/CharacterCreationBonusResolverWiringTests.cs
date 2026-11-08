using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CharacterCreationBonusResolverWiringTests
{
    private const string ResolverPath = "Main/Features/CharacterCreation/CharacterCreationBonusResolver.cs";
    private const string CareerPath = "Main/Features/CharacterCreation/CareerMenuService.cs";
    private const string NarrativePath = "Main/Features/CharacterCreation/NarrativeMenuBuilder.cs";

    [DataTestMethod]
    [DataRow("OneHanded")]
    [DataRow("TwoHanded")]
    [DataRow("Polearm")]
    [DataRow("Bow")]
    [DataRow("Crossbow")]
    [DataRow("Throwing")]
    [DataRow("Riding")]
    [DataRow("Athletics")]
    [DataRow("Crafting")]
    [DataRow("Scouting")]
    [DataRow("Tactics")]
    [DataRow("Roguery")]
    [DataRow("Charm")]
    [DataRow("Leadership")]
    [DataRow("Trade")]
    [DataRow("Steward")]
    [DataRow("Medicine")]
    [DataRow("Engineering")]
    public void Resolver_MapsSkillToMatchingEngineFactory(string name)
    {
        var source = ResolverSource();

        StringAssert.Contains(source, $"[\"{name}\"] = () => DefaultSkills.{name}");
    }

    [DataTestMethod]
    [DataRow("Vigor")]
    [DataRow("Control")]
    [DataRow("Endurance")]
    [DataRow("Cunning")]
    [DataRow("Social")]
    [DataRow("Intelligence")]
    public void Resolver_MapsAttributeToMatchingEngineFactory(string name)
    {
        var source = ResolverSource();

        StringAssert.Contains(source, $"[\"{name}\"] = () => DefaultCharacterAttributes.{name}");
    }

    [TestMethod]
    public void Resolver_UsesCaseInsensitiveMaps()
    {
        var source = ResolverSource();

        Assert.AreEqual(2, Count(source, "StringComparer.OrdinalIgnoreCase"));
    }

    [TestMethod]
    public void Resolver_PreservesUnknownNameWarnings()
    {
        var source = ResolverSource();

        StringAssert.Contains(source, "$\"Unknown skill name: '{name}'\"");
        StringAssert.Contains(source, "$\"Unknown attribute name: '{attributeName}'\"");
    }

    [TestMethod]
    public void Resolver_PreservesNarrativeNullWarnings()
    {
        var source = ResolverSource();

        StringAssert.Contains(source, "$\"Skill '{name}' resolved to null — game may not be initialized\"");
        StringAssert.Contains(source, "$\"Attribute '{attributeName}' resolved to null — game may not be initialized\"");
    }

    [TestMethod]
    public void Resolver_DoesNotCatchFactoryExceptions()
    {
        var source = ResolverSource();

        Assert.IsFalse(source.Contains("catch"));
    }

    [TestMethod]
    public void Career_DelegatesInsideCallbackWithSilentNullPolicy()
    {
        var source = RepoPaths.ReadSource(CareerPath, stripComments: true);

        StringAssert.Contains(source,
            "CharacterCreationBonusResolver.ResolveSkills(skillNames, _logger, logNullResolution: false)");
        StringAssert.Contains(source,
            "CharacterCreationBonusResolver.ResolveAttribute(attributeName, _logger, logNullResolution: false)");
        Assert.IsTrue(
            source.IndexOf("ResolveSkills(skillNames", StringComparison.Ordinal)
            > source.IndexOf("NarrativeMenuOptionArgs args", StringComparison.Ordinal),
            "Career skill resolution must remain inside the option-args callback.");
    }

    [TestMethod]
    public void Narrative_DelegatesDuringConstructionWithNullWarningPolicy()
    {
        var source = RepoPaths.ReadSource(NarrativePath, stripComments: true);

        StringAssert.Contains(source,
            "CharacterCreationBonusResolver.ResolveSkills(definition.Skills, _logger, logNullResolution: true)");
        StringAssert.Contains(source,
            "CharacterCreationBonusResolver.ResolveAttribute(definition.Attribute, _logger, logNullResolution: true)");
    }

    [TestMethod]
    public void Consumers_NoLongerOwnDuplicateMaps()
    {
        var career = RepoPaths.ReadSource(CareerPath, stripComments: true);
        var narrative = RepoPaths.ReadSource(NarrativePath, stripComments: true);

        foreach (var source in new[] { career, narrative })
        {
            Assert.IsFalse(source.Contains("Dictionary<string, Func<SkillObject>>"));
            Assert.IsFalse(source.Contains("Dictionary<string, Func<CharacterAttribute>>"));
        }
    }

    private static string ResolverSource() => RepoPaths.ReadSource(ResolverPath, stripComments: true);

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = source.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
