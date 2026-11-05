using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>RepoPaths.ReadSource</c>, the one reader every test that scans Main/SubModule.cs or
/// Main/IoC.cs goes through: LF line endings, either path separator, a missing file FAILS (a wiring
/// gate that goes Inconclusive stops guarding without saying so), and the comment-stripped view keeps
/// length and line breaks while hiding commented-out code.
/// </summary>
[TestClass]
public class RepoPathsTests
{
    [TestMethod]
    public void CSharpFiles_Main_ContainsTheSubModuleAndNothingUnderObjOrBin()
    {
        var files = RepoPaths.CSharpFiles("Main").ToList();

        Assert.IsTrue(files.Any(f => Path.GetFileName(f) == "SubModule.cs"), "SubModule.cs is missing.");
        var repoDir = RepoPaths.RepoPath();
        var built = files.Where(f => f.Substring(repoDir.Length).Split('\\', '/').Any(s => s == "obj" || s == "bin")).ToList();
        CollectionAssert.AreEqual(new string[0], built, "CSharpFiles must skip obj and bin.");
    }

    // This line has one compiled code root. The 1.5.x line adds "Client" (its TAOM.Client
    // submodule), which does not exist here.
    [TestMethod]
    public void CSharpFiles_WithNoRoots_ReadsEveryCodeRoot()
    {
        CollectionAssert.AreEqual(new[] { "Main" }, RepoPaths.CodeRoots);
        Assert.IsTrue(RepoPaths.CSharpFiles().Any(f => Path.GetFileName(f) == "SubModule.cs"));
    }

    [TestMethod]
    public void ReadSource_ReturnsTheFileWithLfLineEndingsOnly()
    {
        var text = RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs");

        StringAssert.Contains(text, "public static class RepoPaths");
        Assert.IsFalse(text.Contains("\r"), "ReadSource must normalise CRLF to LF.");
    }

    // The repo-file read above passes without normalisation on an LF checkout (Codex review of plan
    // 018, P3), so this one reads a file that certainly holds CRLF. bin/ is ignored by git.
    [TestMethod]
    public void ReadSource_NormalisesAnExplicitCrlfFile_ToLf()
    {
        var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-crlf-probe.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        File.WriteAllText(probe, "a\r\nb\r\n");
        try
        {
            Assert.AreEqual("a\nb\n", RepoPaths.ReadSource("TAOM.Tests/bin/repopaths-crlf-probe.txt"));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [TestMethod]
    public void ReadSource_AcceptsEitherPathSeparator()
    {
        Assert.AreEqual(
            RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs"),
            RepoPaths.ReadSource(@"TAOM.Tests\Infrastructure\RepoPaths.cs"));
    }

    [TestMethod]
    public void ReadSource_MissingFile_FailsTheTestInsteadOfSkippingIt()
    {
        Assert.ThrowsException<AssertFailedException>(
            () => RepoPaths.ReadSource("Main/NoSuchFile_Plan018.cs"));
    }

    [TestMethod]
    public void StripComments_BlanksLineAndBlockComments_KeepingLengthAndLineBreaks()
    {
        const string source =
            "keep();\n" +
            "// campaignStarter.AddModel(new ParkedModel());\n" +
            "live(); /* block\n spans */ tail();\n";

        var stripped = RepoPaths.StripComments(source);

        Assert.AreEqual(source.Length, stripped.Length);
        CollectionAssert.AreEqual(
            source.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray(),
            stripped.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray());
        StringAssert.Contains(stripped, "keep();");
        StringAssert.Contains(stripped, "live();");
        StringAssert.Contains(stripped, "tail();");
        Assert.IsFalse(stripped.Contains("new ParkedModel("), "A commented-out registration must not survive.");
        Assert.IsFalse(stripped.Contains("block") || stripped.Contains("spans"), "A block comment must not survive.");
    }

    [DataTestMethod]
    [DataRow("// FooBehavior only here\nint x;\n", "FooBehavior", false, false)]
    [DataRow("/* FooBehavior */ int x;\n", "FooBehavior", true, false)]
    [DataRow("new FooBehavior();\n", "FooBehavior", false, true)]
    [DataRow("new FooBehavior();\n", "FooBehavior", true, true)]
    [DataRow("new FooBehaviorExtra();\n", "FooBehavior", true, false)]
    [DataRow("new FooBehaviorExtra();\n", "FooBehavior", false, true)]
    public void MentionsOutsideComments_ReadsCodeNotComments(string content, string needle, bool wholeWord, bool expected)
    {
        var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-mentions-probe.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        File.WriteAllText(probe, content);
        try
        {
            Assert.AreEqual(expected, RepoPaths.MentionsOutsideComments(probe, needle, wholeWord));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [TestMethod]
    public void MentionsOutsideComments_MissingFile_Throws()
    {
        var missing = RepoPaths.RepoPath("TAOM.Tests", "bin", "no-such-mentions-probe.txt");

        Assert.ThrowsException<FileNotFoundException>(
            () => RepoPaths.MentionsOutsideComments(missing, "X"));
    }
}
