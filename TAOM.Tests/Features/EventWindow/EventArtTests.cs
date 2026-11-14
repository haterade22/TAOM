using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Client.Features.EventWindow;

namespace TAOM.Tests.Features.EventWindow;

/// <summary>
/// The story event art: <see cref="EventArt.Resolve"/> keeps a picture inside GUI/EventArt and falls back on a missing
/// one, every War Chronicle event and the Gondor coronation name a picture that ships, and every shipped picture is a
/// 720x960 8-bit RGBA PNG that something names.
/// </summary>
[TestClass]
public class EventArtTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string ArtFolder = Path.Combine(Root, "Main", "_Module", "GUI", "EventArt");

    private static readonly string[] ChronicleIds =
    {
        "fellowship", "khazaddum", "breaking", "theoden", "hornburg", "ents", "lorien", "osgiliath", "dale", "corsairs",
        "north", "gondor", "blackgate",
    };

    private string _temp = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        _temp = Path.Combine(Path.GetTempPath(), "taom-event-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        File.WriteAllBytes(Path.Combine(_temp, "taom_event_test.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "taom_event_outside.png"), new byte[] { 1 });
    }

    [TestCleanup]
    public void DeleteFolder()
    {
        Directory.Delete(_temp, recursive: true);
        File.Delete(Path.Combine(Path.GetTempPath(), "taom_event_outside.png"));
    }

    [TestMethod]
    public void Resolve_FindsAPictureInTheFolder()
    {
        Assert.AreEqual(Path.Combine(_temp, "taom_event_test.png"), EventArt.Resolve("taom_event_test.png", _temp));
    }

    [DataTestMethod]
    [DataRow(null, DisplayName = "no image")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("  ", DisplayName = "blank")]
    [DataRow("taom_event_missing.png", DisplayName = "missing file")]
    [DataRow("..", DisplayName = "parent folder")]
    [DataRow(@"..\taom_event_outside.png", DisplayName = "backslash path out")]
    [DataRow("../taom_event_outside.png", DisplayName = "slash path out")]
    public void Resolve_IsNone_ForNoPictureAMissingOneOrAPathOutOfTheFolder(string? image)
    {
        Assert.IsNull(EventArt.Resolve(image, _temp));
    }

    private static IEnumerable<JObject> ChronicleEvents() =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root, "Main", "_Module", "ModuleData", "war_chronicle", "war_chronicle.json")))
            ["events"]!.Children<JObject>();

    private static string CoronationImage(string title) =>
        (string?)XDocument.Load(Path.Combine(Root, "Main", "_Module", "ModuleData", "realms_titles.xml"))
            .Descendants("Coronation").Single(c => (string?)c.Attribute("title") == title).Attribute("image")
        ?? string.Empty;

    [TestMethod]
    public void EveryChronicleEvent_ShowsItsOwnShippedPicture()
    {
        var images = ChronicleEvents().ToDictionary(e => (string)e["id"]!, e => (string?)e["image"]);

        CollectionAssert.AreEquivalent(ChronicleIds, images.Keys.ToArray(), "the chronicle's event list changed");
        foreach (var id in ChronicleIds)
        {
            Assert.AreEqual($"taom_event_{id}.png", images[id], id);
            Assert.IsNotNull(EventArt.Resolve(images[id], ArtFolder), $"{id}: {images[id]} is not in GUI/EventArt");
        }
    }

    [TestMethod]
    public void TheGondorCoronation_ShowsItsShippedPicture()
    {
        var image = CoronationImage("k_empire_w");

        Assert.AreEqual("taom_event_coronation_gondor.png", image);
        Assert.IsNotNull(EventArt.Resolve(image, ArtFolder));
    }

    [TestMethod]
    public void EveryShippedPicture_IsAn8BitRgbaPng_At720x960_ThatAnEventNames()
    {
        var named = new HashSet<string>(ChronicleEvents().Select(e => (string?)e["image"] ?? string.Empty),
            StringComparer.OrdinalIgnoreCase) { CoronationImage("k_empire_w") };
        var files = Directory.GetFiles(ArtFolder);

        Assert.AreEqual(14, files.Length, string.Join(", ", files.Select(Path.GetFileName)));
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            Assert.IsTrue(named.Contains(name), $"{name} ships but no event names it");
            // PNG signature, then the IHDR chunk: width and height (big-endian), bit depth, colour type 6 (RGBA). The engine's
            // LoadTextureFromPath showed the 24-bit RGB art (colour type 2) with red and blue exchanged; the RGBA
            // encyclopedia pictures in the same widget show true colours. Alpha is 255 throughout, so the art stays opaque.
            var header = new byte[26];
            using (var stream = File.OpenRead(file))
                Assert.AreEqual(header.Length, stream.Read(header, 0, header.Length), name);
            CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header.Take(8).ToArray(), name);
            Assert.AreEqual("IHDR", System.Text.Encoding.ASCII.GetString(header, 12, 4), name);
            Assert.AreEqual(720, BigEndian(header, 16), $"{name} width");
            Assert.AreEqual(960, BigEndian(header, 20), $"{name} height");
            Assert.AreEqual(8, header[24], $"{name} bit depth");
            Assert.AreEqual(6, header[25], $"{name} colour type: RGBA, never 24-bit RGB");
        }
    }

    private static int BigEndian(byte[] bytes, int offset) =>
        bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
}
