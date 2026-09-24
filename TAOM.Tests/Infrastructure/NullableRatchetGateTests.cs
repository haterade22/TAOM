using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The nullable ratchet (docs/ai-includes/code-quality.md, "How nullable is enforced") only works
/// while no production csproj passes the ratchet ids to the compiler's /nowarn: /nowarn beats every
/// .editorconfig, so one id back in a NoWarn silently switches off every graduated folder while
/// the build stays green.
/// </summary>
[TestClass]
public class NullableRatchetGateTests
{
    private static readonly string[] RatchetIds =
        { "8600", "8601", "8602", "8603", "8604", "8618", "8625" };

    [DataTestMethod]
    [DataRow("Main", "TAOM.csproj")]
    [DataRow("Dependencies", "TAOM.Dependencies.csproj")]
    public void ProductionCsproj_NoWarn_OmitsEveryRatchetId(string folder, string csproj)
    {
        var suppressed = XDocument.Load(RepoPath(folder, csproj))
            .Descendants("NoWarn")
            .SelectMany(e => e.Value.Split(';'))
            .Select(id => id.Trim())
            .Select(id => id.StartsWith("CS", StringComparison.OrdinalIgnoreCase) ? id.Substring(2) : id);

        var leaked = suppressed.Intersect(RatchetIds).ToList();

        Assert.AreEqual(0, leaked.Count,
            $"{folder}/{csproj} suppresses nullable ratchet id(s) {string.Join(", ", leaked)} in <NoWarn>. " +
            "Set them in the root .editorconfig instead (see code-quality.md, \"How nullable is enforced\").");
    }
}
