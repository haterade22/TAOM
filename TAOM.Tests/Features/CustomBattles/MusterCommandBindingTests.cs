using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Every public Execute command on the Custom Battle muster's view models is bound by a muster prefab (#1293). The
/// OLD SCREEN command was never bound, so the docs promised a way back to the vanilla screen that the player could
/// not reach; a command no prefab binds is dead code or a missing button. Every prefab command also names a method a
/// muster view model declares, so a later removal cannot leave a button that does nothing.
/// </summary>
[TestClass]
public class MusterCommandBindingTests
{
    [TestMethod]
    public void EveryMusterExecuteCommand_IsBoundByAMusterPrefab()
    {
        var bound = Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "CustomBattle", "Muster"), "*.xml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Command\.\w+=""(\w+)""").Cast<Match>().Select(m => m.Groups[1].Value))
            .ToHashSet();
        var sources = Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "CustomBattles", "Muster"), "*.cs");
        Assert.IsTrue(sources.Any(f => Path.GetFileName(f) == "TcbVM.cs") && bound.Count > 10,
            "the muster sources or prefabs were not found");
        var code = sources.ToDictionary(f => Path.GetFileName(f), f => RepoPaths.StripComments(File.ReadAllText(f)));

        var unbound = code
            .SelectMany(kv => Regex.Matches(kv.Value, @"public void (Execute\w+)\s*\(").Cast<Match>()
                .Select(m => (file: kv.Key, name: m.Groups[1].Value)))
            .Where(c => !bound.Contains(c.name))
            .Select(c => $"{c.file}: {c.name}")
            .ToList();
        Assert.AreEqual(0, unbound.Count, "commands no muster prefab binds:\n" + string.Join("\n", unbound));

        // the other way: a prefab command no view model declares is skipped silently by the engine
        var declared = code.Values
            .SelectMany(c => Regex.Matches(c, @"public (?:override |virtual )?void (\w+)\s*\(").Cast<Match>().Select(m => m.Groups[1].Value))
            .ToHashSet();
        var missing = bound.Where(b => !declared.Contains(b)).OrderBy(b => b).ToList();
        Assert.AreEqual(0, missing.Count, "prefab commands no muster view model declares:\n" + string.Join("\n", missing));
    }
}
