using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.PrisonerRecruitment;

/// <summary>
/// Pins the shipped alignment.json against the PrisonerRecruitment waiver's silent-failure mode:
/// a bandit culture classified onto a side would start waiving the vanilla -2 bandit morale cost
/// for every same-side player, with nothing else in the stack to catch it. Drives the real
/// provider + real AlignmentService so it tests the load path, not just the file's text.
/// </summary>
[TestClass]
public class ShippedAlignmentConfigTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private AlignmentService _alignment = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _alignment = new AlignmentService(
            new AlignmentConfigProvider(pathService, Substitute.For<IModLogger>()),
            Substitute.For<IModLogger>());
    }

    /// <summary>
    /// Derived from taom_spcultures.xml rather than hardcoded, so a newly-added bandit culture is
    /// covered automatically. A hardcoded list would itself go stale — the dead-key class that
    /// shipped the BannerBearers CRITICAL.
    /// </summary>
    private static List<string> BanditCultureIds()
    {
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        return Regex.Matches(xml, "<Culture\\b[^>]*?\\bid\\s*=\\s*\"([^\"]+)\"[^>]*?\\bis_bandit\\s*=\\s*\"true\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    [TestMethod]
    public void ShippedSpcultures_DeclaresBanditCultures_SoTheGuardBelowIsMeaningful()
    {
        // If the derivation regex ever silently matches nothing, every assertion below would
        // vacuously pass. Pin the count's floor.
        var bandits = BanditCultureIds();

        Assert.IsTrue(bandits.Count >= 8,
            $"expected at least 8 is_bandit cultures in taom_spcultures.xml, found {bandits.Count} " +
            "— the derivation regex may have gone stale against the file's attribute order.");
    }

    [TestMethod]
    public void ShippedAlignmentConfig_BanditCultures_ResolveNeutral_SoBanditPenaltyIsNeverWaived()
    {
        foreach (var banditCulture in BanditCultureIds())
        {
            Assert.AreEqual(FactionSide.Neutral, _alignment.GetCultureSide(banditCulture),
                $"bandit culture '{banditCulture}' is classified onto a side in execution/alignment.json. " +
                "PrisonerRecruitment would then waive the vanilla -2 bandit morale cost for every " +
                "same-side recruiter. Bandit cultures must stay unclassified (absent = Neutral).");
        }
    }

    /// <summary>
    /// Pins the data-authoring invariant the "-2 bandit cost is never waived" guarantee rests on.
    ///
    /// Vanilla keys the -2 on `character.Occupation == Bandit` (a per-TROOP field) but gates
    /// recruitability on `character.Culture.IsBandit` (a per-CULTURE field). Those are independent.
    /// A troop pairing occupation="Bandit" with a MAINLINE culture would be both recruitable (its
    /// culture isn't bandit, so vanilla's IsPrisonerRecruitable lets it through) AND eligible for our
    /// waiver — silently zeroing a -2. No such troop exists today; nothing but this test stops one
    /// from being authored. Deep-review data-flow finding, 2026-07-16.
    /// </summary>
    [TestMethod]
    public void ShippedTroops_EveryBanditOccupationTroop_CarriesABanditCulture()
    {
        var banditCultures = new HashSet<string>(BanditCultureIds(), StringComparer.Ordinal);
        var offenders = new List<string>();
        var checkedCount = 0;

        foreach (var file in Directory.GetFiles(Path.Combine(ModuleDataPath, "troops"), "*.xml")
                     .Concat(Directory.GetFiles(Path.Combine(ModuleDataPath, "characters"), "*.xml")))
        {
            foreach (Match npc in Regex.Matches(File.ReadAllText(file), "<NPCCharacter\\b[^>]*>"))
            {
                if (!npc.Value.Contains("occupation=\"Bandit\"")) continue;
                checkedCount++;

                var id = Regex.Match(npc.Value, "\\bid\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
                var culture = Regex.Match(npc.Value, "\\bculture\\s*=\\s*\"Culture\\.([^\"]+)\"").Groups[1].Value;

                if (!banditCultures.Contains(culture))
                    offenders.Add($"{Path.GetFileName(file)}: {id} -> culture '{culture}'");
            }
        }

        Assert.IsTrue(checkedCount >= 8,
            $"expected at least 8 occupation=\"Bandit\" troops, found {checkedCount} — the scan may be stale.");
        Assert.AreEqual(0, offenders.Count,
            "These troops pair occupation=\"Bandit\" with a non-bandit culture. Vanilla charges them -2 " +
            "morale but still allows recruiting them (IsPrisonerRecruitable only blocks bandit CULTURES), " +
            "so PrisonerRecruitment's same-culture/same-side waiver would zero a -2 bandit cost:\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The seven Evil factions the waiver exists to serve. Uses real culture StringIds — the six
    /// XSLT-reskinned cultures keep their VANILLA ids (spcultures.xslt renames <name>, never id),
    /// so Dunland is 'empire' and Rhun is 'khuzait'. Writing 'dunland' or 'rhun' here would be a
    /// dead key that silently resolves Neutral.
    /// </summary>
    [DataTestMethod]
    [DataRow("mordor")]
    [DataRow("isengard")]
    [DataRow("gundabad")]
    [DataRow("dolguldur")]
    [DataRow("empire")]     // Dunland
    [DataRow("khuzait")]    // Rhun
    [DataRow("aserai")]     // Harad
    public void ShippedAlignmentConfig_SauronServingCultures_ResolveEvil(string cultureId)
    {
        Assert.AreEqual(FactionSide.Evil, _alignment.GetCultureSide(cultureId),
            $"culture '{cultureId}' must resolve Evil for the prisoner-recruitment waiver to fire.");
    }

    [DataTestMethod]
    [DataRow("gondor")]
    [DataRow("vlandia")]    // Rohan
    [DataRow("erebor")]
    [DataRow("rivendell")]
    [DataRow("lothlorien")]
    [DataRow("mirkwood")]
    [DataRow("sturgia")]    // Dale
    public void ShippedAlignmentConfig_FreePeoplesCultures_ResolveFree(string cultureId)
    {
        Assert.AreEqual(FactionSide.Free, _alignment.GetCultureSide(cultureId));
    }
}
