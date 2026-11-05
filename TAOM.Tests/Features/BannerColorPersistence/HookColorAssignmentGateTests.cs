using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// #993 gate: <c>CharacterCode.Code</c> is built at creation, so assigning <c>.Color1</c> or
/// <c>.Color2</c> on a finished code changes nothing visible. Every UI hook must rebuild through
/// <c>ClanColoredCharacterCode.Recolor</c> instead.
/// </summary>
[TestClass]
public class HookColorAssignmentGateTests
{
    private static readonly Regex Assignment = new(@"\.Color[12]\s*=(?!=)", RegexOptions.Compiled);

    [TestMethod]
    public void Hooks_NeverAssignColor1OrColor2OnABuiltCode()
    {
        var dir = RepoPaths.RepoPath("Main", "Features", "BannerColorPersistence", "Hooks");
        var offenders = Directory.EnumerateFiles(dir, "*.cs")
            .Where(f => Path.GetFileName(f) != "ClanColoredCharacterCode.cs")
            .Where(f => Assignment.IsMatch(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "These hooks assign Color1/Color2 on a built CharacterCode, which changes nothing visible; "
            + "use ClanColoredCharacterCode.Recolor: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void Hooks_DirectoryHasTheRecolourHelper()
    {
        // Guards the gate itself: a moved folder would make the scan above vacuous.
        var src = RepoPaths.ReadSource("Main/Features/BannerColorPersistence/Hooks/ClanColoredCharacterCode.cs");
        StringAssert.Contains(src, "internal static CharacterCode Recolor");
    }
}
