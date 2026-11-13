using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Mike, 2026-11-07: no warg rage in a Fords of War match ("it is not needed for this"). The hit listener is the only code that
/// starts a rage (it alone sets the rage attack count above 0), so it reads once, when the tree is built inside the mission,
/// whether the mission is a Fords match, and starts none there. These are source reads; in a match, check 12 FAILs on any rage start.
/// </summary>
[TestClass]
public class WargRageFordsTests
{
    private const string Listener = "Main/Features/Warg/BehaviorTreeElements/WargTryToGoRage.cs";

    private static string Squashed(string file) => Regex.Replace(RepoPaths.ReadSource(file, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheHitListener_ReadsTheFordsMission_WhenTheTreeIsBuilt()
    {
        StringAssert.Contains(Squashed(Listener), "private readonly bool _rageAllowed = !FordsOfWarMission.IsActive(Mission.Current);");
    }

    [TestMethod]
    public void TheHitListener_StartsNoRage_WhenRageIsNotAllowed()
    {
        StringAssert.Contains(Squashed(Listener), "if (!_rageAllowed || RageAttackAmount.GetValue() > 0 || blow.IsFallDamage || blow.IsMissile) return;",
            "the gate must end Notify before the rage roll, not only appear in its condition");
    }

    [TestMethod]
    public void OnlyTheHitListener_StartsARage()
    {
        // Every write of the rage attack count in core and client, through the property or its backing field: only the listener sets a
        // count above 0; the others end a rage or count one down.
        var write = new Regex(@"\b_?[Rr]ageAttackAmount\.SetValue\(([^;]*)\);");
        var writes = RepoPaths.CSharpFiles()
            .SelectMany(f => write.Matches(Regex.Replace(RepoPaths.StripComments(RepoPaths.Text(f)), @"\s+", " ")).Cast<Match>()
                .Select(m => Path.GetFileName(f) + ": " + m.Groups[1].Value.Trim()))
            .OrderBy(w => w, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[]
        {
            "CleanIfEnemyDied.cs: 0",
            "FinishRageMode.cs: 0",
            "WargAttackTask.cs: RageAttackAmount.GetValue() - 1",
            "WargTryToGoRage.cs: WargConfig.rageAttacks",
        }, writes, string.Join(" | ", writes));
    }
}
