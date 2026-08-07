using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Source-level guard against a lookup that compiles, runs, throws nothing, and always returns null.
///
/// `CampaignObjectManager` registers exactly five object types on 1.4.7 — MobileParty, two Hero
/// lists, Clan and Kingdom. `Find&lt;T&gt;` only descends into a bucket when `typeof(T)` matches one of
/// them, so `Find&lt;Settlement&gt;(anyId)` returns null UNCONDITIONALLY. Settlements resolve through
/// `Settlement.Find`, which goes to `MBObjectManager.Instance.GetObject&lt;Settlement&gt;`.
///
/// This shipped twice: `MobilePartyAttachmentAdapter.MoveIntoSettlement` could never have worked
/// (it was also never called, so nothing surfaced it), and `DutyWorldAdapter` silently failed every
/// spawned hunt duty — including after a fix on 2026-08-07 that corrected the settlement CHOICE
/// while still resolving it through the broken lookup.
///
/// A runtime test cannot catch this without a live Campaign, so the guard is on the source.
/// </summary>
[TestClass]
public class SettlementLookupBindingTests
{
    private static string AdaptersDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Main", "Adapters")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "could not locate the repo root from the test working directory");
        return Path.Combine(dir!.FullName, "Main", "Adapters");
    }

    [TestMethod]
    public void NoAdapterResolvesASettlementThroughCampaignObjectManager()
    {
        var offenders = Directory.EnumerateFiles(AdaptersDir(), "*.cs", SearchOption.AllDirectories)
            .Select(f => new { File = Path.GetFileName(f), Lines = File.ReadAllLines(f) })
            .SelectMany(x => x.Lines.Select((line, i) => new { x.File, Line = line, Number = i + 1 }))
            .Where(x => x.Line.Contains("Find<Settlement>")
                        && !x.Line.TrimStart().StartsWith("//"))
            .Select(x => $"{x.File}:{x.Number}")
            .ToList();

        Assert.AreEqual(
            0,
            offenders.Count,
            "CampaignObjectManager.Find<Settlement> returns null unconditionally on 1.4.7 — use "
            + "Settlement.Find(id) instead. Offending lines: " + string.Join(", ", offenders));
    }
}
