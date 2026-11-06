using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// The warg rage controller is a [DefaultView] the engine builds only for a mission with a screen, and SubModule no
/// longer adds a second one (#970). A mission without it must switch nothing instead of disabling vanilla's controller
/// and then throwing (the guard the 1.5.x line added in c91380274). Mission is engine state, so the guards are pinned
/// in source: every lookup is null-checked before its first use.
/// </summary>
[TestClass]
public class WargControllerNullGuardTests
{
    private static readonly Regex Lookup = new Regex(
        @"(?:var|AutonomousMovementPlayerController)\s+(\w+)\s*=\s*[\w.?]*GetMissionBehavior<AutonomousMovementPlayerController>\(\);");

    private static string Squashed(string path) =>
        Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    private static int Pos(string text, string needle)
    {
        var i = text.IndexOf(needle, StringComparison.Ordinal);
        return i < 0 ? int.MaxValue : i;
    }

    [DataTestMethod]
    [DataRow("Main/Features/Warg/WargMissionBehavior.cs")]
    [DataRow("Main/Features/Warg/WargRiderHandManager.cs")]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs")]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/FinishRageMode.cs")]
    public void EveryRageControllerLookup_IsNullCheckedBeforeItsFirstUse(string path)
    {
        var src = Squashed(path);
        var lookups = Lookup.Matches(src);
        Assert.IsTrue(lookups.Count > 0, path + " no longer looks the rage controller up; update this pin");
        foreach (Match m in lookups)
        {
            var name = m.Groups[1].Value;
            var rest = src.Substring(m.Index + m.Length);
            var use = rest.IndexOf(name + ".", StringComparison.Ordinal);
            var guard = Math.Min(Pos(rest, "if (" + name + " == null)"), Pos(rest, "if (" + name + " != null)"));
            Assert.IsTrue(use < 0 || guard < use, $"{path}: '{name}' is used before a null check");
        }
    }

    [TestMethod]
    public void SwitchMainAgentController_ReturnsBeforeTouchingVanillasController()
    {
        var src = Squashed("Main/Features/Warg/WargMissionBehavior.cs");
        var guard = Pos(src, "if (noMovementController == null) return;");
        Assert.IsTrue(guard < int.MaxValue, "no null guard on the rage controller");
        Assert.IsTrue(guard < Pos(src, "baseController.Disable();"), "vanilla's controller is switched before the guard");
    }
}
