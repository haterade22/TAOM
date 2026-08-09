using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.AutoResolveDiagnostics;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Tests.Features.AutoResolveDiagnostics;

/// <summary>
/// The formatter is the whole testable surface of this feature — capture needs a live MapEvent,
/// but turning a record into a log line is pure. These tests pin the contract that
/// tools/analyze_battle_logs.py parses, because a silently-changed field name produces a log that
/// looks fine and analyses to nothing. That is not hypothetical: during development the tool read a
/// 'losses' key the C# never wrote and reported a 0.0% loss rate for every class.
/// </summary>
[TestClass]
public class AutoResolveLogFormatterTests
{
    private static BattleLogRecord Sample() => new()
    {
        Id = "1084.3",
        Day = 1084,
        Hour = 14.5f,
        Type = "FieldBattle",
        Settlement = null,
        Terrain = "PlainBattle",
        PlayerInvolved = false,
        Rounds = 56,
        Session = "abc123",
        Winner = "attacker",
        EndedBy = "attackerVictory",
        Sides = new Dictionary<string, BattleLogSide>
        {
            ["attacker"] = new()
            {
                LeaderCulture = "gondor", Kingdom = "kingdom_gondor",
                Leader = "lord_EW1_1", Tactics = 214, PowerModifier = 0.07f, SideMorale = 62.4f,
                MenStart = 612, Strength = 1840.5f, Advantage = 1.214f,
                Parties = new List<BattleLogParty>
                {
                    new()
                    {
                        Culture = "gondor", Present = 200, Participating = 169, TroopLimit = true,
                        Fielded = new Dictionary<string, int> { ["gondor_ano_spearman"] = 169 },
                        Killed = new Dictionary<string, int> { ["gondor_ano_spearman"] = 61 },
                        Wounded = new Dictionary<string, int> { ["gondor_ano_spearman"] = 12 },
                        Routed = new Dictionary<string, int>(),
                    },
                    new()   // a second party of a DIFFERENT culture on the same side
                    {
                        Culture = "vlandia",
                        Fielded = new Dictionary<string, int> { ["rohan_eastfold_rider"] = 40 },
                        Killed = new Dictionary<string, int>(),
                        Wounded = new Dictionary<string, int>(),
                        Routed = new Dictionary<string, int>(),
                    },
                },
            },
            ["defender"] = new()
            {
                LeaderCulture = "mordor", Kingdom = "kingdom_mordor",
                Leader = "lord_ES1_1", Tactics = 180, PowerModifier = 0.02f, SideMorale = 41.0f,
                MenStart = 803,
                Parties = new List<BattleLogParty>
                {
                    new()
                    {
                        Culture = "mordor",
                        Fielded = new Dictionary<string, int> { ["mordor_orc_warrior"] = 803 },
                        Killed = new Dictionary<string, int> { ["mordor_orc_warrior"] = 500 },
                        Wounded = new Dictionary<string, int>(),
                        Routed = new Dictionary<string, int>(),
                    },
                },
            },
        },
    };

    private static JObject Parse(BattleLogRecord record) =>
        JObject.Parse(AutoResolveLogFormatter.ExtractPayload(AutoResolveLogFormatter.Format(record)));

    [TestMethod]
    public void Format_ProducesExactlyOneLine()
    {
        var line = AutoResolveLogFormatter.Format(Sample());

        // JSON Lines: one record per line. An embedded newline splits one battle into two
        // unparseable fragments and the analyzer silently drops both.
        Assert.IsFalse(line.Contains("\n"), "record must not contain a newline");
        Assert.IsFalse(line.Contains("\r"), "record must not contain a carriage return");
    }

    [TestMethod]
    public void Format_CarriesTheAutoResolveTag_SoTheSharedLogCanBeGrepped()
    {
        Assert.IsTrue(AutoResolveLogFormatter.Format(Sample()).StartsWith(AutoResolveLogFormatter.Tag));
    }

    [TestMethod]
    public void Format_EmitsSchemaVersion6()
    {
        // v1/v2 predate reading MapEventParty.Troops. The analyzer refuses a version it does not know.
        Assert.AreEqual(6, (int)Parse(Sample())["v"]!);
    }

    [TestMethod]
    public void Format_KeepsTheTopLevelSchemaFieldNames()
    {
        var json = Parse(Sample());

        Assert.AreEqual("1084.3", (string?)json["id"]);
        Assert.AreEqual(1084, (int)json["day"]!);
        Assert.AreEqual("PlainBattle", (string?)json["terrain"]);
        Assert.AreEqual(false, (bool)json["player"]!);
        Assert.AreEqual(56, (int)json["rounds"]!);
        Assert.AreEqual("abc123", (string?)json["session"]);
        Assert.AreEqual("attacker", (string?)json["winner"]);
        Assert.AreEqual("attackerVictory", (string?)json["endedBy"]);
    }

    [TestMethod]
    public void Format_EmitsFieldedRosterSeparatelyFromCasualties()
    {
        // The fielded roster is the army that FOUGHT. Casualties are what happened to it. Conflating
        // them is the v1 bug this schema exists to prevent (the loser's MemberRoster is already gutted).
        var attacker = Parse(Sample())["sides"]!["attacker"]!;
        var firstParty = attacker["parties"]![0]!;

        Assert.AreEqual(169, (int)firstParty["fielded"]!["gondor_ano_spearman"]!);
        Assert.AreEqual(61, (int)firstParty["killed"]!["gondor_ano_spearman"]!);
        Assert.AreEqual(12, (int)firstParty["wounded"]!["gondor_ano_spearman"]!);
    }

    [TestMethod]
    public void Format_OmitsTheSiegeBlockForAFieldBattle()
    {
        // Null, not zeroes. A zeroed siege block would read as "wall level 0, no advantage", which
        // is a claim about a siege that never happened.
        Assert.AreEqual(null, Parse(Sample())["siege"]!.Type == JTokenType.Null ? null : "set");
    }

    [TestMethod]
    public void Format_EmitsTheSiegeTermsThatActuallyDecideASiege()
    {
        // GetSettlementAdvantage dwarfs every troop-quality term — an unbreached wall-3 town hands
        // the defender roughly 7x. Without it a siege outcome cannot be explained at all.
        var record = Sample();
        record.Type = "Siege";
        record.Settlement = "town_ES2";
        record.Siege = new BattleLogSiege
        {
            SettlementAdvantage = 6.4f, WallLevel = 3, WallHitPoints = 12500f,
            EnginesBuilt = 2, EngineProgress = 0.45f, SettlementOwner = "kingdom_mordor",
        };

        var siege = Parse(record)["siege"]!;

        Assert.AreEqual(6.4f, (float)siege["settlementAdvantage"]!, 0.01f);
        Assert.AreEqual(3, (int)siege["wallLevel"]!);
        Assert.AreEqual(12500f, (float)siege["wallHitPoints"]!, 1f);
        Assert.AreEqual(2, (int)siege["enginesBuilt"]!);
        Assert.AreEqual("kingdom_mordor", (string?)siege["settlementOwner"]);
    }

    [TestMethod]
    public void Format_DistinguishesPresentFromParticipating()
    {
        // The engine trims the allocated roster when a troop limit applies, so `fielded` can sit
        // legitimately below `present`. Logging both is what stops that gap being read as a bug —
        // the first live run showed a -23.6% median divergence for exactly this reason.
        var party = Parse(Sample())["sides"]!["attacker"]!["parties"]![0]!;

        Assert.AreEqual(200, (int)party["present"]!);
        Assert.AreEqual(169, (int)party["participating"]!);
        Assert.AreEqual(true, (bool)party["troopLimit"]!);
    }

    [TestMethod]
    public void FormatCensus_EmitsEngineGroundTruth()
    {
        // The census is what lets the offline tier/power derivation be checked instead of trusted.
        var line = AutoResolveLogFormatter.FormatCensus(new TroopCensusRecord
        {
            Id = "gondor_mt_fountain_guard", Level = 46, Tier = 9, Power = 3.61f,
            HitPoints = 100, Formation = "Infantry", Mounted = false, Ranged = false,
            IsHero = false, Culture = "gondor", Race = 0,
        });

        Assert.IsTrue(line.StartsWith(AutoResolveLogFormatter.CensusTag));
        var json = JObject.Parse(AutoResolveLogFormatter.ExtractPayload(line));
        Assert.AreEqual("gondor_mt_fountain_guard", (string?)json["id"]);
        Assert.AreEqual(9, (int)json["tier"]!);
        Assert.AreEqual(3.61f, (float)json["power"]!, 0.001f);
        Assert.AreEqual(100, (int)json["hp"]!);
        Assert.AreEqual("Infantry", (string?)json["formation"]);
    }

    [TestMethod]
    public void FormatCensus_NullRecord_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AutoResolveLogFormatter.FormatCensus(null!));
    }

    [TestMethod]
    public void Format_KeepsEachPartysOwnCulture_NotJustTheLeaders()
    {
        // A side can hold parties of several cultures. Attributing the whole roster to the leader
        // corrupts every per-culture composition number downstream.
        var attacker = Parse(Sample())["sides"]!["attacker"]!;

        Assert.AreEqual("gondor", (string?)attacker["leaderCulture"]);
        Assert.AreEqual("gondor", (string?)attacker["parties"]![0]!["culture"]);
        Assert.AreEqual("vlandia", (string?)attacker["parties"]![1]!["culture"]);
    }

    [TestMethod]
    public void Format_EmitsSideMoraleAndTactics_TheConfoundsTuningMustControlFor()
    {
        var attacker = Parse(Sample())["sides"]!["attacker"]!;

        Assert.AreEqual(214, (int)attacker["tactics"]!);
        Assert.AreEqual(62.4f, (float)attacker["sideMorale"]!, 0.01f);
        Assert.AreEqual(0.07f, (float)attacker["powerModifier"]!, 0.001f);
        Assert.AreEqual(612, (int)attacker["menStart"]!);
        Assert.AreEqual(1840.5f, (float)attacker["strength"]!, 0.01f);
        Assert.AreEqual(1.214f, (float)attacker["advantage"]!, 0.001f);
        
    }

    [TestMethod]
    public void Format_TroopIdWithAQuote_StaysParseable()
    {
        // Troop ids are authored data. A stray quote or backslash must not corrupt the log.
        var record = Sample();
        record.Sides["attacker"].Parties[0].Fielded =
            new Dictionary<string, int> { ["evil\"troop\\id"] = 3 };

        var json = Parse(record);

        Assert.AreEqual(3,
            (int)json["sides"]!["attacker"]!["parties"]![0]!["fielded"]!["evil\"troop\\id"]!);
    }

    [TestMethod]
    public void ExtractPayload_ReturnsJson_EvenWhenTheSharedLoggerPrefixedTheLine()
    {
        // FileLogger writes "[timestamp] [INFO] {message}". The analyzer strips to the first
        // brace; this pins that the tag never introduces one ahead of the payload.
        var line = "[2026-08-08 14:32:01] [INFO] " + AutoResolveLogFormatter.Format(Sample());

        Assert.AreEqual("1084.3",
            (string?)JObject.Parse(AutoResolveLogFormatter.ExtractPayload(line))["id"]);
    }

    [TestMethod]
    public void ExtractPayload_ReturnsEmpty_ForALineWithNoPayload()
    {
        Assert.AreEqual(string.Empty, AutoResolveLogFormatter.ExtractPayload("no json here"));
        Assert.AreEqual(string.Empty, AutoResolveLogFormatter.ExtractPayload(string.Empty));
        Assert.AreEqual(string.Empty, AutoResolveLogFormatter.ExtractPayload(null!));
    }

    [TestMethod]
    public void Format_NullRecord_ReturnsEmpty_RatherThanThrowing()
    {
        // A diagnostic must never propagate. Nothing logged beats a crash on the campaign tick.
        Assert.AreEqual(string.Empty, AutoResolveLogFormatter.Format(null!));
    }

    [TestMethod]
    public void Format_WithNonFiniteFloats_EmitsNoBareNaNOrInfinityToken()
    {
        // Newtonsoft's default FloatFormatHandling.Symbol writes a bare NaN / Infinity token. That
        // is not valid JSON, but Python's json.loads ACCEPTS it (parse_constant returns
        // float('nan')) — so a poisoned value would sail past the analyzer's malformed-line
        // counter and silently contaminate every mean, median and comparison downstream. Worse
        // than a parse error, because it is invisible.
        var record = Sample();
        record.Sides["attacker"].SideMorale = float.NaN;
        record.Sides["attacker"].Advantage = float.PositiveInfinity;
        record.Sides["attacker"].Strength = float.NegativeInfinity;

        var json = AutoResolveLogFormatter.Format(record);

        StringAssert.DoesNotMatch(json, new Regex(@"[:\[,]\s*-?(NaN|Infinity)\b"),
            "a bare non-finite token reached the log");
        // And it must still be parseable rather than dropped entirely.
        Assert.AreNotEqual(string.Empty, json);
        JObject.Parse(AutoResolveLogFormatter.ExtractPayload(json));
    }
}
