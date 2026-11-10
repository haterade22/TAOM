using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.HeroDefence.Domain;

/// <summary>The Hero Defence domain is engine-free, in the shape of LongDefeatDomainPurityTests.</summary>
[TestClass]
public class HeroDefenceDomainPurityTests
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

        Assert.AreEqual(0, offenders.Count, "Hero Defence domain must stay engine-free:\n" + string.Join("\n", offenders));
    }

    private static string[] DomainFiles() =>
        CSharpFiles("Main")
            .Where(f => f.Replace('\\', '/').Contains("/Main/Features/HeroDefence/Domain/"))
            .ToArray();
}
