using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core;

/// <summary>
/// The custom (non-book) named companions keep the face their creators made (owner, 2026-11-14: "i meant to not
/// touch the custom non lotr characters"). The look passes of #1181, #1190, #1191 and #1261 recoloured Thyrell, Swan,
/// Belecthar (spartan), Eohart (hart) and, through Aragorn's key, Igor; they hold their authored key again, the one
/// in named_companions.xml before 0b1e2f2e2. Each face below is pinned as a literal, so a future pass that edits one
/// fails here. A wanted change by a character's creator updates the literal in the same PR.
/// </summary>
[TestClass]
public class CustomCharacterLookTests
{
    private static string NamedCompanionsXml => RepoPath("Main", "_Module", "ModuleData", "named_companions", "named_companions.xml");

    // The key Swan, Igor, Thyrell, Eohart and Belecthar were authored with (named_companions.xml at 0b1e2f2e2^).
    private const string AuthoredSharedKey =
        "0015000D800013420F800A1A098007985FFD8660BE886A0167895C1885BB9A60009B06030909B70500000000000000000000000000000000000000003F3C2002";
    private const string DwarfKey =
        "0000D404C000314508807EB03F1F0F70108788878FF88067888878888D3017F0008DB60308FFF0AE000000000000000000000000000000000000000043040142";
    private const string ElfKey =
        "00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002";
    private const string UrukKey =
        "000C580FC0002000B78479D2379D74D8B78C57C97AAB4B927948C45B978B4768009716030956D6E5000000000000000000000000000000000000000043340000";
    private const string HateradeKey =
        "0017B80E40003141B336CC45379764C8A799445A61778AB5CA7B84364597A77900987603098DAA840000000000000000000000000000000000000000432C0000";

    private static readonly Dictionary<string, string> AuthoredFaces = new(StringComparer.Ordinal)
    {
        ["named_companion_swan"] = Body("37.56", "0.6878", "0.678", AuthoredSharedKey),
        ["named_companion_rcmcplayz"] = Body("32.01", "0.7454", "0.8379", DwarfKey),
        ["named_companion_leovsilva"] = Body("25.97", "0.4301", "0.4", ElfKey),
        ["named_companion_yotthani"] = Body("32.01", "0.7454", "0.8379", DwarfKey),
        ["named_companion_whitegoat"] = Body("32.01", "0.7454", "0.8379", DwarfKey),
        ["named_companion_igor"] = Body("37.56", "0.6878", "0.678", AuthoredSharedKey),
        ["named_companion_thyrell"] = Body("37.56", "0.6878", "0.678", AuthoredSharedKey),
        ["named_companion_haterade"] = Body("34.01", "0.4861", "0.5255", HateradeKey),
        ["named_companion_hart"] = Body("37.56", "0.6878", "0.678", AuthoredSharedKey),
        ["named_companion_blackrose"] = Body("22.01", "0.0348", "0.456", UrukKey),
        ["named_companion_spartan"] = Body("37.56", "0.6878", "0.678", AuthoredSharedKey),
        ["named_companion_solus"] = Body("25.97", "0.4301", "0.4", ElfKey),
        ["named_companion_maztog"] = Body("32.01", "0.0348", "0.456", UrukKey),
        ["named_companion_balakhor"] = Body("30.01", "0.6454", "0.7379", DwarfKey),
        ["named_companion_elegost"] = "template BodyProperty.fighter_gondor",
    };

    private static readonly string[] BookCompanions =
        { "named_companion_aragorn", "named_companion_legolas", "named_companion_gimli" };

    private static string Body(string age, string weight, string build, string key) =>
        $"BodyProperties age={age} weight={weight} build={build} key={key}";

    /// <summary>Every face definition of a character, in file order: body keys with age, weight and build, and templates.</summary>
    private static string Face(XElement character)
    {
        var face = character.Element("face");
        if (face == null) return "";
        return string.Join("; ", face.Elements().Select(e => e.Name.LocalName switch
        {
            "BodyProperties" or "BodyPropertiesMax" =>
                $"{e.Name.LocalName} age={(string)e.Attribute("age")} weight={(string)e.Attribute("weight")} build={(string)e.Attribute("build")} key={(string)e.Attribute("key")}",
            "face_key_template" => "template " + (string)e.Attribute("value"),
            _ => null,
        }).Where(s => s != null));
    }

    private static Dictionary<string, XElement> Companions()
    {
        var doc = XDocument.Load(NamedCompanionsXml);
        return doc.Descendants("NPCCharacter").ToDictionary(e => (string)e.Attribute("id"), StringComparer.Ordinal);
    }

    [TestMethod]
    public void EveryCustomCharacter_KeepsTheFaceItsCreatorMade()
    {
        var companions = Companions();
        var bad = new List<string>();
        foreach (var pair in AuthoredFaces)
        {
            if (!companions.TryGetValue(pair.Key, out var character)) { bad.Add(pair.Key + " is missing"); continue; }
            var face = Face(character);
            if (face != pair.Value) bad.Add($"{pair.Key}\n    authored {pair.Value}\n    now      {face}");
        }
        Assert.AreEqual(0, bad.Count, "Custom characters whose face changed (leave them out of look passes):\n  " + string.Join("\n  ", bad));
    }

    [TestMethod]
    public void TheCustomList_IsEveryNamedCompanionNotFromTheBooks()
    {
        var notFromBooks = Companions().Keys.Where(id => !BookCompanions.Contains(id)).OrderBy(x => x, StringComparer.Ordinal);
        CollectionAssert.AreEqual(notFromBooks.ToList(), CustomCharacters.Ids.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            "A named companion was added or removed: put a new non-book character in CustomCharacters.Ids and pin its face here.");
        CollectionAssert.AreEquivalent(CustomCharacters.Ids.ToList(), AuthoredFaces.Keys.ToList(), "Each custom character needs a pinned face.");
    }

    [TestMethod]
    public void LookGates_SkipTheCustomCharacters_AndStillReadTheBookCompanions()
    {
        var ids = new HashSet<string>(HeroFaceKeys.HeroFaces().Select(h => h.Id), StringComparer.Ordinal);
        Assert.IsTrue(ids.Contains("named_companion_aragorn"), "The look gates no longer read named_companions.xml.");
        var leaked = CustomCharacters.Ids.Where(ids.Contains).ToList();
        Assert.AreEqual(0, leaked.Count, "Look gates read custom characters: " + string.Join(", ", leaked));
    }
}
