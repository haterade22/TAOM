using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Domain;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the realm colour rules. Banner colours were the external mod's only source and left 9 of 22
/// TAOM realms near black, with Gundabad and the Misty Mountain Orcs indistinguishable; the curated
/// palette exists to stop that, and these tests keep it that way as kingdoms are added.
/// </summary>
[TestClass]
public class RealmPaletteTests
{
    private const uint Red = 0xFFD02020, DarkRed = 0xFFB01818, Blue = 0xFF2040D0;

    [TestMethod]
    public void ColourOf_CuratedRealm_ReturnsItsColour()
    {
        var palette = new RealmPalette(new Dictionary<string, uint> { ["empire_s"] = Red }, new uint[0]);

        Assert.AreEqual(Red, palette.ColourOf("empire_s"));
    }

    [TestMethod]
    public void ColourOf_RealmCreatedInPlay_TakesTheReserveColourFarthestFromThoseInUse()
    {
        var palette = new RealmPalette(new Dictionary<string, uint> { ["empire_s"] = Red }, new[] { DarkRed, Blue });

        Assert.AreEqual(Blue, palette.ColourOf("player_faction"));
    }

    [TestMethod]
    public void ColourOf_SameNewRealmTwice_KeepsItsColour()
    {
        var palette = new RealmPalette(new Dictionary<string, uint>(), new[] { Red, Blue });

        uint first = palette.ColourOf("rebels_1");
        palette.ColourOf("rebels_2");

        Assert.AreEqual(first, palette.ColourOf("rebels_1"));
    }

    [TestMethod]
    public void ColourOf_TwoNewRealms_GetDifferentReserveColours()
    {
        var palette = new RealmPalette(new Dictionary<string, uint>(), new[] { Red, Blue });

        Assert.AreNotEqual(palette.ColourOf("rebels_1"), palette.ColourOf("rebels_2"));
    }

    [TestMethod]
    public void ColourOf_ReserveExhausted_StillGivesAStableOpaqueColour()
    {
        var palette = new RealmPalette(new Dictionary<string, uint>(), new uint[0]);

        uint colour = palette.ColourOf("rebels_9");

        Assert.AreEqual(colour, new RealmPalette(new Dictionary<string, uint>(), new uint[0]).ColourOf("rebels_9"), "stable across sessions");
        Assert.AreEqual(0xFF000000u, colour & 0xFF000000u, "opaque");
    }

    [TestMethod]
    public void DeltaE_IdenticalColours_IsZero()
    {
        Assert.AreEqual(0.0, RealmPalette.DeltaE(Red, Red), 1e-9);
    }

    [TestMethod]
    public void DeltaE_BlackAgainstWhite_IsAHundred()
    {
        Assert.AreEqual(100.0, RealmPalette.DeltaE(0xFF000000, 0xFFFFFFFF), 0.5);
    }

    // --- the shipped palette ---

    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string ModuleData => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData");

    private static RealmPaletteConfig ShippedConfig() =>
        JsonConvert.DeserializeObject<RealmPaletteConfig>(File.ReadAllText(Path.Combine(ModuleData, "realm_borders", "palette.json")))!;

    private static HashSet<string> ShippedKingdomIds()
    {
        var ids = new HashSet<string>(
            XDocument.Load(Path.Combine(ModuleData, "taom_spkingdoms.xml")).Descendants("Kingdom").Select(k => (string)k.Attribute("id")!));
        foreach (Match m in Regex.Matches(File.ReadAllText(Path.Combine(ModuleData, "spkingdoms.xslt")), @"match=""Kingdom\[@id='([^']+)'\]"""))
            ids.Add(m.Groups[1].Value);
        return ids;
    }

    [TestMethod]
    public void ShippedPalette_CoversEveryKingdomAndNothingElse()
    {
        var realms = ShippedConfig().Realms.Keys;
        var kingdoms = ShippedKingdomIds();

        CollectionAssert.AreEquivalent(kingdoms.OrderBy(k => k).ToList(), realms.OrderBy(k => k).ToList(),
            "every kingdom needs a border colour, and a row for a kingdom that no longer exists is stale");
    }

    /// <summary>Every colour the shipped palette can put on the map: the curated realms and the reserve.</summary>
    private static List<(string Name, uint Colour)> ShippedColours(RealmPaletteConfig config)
    {
        uint Parse(string hex)
        {
            Assert.IsTrue(RealmPaletteProvider.TryParseColour(hex, out uint colour), $"'{hex}' is not a #RRGGBB colour");
            return colour;
        }

        return config.Realms.OrderBy(p => p.Key).Select(p => (p.Key, Parse(p.Value)))
            .Concat(config.Reserve.Select((hex, i) => ($"reserve[{i}] {hex}", Parse(hex))))
            .ToList();
    }

    [TestMethod]
    public void ShippedPalette_NoColour_LooksLikeTheProvincePicturesOwn()
    {
        var config = ShippedConfig();
        var picture = new (string Name, uint Colour)[]
        {
            ("unowned fief", RealmBorderService.PictureUnownedFief), ("wild land", RealmBorderService.PictureWildLand),
            ("water", RealmBorderService.PictureWater), ("edge", RealmBorderService.PictureEdge),
        };

        foreach (var (name, colour) in ShippedColours(config))
            foreach (var (pictureName, pictureColour) in picture)
            {
                double d = RealmPalette.DeltaE(colour, pictureColour);
                Assert.IsTrue(d >= config.MinimumDeltaE, $"{name} is only {d:F1} from the picture's {pictureName}");
            }
        for (int i = 0; i < picture.Length; i++)
            for (int j = i + 1; j < picture.Length; j++)
                Assert.IsTrue(RealmPalette.DeltaE(picture[i].Colour, picture[j].Colour) >= config.MinimumDeltaE, $"{picture[i].Name} / {picture[j].Name}");
    }

    [TestMethod]
    public void Override_ThenRestore_AndTheReserveKeepsClearOfIt()
    {
        var palette = new RealmPalette(new Dictionary<string, uint> { ["empire_s"] = Red }, new[] { DarkRed, Blue });

        palette.Override("empire_s", Blue);
        Assert.AreEqual(Blue, palette.ColourOf("empire_s"));
        Assert.AreEqual(DarkRed, palette.ColourOf("rebels"), "the reserve colour farthest from Blue, the colour now in use");

        palette.Override("empire_s", null);
        Assert.AreEqual(Red, palette.ColourOf("empire_s"), "a cleared field restores the file's colour");
    }

    [TestMethod]
    public void ShippedPalette_EveryPairOfColoursReserveIncluded_CanBeToldApart()
    {
        var config = ShippedConfig();
        var colours = ShippedColours(config);

        for (int i = 0; i < colours.Count; i++)
            for (int j = i + 1; j < colours.Count; j++)
            {
                double d = RealmPalette.DeltaE(colours[i].Colour, colours[j].Colour);
                Assert.IsTrue(d >= config.MinimumDeltaE,
                    $"{colours[i].Name} and {colours[j].Name} are only {d:F1} apart (floor {config.MinimumDeltaE})");
            }
    }

    [TestMethod]
    public void ShippedPalette_NoColourReserveIncluded_IsNearBlack()
    {
        var config = ShippedConfig();

        foreach (var (name, colour) in ShippedColours(config))
        {
            double lightness = RealmPalette.Lightness(colour);
            Assert.IsTrue(lightness >= config.MinimumLightness, $"{name} has L* {lightness:F1}, darker than {config.MinimumLightness}");
        }
    }

    // --- the provider ---

    private static RealmPaletteProvider ProviderOver(string moduleData, IModLogger logger)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData);
        return new RealmPaletteProvider(paths, logger);
    }

    [TestMethod]
    public void NewPalette_EachCall_HandsOutTheReserveAfresh()
    {
        var provider = ProviderOver(ModuleData, Substitute.For<IModLogger>());

        uint first = provider.NewPalette().ColourOf("rebels_a");
        uint second = provider.NewPalette().ColourOf("rebels_b");

        Assert.AreEqual(first, second, "a new campaign starts from the best reserve colour again");
    }

    [TestMethod]
    public void NewPalette_NoPaletteFile_WarnsAndStillColoursEveryRealm()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = ProviderOver(Path.Combine(Path.GetTempPath(), "taom-no-palette-" + Guid.NewGuid().ToString("N")), logger);

        var palette = provider.NewPalette();

        Assert.AreNotEqual(palette.ColourOf("empire_w"), palette.ColourOf("empire_s"));
        logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("palette not found")));
    }

    [TestMethod]
    public void NewPalette_MalformedJson_WarnsAndStillColoursEveryRealm()
    {
        var logger = Substitute.For<IModLogger>();
        string dir = Path.Combine(Path.GetTempPath(), "taom-bad-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "realm_borders"));
        File.WriteAllText(Path.Combine(dir, "realm_borders", "palette.json"), "{ \"realms\": [ not json");
        try
        {
            var palette = ProviderOver(dir, logger).NewPalette();

            Assert.AreNotEqual(0u, palette.ColourOf("empire_w"));
            logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("palette unreadable")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Build_BlankRealmId_IsRejectedWithAWarning()
    {
        var logger = Substitute.For<IModLogger>();
        var config = new RealmPaletteConfig { Realms = new Dictionary<string, string> { ["  "] = "#3F76B8" } };

        RealmPaletteProvider.Build(config, logger);

        logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("blank realm id")));
        logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("no valid #RRGGBB colour")));
    }

    [DataTestMethod]
    [DataRow("#FFB0231B")] // ARGB order, as the C# literals are written: would draw orange, not dark red
    [DataRow("#3F76B ")]   // a typo the byte parser would pad into #3F760B
    [DataRow("3F76B8")]
    public void Build_NotHashAndSixHexDigits_IsSkippedWithAWarning(string hex)
    {
        var logger = Substitute.For<IModLogger>();
        var config = new RealmPaletteConfig { Realms = new Dictionary<string, string> { ["empire_w"] = hex } };

        RealmPaletteProvider.Build(config, logger);

        logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("empire_w")));
    }

    [TestMethod]
    public void McmColourFields_CoverEveryRealm_AndNameItsDefault()
    {
        var config = ShippedConfig();
        var fields = RealmBordersSettingsProvider.ColourFields;

        CollectionAssert.AreEquivalent(config.Realms.Keys.ToList(), fields.Select(f => f.Realm).ToList(),
            "every realm needs exactly one MCM colour field");
        foreach (var property in typeof(TAOM.Features.TaomSettings).GetProperties().Where(p => p.Name.StartsWith("RealmColour", StringComparison.Ordinal)))
        {
            var text = property.GetCustomAttributes(false).Single(a => a.GetType().Name == "SettingPropertyTextAttribute");
            string hint = (string)text.GetType().GetProperty("HintText")!.GetValue(text, null)!;
            string label = (string)text.GetType().GetProperty("DisplayName")!.GetValue(text, null)!;
            var field = fields.Single(f => f.Label == label);
            StringAssert.Contains(hint, config.Realms[field.Realm], $"{label}'s tooltip names another default than palette.json");
        }
    }
}
