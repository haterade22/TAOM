using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Pins the 2026-08-07 decision that enlistment never attaches the player's party to the
/// commander, so a future session cannot re-derive it from first principles and reintroduce a
/// guaranteed campaign crash.
///
/// Setting <c>MobileParty.MainParty.AttachedTo</c> without an <c>Army</c> is an unavoidable NRE:
/// <c>DefaultEncounterGameMenuModel.GetGenericStateMenu()</c> (:230-232) dereferences
/// <c>mainParty.Army.LeaderParty</c> UNGUARDED inside <c>if (mainParty.AttachedTo != null)</c>, and
/// <c>Campaign.Tick()</c> (Campaign.cs:992-994) calls it on every tick where the active state is a
/// <c>MapState</c> not at a menu — i.e. every frame on the open map.
///
/// Attaching WITH an army means genuinely joining it (kingdom membership, army UI, and it undoes
/// TAOM's own join-army suppression), and a parked attached party additionally fails
/// <c>MapEvent.CanPartyJoinBattle</c> for every AI lord reinforcing either side, because that check
/// requires <c>IsActive</c> for every party on both sides and the cascade puts the parked party in
/// the map event side.
///
/// Full reasoning: docs/features/enlistment.md, "Why the player's party is NOT attached".
/// </summary>
[TestClass]
public class EnlistmentAttachedToPolicyTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Main", "Adapters")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "could not locate the repo root from the test working directory");
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [TestMethod]
    public void MobilePartyAttachmentAdapter_NeverAssignsAttachedTo()
    {
        var path = RepoFile("Main", "Adapters", "MobilePartyAttachmentAdapter.cs");
        var offenders = File.ReadAllLines(path)
            .Select((line, i) => new { Line = line.Trim(), Number = i + 1 })
            .Where(x => x.Line.Contains("AttachedTo")
                        && x.Line.Contains("=")
                        && !x.Line.Contains("AttachedTo = null")
                        && !x.Line.Contains("AttachedTo !=")
                        && !x.Line.Contains("AttachedTo ==")
                        && !x.Line.StartsWith("//")
                        && !x.Line.StartsWith("///"))
            .Select(x => $"line {x.Number}: {x.Line}")
            .ToList();

        Assert.AreEqual(
            0,
            offenders.Count,
            "Enlistment must never ASSIGN MobileParty.AttachedTo — only clear it. Setting it without "
            + "an Army is a guaranteed campaign CTD (DefaultEncounterGameMenuModel dereferences "
            + "mainParty.Army unguarded behind the AttachedTo null check, and Campaign.Tick calls it "
            + "every tick on the open map). See docs/features/enlistment.md. Offenders: "
            + string.Join(" | ", offenders));
    }

    [TestMethod]
    public void ClearArmyAttachment_LeavesAnArmyThePlayerLeadsAlone()
    {
        // The carve-out is source-pinned because disbanding an army the player LEADS, out from
        // under its members, is a far worse outcome than leaving a stale membership in place.
        var source = File.ReadAllText(RepoFile("Main", "Adapters", "MobilePartyAttachmentAdapter.cs"));

        Assert.IsTrue(
            source.Contains("main.Army.LeaderParty != main"),
            "ClearArmyAttachment must guard the Army clear on the player NOT being the army's leader; "
            + "clearing a led army disbands it for every member.");
    }
}
