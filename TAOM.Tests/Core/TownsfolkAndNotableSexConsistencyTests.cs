using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Three invariants over the townsfolk and notable rosters in characters/npcs_*.xml.
///
/// Why this exists: every Townswoman, Tavern Wench, village woman, beggar and dancer outside Rohan
/// shipped without `is_female="true"`. Bannerlord defaults IsFemale to false when the attribute is
/// absent, so the engine chose the male skin, the male face range and the male action set, and
/// players saw women with men's bodies in every culture but one. 166 entries across 17 files.
/// Rohan was right only because f3dbbfe6 added the attribute there and nobody back-filled the rest.
///
/// The same sweep found the notable pools uniformly male: 596 templates and not one female, in
/// every culture including Rohan, against vanilla's 28 female out of 128. A notable's sex is its
/// template's sex, because HeroCreator.CreateNotable builds the hero with
/// CharacterObject.CreateFrom(template) and HeroInitializationArgs then reads IsFemale off that
/// hero, so an all-male pool is an all-male settlement.
///
/// Nothing else in the repo can see either problem. tools/validate_moduledata.py has no is_female
/// rule at all, and tools/schemas/taom_npccharacter.json enumerates default_group and nothing else,
/// so a character whose sex contradicts its own role passes every run.
///
/// Data-only, needs no game, runs in milliseconds. This is the townsfolk half of the pair whose
/// lord half is LordNameAndSexConsistencyTests.
/// </summary>
[TestClass]
public class TownsfolkAndNotableSexConsistencyTests
{
    /// <summary>Id prefixes that name a character who must be female.</summary>
    private const string FemaleRolePattern =
        @"^(townswoman|village_woman|villager_female|tavern_wench|female_beggar|female_dancer)";

    /// <summary>
    /// Notable occupations that must field at least one woman per culture. Vanilla's measured
    /// split is Merchant 12/47, GangLeader 8/19 and Preacher 6/14.
    /// </summary>
    private static readonly string[] MixedSexOccupations = { "Merchant", "GangLeader", "Preacher" };

    /// <summary>
    /// Non-template characters that are deliberately female without matching FemaleRolePattern.
    /// Keep this list tiny and justify every entry. It is empty today: barber_harad and barber_rhun
    /// used to be the only two female barbers of eighteen, and were made male so the set agrees.
    /// </summary>
    private static readonly HashSet<string> AcceptedFemaleOutsideRolePattern =
        new(StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string[] CultureFiles()
    {
        var dir = Path.Combine(FindRepoRoot(), "Main", "_Module", "ModuleData", "characters");
        Assert.IsTrue(Directory.Exists(dir), $"not found at {dir}");
        var files = Directory.GetFiles(dir, "npcs_*.xml");
        Assert.IsTrue(files.Length > 0, "no npcs_*.xml found; the characters folder layout changed");
        return files;
    }

    /// <summary>
    /// One NPCCharacter opening tag. Attribute-order independent on purpose: these files mix
    /// `id` first with `id` after `race`, and an order-sensitive pattern silently skips whole
    /// cultures. That exact mistake once hid the entire Dol Guldur roster from the lord test.
    /// </summary>
    private static IEnumerable<(string Id, string Attrs)> Entries(string xml)
    {
        foreach (Match m in Regex.Matches(xml, @"<NPCCharacter\b(?<attrs>[^>]*)>"))
        {
            var attrs = m.Groups["attrs"].Value;
            var id = Regex.Match(attrs, @"\bid=""(?<v>[^""]*)""");
            if (id.Success) yield return (id.Groups["v"].Value, attrs);
        }
    }

    private static bool IsFemale(string attrs) => Regex.IsMatch(attrs, @"\bis_female=""true""");

    private static bool IsTemplate(string attrs) => Regex.IsMatch(attrs, @"\bis_template=""true""");

    private static string Occupation(string attrs) =>
        Regex.Match(attrs, @"\boccupation=""(?<v>[^""]*)""").Groups["v"].Value;

    [TestMethod]
    public void EveryFemaleRoleCharacterIsMarkedFemale()
    {
        var offenders = new List<string>();
        var checkedCount = 0;

        foreach (var file in CultureFiles())
        {
            var xml = File.ReadAllText(file);
            foreach (var (id, attrs) in Entries(xml))
            {
                if (!Regex.IsMatch(id, FemaleRolePattern)) continue;
                checkedCount++;
                if (!IsFemale(attrs)) offenders.Add($"  {Path.GetFileName(file)}: {id}");
            }
        }

        Assert.IsTrue(checkedCount > 0, "matched no female-role ids; the id conventions changed");
        Assert.AreEqual(0, offenders.Count,
            "These characters are named as women but carry no is_female=\"true\".\n" +
            "Bannerlord defaults IsFemale to false, so each one renders with a male body, a male\n" +
            "face range and the male action set. Add is_female=\"true\" to the opening tag.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void NoMaleRoleCharacterIsMarkedFemale()
    {
        var offenders = new List<string>();

        foreach (var file in CultureFiles())
        {
            var xml = File.ReadAllText(file);
            foreach (var (id, attrs) in Entries(xml))
            {
                // Notable pools are deliberately mixed, and EveryCultureFieldsFemaleNotables owns them.
                if (IsTemplate(attrs)) continue;
                if (Regex.IsMatch(id, FemaleRolePattern)) continue;
                if (AcceptedFemaleOutsideRolePattern.Contains(id)) continue;
                if (IsFemale(attrs)) offenders.Add($"  {Path.GetFileName(file)}: {id}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "These characters are marked female but their id does not name a female role.\n" +
            "Either the id is wrong, or the character is deliberately female and belongs in\n" +
            "AcceptedFemaleOutsideRolePattern with a reason.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void EveryCultureFieldsFemaleNotables()
    {
        var offenders = new List<string>();

        foreach (var file in CultureFiles())
        {
            var xml = File.ReadAllText(file);
            var pools = Entries(xml)
                .Where(e => IsTemplate(e.Attrs))
                .GroupBy(e => Occupation(e.Attrs))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var occupation in MixedSexOccupations)
            {
                if (!pools.TryGetValue(occupation, out var pool) || pool.Count == 0) continue;
                if (!pool.Any(e => IsFemale(e.Attrs)))
                    offenders.Add($"  {Path.GetFileName(file)}: {occupation} pool of {pool.Count} is all male");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A notable's sex is its template's sex, so a pool with no women produces a settlement\n" +
            "with no women in that role. Rural notables and headmen are all male on purpose,\n" +
            "matching vanilla, and are not checked here.\n" +
            string.Join("\n", offenders));
    }
}
