using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CulturalFeats;

/// <summary>
/// Bannerlord v1.5.7 put <c>bool isCurrentlyAtSea</c> in front of <c>bool includeDescriptions</c> in
/// <c>PartySizeLimitModel.GetPartyMemberSizeLimit</c> (#1112). A call written for v1.5.6 as
/// <c>(party, true)</c> still compiles, and it now asks for the at-sea limit with no descriptions. A bool
/// literal in that call must therefore carry its parameter name.
/// </summary>
[TestClass]
public class PartyMemberSizeLimitCallShapeTests
{
    private static readonly Regex Call = new(@"GetPartyMemberSizeLimit\(([^()]*)\)", RegexOptions.Compiled);
    private static readonly Regex PositionalBoolLiteral = new(@"(^|,)\s*(true|false)\s*(,|$)", RegexOptions.Compiled);

    [TestMethod]
    public void TaomCallers_PassNoPositionalBoolLiteral()
    {
        var calls = CSharpFiles("Main", "Client", "Dependencies")
            .SelectMany(file => Call.Matches(StripComments(File.ReadAllText(file))).Cast<Match>()
                .Select(m => (File: file, Text: m.Value, Args: m.Groups[1].Value)))
            .ToList();

        // TaomPartySizeModel's base call is always there. Zero matches means the scan broke, not that the code is clean.
        Assert.AreNotEqual(0, calls.Count, "found no call to GetPartyMemberSizeLimit under Main, Client or Dependencies");

        var offenders = calls.Where(c => PositionalBoolLiteral.IsMatch(c.Args))
            .Select(c => $"{Path.GetFileName(c.File)}: {c.Text}")
            .ToList();
        Assert.AreEqual(0, offenders.Count, "name the bool argument: " + string.Join("; ", offenders));
    }
}
