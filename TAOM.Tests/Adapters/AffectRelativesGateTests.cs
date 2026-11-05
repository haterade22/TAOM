using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Adapters;

/// <summary>
/// v1.4.8 <c>ChangeRelationAction.ApplyPlayerRelation</c> never reads <c>affectRelatives</c>: the change lands on the
/// pair from <c>DiplomacyModel.GetHeroesForEffectiveRelation</c>, and nothing spreads to the hero's kin. A call that
/// passes <c>affectRelatives: true</c> reads as that spread (#923).
/// </summary>
[TestClass]
public class AffectRelativesGateTests
{
    private static readonly Regex AffectRelativesTrue = new(@"affectRelatives\s*:\s*true", RegexOptions.Compiled);

    [TestMethod]
    public void NoRelationCall_PassesAffectRelativesTrue()
    {
        // The raw match is a necessary condition, so only its few candidates pay for StripComments.
        var offenders = CSharpFiles()
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .Where(f => AffectRelativesTrue.IsMatch(f.Text) && AffectRelativesTrue.IsMatch(StripComments(f.Text)))
            .Select(f => f.Path)
            .Select(f => f.Substring(RepoPath().Length))
            .ToArray();

        Assert.AreEqual(0, offenders.Length,
            "the engine ignores affectRelatives, so true promises a spread to kin that never happens: "
            + string.Join(", ", offenders));
    }
}
