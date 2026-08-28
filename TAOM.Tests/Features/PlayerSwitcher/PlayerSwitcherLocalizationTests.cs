using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The REVERSE localization check.
///
/// TAOM's existing localization suite is one-directional: it proves every key the English source
/// declares has a row in all twelve language files. Nothing proved the opposite, that a declared key
/// is ever actually rendered. This feature shipped seven keys that nothing referenced, and they were
/// not merely wasted translation: three of them
/// (`taom_ps_switched`, `taom_ps_failed`, `taom_ps_unavailable`) were the player-facing outcome
/// messages, specified in the implementation plan and then never wired, so a failed handover told
/// the player nothing at all while the string for it sat translated in twelve languages.
///
/// A dead key is usually the fossil of a step that was specified and never built. That is what makes
/// this worth a test rather than a tidy-up.
/// </summary>
[TestClass]
public class PlayerSwitcherLocalizationTests
{
    private const string StringsRelativePath = @"Main\_Module\ModuleData\taom_player_switcher_strings.xml";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static IReadOnlyList<string> DeclaredKeys(string root)
    {
        var path = Path.Combine(root, StringsRelativePath);
        Assert.IsTrue(File.Exists(path), $"strings file not found at {path}");

        return Regex.Matches(File.ReadAllText(path), @"<string\s+id=""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    /// <summary>Everything the feature could plausibly render from: its own C# and the picker prefab.</summary>
    private static string ConsumerText(string root)
    {
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(Path.Combine(root, "Main"), "*.cs", SearchOption.AllDirectories));

        var prefab = Path.Combine(root, @"Main\_Module\GUI\Prefabs\FacGen\PreBuildCharacterSelection.xml");
        if (File.Exists(prefab))
            files.Add(prefab);

        return string.Join("\n", files.Select(File.ReadAllText));
    }

    [TestMethod]
    public void EveryDeclaredKeyIsActuallyReferenced()
    {
        var root = FindRepoRoot();
        var consumers = ConsumerText(root);

        var dead = DeclaredKeys(root)
            .Where(k => consumers.IndexOf(k, StringComparison.Ordinal) < 0)
            .ToArray();

        Assert.AreEqual(0, dead.Length,
            "these keys are declared and translated into twelve languages but nothing renders them: " +
            string.Join(", ", dead) +
            ". A dead key is usually a feature step that was specified and never implemented; " +
            "either wire it or delete it rather than shipping translated dead weight.");
    }

    [TestMethod]
    public void TheOutcomeMessagesExist_SoAFailedHandoverIsNeverSilent()
    {
        var declared = DeclaredKeys(FindRepoRoot());

        foreach (var key in new[] { "taom_ps_switched", "taom_ps_failed", "taom_ps_unavailable" })
        {
            CollectionAssert.Contains(declared.ToArray(), key,
                $"'{key}' is how the player learns what happened to their choice");
        }
    }
}
