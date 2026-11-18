using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.CampaignStartOptions;

/// <summary>
/// The MCM properties a campaign can override (the keys of the Readers table below) have one reader each. A new direct read of one of them elsewhere would see
/// MCM while the campaign holds another value, which is the bug this feature removes.
/// </summary>
[TestClass]
public class CampaignOptionReadersGateTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    // The files that are allowed to read the property from MCM, and nowhere else.
    private static readonly Dictionary<string, string[]> Readers = new Dictionary<string, string[]>
    {
        ["EnableTroopWeight"] = new[] { "TroopWeightSettingsProvider.cs" },
        ["WarOfTheRingEnabled"] = new[] { "TaomSettingsProvider.cs" },
        ["WarPhase1TriggerDay"] = new[] { "TaomSettingsProvider.cs" },
        ["WarPhase2TriggerDay"] = new[] { "TaomSettingsProvider.cs" },
        ["MomentumVictoryEnabled"] = new[] { "MomentumSettingsProvider.cs" },
        ["EyeOfSauronEnabled"] = new[] { "EyeOfSauronSettingsProvider.cs" },
        ["EnableAiPartyScaling"] = new[] { "AiPartyScalingSettingsProvider.cs" },
        ["EnableAlignmentRecruitment"] = new[] { "RecruitmentAlignmentSettingsProvider.cs" },
        ["EnableMarriageAlignment"] = new[] { "MarriageAlignmentSettingsProvider.cs" },
        ["EnableAlignmentDesertion"] = new[] { "AlignmentDesertionSettingsProvider.cs" },
        ["AllowPlayerAlliancesWithAnyone"] = new[] { "HostilePeaceSettingsProvider.cs" },
    };

    private static readonly string Names =
        "EnableTroopWeight|WarOfTheRingEnabled|WarPhase1TriggerDay|WarPhase2TriggerDay"
        + "|MomentumVictoryEnabled|EyeOfSauronEnabled|EnableAiPartyScaling|EnableAlignmentRecruitment|EnableMarriageAlignment"
        + "|EnableAlignmentDesertion|AllowPlayerAlliancesWithAnyone";

    // A read through MCM's object (`TaomSettings.Instance?.X`, `Instance!.X`, `Settings.X`, `mcm.X`, `_settings.X`),
    // plus a property pattern (`Instance is { X: true }`). Provider interfaces reuse some of these names
    // (`_wotrSettings.WarOfTheRingEnabled`), so a bare identifier receiver is not matched. TaomSettings.cs declares them.
    // `_settings` is a receiver here, so a field that holds a provider must not use that name: HostilePeaceService's
    // provider field is `_settingsProvider` for that reason (`_settings.AllowPlayerAlliancesWithAnyone` would be a false hit).
    private static readonly Regex McmRead = new Regex(
        @"\b(?:Instance|Settings|Mcm|mcm|_mcm|_settings|settings)[?!]?\.(" + Names + @")\b|\{\s*(" + Names + @")\s*:",
        RegexOptions.Compiled);

    [TestMethod]
    public void NoDirectMcmRead_OfAPerCampaignProperty_OutsideItsProvider()
    {
        var offenders = new List<string>();
        var files = new[] { "Main", "Client" }
            .Select(dir => Path.Combine(RepoRoot, dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories));
        foreach (var file in files)
        {
            if (file.Contains(@"\obj\") || file.Contains(@"\bin\")) continue;
            var name = Path.GetFileName(file);
            if (name == "TaomSettings.cs") continue; // declares the properties
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("///", StringComparison.Ordinal)) continue;
                foreach (Match m in McmRead.Matches(lines[i]))
                {
                    var property = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    if (!Readers[property].Contains(name))
                        offenders.Add($"{name}:{i + 1} reads {property}");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Read these through their provider, which puts the campaign's own choice first (the provider file for "
            + "each property is in the Readers table above):\n" + string.Join("\n", offenders));
    }

    [TestMethod]
    public void EachProvider_StillReadsItsProperty_SoTheGateIsNotVacuous()
    {
        foreach (var pair in Readers)
        {
            foreach (var reader in pair.Value)
            {
                var file = Directory.EnumerateFiles(Path.Combine(RepoRoot, "Main"), reader, SearchOption.AllDirectories).Single();
                StringAssert.Contains(File.ReadAllText(file), pair.Key, reader + " no longer mentions " + pair.Key);
            }
        }
    }
}
