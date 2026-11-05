using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// A career names its ability icon by ability template id unless it carries an ability_sprite, so a
/// career cloned from another reuses the source's registered icon instead of showing an empty frame
/// (rca-ered-luin-2026-10-27 row 15). Portraits are a separate gap tracked under #1058.
/// </summary>
[TestClass]
public class CareerAbilitySpriteTests
{
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "career_system"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static CareerConfigProvider ProviderFor(string moduleDataPath)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleDataPath);
        return new CareerConfigProvider(paths, Substitute.For<IModLogger>());
    }

    private static string CareerXml(string extraAttributes) => $@"<?xml version='1.0'?>
<Careers max_perk_points=""25"">
  <Career id=""clone"" display_name=""Clone"" description=""d""
          portrait_sprite=""p"" ability_template_id=""clone_ability"" {extraAttributes}
          min_clan_tier=""0"" root_choice_id=""r"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""g"" /></ChoiceGroups>
  </Career>
</Careers>";

    [TestMethod]
    public void AbilitySprite_WithoutTheAttribute_IsTheTemplateId()
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_careers.xml"), CareerXml(""));

        var careers = ProviderFor(_tempDir).LoadCareers();

        Assert.AreEqual("clone_ability", careers.Single().AbilitySprite);
    }

    [TestMethod]
    public void AbilitySprite_WithTheAttribute_IsTheAttribute()
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_careers.xml"),
            CareerXml(@"ability_sprite=""source_ability"""));

        var careers = ProviderFor(_tempDir).LoadCareers();

        Assert.AreEqual("source_ability", careers.Single().AbilitySprite);
    }

    [TestMethod]
    public void EveryShippedCareer_AbilitySprite_IsRegisteredInTheSpriteData()
    {
        var registered = Regex.Matches(
                ReadSource("Main/_Module/GUI/TAOMSpriteData.xml"),
                @"<Name>CareerSystem\\Abilities\\([^<]+)</Name>")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();

        var careers = ProviderFor(RepoPath("Main", "_Module", "ModuleData")).LoadCareers();

        Assert.IsTrue(careers.Count > 0, "no careers loaded from the shipped taom_careers.xml");
        var unregistered = careers.Where(c => !registered.Contains(c.AbilitySprite))
            .Select(c => $"{c.Id} -> {c.AbilitySprite}").ToList();
        Assert.AreEqual(0, unregistered.Count,
            "careers whose ability icon has no CareerSystem\\Abilities sprite: " + string.Join(", ", unregistered));
    }
}
