using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Arg = NSubstitute.Arg;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// special_resources_config.xml holds plain English names and tier texts. The provider shows each one
/// under a key it builds from the resource id (#1385), so a translated row reaches the player:
/// <c>taom_res_name_&lt;id&gt;</c>, <c>taom_res_tier_&lt;id&gt;_&lt;level&gt;_name</c> and <c>_desc</c>.
/// The registry tests keep taom_module_strings.xml equal to the real config.
/// </summary>
[TestClass]
public class SpecialResourceConfigProviderTextTests
{
    private const string Xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""taom_gems_icon"" cap=""600"">
    <Kingdom id=""erebor"" />
    <Tiers>
      <Tier level=""1"" name=""Apprentice Miner"" threshold=""100"" description=""Dwarven mining efficiency improves."" />
      <Tier level=""2"" name="""" threshold=""250"" description="""" />
    </Tiers>
  </Resource>
  <Resource id=""nameless"" display_name="""" icon_sprite=""x"" cap=""600"">
    <Kingdom id=""dale"" />
  </Resource>
</SpecialResources>";

    private string _tempDir = "";
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private ITextLocalizerAdapter _text = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_res_text_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "special_resources"));
        File.WriteAllText(Path.Combine(_tempDir, "special_resources", "special_resources_config.xml"), Xml);
        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _text = Substitute.For<ITextLocalizerAdapter>();
        _text.Localize(Arg.Any<string>()).Returns(call => "L:" + call.ArgAt<string>(0));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void DisplayName_IsLocalizedUnderItsResourceKey()
    {
        var gems = new SpecialResourceConfigProvider(_pathService, _logger, text: _text).GetByKingdomId("erebor");

        Assert.AreEqual("L:{=taom_res_name_gems}Gems", gems!.DisplayName);
    }

    [TestMethod]
    public void TierNameAndDescription_AreLocalizedUnderTheirLevelKeys()
    {
        var tier = new SpecialResourceConfigProvider(_pathService, _logger, text: _text).GetByKingdomId("erebor")!.TierThresholds[0];

        Assert.AreEqual("L:{=taom_res_tier_gems_1_name}Apprentice Miner", tier.Name);
        Assert.AreEqual("L:{=taom_res_tier_gems_1_desc}Dwarven mining efficiency improves.", tier.Description);
    }

    [TestMethod]
    public void EmptyTexts_StayEmpty_AndAreNotLocalized()
    {
        var provider = new SpecialResourceConfigProvider(_pathService, _logger, text: _text);
        var tier = provider.GetByKingdomId("erebor")!.TierThresholds[1];

        Assert.AreEqual("", provider.GetByKingdomId("dale")!.DisplayName);
        Assert.AreEqual("", tier.Name);
        Assert.AreEqual("", tier.Description);
        _text.DidNotReceive().Localize(Arg.Is<string>(s => s.Contains("nameless") || s.Contains("_2_")));
    }

    [TestMethod]
    public void WithoutALocalizer_TheEnglishIsShownAsWritten()
    {
        var gems = new SpecialResourceConfigProvider(_pathService, _logger).GetByKingdomId("erebor");

        Assert.AreEqual("Gems", gems!.DisplayName);
        Assert.AreEqual("Apprentice Miner", gems.TierThresholds[0].Name);
    }

    [TestMethod]
    public void TheContainer_GivesTheProviderTheRegisteredLocalizer()
    {
        using var container = new Container();
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        container.RegisterInstance(_pathService);
        container.RegisterInstance(_logger);
        container.RegisterInstance(_text);
        container.RegisterInstance(Substitute.For<ISpecialResourceSettingsProvider>(), IfAlreadyRegistered.Replace);

        var gems = container.Resolve<ISpecialResourceConfigProvider>().GetByKingdomId("erebor");

        Assert.AreEqual("L:{=taom_res_name_gems}Gems", gems!.DisplayName);
    }

    // --- The registry: taom_module_strings.xml holds the shown English under every key ---

    private static Dictionary<string, string> ExpectedRows()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        var config = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "special_resources", "special_resources_config.xml"));
        foreach (var resource in config.Root!.Elements("Resource"))
        {
            var id = (string?)resource.Attribute("id") ?? "";
            Add(expected, "taom_res_name_" + id, (string?)resource.Attribute("display_name"));
            foreach (var tier in resource.Elements("Tiers").Elements("Tier"))
            {
                var stem = "taom_res_tier_" + id + "_" + (string?)tier.Attribute("level");
                Add(expected, stem + "_name", (string?)tier.Attribute("name"));
                Add(expected, stem + "_desc", (string?)tier.Attribute("description"));
            }
        }
        return expected;
    }

    private static void Add(Dictionary<string, string> expected, string key, string? english)
    {
        if (!string.IsNullOrEmpty(english))
            expected[key] = "{=" + key + "}" + english;
    }

    private static Dictionary<string, string> RegisteredRows() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"))
            .Descendants("string")
            .Where(s => ((string?)s.Attribute("id") ?? "").StartsWith("taom_res_name_", StringComparison.Ordinal)
                        || ((string?)s.Attribute("id") ?? "").StartsWith("taom_res_tier_", StringComparison.Ordinal))
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string?)s.Attribute("text") ?? "", StringComparer.Ordinal);

    [TestMethod]
    public void Registry_HoldsTheShownEnglishUnderEveryKey()
    {
        var rows = RegisteredRows();
        var expected = ExpectedRows();
        var wrong = expected
            .Where(e => !rows.TryGetValue(e.Key, out var text) || text != e.Value)
            .Select(e => $"{e.Key}: needs text=\"{e.Value}\", has " + (rows.TryGetValue(e.Key, out var text) ? $"\"{text}\"" : "no row"))
            .ToList();

        Assert.IsTrue(expected.Count > 10, $"Only {expected.Count} texts in special_resources_config.xml: the read is broken.");
        Assert.AreEqual(0, wrong.Count,
            "taom_module_strings.xml is out of step with special_resources_config.xml:\n" + string.Join("\n", wrong));
    }

    [TestMethod]
    public void Registry_HoldsNoResourceRowThatNothingShows()
    {
        var expected = ExpectedRows();
        var stale = RegisteredRows().Keys.Where(id => !expected.ContainsKey(id)).OrderBy(id => id).ToList();

        Assert.AreEqual(0, stale.Count,
            "Rows that no resource or tier shows; remove each with its language rows: " + string.Join(", ", stale));
    }
}
