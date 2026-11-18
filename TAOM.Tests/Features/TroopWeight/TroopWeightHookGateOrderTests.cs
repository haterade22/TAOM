using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Each Troop Weight hook asks <c>ITroopWeightService.IsEnabled</c> before its try block, so a campaign that
/// turned Troop Weight off pays no cost and cannot fault in the hook body. The hooks take sealed engine types
/// that a test cannot construct, so this is a source-text assertion, as in <see cref="PartyUpgraderShedGuardTests"/>.
/// </summary>
[TestClass]
public class TroopWeightHookGateOrderTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    [DataTestMethod]
    [DataRow("TroopWeightDisplayHook.cs", "OnRefreshPartyInformation")]
    [DataRow("TroopWeightDisplayHook.cs", "OnClanPartyItemUpdateProperties")]
    [DataRow("TroopWeightDisplayHook.cs", "OnRecruitmentRefreshPartyProperties")]
    [DataRow("TroopWeightDisplayHook.cs", "OnGetPartyHealthTooltip")]
    [DataRow("TroopWeightDisplayHook.cs", "OnPartyCharacterRefreshValues")]
    [DataRow("PartyUpgraderUpgradeReadyTroopsHook.cs", "OnUpgradeReadyTroops")]
    public void HookMethod_ChecksIsEnabledBeforeItsTry(string file, string method)
    {
        var path = Path.Combine(FindRepoRoot(), "Main", "Features", "TroopWeight", "Hooks", file);
        Assert.IsTrue(File.Exists(path), $"{file} not found at {path}");
        var src = File.ReadAllText(path);

        var start = src.IndexOf("public void " + method + "(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{method} not found in {file}");
        var next = src.IndexOf("\n    public ", start + 1, StringComparison.Ordinal);
        var body = next < 0 ? src.Substring(start) : src.Substring(start, next - start);

        var gate = body.IndexOf("IsEnabled", StringComparison.Ordinal);
        var tryMatch = Regex.Match(body, @"\btry\b");
        Assert.IsTrue(gate >= 0, $"{method}: no IsEnabled check");
        Assert.IsTrue(tryMatch.Success, $"{method}: no try block found");
        Assert.IsTrue(gate < tryMatch.Index, $"{method}: IsEnabled must be checked before the try block");
    }
}
