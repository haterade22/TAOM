using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Pins the config file actually shipped to players. The provider is fail-soft by design, so a
/// shipped file with a bad value would revert to compiled defaults and the feature would look
/// fine while ignoring everything authored here. These tests make that loud.
/// </summary>
[TestClass]
public class ShippedSignatureStrikesConfigTests
{
    private const string ScreamSound = "LOTR/Mordor/Nazgul/nazgul_scream";

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath =>
        Path.Combine(ModuleDataPath, "signature_strikes", "signature_strikes_config.json");

    private IModLogger _logger = null!;
    private SignatureStrikesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new SignatureStrikesConfigProvider(pathService, _logger);
    }

    private SignatureConfig Signature(string id) => _sut.GetConfig().Signatures.Single(s => s.Id == id);

    [TestMethod]
    public void ShippedConfig_FileExists()
        => Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetConfig();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_ShipsExactlySauronAndTheNine()
    {
        // Every signature is a balance change: a new one needs an issue, a control battle and a
        // docs/features/signature-strikes.md update, never a quiet data edit.
        CollectionAssert.AreEqual(new[] { "sauron", "nazgul" }, _sut.GetConfig().Signatures.Select(s => s.Id).ToArray());
    }

    // ---- Sauron (#605) ---------------------------------------------------------------------

    [TestMethod]
    public void Sauron_IsListedByBothHeroIdAndRace_AndNothingElse()
    {
        // Both axes on purpose: the hero id survives a data change that drops his race attribute,
        // the race finds him in a custom battle where the agent carries no HeroObject.
        var sauron = Signature("sauron");

        CollectionAssert.AreEquivalent(new[] { "lord_1_17" }, sauron.HeroIds);
        CollectionAssert.AreEquivalent(new[] { "sauron" }, sauron.Races);
        Assert.AreEqual(0, sauron.HeroSets.Count);
    }

    [TestMethod]
    public void Sauron_MapsOverheadToSlamAndSideSwingsToSweep()
    {
        var strikes = Signature("sauron").Strikes;

        Assert.AreEqual("Slam", strikes["Overhead"].Kind);
        Assert.AreEqual("Sweep", strikes["Left"].Kind);
        Assert.AreEqual("Sweep", strikes["Right"].Kind);
        Assert.IsFalse(strikes.ContainsKey("Thrust"), "A thrust is a plain hit.");
    }

    [TestMethod]
    public void Sauron_SlamKnocksDownAndSweepKnocksBack()
    {
        var strikes = Signature("sauron").Strikes;

        Assert.IsTrue(strikes["Overhead"].KnockDown);
        Assert.IsFalse(strikes["Overhead"].KnockBack);
        Assert.IsTrue(strikes["Left"].KnockBack);
        Assert.IsFalse(strikes["Left"].KnockDown);
        Assert.IsTrue(strikes["Right"].KnockBack);
    }

    [TestMethod]
    public void Sauron_CooldownsAreTheDocumentedBalanceContract()
    {
        // Mike's standing instruction (2026-09-16): long cooldowns, Sauron must not be overpowered.
        // These two numbers are quoted in docs/features/signature-strikes.md and issue #605.
        var cooldowns = Signature("sauron").Cooldowns;

        Assert.AreEqual(20f, cooldowns["Slam"], 0.001f);
        Assert.AreEqual(12f, cooldowns["Sweep"], 0.001f);
    }

    [TestMethod]
    public void Sauron_OnlyTheSlamFiresOnAGroundHit()
    {
        var strikes = Signature("sauron").Strikes;

        Assert.IsTrue(strikes["Overhead"].WorldHitBaseDamage > 0);
        Assert.AreEqual(0, strikes["Left"].WorldHitBaseDamage);
        Assert.AreEqual(0, strikes["Right"].WorldHitBaseDamage);
    }

    [TestMethod]
    public void Sauron_RingsAroundTheImpactAndPlaysNoSound()
    {
        foreach (var profile in Signature("sauron").Strikes.Values)
        {
            Assert.AreEqual("Impact", profile.Origin);
            Assert.IsNull(profile.Sound);
        }
    }

    [TestMethod]
    public void Sauron_StillCarriesTheSauronRaceInLordsXslt()
    {
        // Cross-file: the config names a hero id and a race; lords.xslt is what binds the two. If
        // a lord regen ever drops the race attribute, the hero-id axis still finds him in a
        // campaign but the race axis stops finding him in a Custom Battle, with no error anywhere.
        // The Nine's race is pinned by NazgulRaceDataTests (#644).
        var xslt = File.ReadAllText(Path.Combine(ModuleDataPath, "lords.xslt"));
        var start = xslt.IndexOf("NPCCharacter[@id='lord_1_17']", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "lords.xslt no longer has a template for lord_1_17 (Sauron).");

        var end = xslt.IndexOf("</xsl:template>", start, StringComparison.Ordinal);
        var block = xslt.Substring(start, end - start);

        StringAssert.Contains(block, "name=\"race\">sauron<",
            "lord_1_17 must keep race=\"sauron\" in lords.xslt; the shipped config lists that race.");
    }

    // ---- The Nine (#645) ----------------------------------------------------------------------

    [TestMethod]
    public void Nazgul_IsListedByTheHeroSetAndTheRace()
    {
        // The hero set names exactly the Nine whatever their race data says; the race finds them
        // in a Custom Battle too.
        var nazgul = Signature("nazgul");

        CollectionAssert.AreEquivalent(new[] { "nazgul_nine" }, nazgul.HeroSets);
        CollectionAssert.AreEquivalent(new[] { "nazghul" }, nazgul.Races);
        Assert.AreEqual(0, nazgul.HeroIds.Count);
    }

    [TestMethod]
    public void Nazgul_ScreamsOnTheOverheadAndBothSideSwings()
    {
        // Mike, 2026-09-23: "on an overhead attack and slashing attack. Same as Sauron."
        var strikes = Signature("nazgul").Strikes;

        foreach (var direction in new[] { "Overhead", "Left", "Right" })
            Assert.AreEqual("Scream", strikes[direction].Kind, direction);
        Assert.IsFalse(strikes.ContainsKey("Thrust"), "A thrust is a plain hit.");
    }

    [TestMethod]
    public void Nazgul_ScreamIsARingAroundTheWraithThatStaggersAndFrightens()
    {
        // Mike, 2026-09-23: scream + stagger, no knockdown, morale 25.
        foreach (var pair in Signature("nazgul").Strikes)
        {
            var profile = pair.Value;
            Assert.AreEqual("Self", profile.Origin, pair.Key);
            Assert.IsTrue(profile.KnockBack, pair.Key);
            Assert.IsFalse(profile.KnockDown, pair.Key);
            Assert.AreEqual(25f, profile.FearMorale, 0.001f, pair.Key);
            Assert.AreEqual(0, profile.WorldHitBaseDamage, pair.Key + ": a scream answers a hit on a foe");
            Assert.AreEqual(ScreamSound, profile.Sound, pair.Key);
        }
    }

    [TestMethod]
    public void Nazgul_OneFifteenSecondTimerForAllThreeDirections()
    {
        // Mike, 2026-09-23: "every 15 s", one cooldown per Nazgul across overhead and side swings.
        var cooldowns = Signature("nazgul").Cooldowns;

        CollectionAssert.AreEquivalent(new[] { "Scream" }, cooldowns.Keys.ToArray());
        Assert.AreEqual(15f, cooldowns["Scream"], 0.001f);
    }

    [TestMethod]
    public void Nazgul_ScreamSoundIsARegisteredModuleSoundWithFilesOnDisk()
    {
        // A sound name nothing registers resolves to -1 at runtime and the wraith only yells.
        var sounds = XDocument.Load(Path.Combine(ModuleDataPath, "module_sounds.xml"));
        var entry = sounds.Descendants("module_sound").SingleOrDefault(e => (string?)e.Attribute("name") == ScreamSound);

        Assert.IsNotNull(entry, $"module_sounds.xml has no module_sound named '{ScreamSound}'.");
        Assert.AreEqual("mission_voice_shout", (string?)entry!.Attribute("sound_category"),
            "A sound without a valid category is never played (Native module_sounds.xml).");

        var variations = entry.Elements("variation").Select(v => (string?)v.Attribute("path")).ToArray();
        Assert.IsTrue(variations.Length > 0, "the scream has no variations");
        foreach (var path in variations)
        {
            Assert.IsNotNull(path);
            var extension = Path.GetExtension(path!).ToLowerInvariant();
            Assert.IsTrue(extension == ".ogg" || extension == ".wav",
                $"{path}: ship the formats Native's module_sounds.xml header lists, .ogg or .wav.");
            var file = Path.Combine(RepoRoot, "Main", "_Module", "ModuleSounds", path!.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(file), $"{path} is registered but not on disk at {file}");
        }
    }

    // ---- The file itself --------------------------------------------------------------------

    [TestMethod]
    public void ShippedConfig_EveryCommentKeyHasALiveSibling()
    {
        // A _comment_x whose x was renamed or removed is documentation pointing at nothing.
        var root = JObject.Parse(File.ReadAllText(ConfigPath));
        var scopes = new[] { root }.Concat(root["signatures"]!.Children<JObject>());

        foreach (var scope in scopes)
        {
            foreach (var property in scope.Properties())
            {
                if (!property.Name.StartsWith("_comment_", StringComparison.Ordinal))
                    continue;

                var sibling = property.Name.Substring("_comment_".Length);
                if (sibling == "feature" || sibling == "balance")
                    continue;

                Assert.IsNotNull(scope[sibling],
                    $"{property.Name} documents a key '{sibling}' that no longer exists in the config.");
            }
        }
    }

    [TestMethod]
    public void ShippedConfig_ContainsNoLongDashes()
    {
        // output-style.md Part 2: em and en dashes are banned in produced prose, and the comment
        // keys here are prose players read.
        var text = File.ReadAllText(ConfigPath);

        Assert.IsFalse(Regex.IsMatch(text, "[\u2013\u2014]"),
            "signature_strikes_config.json contains an em or en dash. Use a comma, colon, or new sentence.");
    }
}
