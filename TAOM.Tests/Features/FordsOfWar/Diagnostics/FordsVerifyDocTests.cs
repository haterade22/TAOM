using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>The in-game checklist names the verdict row each of its steps writes to the log.</summary>
[TestClass]
public class FordsVerifyDocTests
{
    [TestMethod]
    public void EveryHowToVerifyItem_NamesItsVerdictRow()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("docs", "features", "fords-of-war.md"));
        var start = text.IndexOf("## How to verify in game", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the section is missing");
        var end = text.IndexOf("\n## ", start + 5, System.StringComparison.Ordinal);
        var section = end < 0 ? text.Substring(start) : text.Substring(start, end - start);

        var items = Regex.Split(section, @"\r?\n(?=\d+\. )");
        for (var n = 1; n <= 16; n++)
        {
            var prefix = n + ". ";
            string found = null;
            foreach (var item in items)
                if (item.StartsWith(prefix, System.StringComparison.Ordinal)) found = item;
            Assert.IsNotNull(found, "item " + n + " is missing");
            Assert.IsTrue(found.Contains("check=" + n.ToString("00")), "item " + n + " does not name check=" + n.ToString("00"));
        }
    }
}
