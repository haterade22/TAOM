using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the consistency reading behind <c>taom.print_player_state</c>.
///
/// Two links are deliberately reported rather than asserted, and the tests pin that distinction
/// because getting it wrong produces false alarms on states TAOM itself creates:
/// hero-vs-PlayerTroop is a tautology (Hero.MainHero is derived from Game.Current.PlayerTroop), and
/// hero-party-vs-main-party legitimately diverges while enlisted, imprisoned or travelling.
/// </summary>
[TestClass]
public class PlayerStateDiagnosisTests
{
    private static PlayerStateSnapshot Consistent() => new PlayerStateSnapshot
    {
        MainHeroId = "lord_1_34",
        MainHeroName = "Faramir",
        MainHeroIsAlive = true,
        MainHeroClanId = "clan_hurinionath",
        MainHeroPartyId = "party_faramir",
        PlayerClanId = "clan_hurinionath",
        PlayerClanLeaderId = "lord_1_34",
        MainPartyId = "party_faramir",
        MainPartyLeaderHeroId = "lord_1_34",
        MainPartyIsActive = true,
        PlayerTroopId = "lord_1_34",
    };

    private static string Joined(IReadOnlyList<string> lines) => string.Join("\n", lines);

    private static int MismatchCount(IReadOnlyList<string> lines) => lines.Count(l => l.Contains("MISMATCH"));

    [TestMethod]
    public void Build_AllLinksConsistent_ReportsNoMismatch()
    {
        var lines = PlayerStateDiagnosis.Build(Consistent());

        Assert.AreEqual(0, MismatchCount(lines), Joined(lines));
        StringAssert.Contains(Joined(lines), "PLAYER STATE OK");
    }

    [TestMethod]
    public void Build_PlayerClanIsNotTheHerosClan_ReportsMismatch()
    {
        // The orphan-clan case: the throwaway creation clan survived as the player clan.
        var snapshot = Consistent();
        snapshot.PlayerClanId = "player_faction";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH (clan)")), Joined(lines));
    }

    [TestMethod]
    public void Build_MainPartyLedBySomeoneElse_ReportsMismatch()
    {
        var snapshot = Consistent();
        snapshot.MainPartyLeaderHeroId = "lord_9_99";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH (party leader)")), Joined(lines));
    }

    [TestMethod]
    public void Build_DeadMainHero_ReportsMismatch()
    {
        var snapshot = Consistent();
        snapshot.MainHeroIsAlive = false;

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH") && l.Contains("not alive")), Joined(lines));
    }

    [TestMethod]
    public void Build_InactiveMainParty_ReportsMismatch()
    {
        var snapshot = Consistent();
        snapshot.MainPartyIsActive = false;

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH (party)")), Joined(lines));
    }

    [TestMethod]
    public void Build_NoMainHero_ReportsMismatchAndDoesNotBlameTheInactiveParty()
    {
        // With no hero there is also no party; reporting a party mismatch on top would bury the
        // one fact that matters.
        var lines = PlayerStateDiagnosis.Build(new PlayerStateSnapshot());

        Assert.IsTrue(lines.Any(l => l.Contains("no Hero.MainHero")), Joined(lines));
        Assert.IsFalse(lines.Any(l => l.Contains("MISMATCH (party)")), Joined(lines));
    }

    // ---------- Reported, never asserted ----------

    /// <summary>
    /// The exact state reproduced in play on 2026-09-03 (playing Faramir, clan led by his father).
    /// It is a NOTE rather than a MISMATCH because it is the expected result of taking over a lord
    /// who is not his house's head, and because the obvious "repair" transfers kingdom rulership.
    /// </summary>
    [TestMethod]
    public void Build_PlayerIsNotTheirOwnClansLeader_ReportsANoteNotAMismatch()
    {
        var snapshot = Consistent();
        snapshot.PlayerClanLeaderId = "lord_1_7";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.AreEqual(0, MismatchCount(lines), Joined(lines));
        Assert.IsTrue(lines.Any(l => l.Contains("NOTE (clan leader)")), Joined(lines));
    }

    [TestMethod]
    public void Build_ClanLeaderNote_WarnsThatPromotingThePlayerTransfersRulership()
    {
        // Kingdom.Leader is a projection of RulingClan.Leader (Kingdom.cs:168), so promoting the
        // player inside a ruling clan silently crowns them. The note has to say so, or the next
        // reader "fixes" it the way this session nearly shipped.
        var snapshot = Consistent();
        snapshot.PlayerClanLeaderId = "lord_1_7";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "rulership");
    }

    [TestMethod]
    public void Build_HeroInADifferentPartyThanMainParty_ReportsANoteNotAMismatch()
    {
        // Enlisted service (a TAOM feature), imprisonment and travelling all produce this legitimately.
        var snapshot = Consistent();
        snapshot.MainHeroPartyId = "commander_party";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.AreEqual(0, MismatchCount(lines), Joined(lines));
        Assert.IsTrue(lines.Any(l => l.Contains("NOTE (party)")), Joined(lines));
        StringAssert.Contains(Joined(lines), "enlisted");
    }

    [TestMethod]
    public void Build_PlayerTroopDiffersFromTheHero_IsNotTreatedAsAMismatch()
    {
        // Hero.MainHero IS CharacterObject.PlayerCharacter.HeroObject, so these cannot genuinely
        // disagree. Asserting it dressed a tautology up as a safety net.
        var snapshot = Consistent();
        snapshot.PlayerTroopId = "something_else";

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.AreEqual(0, MismatchCount(lines), Joined(lines));
    }

    // ---------- Capture failure ----------

    [TestMethod]
    public void Build_CaptureThrew_ReportsThatAsTheFindingAndStopsThere()
    {
        var lines = PlayerStateDiagnosis.Build(new PlayerStateSnapshot
        {
            CaptureError = "NullReferenceException reading Hero.MainHero: Object reference not set",
        });

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH (capture)")), Joined(lines));
        StringAssert.Contains(Joined(lines), "Hero.MainHero");
    }

    [TestMethod]
    public void Build_NullSnapshot_DoesNotThrow()
    {
        var lines = PlayerStateDiagnosis.Build(null);

        Assert.IsTrue(lines.Any(), "Expected at least one line for a null snapshot.");
    }

    [TestMethod]
    public void Build_EveryAssertedMismatch_IsReportedNotJustTheFirst()
    {
        var snapshot = Consistent();
        snapshot.PlayerClanId = "player_faction";
        snapshot.MainPartyLeaderHeroId = "lord_9_99";
        snapshot.MainPartyIsActive = false;

        var lines = PlayerStateDiagnosis.Build(snapshot);

        Assert.IsTrue(MismatchCount(lines) >= 3,
            "Expected every broken link to be reported, got:\n" + Joined(lines));
    }
}
