using System;
using System.Collections.Generic;
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
        var leaked = XDocument.Load(RepoPath(folder, csproj))
            .Descendants("NoWarn")
            .SelectMany(e => LeakedRatchetIds(e.Value))
            .Distinct()
            .ToList();

        Assert.AreEqual(0, leaked.Count,
            $"{folder}/{csproj} suppresses nullable ratchet id(s) {string.Join(", ", leaked)} in <NoWarn>. " +
            "Set them in the root .editorconfig instead (see code-quality.md, \"How nullable is enforced\").");
    }

    [DataTestMethod]
    [DataRow("$(NoWarn);8602")]
    [DataRow("$(NoWarn);CS8602")]
    [DataRow("$(NoWarn);1701,8602")]
    [DataRow("$(NoWarn);1701 8602")]
    [DataRow("$(NoWarn);nullable")]
    [DataRow("$(NoWarn);Nullable")]
    public void LeakedRatchetIds_CatchesEveryCompilerSpelling(string noWarn)
    {
        Assert.AreNotEqual(0, LeakedRatchetIds(noWarn).Count, $"missed a ratchet id in '{noWarn}'");
    }

    [TestMethod]
    public void LeakedRatchetIds_IgnoresOtherIds()
    {
        Assert.AreEqual(0, LeakedRatchetIds("$(NoWarn);8374;8174").Count);
    }

    // Mirrors the compiler's /nowarn parsing: the Csc task splits on ';' and ',', the compiler on
    // ',', ';' and ' ', and the word "nullable" (any case) expands to every nullable warning.
    private static List<string> LeakedRatchetIds(string noWarn)
    {
        return noWarn.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(id => string.Equals(id, "nullable", StringComparison.OrdinalIgnoreCase)
                ? RatchetIds
                : new[] { id.StartsWith("CS", StringComparison.OrdinalIgnoreCase) ? id.Substring(2) : id })
            .Intersect(RatchetIds)
            .ToList();
    }
}
