using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

[TestClass]
public class PopupPauseGateTests
{
    private static readonly Regex PopupCall = new(
        @"InformationManager\s*\.\s*Show(?:Inquiry|TextInquiry|MultiSelectionInquiry)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PausesByName = new(
        @"^pauseGameActiveState\s*:\s*true$", RegexOptions.Compiled);

    private static readonly Dictionary<string, (int Calls, string Reason)> Allowed = new()
    {
        ["Main/Adapters/BattleCheatHostAdapter.cs"] = (1, "battle cheat picker opens inside a mission"),
        ["Main/Adapters/FordsMenuAdapter.cs"] = (2, "Fords of War is its own game mode: main menu setup prompt and its mission exit notice"),
        ["Main/Adapters/LongDefeatMenuAdapter.cs"] = (4, "Long Defeat is its own game mode: main menu setup prompts and its mission notices"),
        ["Main/Composition/FeatureModuleHooks.cs"] = (1, "module fault notice shows at the main menu phase only"),
        ["Main/Features/BattleCorpses/BattleSettingsAdviceNotifier.cs"] = (1, "offered once on the first main menu of a session"),
        ["Main/Features/BattleLoadDiagnostics/StallReportNotifier.cs"] = (1, "shown on the next session's main menu"),
        ["Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs"] = (2, "order of battle screen inside a mission"),
        ["Main/Features/CrashReport/UI/BugReportNotifier.cs"] = (1, "the offer after an unexpected end shows on the main menu"),
        ["Main/Features/CrashReport/UI/CrashNotifier.cs"] = (1, "pauses over the campaign map only: a paused MissionState stops a mission load (MissionState.OnIdleTick)"),
        ["Main/Features/EngineBugGuards/Hooks/Patch160_LoadRetryStop.cs"] = (1, "shows while a save load is failing, before any map exists"),
        ["Main/Features/EquipPresets/UI/PresetsOverlayVM.cs"] = (4, "inside the inventory screen, which already holds the campaign clock"),
        ["Main/Features/Messengers/MessengerCampaignBehavior.cs"] = (1, "a notice pauses only when no popup is open: queued behind the arrival popup, whose Speak opens a mission, it would pause that mission's load (MissionState.OnIdleTick)"),
        ["Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs"] = (1, "main menu option"),
        ["Main/Features/StartupAlerts/StartupAlertsEntry.cs"] = (1, "startup notice before a campaign exists"),
        ["Main/SubModule.cs"] = (1, "patch failure notice for the startup phase, shown at the main menu"),
    };

    [TestMethod]
    public void Scan_AcceptsOnlyTheNamedTrueArgument()
    {
        var source = string.Join("\n",
            "class A {",
            "  void Ok() => InformationManager.ShowInquiry(new InquiryData(a, b(c, d)), pauseGameActiveState: true);",
            "  void Multi() => MBInformationManager.ShowMultiSelectionInquiry(new D(x: 1,",
            "      y: 2), pauseGameActiveState: true, prioritize: false);",
            "  void Text() => InformationManager.ShowTextInquiry(new T(\"a, b)\"), true);",
            "  void Default() => InformationManager.ShowInquiry(new InquiryData(a));",
            "  void False() => InformationManager.ShowInquiry(new InquiryData(a), pauseGameActiveState: false);",
            "}");

        var bad = Offences("Main/Planted.cs", source);

        Assert.AreEqual(3, bad.Count, string.Join("\n", bad));
        StringAssert.Contains(bad[0], "Main/Planted.cs:5");
        StringAssert.Contains(bad[1], "Main/Planted.cs:6");
        StringAssert.Contains(bad[2], "Main/Planted.cs:7");
    }

    [TestMethod]
    public void EveryTaomPopupPausesTheCampaignMap()
    {
        var repoDir = RepoPath();
        var perFile = new Dictionary<string, List<string>>();
        foreach (var path in CSharpFiles())
        {
            var relative = path.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/');
            var hits = Offences(relative, StripComments(Text(path).Replace("\r\n", "\n")));
            if (hits.Count > 0)
                perFile[relative] = hits;
        }

        var violations = new List<string>();
        foreach (var entry in perFile)
        {
            var allowed = Allowed.TryGetValue(entry.Key, out var rule) ? rule.Calls : 0;
            if (entry.Value.Count != allowed)
                violations.AddRange(entry.Value.Select(h => $"{h} (allowed in this file: {allowed})"));
        }

        foreach (var rule in Allowed.Where(a => !perFile.ContainsKey(a.Key)))
            violations.Add($"{rule.Key}: allow-list entry matches no popup call, remove it");

        Assert.AreEqual(0, violations.Count,
            "A popup shown with InformationManager.ShowInquiry, ShowTextInquiry or ShowMultiSelectionInquiry must pass "
            + "pauseGameActiveState: true by name, so the campaign map stops under it. A popup that only shows in a "
            + "mission, at startup or inside a screen that already pauses goes in Allowed with its reason:\n"
            + string.Join("\n", violations));
    }

    [TestMethod]
    public void AllowListEntriesCarryAReason()
    {
        var blank = Allowed.Where(a => string.IsNullOrWhiteSpace(a.Value.Reason) || a.Value.Calls < 1).Select(a => a.Key);

        Assert.AreEqual(0, blank.Count(), string.Join("\n", blank));
    }

    private static List<string> Offences(string relativePath, string source)
    {
        var hits = new List<string>();
        foreach (Match call in PopupCall.Matches(source))
        {
            var args = SplitArguments(source, call.Index + call.Length);
            if (args.Skip(1).Any(a => PausesByName.IsMatch(a.Trim())))
                continue;

            var line = source.Take(call.Index).Count(c => c == '\n') + 1;
            hits.Add($"{relativePath}:{line}: popup does not pass pauseGameActiveState: true by name");
        }

        return hits;
    }

    private static List<string> SplitArguments(string source, int start)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 1;
        for (var i = start; i < source.Length && depth > 0; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                current.Append(c);
                for (i++; i < source.Length && source[i] != '"'; i++)
                {
                    if (source[i] == '\\')
                        current.Append(source[i++]);
                    current.Append(source[i]);
                }

                current.Append('"');
                continue;
            }

            if (c == '(' || c == '[' || c == '{')
                depth++;
            else if (c == ')' || c == ']' || c == '}')
                depth--;

            if (depth == 0)
                break;

            if (c == ',' && depth == 1)
            {
                args.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        args.Add(current.ToString());
        return args;
    }
}
