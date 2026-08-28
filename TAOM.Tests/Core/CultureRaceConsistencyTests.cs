using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every race a culture's lords actually use must be a race that culture is allowed to offer.
///
/// Why this exists: Isengard shipped with `cultures.json` allowing only uruk_hai, berserker and
/// human, while `lord_I2_3` (Sharku) was race="uruk". It was the only culture in the game with a
/// lord race outside its own allowed list, and nothing anywhere noticed. It surfaced only because a
/// player reported the character-creation preview behaving oddly for that one culture, and it took a
/// diagnostic build and a log to find.
///
/// The mismatch matters beyond tidiness. `cultures.json` races drive the character-creation race
/// filter (FaceGenRaceSelectorRebuilder), and races[0] is the default the filter forces on first
/// apply. A lord whose race is absent from that list is a race the player can be shown but never
/// select, and the two halves of the data disagree about what the culture IS.
///
/// Data-only check, so it needs no game and runs in milliseconds.
/// </summary>
[TestClass]
public class CultureRaceConsistencyTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    /// <summary>culture_id -> allowed races, parsed straight from the shipped JSON.</summary>
    private static Dictionary<string, List<string>> AllowedRaces(string root)
    {
        var path = Path.Combine(root, "Main", "_Module", "ModuleData", "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(path), $"cultures.json not found at {path}");

        var text = File.ReadAllText(path);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Deliberately regex rather than a JSON dependency: the test project has no JSON library
        // reference and the shape here is stable and simple.
        foreach (Match m in Regex.Matches(
            text,
            @"""culture_id""\s*:\s*""(?<id>[^""]+)""\s*,\s*""races""\s*:\s*\[(?<races>[^\]]*)\]",
            RegexOptions.Singleline))
        {
            var races = Regex.Matches(m.Groups["races"].Value, @"""([^""]+)""")
                .Cast<Match>().Select(r => r.Groups[1].Value).ToList();
            result[m.Groups["id"].Value] = races;
        }

        Assert.IsTrue(result.Count > 0, "parsed no cultures from cultures.json; the file shape changed");
        return result;
    }

    /// <summary>culture_id -> the distinct races its lords actually declare.</summary>
    private static Dictionary<string, HashSet<string>> LordRaces(string root)
    {
        var path = Path.Combine(root, "Main", "_Module", "ModuleData", "characters", "lords.xml");
        Assert.IsTrue(File.Exists(path), $"lords.xml not found at {path}");

        var text = File.ReadAllText(path);
        var used = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (Match m in Regex.Matches(
            text,
            @"<NPCCharacter\b[^>]*?race=""(?<race>[a-z_]+)""[^>]*?culture=""Culture\.(?<culture>[a-z_0-9]+)""",
            RegexOptions.Singleline))
        {
            var culture = m.Groups["culture"].Value;
            if (!used.TryGetValue(culture, out var set))
                used[culture] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(m.Groups["race"].Value);
        }

        Assert.IsTrue(used.Count > 0, "parsed no lords from lords.xml; the file shape changed");
        return used;
    }

    [TestMethod]
    public void EveryLordRaceIsOfferedByItsOwnCulture()
    {
        var root = FindRepoRoot();
        var allowed = AllowedRaces(root);
        var used = LordRaces(root);

        var problems = new List<string>();

        foreach (var pair in used.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            // Only cultures that declare a character-creation race list are in scope. A culture
            // absent from cultures.json is not player-selectable, so it has no list to violate.
            if (!allowed.TryGetValue(pair.Key, out var permitted))
                continue;

            var strays = pair.Value.Where(r => !permitted.Contains(r)).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            if (strays.Length > 0)
                problems.Add($"{pair.Key}: lords use [{string.Join(", ", strays)}] " +
                             $"but cultures.json allows only [{string.Join(", ", permitted)}]");
        }

        Assert.AreEqual(0, problems.Count,
            "A culture's lords use a race that culture cannot offer at character creation. " +
            "Either add the race to cultures.json or change the lord.\n  " +
            string.Join("\n  ", problems));
    }

    [TestMethod]
    public void EveryAllowedRaceIsARealRegisteredRace()
    {
        // Guards the other direction: a typo in cultures.json (say "uruk-hai" for "uruk_hai")
        // would silently produce a filter entry that matches no race. The registered set lives in
        // the Armory's skins.xml, which is a game-install path, so this skips rather than fails
        // when the install is not present.
        var skins = new[]
        {
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\skins.xml",
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\skins.xml",
        }.Where(File.Exists).ToArray();

        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");

        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in skins)
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"<race\b[^>]*?id\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
                registered.Add(m.Groups[1].Value);

        Assert.IsTrue(registered.Count > 0, "parsed no races from skins.xml; the file shape changed");

        var unknown = AllowedRaces(FindRepoRoot())
            .SelectMany(c => c.Value.Select(r => (culture: c.Key, race: r)))
            .Where(x => !registered.Contains(x.race))
            .Select(x => $"{x.culture} -> '{x.race}'")
            .Distinct()
            .ToArray();

        Assert.AreEqual(0, unknown.Length,
            "cultures.json names a race that no skins.xml registers, so the filter entry can never match: "
            + string.Join(", ", unknown));
    }
}
