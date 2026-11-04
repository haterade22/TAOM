using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The Long Defeat domain is engine-free, in the shape of FordsDomainPurityTests.</summary>
[TestClass]
public class LongDefeatDomainPurityTests
{
    private static readonly string[] Forbidden = { "TaleWorlds", "TAOM.Adapters" };

    [TestMethod]
    public void DomainFolder_HasSourceFiles()
    {
        Assert.IsTrue(DomainFiles().Any(), "the purity scan found no files; the folder moved?");
    }

    [TestMethod]
    public void DomainFiles_ReferenceNeitherTaleWorldsNorAdapters()
    {
        var offenders = DomainFiles()
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (f, line, n: i + 1))
                .Where(x => Forbidden.Any(token => x.line.Contains(token))))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.n}: {x.line.Trim()}")
            .ToList();

        Assert.AreEqual(0, offenders.Count, "Long Defeat domain must stay engine-free:\n" + string.Join("\n", offenders));
    }

    [TestMethod]
    public void Scan_TokensMatchPlantedReferences()
    {
        Assert.IsTrue(Forbidden.Any(t => "using TaleWorlds.Core;".Contains(t)));
        Assert.IsTrue(Forbidden.Any(t => "using static TaleWorlds.Library.MathF;".Contains(t)));
        Assert.IsTrue(Forbidden.Any(t => "var x = TAOM.Adapters.Foo.Bar;".Contains(t)));
        Assert.IsFalse(Forbidden.Any(t => "using System.Linq;".Contains(t)));
    }

    private static string[] DomainFiles() =>
        CSharpFiles("Main")
            .Where(f => f.Replace('\\', '/').Contains("/Main/Features/LongDefeat/Domain/"))
            .ToArray();
}
