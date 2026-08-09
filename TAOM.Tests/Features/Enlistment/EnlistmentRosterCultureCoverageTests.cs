using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Cross-references TAOM's main cultures against the enlistment equipment rosters.
///
/// <c>EnlistmentRosterResolver</c> falls back to <c>enlist_default_{rank}</c> when a commander's
/// culture has no roster of its own — and that fallback is tagged <c>Culture.neutral_culture</c>
/// but is not neutral: all four ranks are Rohan militia with Dunland boots and bracers. So a
/// missing roster does not fail, log, or crash. It quietly issues the wrong faction's kit, which is
/// the complaint that opened #427 arriving by a second route (#431).
///
/// The predicate is <c>is_main_culture="true"</c> in <c>taom_spcultures.xml</c> — the same one
/// <c>taom_schema.py</c>'s MISSING_EDUCATION_TEMPLATES rule uses, for the same reason: it is
/// derived from the data rather than hand-maintained. Four TAOM systems have now shipped a
/// per-culture gap (careers, narrative options, narrative strings, education templates), every one
/// through a list written before the missing culture existed.
///
/// The vanilla six (aserai, battania, empire, khuzait, sturgia, vlandia) are not main cultures but
/// do have rosters, since TAOM re-skins them as Harad, Khand, Dunland, Rhûn, Dale and Rohan. They
/// are covered incidentally, not by this rule.
/// </summary>
[TestClass]
public class EnlistmentRosterCultureCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>Mirrors <c>EnlistmentRosterIds.RankToken</c> — every rank needs its own roster.</summary>
    private static readonly string[] RankTokens = { "recruit", "soldier", "veteran", "sergeant" };

    /// <summary>
    /// Main cultures knowingly without rosters. Removing an entry is the last step of authoring the
    /// four rosters; adding one requires an issue explaining why that culture's lords issue another
    /// faction's gear.
    /// </summary>
    private static readonly HashSet<string> DocumentedExceptions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // #431 — 8 abanissa and 9 shaghana lord clans whose soldiers are dressed as Rohan
            // militia by the fallback. Blocked on a decision: author the rosters, or make the
            // fallback genuinely culture-neutral so no future culture inherits the same answer.
            "abanissa",
            "shaghana",
        };

    private static readonly Regex CultureElement = new Regex(@"<Culture\b([^>]*?)/?>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex IdAttribute = new Regex(@"\bid=""([A-Za-z0-9_.\-]+)""", RegexOptions.Compiled);

    private static List<string> LoadMainCultures()
    {
        var path = Path.Combine(ModuleDataPath, "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");

        var cultures = new List<string>();
        foreach (Match element in CultureElement.Matches(File.ReadAllText(path)))
        {
            var attributes = element.Groups[1].Value;
            if (!attributes.Contains("is_main_culture=\"true\""))
                continue;

            var id = IdAttribute.Match(attributes);
            if (id.Success)
                cultures.Add(id.Groups[1].Value);
        }

        Assert.IsTrue(cultures.Count > 0, "No main cultures parsed from taom_spcultures.xml — the "
            + "parse is broken, and a test that finds nothing to check passes for the wrong reason.");
        return cultures;
    }

    private static HashSet<string> LoadRosterIds()
    {
        var path = Path.Combine(ModuleDataPath, "equipmentsets", "taom_enlistment_equipment.xml");
        Assert.IsTrue(File.Exists(path), $"taom_enlistment_equipment.xml not found at {path}");

        return new HashSet<string>(
            XDocument.Load(path).Descendants("EquipmentRoster")
                .Select(r => (string?)r.Attribute("id"))
                .Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void EveryMainCulture_HasAllFourRankRosters_OrIsDocumented()
    {
        var rosters = LoadRosterIds();

        var missing = (from culture in LoadMainCultures()
                       where !DocumentedExceptions.Contains(culture)
                       from rank in RankTokens
                       let id = $"enlist_{culture}_{rank}"
                       where !rosters.Contains(id)
                       select id).ToList();

        Assert.AreEqual(0, missing.Count,
            "A main culture with no enlistment roster does not fail — EnlistmentRosterResolver falls "
            + "back to enlist_default_*, which is Rohan militia gear, so the player is silently "
            + "issued another faction's kit (#431). Author these rosters, or document the "
            + "exception:\n  " + string.Join("\n  ", missing));
    }

    [TestMethod]
    public void TheDefaultFallback_CoversEveryRank()
    {
        // The fallback is what every undocumented gap lands on. A rank missing from it turns a
        // cosmetic problem into an unequipped soldier, and the resolver returns the REQUESTED rank
        // or null — it does not walk down to a lower one.
        var rosters = LoadRosterIds();

        var missing = RankTokens.Select(r => $"enlist_default_{r}")
            .Where(id => !rosters.Contains(id))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "The culture-neutral fallback is missing a rank: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void DocumentedExceptions_AreStillMainCulturesAndStillUnauthored()
    {
        // A resolved exception is a stale suppression: it silently widens the blind spot for
        // whoever inherits it, and nothing else would ever prompt its deletion.
        var cultures = LoadMainCultures();
        var rosters = LoadRosterIds();
        var stale = new List<string>();

        foreach (var exception in DocumentedExceptions)
        {
            if (!cultures.Contains(exception, StringComparer.OrdinalIgnoreCase))
            {
                stale.Add($"{exception}: no longer a main culture — delete the exception");
                continue;
            }

            var authored = RankTokens.Count(r => rosters.Contains($"enlist_{exception}_{r}"));
            if (authored == RankTokens.Length)
                stale.Add($"{exception}: all four rosters now exist — delete the exception and close #431");
            else if (authored > 0)
                stale.Add($"{exception}: partially authored ({authored}/{RankTokens.Length} ranks) — "
                    + "the unauthored ranks still fall back to Rohan gear, so a half-finished "
                    + "culture is harder to spot than an untouched one; finish it or revert");
        }

        Assert.AreEqual(0, stale.Count,
            "Stale entries in DocumentedExceptions:\n  " + string.Join("\n  ", stale));
    }
}
